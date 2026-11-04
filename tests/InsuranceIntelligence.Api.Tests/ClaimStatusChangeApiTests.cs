using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using InsuranceIntelligence.Api.Tests.Support;

namespace InsuranceIntelligence.Api.Tests;

/// <summary>Build-loop item 20: one audited "Change status" action along defined moves, with a reason,
/// a person, and the right authority; deciding moves keep their own actions.</summary>
public sealed class ClaimStatusChangeApiTests(MockedApiFactory factory) : IClassFixture<MockedApiFactory>
{
    private async Task<(string Tenant, HttpClient Admin, CaseInfo Claim)> ArrangeAsync()
    {
        var tenant = MockedApiFactory.NewTenant();
        var admin = factory.CreateScopedClient(tenant);
        return (tenant, admin, await TestData.CreateCaseAsync(admin, tenant));
    }

    private static Task<HttpResponseMessage> ChangeAsync(HttpClient client, Guid caseId, string? toStatus, string? reason) =>
        client.PostAsJsonAsync($"/api/v1/cases/{caseId}/status", new { toStatus, reason });

    private static async Task<string> StatusOfAsync(HttpClient client, Guid caseId) =>
        (await MockedApiFactory.ReadJsonAsync(await client.GetAsync($"/api/v1/cases/{caseId}"))).GetProperty("status").GetString()!;

    private static async Task<JsonElement> OptionsAsync(HttpClient client, Guid caseId) =>
        await MockedApiFactory.ReadJsonAsync(await client.GetAsync($"/api/v1/cases/{caseId}/status-options"));

    [Fact]
    public async Task AnOfficerSeesWhatTheyCanChooseAndWhereTheOtherStatusesComeFrom()
    {
        var (tenant, admin, claim) = await ArrangeAsync();
        using var _a = admin;
        using var officer = factory.CreateScopedClient(tenant, role: "ClaimsOfficer", userId: "officer-1");

        var options = await OptionsAsync(officer, claim.CaseId);

        Assert.Equal("New", options.GetProperty("currentStatus").GetString());
        var choices = options.GetProperty("options").EnumerateArray().Select(option => option.GetProperty("status").GetString()).ToArray();
        Assert.Equal(["InReview", "MissingDocuments"], choices.OrderBy(name => name).ToArray());
        var others = options.GetProperty("others").EnumerateArray().ToDictionary(item => item.GetProperty("status").GetString()!, item => item.GetProperty("howItIsReached").GetString()!);
        Assert.Contains("Approvals queue", others["PendingApproval"]);
        Assert.Contains("Resolve", others["Settled"]);
        Assert.Contains("Escalate", others["Escalated"]);
        Assert.DoesNotContain("InReview", others.Keys);
    }

    [Fact]
    public async Task ChangingStatusRecordsWhoWhyAndWhenAndTheCustomerTimelineFollows()
    {
        var (tenant, admin, claim) = await ArrangeAsync();
        using var _a = admin;
        using var officer = factory.CreateScopedClient(tenant, role: "ClaimsOfficer", userId: "officer-1");
        using var claimant = await TestData.ClaimantClientAsync(factory, admin, claim);

        var response = await ChangeAsync(officer, claim.CaseId, "InReview", "Started going through the documents.");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await MockedApiFactory.ReadJsonAsync(response);
        Assert.Equal("InReview", body.GetProperty("status").GetString());
        Assert.Equal("Under review", body.GetProperty("label").GetString());
        Assert.Equal("InReview", await StatusOfAsync(admin, claim.CaseId));

        var audit = await MockedApiFactory.ReadJsonAsync(await admin.GetAsync("/api/v1/audit/events?pageSize=200"));
        var raw = audit.GetRawText();
        Assert.Contains("Started going through the documents.", raw);
        Assert.Contains("officer-1", raw);

        var view = await MockedApiFactory.ReadJsonAsync(await claimant.GetAsync($"/api/v1/client-portal/claims/{claim.CaseId}"));
        Assert.Equal("We are reviewing your claim", view.GetProperty("statusHeadline").GetString());
        var current = view.GetProperty("timeline").EnumerateArray().Single(step => step.GetProperty("state").GetString() == "Current");
        Assert.Equal("Assessment", current.GetProperty("label").GetString());
        Assert.NotEqual(JsonValueKind.Null, current.GetProperty("date").ValueKind);
    }

    [Fact]
    public async Task AReasonIsRequiredAndSoIsARealStatusThatIsNotTheCurrentOne()
    {
        var (_, admin, claim) = await ArrangeAsync();
        using var _a = admin;

        Assert.Equal(HttpStatusCode.BadRequest, (await ChangeAsync(admin, claim.CaseId, "InReview", null)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await ChangeAsync(admin, claim.CaseId, "InReview", "   ")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await ChangeAsync(admin, claim.CaseId, "InReview", new string('x', 501))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await ChangeAsync(admin, claim.CaseId, null, "why")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await ChangeAsync(admin, claim.CaseId, "Banana", "why")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await ChangeAsync(admin, claim.CaseId, "New", "same place")).StatusCode);
        Assert.Equal("New", await StatusOfAsync(admin, claim.CaseId));
    }

