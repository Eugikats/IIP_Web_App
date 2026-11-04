using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using InsuranceIntelligence.Api.Jobs;
using InsuranceIntelligence.Api.Tests.Support;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Jobs;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace InsuranceIntelligence.Api.Tests;

/// <summary>Build-loop item 25: a claims pack emailed to the people the insurer names, attached when small and
/// as a secure expiring link when large, by hand or by a chosen trigger, once per reason, audited.</summary>
public sealed class ClaimsPackEmailApiTests(MockedApiFactory factory) : IClassFixture<MockedApiFactory>
{
    private static readonly string[] Queues = [BackgroundQueues.ClaimReview];

    private static Task<HttpResponseMessage> SaveAsync(HttpClient admin, object body) =>
        admin.PutAsJsonAsync("/api/v1/admin/claims-pack-delivery", body);

    private static async Task ConfigureAsync(HttpClient admin, string[] recipients, string trigger = "Manual", string? status = null)
    {
        var response = await SaveAsync(admin, new { recipients, trigger, triggerStatus = status });
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
    }

    private static async Task<(Guid CaseId, CaseInfo Info)> CreateAsync(HttpClient admin, string tenant)
    {
        var claim = await TestData.CreateCaseAsync(admin, tenant);
        return (claim.CaseId, claim);
    }

    private static Task<HttpResponseMessage> SendNowAsync(HttpClient client, Guid caseId) =>
        client.PostAsync($"/api/v1/compliance/cases/{caseId}/claims-pack/send", null);

    private static async Task<JsonElement[]> PackMailsAsync(HttpClient admin) =>
        (await MockedApiFactory.ReadJsonAsync(await admin.GetAsync("/api/v1/dev/mock-outbox"))).EnumerateArray()
            .Where(message => message.GetProperty("subject").GetString()!.StartsWith("Claims pack CLM-", StringComparison.Ordinal))
            .ToArray();

