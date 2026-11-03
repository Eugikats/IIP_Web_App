using System.Net;
using System.Net.Http.Json;
using InsuranceIntelligence.Api.Tests.Support;
using InsuranceIntelligence.Application.Cases;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;
using Microsoft.Extensions.DependencyInjection;

namespace InsuranceIntelligence.Api.Tests;

/// <summary>Build-loop item 6: operating values that used to be constants are workspace settings
/// whose defaults are the old values.</summary>
public sealed class TenantSettingsApiTests(MockedApiFactory factory) : IClassFixture<MockedApiFactory>
{
    [Fact]
    public async Task ANewWorkspaceReadsTheOldConstantsAsItsDefaults()
    {
        using var admin = factory.CreateScopedClient(MockedApiFactory.NewTenant());

        var settings = await MockedApiFactory.ReadJsonAsync(await admin.GetAsync("/api/v1/admin/tenant-settings"));

        Assert.True(settings.GetProperty("requireIraApprovalForPublish").GetBoolean());
        Assert.Equal(3, settings.GetProperty("missingDocumentReminderDays").GetInt32());
        Assert.Equal(24, settings.GetProperty("slaWarningLeadHours").GetInt32());
        Assert.Equal(50, settings.GetProperty("seatLimit").GetInt32());
        Assert.Equal(5000, settings.GetProperty("claimsMeterLimit").GetInt32());
    }

    [Fact]
    public async Task OnlyTheSettingsSuppliedChange_AndTheChangeIsAuditedByName()
    {
        var tenant = MockedApiFactory.NewTenant();
        using var admin = factory.CreateScopedClient(tenant);

        var response = await admin.PutAsJsonAsync("/api/v1/admin/tenant-settings", new { missingDocumentReminderDays = 7 });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var settings = await MockedApiFactory.ReadJsonAsync(response);
        Assert.Equal(7, settings.GetProperty("missingDocumentReminderDays").GetInt32());
        Assert.Equal(24, settings.GetProperty("slaWarningLeadHours").GetInt32());
        Assert.Equal("test-admin", settings.GetProperty("updatedByUserId").GetString());

        var audit = await admin.GetAsync("/api/v1/audit/events");
        var text = await audit.Content.ReadAsStringAsync();
        Assert.Contains("tenant.settings.updated", text);
        Assert.DoesNotContain("workspace-other", text);
    }

