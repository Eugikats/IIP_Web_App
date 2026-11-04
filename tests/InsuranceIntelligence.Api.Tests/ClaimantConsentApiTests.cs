using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using InsuranceIntelligence.Api.Tests.Support;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Compliance;
using Microsoft.Extensions.DependencyInjection;

namespace InsuranceIntelligence.Api.Tests;

/// <summary>Build-loop item 9: a claimant reads a short privacy notice and agrees before the first
/// upload; the agreement names the notice version and can be withdrawn from the claim page.</summary>
public sealed class ClaimantConsentApiTests(MockedApiFactory factory) : IClassFixture<MockedApiFactory>
{
    private static async Task<HttpResponseMessage> UploadAsync(HttpClient claimant, CaseInfo info) =>
        await claimant.PostAsync(
            $"/api/v1/client-portal/claims/{info.CaseId}/documents",
            TestData.FileForm("police.pdf", "IIP-MOCK-DOC:PoliceAbstract", documentTypeKey: "PoliceAbstract", contentType: "application/pdf"));

    private static async Task<JsonElement> NoticeAsync(HttpClient claimant) =>
        await MockedApiFactory.ReadJsonAsync(await claimant.GetAsync("/api/v1/client-portal/privacy-notice"));

    private async Task<(HttpClient Staff, CaseInfo Info, HttpClient Claimant, string Tenant)> ArrangeAsync()
    {
        var tenant = MockedApiFactory.NewTenant();
        var staff = factory.CreateScopedClient(tenant);
        var info = await TestData.CreateCaseAsync(staff, tenant);
        var claimant = await TestData.ClaimantClientAsync(factory, staff, info, agreeToPrivacyNotice: false);
        return (staff, info, claimant, tenant);
    }

    [Fact]
    public async Task TheDefaultNoticeAnswersWhatWhyWhoHowLongAndHowToWithdraw()
    {
        var (_, _, claimant, _) = await ArrangeAsync();

        var notice = await NoticeAsync(claimant);

        Assert.Equal("NotGiven", notice.GetProperty("state").GetString());
        Assert.True(notice.GetProperty("noticeIsDefault").GetBoolean());
        Assert.Equal(1, notice.GetProperty("noticeVersion").GetInt32());
        var text = notice.GetProperty("noticeText").GetString()!;
        foreach (var heading in new[] { "What we collect", "Why", "Who sees it", "How long we keep it", "Your choices" })
        {
            Assert.Contains(heading, text);
        }

        Assert.Contains("withdraw", text);
        Assert.DoesNotContain((char)0x2014, text);
    }

    [Fact]
    public async Task NothingCanBeUploadedUntilTheClaimantAgrees()
    {
        var (_, info, claimant, _) = await ArrangeAsync();

        var refused = await UploadAsync(claimant, info);

        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        Assert.Equal("consent_required", (await MockedApiFactory.ReadJsonAsync(refused)).GetProperty("code").GetString());

        var agreed = await claimant.PostAsJsonAsync("/api/v1/client-portal/consent", new { noticeVersion = "v1" });
        Assert.Equal(HttpStatusCode.OK, agreed.StatusCode);
        Assert.Equal("Given", (await MockedApiFactory.ReadJsonAsync(agreed)).GetProperty("state").GetString());

        Assert.Equal(HttpStatusCode.OK, (await UploadAsync(claimant, info)).StatusCode);
    }

    [Fact]
    public async Task TheAgreementIsStoredWithTheNoticeVersionAndAuditedWithoutTheNoticeTextOrContact()
    {
        var (staff, info, claimant, tenant) = await ArrangeAsync();
        await claimant.PostAsJsonAsync("/api/v1/client-portal/consent", new { noticeVersion = "v1" });

        await using var scope = factory.Services.CreateAsyncScope();
        var records = await scope.ServiceProvider.GetRequiredService<IConsentRepository>()
            .ListForCustomerAsync(TenantScope.Create(tenant, "workspace-main"), ClaimantConsent.Identifier(info.Email));
        var record = Assert.Single(records);
        Assert.Equal(ConsentPurposes.ClaimDataProcessing, record.Purpose);
        Assert.Equal(ConsentBasis.ExplicitOptIn, record.Basis);
        Assert.Equal("v1", record.NoticeVersion);
        Assert.Equal("portal", record.Channel);

        var audit = await staff.GetStringAsync("/api/v1/audit/events");
        Assert.Contains("claimant.consent.given", audit);
        Assert.DoesNotContain(info.Email, audit.Replace("customerEmail", string.Empty));
        Assert.DoesNotContain("What we collect", audit);
    }