    private static async Task UploadAsync(HttpClient claimant, Guid caseId, string slotKey, string fileName, byte[] bytes, string contentType)
    {
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        form.Add(file, "files", fileName);
        form.Add(new StringContent(slotKey), "documentTypeKey");
        var response = await claimant.PostAsync($"/api/v1/client-portal/claims/{caseId}/documents", form);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task SettingsStartWithNobodyAndByHandAndCanBeSavedAndAreAudited()
    {
        var tenant = MockedApiFactory.NewTenant();
        using var admin = factory.CreateScopedClient(tenant);

        var start = await MockedApiFactory.ReadJsonAsync(await admin.GetAsync("/api/v1/admin/claims-pack-delivery"));
        Assert.Empty(start.GetProperty("recipients").EnumerateArray());
        Assert.Equal("Manual", start.GetProperty("trigger").GetString());
        Assert.Equal(10, start.GetProperty("maxRecipients").GetInt32());

        await ConfigureAsync(admin, ["reinsurer@example.com", " Assessor@Example.com "], "Complete");

        var saved = await MockedApiFactory.ReadJsonAsync(await admin.GetAsync("/api/v1/admin/claims-pack-delivery"));
        Assert.Equal(["reinsurer@example.com", "Assessor@Example.com"], saved.GetProperty("recipients").EnumerateArray().Select(item => item.GetString()!).ToArray());
        Assert.Equal("Complete", saved.GetProperty("trigger").GetString());
        Assert.Contains("settings.updated", await admin.GetStringAsync("/api/v1/audit/events?pageSize=200"), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task BadAddressesTooManyAddressesAndAnAutomaticTriggerWithNobodyAreRefused()
    {
        using var admin = factory.CreateScopedClient(MockedApiFactory.NewTenant());

        Assert.Equal(HttpStatusCode.BadRequest, (await SaveAsync(admin, new { recipients = new[] { "not an address" }, trigger = "Manual" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await SaveAsync(admin, new { recipients = Enumerable.Range(0, 11).Select(index => $"p{index}@example.com").ToArray(), trigger = "Manual" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await SaveAsync(admin, new { recipients = Array.Empty<string>(), trigger = "Complete" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await SaveAsync(admin, new { recipients = new[] { "a@example.com" }, trigger = "Status" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await SaveAsync(admin, new { recipients = new[] { "a@example.com" }, trigger = "Whenever" })).StatusCode);
    }

    [Theory]
    [InlineData("ClaimsOfficer")]
    [InlineData("ClaimsSupervisor")]
    [InlineData("Compliance")]
    [InlineData("Broker")]
    public async Task OnlyAdministratorsCanReadOrChangeTheDeliverySettings(string role)
    {
        using var other = factory.CreateScopedClient(MockedApiFactory.NewTenant(), role: role);

        Assert.Equal(HttpStatusCode.Forbidden, (await other.GetAsync("/api/v1/admin/claims-pack-delivery")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await SaveAsync(other, new { recipients = new[] { "a@example.com" }, trigger = "Manual" })).StatusCode);
    }

    [Fact]
    public async Task SendingWithNobodyToSendToSaysSoAndSendsNothing()
    {
        var tenant = MockedApiFactory.NewTenant();
        using var admin = factory.CreateScopedClient(tenant);
        var (caseId, _) = await CreateAsync(admin, tenant);

        var response = await SendNowAsync(admin, caseId);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("NoRecipients", (await MockedApiFactory.ReadJsonAsync(response)).GetProperty("outcome").GetString());
        Assert.Empty(await PackMailsAsync(admin));
    }

    [Fact]
    public async Task ASmallPackIsAttachedToTheEmailForEachRecipientAndTheSendIsRecorded()
    {
        var tenant = MockedApiFactory.NewTenant();
        using var admin = factory.CreateScopedClient(tenant);
        await ConfigureAsync(admin, ["reinsurer@example.com", "assessor@example.com"]);
        var (caseId, _) = await CreateAsync(admin, tenant);

        var response = await SendNowAsync(admin, caseId);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await MockedApiFactory.ReadJsonAsync(response);
        Assert.Equal("Attached", result.GetProperty("mode").GetString());
        Assert.Equal(2, result.GetProperty("recipients").GetInt32());

        var mails = await PackMailsAsync(admin);
        Assert.Equal(2, mails.Length);
        Assert.Equal(["assessor@example.com", "reinsurer@example.com"], mails.Select(mail => mail.GetProperty("recipient").GetString()!).OrderBy(name => name).ToArray());
        var reference = $"CLM-{caseId.ToString("N")[..8].ToUpperInvariant()}";
        foreach (var mail in mails)
        {
            Assert.Equal($"Claims pack {reference}", mail.GetProperty("subject").GetString());
            var attachment = Assert.Single(mail.GetProperty("attachments").EnumerateArray());
            Assert.Equal($"ClaimsPack_{reference}.zip", attachment.GetProperty("fileName").GetString());
            Assert.True(attachment.GetProperty("sizeBytes").GetInt64() > 100);
            Assert.Contains("contains personal information", mail.GetProperty("body").GetString(), StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("/shared/claims-pack", mail.GetProperty("body").GetString());
        }

        var history = (await MockedApiFactory.ReadJsonAsync(await admin.GetAsync($"/api/v1/compliance/cases/{caseId}/claims-pack/history"))).EnumerateArray().ToArray();
        var entry = Assert.Single(history);
        Assert.Equal("Attached", entry.GetProperty("mode").GetString());
        Assert.Equal("Sent", entry.GetProperty("outcome").GetString());
        Assert.Equal(["r***@example.com", "a***@example.com"], entry.GetProperty("recipients").EnumerateArray().Select(item => item.GetString()!).ToArray());
        Assert.Contains("claims_pack.emailed", await admin.GetStringAsync("/api/v1/audit/events?pageSize=200"));
    }

    [Fact]
    public async Task EachPressOfSendNowSendsAgainAndEachIsInTheHistory()
    {
        var tenant = MockedApiFactory.NewTenant();
        using var admin = factory.CreateScopedClient(tenant);
        await ConfigureAsync(admin, ["reinsurer@example.com"]);
        var (caseId, _) = await CreateAsync(admin, tenant);

        await SendNowAsync(admin, caseId);
        await SendNowAsync(admin, caseId);

        Assert.Equal(2, (await PackMailsAsync(admin)).Length);
        Assert.Equal(2, (await MockedApiFactory.ReadJsonAsync(await admin.GetAsync($"/api/v1/compliance/cases/{caseId}/claims-pack/history"))).GetArrayLength());
    }

    [Fact]
    public async Task ALargePackGoesAsASecureLinkThatDownloadsTheSamePackAndStopsAfterTenOpens()
    {
        var tenant = MockedApiFactory.NewTenant();
        using var admin = factory.CreateScopedClient(tenant);
        await ConfigureAsync(admin, ["reinsurer@example.com"]);
        var (caseId, claim) = await CreateAsync(admin, tenant);
        using var claimant = await TestData.ClaimantClientAsync(factory, admin, claim);
        var big = new byte[4 * 1024 * 1024];
        new Random(1).NextBytes(big);
        await UploadAsync(claimant, caseId, "PhotoEvidence", "IMG_big.jpg", big, "image/jpeg");

        var response = await SendNowAsync(admin, caseId);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await MockedApiFactory.ReadJsonAsync(response);
        Assert.Equal("Link", result.GetProperty("mode").GetString());
        Assert.NotEqual(JsonValueKind.Null, result.GetProperty("linkExpiresAt").ValueKind);
        var mail = Assert.Single(await PackMailsAsync(admin));
        Assert.Empty(mail.GetProperty("attachments").EnumerateArray());
        var body = mail.GetProperty("body").GetString()!;
        Assert.Contains("too large to attach", body);
        Assert.Contains("at most 10 downloads", body);
        var token = Uri.UnescapeDataString(Regex.Match(body, "token=([^\\s]+)").Groups[1].Value);

        // Anyone holding the link can download the pack, with no sign in, and it is the real pack.
        using var anonymous = factory.CreateClient();
        var download = await anonymous.GetAsync($"/api/v1/shared/claims-packs/{Uri.EscapeDataString(token)}");
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal("application/zip", download.Content.Headers.ContentType!.MediaType);
        using var archive = new ZipArchive(new MemoryStream(await download.Content.ReadAsByteArrayAsync()), ZipArchiveMode.Read);
        Assert.Contains(archive.Entries, entry => entry.FullName.StartsWith("00_ClaimSummary_", StringComparison.Ordinal));
        Assert.Contains(archive.Entries, entry => entry.FullName.StartsWith("02_Evidence/", StringComparison.Ordinal));
        Assert.Contains("claims_pack.link.opened", await admin.GetStringAsync("/api/v1/audit/events?pageSize=200"));
        Assert.DoesNotContain(token, await admin.GetStringAsync("/api/v1/audit/events?pageSize=200"));

        for (var opened = 2; opened <= 10; opened++)
        {
            Assert.Equal(HttpStatusCode.OK, (await anonymous.GetAsync($"/api/v1/shared/claims-packs/{Uri.EscapeDataString(token)}")).StatusCode);
        }

        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync($"/api/v1/shared/claims-packs/{Uri.EscapeDataString(token)}")).StatusCode);
    }

    [Fact]
    public async Task AGuessedOrMadeUpLinkGetsTheSameAnswerAsAnExpiredOne()
    {
        using var anonymous = factory.CreateClient();

        var unknown = await anonymous.GetAsync("/api/v1/shared/claims-packs/not-a-real-token");
        var tooLong = await anonymous.GetAsync($"/api/v1/shared/claims-packs/{new string('a', 300)}");

        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, tooLong.StatusCode);
        Assert.Equal(await unknown.Content.ReadAsStringAsync(), await tooLong.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("ClaimsOfficer")]
    [InlineData("Broker")]
    [InlineData("Client")]
    public async Task OnlyRolesThatMayExportEvidenceCanSendAPackOrSeeItsHistory(string role)
    {
        var tenant = MockedApiFactory.NewTenant();
        using var admin = factory.CreateScopedClient(tenant);
        await ConfigureAsync(admin, ["reinsurer@example.com"]);
        var (caseId, _) = await CreateAsync(admin, tenant);
        using var other = factory.CreateScopedClient(tenant, role: role);

        Assert.Equal(HttpStatusCode.Forbidden, (await SendNowAsync(other, caseId)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await other.GetAsync($"/api/v1/compliance/cases/{caseId}/claims-pack/history")).StatusCode);
        Assert.Empty(await PackMailsAsync(admin));
    }

    [Fact]
    public async Task AnotherWorkspaceCannotSendOrReadThisClaimsPackHistoryAndHasItsOwnRecipients()
    {
        var tenant = MockedApiFactory.NewTenant();
        using var admin = factory.CreateScopedClient(tenant);
        await ConfigureAsync(admin, ["reinsurer@example.com"]);
        var (caseId, _) = await CreateAsync(admin, tenant);
        using var intruder = factory.CreateScopedClient(MockedApiFactory.NewTenant());
        await ConfigureAsync(intruder, ["other@example.com"]);

        Assert.Equal(HttpStatusCode.NotFound, (await SendNowAsync(intruder, caseId)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await intruder.GetAsync($"/api/v1/compliance/cases/{caseId}/claims-pack/history")).StatusCode);
        Assert.Empty(await PackMailsAsync(admin));
        Assert.Empty(await PackMailsAsync(intruder));
    }

    // ---- triggers, run through the background queue ----

    private WebApplicationFactory<Program> QueueHost() =>
        factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.Replace(ServiceDescriptor.Singleton(new BackgroundPipelineOptions { Inline = true, WorkerEnabled = false, BackoffSeconds = [0], ReviewSettleSeconds = 0 }))));

    private static async Task DrainAsync(WebApplicationFactory<Program> host, string tenant)
    {
        var processor = host.Services.GetRequiredService<IBackgroundJobProcessor>();
        var scope = TenantScope.Create(tenant, "workspace-main");
        for (var ran = 0; ran < 20 && await processor.ProcessNextAsync(Queues, "test-worker", CancellationToken.None, scope); ran++)
        {
        }
    }

    [Fact]
    public async Task WhenTheDocumentsAreCompleteThePackIsEmailedOnceInTheBackground()
    {
        await using var host = QueueHost();
        var tenant = MockedApiFactory.NewTenant();
        using var admin = host.CreateScopedClient(tenant);
        await ConfigureAsync(admin, ["reinsurer@example.com"], "Complete");
        var (caseId, claim) = await CreateAsync(admin, tenant);
        using var claimant = await TestData.ClaimantClientAsync(host, admin, claim);

        await UploadAsync(claimant, caseId, "ClaimForm", "form.pdf", System.Text.Encoding.UTF8.GetBytes("IIP-MOCK-DOC:ClaimForm form"), "application/pdf");
        await DrainAsync(host, tenant);
        Assert.Empty(await PackMailsAsync(admin));

        await UploadAsync(claimant, caseId, "PoliceAbstract", "police.pdf", System.Text.Encoding.UTF8.GetBytes("IIP-MOCK-DOC:PoliceAbstract police"), "application/pdf");
        await UploadAsync(claimant, caseId, "PhotoEvidence", "photo.jpg", new byte[3000], "image/jpeg");
        Assert.Equal(100, (await MockedApiFactory.ReadJsonAsync(await admin.GetAsync($"/api/v1/cases/{caseId}"))).GetProperty("completenessScore").GetDecimal());
        await DrainAsync(host, tenant);

        var mail = Assert.Single(await PackMailsAsync(admin));
        Assert.Equal("reinsurer@example.com", mail.GetProperty("recipient").GetString());
        var entry = Assert.Single((await MockedApiFactory.ReadJsonAsync(await admin.GetAsync($"/api/v1/compliance/cases/{caseId}/claims-pack/history"))).EnumerateArray());
        Assert.Equal("Sent", entry.GetProperty("outcome").GetString());
        Assert.Contains("complete", entry.GetProperty("reason").GetString());
        Assert.Equal("system:claims-pack", entry.GetProperty("sentBy").GetString());

        // Replacing a document while the claim stays complete does not send the pack again.
        await UploadAsync(claimant, caseId, "PoliceAbstract", "police2.pdf", System.Text.Encoding.UTF8.GetBytes("IIP-MOCK-DOC:PoliceAbstract newer"), "application/pdf");
        await DrainAsync(host, tenant);
        Assert.Single(await PackMailsAsync(admin));
    }

    [Fact]
    public async Task WhenTheClaimReachesTheChosenStatusThePackIsEmailedAndOtherStatusesDoNotSendIt()
    {
        await using var host = QueueHost();
        var tenant = MockedApiFactory.NewTenant();
        using var admin = host.CreateScopedClient(tenant);
        await ConfigureAsync(admin, ["reinsurer@example.com"], "Status", "ReadyForReview");
        var (caseId, _) = await CreateAsync(admin, tenant);

        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync($"/api/v1/cases/{caseId}/status", new { toStatus = "InReview", reason = "Started." })).StatusCode);
        await DrainAsync(host, tenant);
        Assert.Empty(await PackMailsAsync(admin));

        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync($"/api/v1/cases/{caseId}/status", new { toStatus = "ReadyForReview", reason = "Evidence is in." })).StatusCode);
        await DrainAsync(host, tenant);

        Assert.Single(await PackMailsAsync(admin));
        Assert.Contains("ready for review", (await MockedApiFactory.ReadJsonAsync(await admin.GetAsync($"/api/v1/compliance/cases/{caseId}/claims-pack/history"))).EnumerateArray().Single().GetProperty("reason").GetString());
    }

    [Fact]
    public async Task WithTheManualTriggerNothingIsSentByItself()
    {
        await using var host = QueueHost();
        var tenant = MockedApiFactory.NewTenant();
        using var admin = host.CreateScopedClient(tenant);
        await ConfigureAsync(admin, ["reinsurer@example.com"], "Manual");
        var (caseId, _) = await CreateAsync(admin, tenant);

        await admin.PostAsJsonAsync($"/api/v1/cases/{caseId}/status", new { toStatus = "InReview", reason = "Started." });
        await DrainAsync(host, tenant);

        Assert.Empty(await PackMailsAsync(admin));
    }
}