    [Theory]
    [InlineData("missingDocumentReminderDays", 0)]
    [InlineData("missingDocumentReminderDays", 31)]
    [InlineData("slaWarningLeadHours", 0)]
    [InlineData("seatLimit", 0)]
    [InlineData("claimsMeterLimit", -1)]
    public async Task OutOfRangeSettingsAreRefusedWithAReason(string name, int value)
    {
        using var admin = factory.CreateScopedClient(MockedApiFactory.NewTenant());

        var response = await admin.PutAsJsonAsync("/api/v1/admin/tenant-settings", new Dictionary<string, int> { [name] = value });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("between", await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("ClaimsOfficer")]
    [InlineData("CustomerService")]
    [InlineData("Broker")]
    public async Task OnlyAdministratorsMayReadOrChangeSettings(string role)
    {
        using var client = factory.CreateScopedClient(MockedApiFactory.NewTenant(), role: role);

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/admin/tenant-settings")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PutAsJsonAsync("/api/v1/admin/tenant-settings", new { seatLimit = 5 })).StatusCode);
    }

    [Fact]
    public async Task OneWorkspacesSettingsNeverReachAnother()
    {
        using var a = factory.CreateScopedClient(MockedApiFactory.NewTenant());
        using var b = factory.CreateScopedClient(MockedApiFactory.NewTenant());

        await a.PutAsJsonAsync("/api/v1/admin/tenant-settings", new { seatLimit = 7 });

        var other = await MockedApiFactory.ReadJsonAsync(await b.GetAsync("/api/v1/admin/tenant-settings"));
        Assert.Equal(50, other.GetProperty("seatLimit").GetInt32());
    }

    [Fact]
    public async Task TheIraRuleIsASetting_ASecondPersonStillHasToApprove()
    {
        var tenant = MockedApiFactory.NewTenant();
        using var admin = factory.CreateScopedClient(tenant, userId: "maker-1");
        using var checker = factory.CreateScopedClient(tenant, userId: "checker-1");
        var created = await MockedApiFactory.ReadJsonAsync(await admin.PostAsJsonAsync("/api/v1/products", new { name = "Plain Cover", productClass = "General", changeReason = "new" }));
        var productId = created.GetProperty("productId").GetGuid();
        Assert.False(created.GetProperty("canPublish").GetBoolean());

        var refused = await admin.PostAsJsonAsync($"/api/v1/products/{productId}/submit", new { changeReason = "go" });
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.Contains("IRA", await refused.Content.ReadAsStringAsync());

        await admin.PutAsJsonAsync("/api/v1/admin/tenant-settings", new { requireIraApprovalForPublish = false });
        var product = await MockedApiFactory.ReadJsonAsync(await admin.GetAsync($"/api/v1/products/{productId}"));
        Assert.True(product.GetProperty("canPublish").GetBoolean());

        var submitted = await admin.PostAsJsonAsync($"/api/v1/products/{productId}/submit", new { changeReason = "go" });
        Assert.Equal(HttpStatusCode.OK, submitted.StatusCode);
        var selfApproval = await admin.PostAsJsonAsync($"/api/v1/products/{productId}/approve", new { changeReason = "me" });
        Assert.Equal(HttpStatusCode.BadRequest, selfApproval.StatusCode);
        var approved = await checker.PostAsJsonAsync($"/api/v1/products/{productId}/approve", new { changeReason = "ok" });
        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        Assert.Equal("Published", (await MockedApiFactory.ReadJsonAsync(approved)).GetProperty("status").GetString());
    }

    [Fact]
    public async Task TheSlaWarningLeadTimeIsASetting()
    {
        var tenant = MockedApiFactory.NewTenant();
        using var admin = factory.CreateScopedClient(tenant);
        var claim = await TestData.CreateCaseAsync(admin, tenant);
        var scope = TenantScope.Create(tenant, "workspace-main");

        // Due in 30 hours with an owner: outside the default 24 hour warning window.
        await using (var arrange = factory.Services.CreateAsyncScope())
        {
            var cases = arrange.ServiceProvider.GetRequiredService<ICaseRepository>();
            var stored = (await cases.GetAsync(claim.CaseId))!;
            await cases.SaveAsync(stored with
            {
                SlaDueAt = DateTimeOffset.UtcNow.AddHours(30),
                AssignedUserId = "officer-1",
                RequiresConfirmation = false,
                Status = CaseStatus.Assigned,
            });
        }

        var before = await RunFollowThroughAsync(scope);
        await admin.PutAsJsonAsync("/api/v1/admin/tenant-settings", new { slaWarningLeadHours = 48 });
        var after = await RunFollowThroughAsync(scope);

        Assert.Equal(0, before.SlaWarningsSent);
        Assert.Equal(1, after.SlaWarningsSent);
    }

    [Fact]
    public async Task TheSeatLimitAndTheClaimsAllowanceOnTheBillingPageFollowTheSettings()
    {
        using var admin = factory.CreateScopedClient(MockedApiFactory.NewTenant());
        await admin.PutAsJsonAsync("/api/v1/admin/tenant-settings", new { seatLimit = 12, claimsMeterLimit = 250 });

        var billing = await MockedApiFactory.ReadJsonAsync(await admin.GetAsync("/api/v1/admin/billing"));

        Assert.Equal(12, billing.GetProperty("seatLimit").GetInt32());
        var claims = billing.GetProperty("meters").EnumerateArray().Single(meter => meter.GetProperty("key").GetString() == "claims");
        Assert.Equal(250, claims.GetProperty("limit").GetInt32());
    }

    private async Task<CaseFollowThroughResult> RunFollowThroughAsync(TenantScope scope)
    {
        await using var run = factory.Services.CreateAsyncScope();
        var service = run.ServiceProvider.GetRequiredService<ICaseFollowThroughService>();
        return await service.RunAsync(scope, "test-follow-through");
    }
}
