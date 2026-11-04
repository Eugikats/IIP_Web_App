using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using InsuranceIntelligence.Api.Tests.Support;

namespace InsuranceIntelligence.Api.Tests;

/// <summary>Build-loop item 19: the customer reads the insurer's own words for where their claim
/// stands, and a timeline from the product's stages.</summary>
public sealed class ClaimantStatusApiTests(MockedApiFactory factory) : IClassFixture<MockedApiFactory>
{
    private static async Task<JsonElement> ClaimAsync(HttpClient claimant, Guid caseId) =>
        await MockedApiFactory.ReadJsonAsync(await claimant.GetAsync($"/api/v1/client-portal/claims/{caseId}"));

    private static string[] StepStates(JsonElement claim) =>
        claim.GetProperty("timeline").EnumerateArray().Select(step => step.GetProperty("state").GetString()!).ToArray();

    private static string[] StepLabels(JsonElement claim) =>
        claim.GetProperty("timeline").EnumerateArray().Select(step => step.GetProperty("label").GetString()!).ToArray();

    private static async Task<HttpResponseMessage> PutWordingAsync(HttpClient admin, object wording) =>
        await admin.PutAsJsonAsync("/api/v1/admin/claim-status-wording", new { wording });

    [Fact]
    public async Task ACustomerSeesDefaultWordingAndATimelineThatSaysWhereTheyAre()
    {
        var tenant = MockedApiFactory.NewTenant();
        using var staff = factory.CreateScopedClient(tenant);
        var claim = await TestData.CreateCaseAsync(staff, tenant);
        using var claimant = await TestData.ClaimantClientAsync(factory, staff, claim);

        var view = await ClaimAsync(claimant, claim.CaseId);

        Assert.Equal("We have your claim", view.GetProperty("statusHeadline").GetString());
        Assert.StartsWith("We have received your claim", view.GetProperty("statusDetail").GetString());
        Assert.Equal(["Claim received", "Under review", "Assessment", "Decision", "Settlement"], StepLabels(view));
        Assert.Equal(["Current", "Upcoming", "Upcoming", "Upcoming", "Upcoming"], StepStates(view));
        Assert.False(view.GetProperty("timeline")[0].GetProperty("complete").GetBoolean());
    }

    [Fact]
    public async Task TheTimelineFollowsTheClaimWhenAnOfficerTakesItOn()
    {
        var tenant = MockedApiFactory.NewTenant();
        using var staff = factory.CreateScopedClient(tenant);
        var claim = await TestData.CreateCaseAsync(staff, tenant);
        using var claimant = await TestData.ClaimantClientAsync(factory, staff, claim);

        var assign = await staff.PostAsJsonAsync($"/api/v1/cases/{claim.CaseId}/assign", new { assignedUserId = "officer-1", auditReason = "Taking this on." });
        Assert.True(assign.IsSuccessStatusCode, await assign.Content.ReadAsStringAsync());

        var view = await ClaimAsync(claimant, claim.CaseId);
        Assert.Equal("Someone is looking after your claim", view.GetProperty("statusHeadline").GetString());
        Assert.Equal(["Done", "Current", "Upcoming", "Upcoming", "Upcoming"], StepStates(view));
        Assert.True(view.GetProperty("timeline")[0].GetProperty("complete").GetBoolean());
    }

    [Fact]
    public async Task AClaimOnAPublishedProductGetsTheCustomerStepsOfThatProductsProcess()
    {
        var tenant = MockedApiFactory.NewTenant();
        using var staff = factory.CreateScopedClient(tenant);
        await TestData.PublishProductAsync(factory, tenant);
        var claim = await TestData.CreateCaseAsync(staff, tenant);
        using var claimant = await TestData.ClaimantClientAsync(factory, staff, claim);

        var view = await ClaimAsync(claimant, claim.CaseId);

        Assert.Equal(["Claim received", "Documents and details", "Assessment", "Report prepared", "Decision", "Settlement"], StepLabels(view));
        Assert.Equal("Current", StepStates(view)[0]);
    }