    [Fact]
    public async Task AChangedNoticeIsAskedAgainAndAnOldVersionCannotBeAgreedTo()
    {
        var (staff, info, claimant, _) = await ArrangeAsync();
        await claimant.PostAsJsonAsync("/api/v1/client-portal/consent", new { noticeVersion = "v1" });
        Assert.Equal(HttpStatusCode.OK, (await UploadAsync(claimant, info)).StatusCode);

        var changed = await staff.PutAsJsonAsync("/api/v1/admin/tenant-settings", new { privacyNoticeText = "Our own notice. We keep your documents for seven years." });
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        Assert.Equal(2, (await MockedApiFactory.ReadJsonAsync(changed)).GetProperty("privacyNoticeVersion").GetInt32());

        var notice = await NoticeAsync(claimant);
        Assert.Equal("NoticeChanged", notice.GetProperty("state").GetString());
        Assert.Equal("Our own notice. We keep your documents for seven years.", notice.GetProperty("noticeText").GetString());
        Assert.False(notice.GetProperty("noticeIsDefault").GetBoolean());
        Assert.Equal(HttpStatusCode.Forbidden, (await UploadAsync(claimant, info)).StatusCode);

        var stale = await claimant.PostAsJsonAsync("/api/v1/client-portal/consent", new { noticeVersion = "v1" });
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal("notice_changed", (await MockedApiFactory.ReadJsonAsync(stale)).GetProperty("code").GetString());

        Assert.Equal(HttpStatusCode.OK, (await claimant.PostAsJsonAsync("/api/v1/client-portal/consent", new { noticeVersion = "v2" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await UploadAsync(claimant, info)).StatusCode);
    }

    [Fact]
    public async Task WithdrawingStopsNewUploadsAndTheHistoryIsKept()
    {
        var (staff, info, claimant, tenant) = await ArrangeAsync();
        await claimant.PostAsJsonAsync("/api/v1/client-portal/consent", new { noticeVersion = "v1" });

        var withdrawn = await claimant.PostAsync("/api/v1/client-portal/consent/withdraw", null);

        Assert.Equal("Withdrawn", (await MockedApiFactory.ReadJsonAsync(withdrawn)).GetProperty("state").GetString());
        Assert.Equal(HttpStatusCode.Forbidden, (await UploadAsync(claimant, info)).StatusCode);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var records = await scope.ServiceProvider.GetRequiredService<IConsentRepository>()
                .ListForCustomerAsync(TenantScope.Create(tenant, "workspace-main"), ClaimantConsent.Identifier(info.Email));
            Assert.Equal(2, records.Count);
            Assert.Contains(records, record => record.Basis == ConsentBasis.Withdrawn && record.WithdrawnAt is not null);
            Assert.Contains(records, record => record.Basis == ConsentBasis.ExplicitOptIn);
        }

        Assert.Contains("claimant.consent.withdrawn", await staff.GetStringAsync("/api/v1/audit/events"));

        // Agreeing again lifts the block.
        await claimant.PostAsJsonAsync("/api/v1/client-portal/consent", new { noticeVersion = "v1" });
        Assert.Equal(HttpStatusCode.OK, (await UploadAsync(claimant, info)).StatusCode);
    }

    [Fact]
    public async Task StaffSeeWhetherTheClaimantHasAgreed()
    {
        var (staff, info, claimant, _) = await ArrangeAsync();
        Assert.Equal("NotGiven", (await MockedApiFactory.ReadJsonAsync(await staff.GetAsync($"/api/v1/cases/{info.CaseId}/privacy-consent"))).GetProperty("state").GetString());

        await claimant.PostAsJsonAsync("/api/v1/client-portal/consent", new { noticeVersion = "v1" });
        var given = await MockedApiFactory.ReadJsonAsync(await staff.GetAsync($"/api/v1/cases/{info.CaseId}/privacy-consent"));
        Assert.Equal("Given", given.GetProperty("state").GetString());
        Assert.Equal("v1", given.GetProperty("consentedVersion").GetString());

        await claimant.PostAsync("/api/v1/client-portal/consent/withdraw", null);
        Assert.Equal("Withdrawn", (await MockedApiFactory.ReadJsonAsync(await staff.GetAsync($"/api/v1/cases/{info.CaseId}/privacy-consent"))).GetProperty("state").GetString());
    }

    [Fact]
    public async Task AnotherWorkspaceNeverSeesOrInheritsAConsent()
    {
        var (_, info, claimant, _) = await ArrangeAsync();
        await claimant.PostAsJsonAsync("/api/v1/client-portal/consent", new { noticeVersion = "v1" });
        using var stranger = factory.CreateScopedClient(MockedApiFactory.NewTenant());

        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync($"/api/v1/cases/{info.CaseId}/privacy-consent")).StatusCode);