    [Theory]
    [InlineData("PendingApproval")]
    [InlineData("ReadyForExport")]
    [InlineData("Settled")]
    [InlineData("Closed")]
    [InlineData("Escalated")]
    [InlineData("Assigned")]
    public async Task StatusesThatDecideSomethingCannotBeChosenAndSayWhatToUseInstead(string target)
    {
        var (_, admin, claim) = await ArrangeAsync();
        using var _a = admin;

        var response = await ChangeAsync(admin, claim.CaseId, target, "I want it there.");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace((await MockedApiFactory.ReadJsonAsync(response)).GetProperty("message").GetString()));
        Assert.Equal("New", await StatusOfAsync(admin, claim.CaseId));
    }

    [Fact]
    public async Task OnlyASupervisorCanTakeAClaimOutOfEscalationOrReopenAClosedOne()
    {
        var (tenant, admin, claim) = await ArrangeAsync();
        using var _a = admin;
        using var officer = factory.CreateScopedClient(tenant, role: "ClaimsOfficer", userId: "officer-1");
        using var supervisor = factory.CreateScopedClient(tenant, role: "ClaimsSupervisor", userId: "supervisor-1");

        var escalate = await admin.PostAsJsonAsync($"/api/v1/cases/{claim.CaseId}/escalate", new { auditReason = "Needs a senior look.", assignedTeam = "Claims" });
        Assert.True(escalate.IsSuccessStatusCode, await escalate.Content.ReadAsStringAsync());
        Assert.Equal("Escalated", await StatusOfAsync(admin, claim.CaseId));

        var officerAttempt = await ChangeAsync(officer, claim.CaseId, "InReview", "Back to normal.");
        Assert.Equal(HttpStatusCode.Forbidden, officerAttempt.StatusCode);
        Assert.Contains("needs a supervisor", (await MockedApiFactory.ReadJsonAsync(officerAttempt)).GetProperty("message").GetString());
        Assert.Equal("Escalated", await StatusOfAsync(admin, claim.CaseId));

        var options = await OptionsAsync(officer, claim.CaseId);
        var option = options.GetProperty("options").EnumerateArray().Single();
        Assert.True(option.GetProperty("requiresSupervisor").GetBoolean());
        Assert.False(option.GetProperty("allowedForYou").GetBoolean());

        Assert.Equal(HttpStatusCode.OK, (await ChangeAsync(supervisor, claim.CaseId, "InReview", "The senior view is in.")).StatusCode);
        Assert.Equal("InReview", await StatusOfAsync(admin, claim.CaseId));

        // Reopening a closed claim is the same authority.
        var escalateAgain = await admin.PostAsJsonAsync($"/api/v1/cases/{claim.CaseId}/escalate", new { auditReason = "Again.", assignedTeam = "Claims" });
        Assert.True(escalateAgain.IsSuccessStatusCode);
        using var resolve = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/cases/{claim.CaseId}/resolve") { Content = JsonContent.Create(new { resolution = "Closed" }) };
        resolve.Headers.Add("X-Audit-Reason", "Withdrawn by the customer.");
        Assert.True((await admin.SendAsync(resolve)).IsSuccessStatusCode);
        Assert.Equal("Closed", await StatusOfAsync(admin, claim.CaseId));
        Assert.Equal(HttpStatusCode.Forbidden, (await ChangeAsync(officer, claim.CaseId, "InReview", "Reopening.")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await ChangeAsync(supervisor, claim.CaseId, "InReview", "The customer sent new evidence.")).StatusCode);
    }

    [Fact]
    public async Task AnAutomatedActorCannotChangeAStatus()
    {
        var (tenant, admin, claim) = await ArrangeAsync();
        using var _a = admin;
        using var bot = factory.CreateScopedClient(tenant, role: "Admin", userId: "system:claim-triage");

        var response = await ChangeAsync(bot, claim.CaseId, "InReview", "Automatically.");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("New", await StatusOfAsync(admin, claim.CaseId));
    }

    [Fact]
    public async Task RolesThatCannotWorkAClaimCannotChangeItsStatus()
    {
        var (tenant, admin, claim) = await ArrangeAsync();
        using var _a = admin;
        using var broker = factory.CreateScopedClient(tenant, role: "Broker");

        Assert.Equal(HttpStatusCode.Forbidden, (await ChangeAsync(broker, claim.CaseId, "InReview", "No.")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await broker.GetAsync($"/api/v1/cases/{claim.CaseId}/status-options")).StatusCode);
    }

    [Fact]
    public async Task AnotherWorkspaceCannotSeeOrChangeThisClaimsStatus()
    {
        var (tenant, admin, claim) = await ArrangeAsync();
        using var _a = admin;
        using var intruder = factory.CreateScopedClient(MockedApiFactory.NewTenant());
        using var otherWorkspace = factory.CreateScopedClient(tenant, workspaceId: "workspace-other");

        foreach (var outsider in new[] { intruder, otherWorkspace })
        {
            Assert.Equal(HttpStatusCode.NotFound, (await outsider.GetAsync($"/api/v1/cases/{claim.CaseId}/status-options")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await ChangeAsync(outsider, claim.CaseId, "InReview", "Not mine.")).StatusCode);
        }

        Assert.Equal("New", await StatusOfAsync(admin, claim.CaseId));
    }
}