    [Fact]
    public async Task AnAdministratorCanReplaceTheWordingAndTheCustomerSeesItAtOnce()
    {
        var tenant = MockedApiFactory.NewTenant();
        using var staff = factory.CreateScopedClient(tenant);
        var claim = await TestData.CreateCaseAsync(staff, tenant);
        using var claimant = await TestData.ClaimantClientAsync(factory, staff, claim);

        var put = await PutWordingAsync(staff, new Dictionary<string, object>
        {
            ["New"] = new { headline = "Thank you, we have your claim", detail = "A claims officer will call you within one working day." },
        });
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        var rows = (await MockedApiFactory.ReadJsonAsync(put)).EnumerateArray().ToArray();
        Assert.Equal(10, rows.Length);
        var row = rows.Single(item => item.GetProperty("status").GetString() == "New");
        Assert.True(row.GetProperty("customised").GetBoolean());
        Assert.Equal("Received", row.GetProperty("staffLabel").GetString());
        Assert.Equal("We have your claim", row.GetProperty("defaultHeadline").GetString());
        Assert.Equal(9, rows.Count(item => !item.GetProperty("customised").GetBoolean()));

        var view = await ClaimAsync(claimant, claim.CaseId);
        Assert.Equal("Thank you, we have your claim", view.GetProperty("statusHeadline").GetString());
        Assert.Equal("A claims officer will call you within one working day.", view.GetProperty("statusDetail").GetString());
        Assert.Contains("settings.updated", await staff.GetStringAsync("/api/v1/audit/events?pageSize=200"), StringComparison.OrdinalIgnoreCase);

        // Sending nothing for a status puts it back to the default.
        Assert.Equal(HttpStatusCode.OK, (await PutWordingAsync(staff, new Dictionary<string, object>())).StatusCode);
        Assert.Equal("We have your claim", (await ClaimAsync(claimant, claim.CaseId)).GetProperty("statusHeadline").GetString());
    }

    [Fact]
    public async Task OneWorkspacesWordingNeverReachesAnothersCustomers()
    {
        var tenantA = MockedApiFactory.NewTenant();
        var tenantB = MockedApiFactory.NewTenant();
        using var staffA = factory.CreateScopedClient(tenantA);
        using var staffB = factory.CreateScopedClient(tenantB);
        Assert.Equal(HttpStatusCode.OK, (await PutWordingAsync(staffA, new Dictionary<string, object> { ["New"] = new { headline = "Tenant A words", detail = "" } })).StatusCode);

        var claimB = await TestData.CreateCaseAsync(staffB, tenantB);
        using var claimantB = await TestData.ClaimantClientAsync(factory, staffB, claimB);
        Assert.Equal("We have your claim", (await ClaimAsync(claimantB, claimB.CaseId)).GetProperty("statusHeadline").GetString());

        var rowsB = (await MockedApiFactory.ReadJsonAsync(await staffB.GetAsync("/api/v1/admin/claim-status-wording"))).EnumerateArray().ToArray();
        Assert.All(rowsB, row => Assert.False(row.GetProperty("customised").GetBoolean()));

        // And a different workspace of the same tenant is separate too.
        using var otherWorkspace = factory.CreateScopedClient(tenantA, workspaceId: "workspace-other");
        var rowsOther = (await MockedApiFactory.ReadJsonAsync(await otherWorkspace.GetAsync("/api/v1/admin/claim-status-wording"))).EnumerateArray().ToArray();
        Assert.All(rowsOther, row => Assert.False(row.GetProperty("customised").GetBoolean()));
    }

    [Fact]
    public async Task WordingThatIsTooLongOrNamesNoRealStatusIsRefused()
    {
        var tenant = MockedApiFactory.NewTenant();
        using var staff = factory.CreateScopedClient(tenant);

        var tooLong = await PutWordingAsync(staff, new Dictionary<string, object> { ["New"] = new { headline = new string('a', 61), detail = "" } });
        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);
        var unknown = await PutWordingAsync(staff, new Dictionary<string, object> { ["Nonsense"] = new { headline = "x", detail = "y" } });
        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
    }

    [Fact]
    public async Task DashesTheProductDoesNotUseAreScrubbedWhenTheWordingIsSaved()
    {
        var tenant = MockedApiFactory.NewTenant();
        using var staff = factory.CreateScopedClient(tenant);
        var emDash = ((char)0x2014).ToString();

        var put = await PutWordingAsync(staff, new Dictionary<string, object> { ["New"] = new { headline = $"We have it {emDash} thanks", detail = "" } });

        var row = (await MockedApiFactory.ReadJsonAsync(put)).EnumerateArray().Single(item => item.GetProperty("status").GetString() == "New");
        Assert.DoesNotContain(emDash, row.GetProperty("headline").GetString());
    }

    [Theory]
    [InlineData("ClaimsOfficer")]
    [InlineData("ClaimsSupervisor")]
    [InlineData("Broker")]
    public async Task OnlyAdministratorsCanReadOrChangeTheWording(string role)
    {
        var tenant = MockedApiFactory.NewTenant();
        using var other = factory.CreateScopedClient(tenant, role: role);

        Assert.Equal(HttpStatusCode.Forbidden, (await other.GetAsync("/api/v1/admin/claim-status-wording")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await PutWordingAsync(other, new Dictionary<string, object>())).StatusCode);
    }
}