        // The same email filing in another workspace has agreed to nothing there.
        var otherTenant = MockedApiFactory.NewTenant();
        using var otherStaff = factory.CreateScopedClient(otherTenant);
        var other = await TestData.CreateCaseAsync(otherStaff, otherTenant);
        using var otherClaimant = await TestData.ClaimantClientAsync(factory, otherStaff, other, agreeToPrivacyNotice: false);
        Assert.Equal("NotGiven", (await NoticeAsync(otherClaimant)).GetProperty("state").GetString());
        Assert.Equal(HttpStatusCode.Forbidden, (await UploadAsync(otherClaimant, other)).StatusCode);
    }

    [Theory]
    [InlineData("ClaimsOfficer")]
    [InlineData("CustomerService")]
    [InlineData("Broker")]
    public async Task OnlyAdministratorsMayWriteTheNotice(string role)
    {
        using var client = factory.CreateScopedClient(MockedApiFactory.NewTenant(), role: role);

        var response = await client.PutAsJsonAsync("/api/v1/admin/tenant-settings", new { privacyNoticeText = "Changed by someone else." });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task OnlyARealChangeRaisesTheVersion_AndAnEmptyTextGoesBackToTheDefault()
    {
        using var admin = factory.CreateScopedClient(MockedApiFactory.NewTenant());

        var first = await MockedApiFactory.ReadJsonAsync(await admin.PutAsJsonAsync("/api/v1/admin/tenant-settings", new { privacyNoticeText = "Notice A" }));
        var same = await MockedApiFactory.ReadJsonAsync(await admin.PutAsJsonAsync("/api/v1/admin/tenant-settings", new { privacyNoticeText = "  Notice A  " }));
        var reset = await MockedApiFactory.ReadJsonAsync(await admin.PutAsJsonAsync("/api/v1/admin/tenant-settings", new { privacyNoticeText = "" }));

        Assert.Equal(2, first.GetProperty("privacyNoticeVersion").GetInt32());
        Assert.Equal(2, same.GetProperty("privacyNoticeVersion").GetInt32());
        Assert.Equal(3, reset.GetProperty("privacyNoticeVersion").GetInt32());
        Assert.Equal(JsonValueKind.Null, reset.GetProperty("privacyNoticeText").ValueKind);
        Assert.Contains("What we collect", reset.GetProperty("defaultPrivacyNoticeText").GetString());
    }

    [Fact]
    public async Task ATooLongNoticeIsRefused()
    {
        using var admin = factory.CreateScopedClient(MockedApiFactory.NewTenant());

        var response = await admin.PutAsJsonAsync("/api/v1/admin/tenant-settings", new { privacyNoticeText = new string('a', 4001) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("at most 4,000 characters", await response.Content.ReadAsStringAsync());
    }
}
