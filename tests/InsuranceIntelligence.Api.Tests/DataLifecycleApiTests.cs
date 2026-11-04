using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using InsuranceIntelligence.Api.Tests.Support;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Compliance;
using InsuranceIntelligence.Domain.CustomerAssistant;
using InsuranceIntelligence.Domain.DataLifecycle;
using InsuranceIntelligence.Domain.Documents;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace InsuranceIntelligence.Api.Tests;

/// <summary>
/// Build-loop item 34: retention periods that really delete (records and stored files), legal holds that stop every deletion, the erasure of one
/// person's data with evidence that it happened, and none of it ever reaching another workspace.
/// </summary>
public sealed class DataLifecycleApiTests(MockedApiFactory factory) : IClassFixture<MockedApiFactory>
{
    private sealed class ManualTime : TimeProvider
    {
        private DateTimeOffset now = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => now;
        public void Advance(TimeSpan by) => now += by;
    }

    private sealed class BrokenStorage : IDocumentStorageProvider
    {
        public Task<DocumentStorageResult> StoreAsync(DocumentStorageRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<int> DeleteCaseObjectsAsync(TenantScope scope, Guid caseId, CancellationToken cancellationToken = default) => throw new InvalidOperationException("the file store is down");
    }

    private WebApplicationFactory<Program> With(ManualTime? time = null, bool brokenStorage = false) =>
        factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            if (time is not null)
            {
                services.Replace(ServiceDescriptor.Singleton<TimeProvider>(time));
            }

            if (brokenStorage)
            {
                services.Replace(ServiceDescriptor.Scoped<IDocumentStorageProvider, BrokenStorage>());
            }
        }));

    private static string StorageRoot(WebApplicationFactory<Program> host) =>
        host.Services.GetRequiredService<IConfiguration>()["INSURANCEINTELLIGENCE_LOCAL_DOCUMENT_STORAGE_PATH"]!;

    private static string[] FilesFor(WebApplicationFactory<Program> host, string tenant, Guid caseId)
    {
        var directory = Path.Combine(StorageRoot(host), tenant, "workspace-main", caseId.ToString());
        return Directory.Exists(directory) ? Directory.GetFiles(directory, "*", SearchOption.AllDirectories) : [];
    }

    private static async Task UploadAsync(HttpClient claimant, Guid caseId)
    {
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes("IIP-MOCK-DOC:PoliceAbstract"));
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(file, "files", "police.pdf");
        form.Add(new StringContent("PoliceAbstract"), "documentTypeKey");
        var response = await claimant.PostAsync($"/api/v1/client-portal/claims/{caseId}/documents", form);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
    }

    /// <summary>A claim with one uploaded document, its stored file, and the customer's consent record.</summary>
    private static async Task<(CaseInfo Claim, HttpClient Admin)> ClaimWithDocumentAsync(WebApplicationFactory<Program> host, string? tenant = null)
    {
        tenant ??= MockedApiFactory.NewTenant();
        var admin = host.CreateScopedClient(tenant);
        var claim = await TestData.CreateCaseAsync(admin, tenant);
        using var claimant = await TestData.ClaimantClientAsync(host, admin, claim);
        await UploadAsync(claimant, claim.CaseId);
        return (claim, admin);
    }

    private static async Task<JsonElement> ErasePreviewAsync(HttpClient admin, object subject) =>
        await MockedApiFactory.ReadJsonAsync(await admin.PostAsJsonAsync("/api/v1/admin/data/erasure/preview", subject));

    private static async Task<HttpResponseMessage> EraseAsync(HttpClient admin, string phone, string reason = "Request received 3 October", string confirm = "ERASE") =>
        await admin.PostAsJsonAsync("/api/v1/admin/data/erasure", new { phone, reason, confirm });

    private static async Task<JsonElement> DeletionsAsync(HttpClient admin) =>
        await MockedApiFactory.ReadJsonAsync(await admin.GetAsync("/api/v1/admin/data/deletions"));

    private static async Task SaveTurnAsync(WebApplicationFactory<Program> host, string tenant, string contact, string text)
    {
        using var services = host.Services.CreateScope();
        await services.ServiceProvider.GetRequiredService<ICustomerConversationRepository>().SaveAsync(new CustomerConversationTurn
        {
            TurnId = Guid.NewGuid(),
            Scope = TenantScope.Create(tenant, "workspace-main"),
            ThreadId = $"wa:{Guid.NewGuid():N}",
            ContactWaId = contact,
            InboundMessageText = text,
            ClassifiedIntent = CustomerIntent.Unknown,
            Outcome = CustomerAssistantOutcome.AutoReplied,
            OccurredAt = DateTimeOffset.UtcNow,
            CorrelationId = "test",
        });
    }

    private static async Task<int> TurnCountAsync(WebApplicationFactory<Program> host, string tenant)
    {
        using var services = host.Services.CreateScope();
        var db = services.ServiceProvider.GetRequiredService<InsuranceIntelligence.Infrastructure.Persistence.InsuranceIntelligenceDbContext>();
        return db.CustomerConversationTurns.Count(item => item.TenantId == tenant);
    }

    // ---- erasure

    [Fact]
    public async Task ThePreviewCountsWhatWouldGoAndDeletesNothing()
    {
        using var host = With();
        var (claim, admin) = await ClaimWithDocumentAsync(host);
        using var _ = admin;

        var preview = await ErasePreviewAsync(admin, new { phone = claim.Phone });

        Assert.Equal(1, preview.GetProperty("claimsThatWillBeErased").GetInt32());
        Assert.Equal(1, preview.GetProperty("counts").GetProperty("documents").GetInt32());
        Assert.False(preview.GetProperty("nothingFound").GetBoolean());
        Assert.Single(FilesFor(host, claim.TenantId, claim.CaseId), file => file.EndsWith("police.pdf", StringComparison.Ordinal));
        var documents = await MockedApiFactory.ReadJsonAsync(await admin.GetAsync($"/api/v1/cases/{claim.CaseId}/documents"));
        Assert.Equal(1, documents.GetArrayLength());
    }

    [Fact]
    public async Task ErasingAPersonDeletesTheirFilesAndRecordsAndKeepsAnAnonymousClaimAndEvidence()
    {
        using var host = With();
        var (claim, admin) = await ClaimWithDocumentAsync(host);
        using var _ = admin;
        await SaveTurnAsync(host, claim.TenantId, claim.Phone.TrimStart('+'), "I lost my car, my number is " + claim.Phone);
        await SaveTurnAsync(host, claim.TenantId, "256799999999", "Someone else entirely");
        Assert.NotEmpty(FilesFor(host, claim.TenantId, claim.CaseId));

        var response = await EraseAsync(admin, claim.Phone);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var record = await MockedApiFactory.ReadJsonAsync(response);
        Assert.Equal("Erasure", record.GetProperty("kind").GetString());
        Assert.Equal(1, record.GetProperty("counts").GetProperty("documents").GetInt32());
        Assert.Equal(1, record.GetProperty("counts").GetProperty("claims").GetInt32());
        Assert.True(record.GetProperty("counts").GetProperty("whatsapp-conversations").GetInt32() >= 1);
        Assert.True(record.GetProperty("storedFilesDeleted").GetInt32() >= 1);
        Assert.Empty(FilesFor(host, claim.TenantId, claim.CaseId));
        Assert.Equal(0, (await MockedApiFactory.ReadJsonAsync(await admin.GetAsync($"/api/v1/cases/{claim.CaseId}/documents"))).GetArrayLength());
        Assert.Equal(1, await TurnCountAsync(host, claim.TenantId));

        // The claim remains for the insurer's records, without the person.
        var kept = await MockedApiFactory.ReadJsonAsync(await admin.GetAsync($"/api/v1/cases/{claim.CaseId}"));
        Assert.Equal("Erased", kept.GetProperty("customerName").GetString());
        Assert.DoesNotContain(claim.Email, kept.GetRawText());
        Assert.DoesNotContain(claim.Phone, kept.GetRawText());
        Assert.NotNull(kept.GetProperty("personalDataErasedAt").GetString());
    }

    [Fact]
    public async Task TheEvidenceOfAnErasureNamesWhatWentByKindAndNumberButNeverTheDataOrThePhone()
    {
        using var host = With();
        var (claim, admin) = await ClaimWithDocumentAsync(host);
        using var _ = admin;
        await EraseAsync(admin, claim.Phone, reason: "Written request from the customer");

        var deletions = await DeletionsAsync(admin);

        var record = Assert.Single(deletions.EnumerateArray());
        var text = record.GetRawText();
        Assert.Equal("Written request from the customer", record.GetProperty("reason").GetString());
        Assert.Equal(new ErasureSubject(claim.Phone, null, null).Fingerprint(), record.GetProperty("subject").GetString());
        Assert.DoesNotContain(claim.Phone.TrimStart('+'), text);
        Assert.DoesNotContain(claim.Email, text);
        Assert.DoesNotContain("police.pdf", text);
        using var services = host.Services.CreateScope();
        var audit = await services.ServiceProvider.GetRequiredService<IAuditRepository>().ListAsync(TenantScope.Create(claim.TenantId, "workspace-main"));
        var event_ = Assert.Single(audit, item => item.ActionName == "data.erasure.completed");
        Assert.DoesNotContain(claim.Phone.TrimStart('+'), event_.AuditReason);
    }

    [Fact]
    public async Task ErasingTwiceIsHarmlessAndThePersonIsStillFoundByTheirPolicyNumber()
    {
        using var host = With();
        var (claim, admin) = await ClaimWithDocumentAsync(host);
        using var _ = admin;

        Assert.Equal(HttpStatusCode.OK, (await EraseAsync(admin, claim.Phone)).StatusCode);
        var second = await MockedApiFactory.ReadJsonAsync(await admin.PostAsJsonAsync("/api/v1/admin/data/erasure", new { policyNumber = claim.PolicyNumber, reason = "Second request", confirm = "ERASE" }));

        Assert.False(second.GetProperty("counts").TryGetProperty("documents", out var _unused));
        Assert.Equal(0, second.GetProperty("failures").GetArrayLength());
    }

    [Theory]
    [InlineData("", "Request received", "ERASE")]
    [InlineData("0700123456", "", "ERASE")]
    [InlineData("0700123456", "no", "ERASE")]
    [InlineData("0700123456", "Request received", "erase")]
    [InlineData("0700123456", "Request received", "")]
    public async Task AnErasureNeedsAWhoAWhyAndTheWordErase(string phone, string reason, string confirm)
    {
        using var host = With();
        using var admin = host.CreateScopedClient(MockedApiFactory.NewTenant());

        var response = await admin.PostAsJsonAsync("/api/v1/admin/data/erasure", new { phone, reason, confirm });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty((await DeletionsAsync(admin)).EnumerateArray());
    }

    [Fact]
    public async Task ASubjectWithNothingStoredIsReportedAsSuchAndLeavesAnHonestRecord()
    {
        using var host = With();
        using var admin = host.CreateScopedClient(MockedApiFactory.NewTenant());

        var preview = await ErasePreviewAsync(admin, new { phone = "+256700555555" });
        var response = await EraseAsync(admin, "+256700555555");

        Assert.True(preview.GetProperty("nothingFound").GetBoolean());
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(0, (await MockedApiFactory.ReadJsonAsync(response)).GetProperty("counts").EnumerateObject().Sum(item => item.Value.GetInt32()));
    }

    // ---- legal hold

    [Fact]
    public async Task AClaimOnLegalHoldKeepsItsFilesAndRecordsThroughAnErasureAndIsNamedInTheEvidence()
    {
        using var host = With();
        var (claim, admin) = await ClaimWithDocumentAsync(host);
        using var _ = admin;
        var reference = ClaimReference.For(claim.CaseId);
        var hold = await admin.PostAsJsonAsync("/api/v1/admin/data/legal-holds", new { claimReference = reference, reason = "Court case CS 14 of 2026" });
        Assert.Equal(HttpStatusCode.OK, hold.StatusCode);

        var preview = await ErasePreviewAsync(admin, new { phone = claim.Phone });
        var record = await MockedApiFactory.ReadJsonAsync(await EraseAsync(admin, claim.Phone));

        Assert.Equal(1, preview.GetProperty("claimsOnHold").GetInt32());
        Assert.Equal(0, preview.GetProperty("claimsThatWillBeErased").GetInt32());
        Assert.Equal(reference, record.GetProperty("heldClaims")[0].GetString());
        Assert.NotEmpty(FilesFor(host, claim.TenantId, claim.CaseId));
        Assert.Equal(1, (await MockedApiFactory.ReadJsonAsync(await admin.GetAsync($"/api/v1/cases/{claim.CaseId}/documents"))).GetArrayLength());
        Assert.NotEqual("Erased", (await MockedApiFactory.ReadJsonAsync(await admin.GetAsync($"/api/v1/cases/{claim.CaseId}"))).GetProperty("customerName").GetString());
    }

    [Fact]
    public async Task ReleasingTheHoldLetsALaterErasureGoAhead()
    {
        using var host = With();
        var (claim, admin) = await ClaimWithDocumentAsync(host);
        using var _ = admin;
        await admin.PostAsJsonAsync("/api/v1/admin/data/legal-holds", new { claimReference = ClaimReference.For(claim.CaseId), reason = "Investigation open" });
        var listed = await MockedApiFactory.ReadJsonAsync(await admin.GetAsync("/api/v1/admin/data/legal-holds"));
        Assert.Equal("Investigation open", listed[0].GetProperty("reason").GetString());

        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/v1/admin/data/legal-holds/{claim.CaseId}")).StatusCode);
        var record = await MockedApiFactory.ReadJsonAsync(await EraseAsync(admin, claim.Phone));

        Assert.Equal(0, (await MockedApiFactory.ReadJsonAsync(await admin.GetAsync("/api/v1/admin/data/legal-holds"))).GetArrayLength());
        Assert.Equal(1, record.GetProperty("counts").GetProperty("claims").GetInt32());
        Assert.Empty(FilesFor(host, claim.TenantId, claim.CaseId));
    }

    [Theory]
    [InlineData("CLM-00000000", "A proper reason", "No claim in this workspace")]
    [InlineData("", "A proper reason", "No claim in this workspace")]
    public async Task AHoldNeedsARealClaim(string reference, string reason, string expected)
    {
        using var host = With();
        using var admin = host.CreateScopedClient(MockedApiFactory.NewTenant());

        var response = await admin.PostAsJsonAsync("/api/v1/admin/data/legal-holds", new { claimReference = reference, reason });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(expected, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task AHoldNeedsAReasonAndCannotBePlacedTwice()
    {
        using var host = With();
        var (claim, admin) = await ClaimWithDocumentAsync(host);
        using var _ = admin;
        var reference = ClaimReference.For(claim.CaseId);

        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/v1/admin/data/legal-holds", new { claimReference = reference, reason = "x" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync("/api/v1/admin/data/legal-holds", new { claimReference = reference, reason = "A proper reason" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/v1/admin/data/legal-holds", new { claimReference = reference, reason = "Again, a proper reason" })).StatusCode);
    }

    // ---- isolation and permission

    [Fact]
    public async Task AnotherWorkspacesErasureNeverTouchesThisWorkspacesDataEvenForTheSameNumber()
    {
        using var host = With();
        var (claim, owner) = await ClaimWithDocumentAsync(host);
        using var _ = owner;
        using var other = host.CreateScopedClient(MockedApiFactory.NewTenant());
        await SaveTurnAsync(host, claim.TenantId, claim.Phone.TrimStart('+'), "mine");

        var preview = await ErasePreviewAsync(other, new { phone = claim.Phone });
        var record = await MockedApiFactory.ReadJsonAsync(await EraseAsync(other, claim.Phone));

        Assert.True(preview.GetProperty("nothingFound").GetBoolean());
        Assert.Equal(0, record.GetProperty("counts").EnumerateObject().Sum(item => item.Value.GetInt32()));
        Assert.NotEmpty(FilesFor(host, claim.TenantId, claim.CaseId));
        Assert.Equal(1, (await MockedApiFactory.ReadJsonAsync(await owner.GetAsync($"/api/v1/cases/{claim.CaseId}/documents"))).GetArrayLength());
        Assert.Equal(1, await TurnCountAsync(host, claim.TenantId));
        Assert.Empty((await DeletionsAsync(owner)).EnumerateArray());
    }

    [Fact]
    public async Task AnotherWorkspaceCannotHoldOrReleaseThisWorkspacesClaimOrSeeItsEvidence()
    {
        using var host = With();
        var (claim, owner) = await ClaimWithDocumentAsync(host);
        using var _ = owner;
        using var other = host.CreateScopedClient(MockedApiFactory.NewTenant());
        await owner.PostAsJsonAsync("/api/v1/admin/data/legal-holds", new { claimReference = ClaimReference.For(claim.CaseId), reason = "A proper reason" });
        await EraseAsync(owner, claim.Phone);

        Assert.Equal(HttpStatusCode.BadRequest, (await other.PostAsJsonAsync("/api/v1/admin/data/legal-holds", new { claimReference = ClaimReference.For(claim.CaseId), reason = "A proper reason" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.DeleteAsync($"/api/v1/admin/data/legal-holds/{claim.CaseId}")).StatusCode);
        Assert.Equal(0, (await MockedApiFactory.ReadJsonAsync(await other.GetAsync("/api/v1/admin/data/legal-holds"))).GetArrayLength());
        Assert.Empty((await DeletionsAsync(other)).EnumerateArray());
        Assert.Equal(1, (await MockedApiFactory.ReadJsonAsync(await owner.GetAsync("/api/v1/admin/data/legal-holds"))).GetArrayLength());
    }

    [Fact]
    public async Task OnlyAnAdministratorCanErasePlaceHoldsOrRunRetention()
    {
        using var host = With();
        var tenant = MockedApiFactory.NewTenant();
        using var officer = host.CreateScopedClient(tenant, role: "ClaimsOfficer", userId: "officer-1");

        Assert.Equal(HttpStatusCode.Forbidden, (await officer.PostAsJsonAsync("/api/v1/admin/data/erasure/preview", new { phone = "0700123456" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await EraseAsync(officer, "0700123456")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await officer.PostAsJsonAsync("/api/v1/admin/data/legal-holds", new { claimReference = "CLM-00000000", reason = "A proper reason" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await officer.PostAsync("/api/v1/admin/data/retention/run", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await officer.GetAsync("/api/v1/admin/data/deletions")).StatusCode);
    }

    // ---- when the file store fails

    [Fact]
    public async Task IfTheFileStoreFailsTheClaimIsNotErasedAndTheFailureIsRecordedForARetry()
    {
        using var host = With(brokenStorage: true);
        var (claim, admin) = await ClaimWithDocumentAsync(factory);
        using var _ = admin;
        var scopedAdmin = host.CreateScopedClient(claim.TenantId);

        var record = await MockedApiFactory.ReadJsonAsync(await EraseAsync(scopedAdmin, claim.Phone));

        Assert.Equal(1, record.GetProperty("failures").GetArrayLength());
        Assert.Contains(ClaimReference.For(claim.CaseId), record.GetProperty("failures")[0].GetString());
        Assert.Equal(1, (await MockedApiFactory.ReadJsonAsync(await scopedAdmin.GetAsync($"/api/v1/cases/{claim.CaseId}/documents"))).GetArrayLength());
        Assert.NotEqual("Erased", (await MockedApiFactory.ReadJsonAsync(await scopedAdmin.GetAsync($"/api/v1/cases/{claim.CaseId}"))).GetProperty("customerName").GetString());

        // With a working store the same request finishes the job.
        var retried = await MockedApiFactory.ReadJsonAsync(await EraseAsync(admin, claim.Phone));
        Assert.Equal(0, retried.GetProperty("failures").GetArrayLength());
        Assert.Equal(0, (await MockedApiFactory.ReadJsonAsync(await admin.GetAsync($"/api/v1/cases/{claim.CaseId}/documents"))).GetArrayLength());
    }

    // ---- retention periods

    private static async Task SetRetentionAsync(HttpClient admin, object months)
    {
        var response = await admin.PutAsJsonAsync("/api/v1/admin/tenant-settings", new { retentionMonths = months });
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task NothingIsDeletedWhenNoPeriodIsSet()
    {
        var time = new ManualTime();
        using var host = With(time);
        var (claim, admin) = await ClaimWithDocumentAsync(host);
        using var _ = admin;
        time.Advance(TimeSpan.FromDays(3650));

        var response = await admin.PostAsync("/api/v1/admin/data/retention/run", null);

        Assert.Equal(0, (await MockedApiFactory.ReadJsonAsync(response)).GetArrayLength());
        Assert.NotEmpty(FilesFor(host, claim.TenantId, claim.CaseId));
        Assert.Empty((await DeletionsAsync(admin)).EnumerateArray());
    }

    [Fact]
    public async Task ADocumentPastItsPeriodIsDeletedWithItsStoredFileAndTheClaimStaysAndANewerOneIsKept()
    {
        var time = new ManualTime();
        using var host = With(time);
        var (claim, admin) = await ClaimWithDocumentAsync(host);
        using var _ = admin;
        await SetRetentionAsync(admin, new Dictionary<string, int> { ["documents"] = 6 });

        var tooSoon = await MockedApiFactory.ReadJsonAsync(await admin.PostAsync("/api/v1/admin/data/retention/run", null));
        Assert.Equal(0, tooSoon.GetArrayLength());
        Assert.NotEmpty(FilesFor(host, claim.TenantId, claim.CaseId));

        time.Advance(TimeSpan.FromDays(200));
        var records = await MockedApiFactory.ReadJsonAsync(await admin.PostAsync("/api/v1/admin/data/retention/run", null));

        var record = Assert.Single(records.EnumerateArray());
        Assert.Equal("Retention", record.GetProperty("kind").GetString());
        Assert.Equal("documents", record.GetProperty("subject").GetString());
        Assert.Equal(1, record.GetProperty("counts").GetProperty("documents").GetInt32());
        Assert.DoesNotContain(FilesFor(host, claim.TenantId, claim.CaseId), file => file.EndsWith("police.pdf", StringComparison.Ordinal));
        Assert.Equal(0, (await MockedApiFactory.ReadJsonAsync(await admin.GetAsync($"/api/v1/cases/{claim.CaseId}/documents"))).GetArrayLength());
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync($"/api/v1/cases/{claim.CaseId}")).StatusCode);
    }

    [Fact]
    public async Task AClosedClaimPastItsPeriodIsDeletedWhollyButAnOpenOneAndAHeldOneAreNot()
    {
        var time = new ManualTime();
        using var host = With(time);
        var tenant = MockedApiFactory.NewTenant();
        var (closed, admin) = await ClaimWithDocumentAsync(host, tenant);
        using var _ = admin;
        var open = await TestData.CreateCaseAsync(admin, tenant);
        var held = await TestData.CreateCaseAsync(admin, tenant);
        using (var services = host.Services.CreateScope())
        {
            var cases = services.ServiceProvider.GetRequiredService<ICaseRepository>();
            await cases.SaveAsync((await cases.GetAsync(closed.CaseId))! with { Status = CaseStatus.Closed });
            await cases.SaveAsync((await cases.GetAsync(held.CaseId))! with { Status = CaseStatus.Settled });
        }

        await admin.PostAsJsonAsync("/api/v1/admin/data/legal-holds", new { claimReference = ClaimReference.For(held.CaseId), reason = "Court case CS 14 of 2026" });
        await SetRetentionAsync(admin, new Dictionary<string, int> { ["closed-claims"] = 12 });
        time.Advance(TimeSpan.FromDays(400));

        var records = await MockedApiFactory.ReadJsonAsync(await admin.PostAsync("/api/v1/admin/data/retention/run", null));

        var record = Assert.Single(records.EnumerateArray());
        Assert.Equal(1, record.GetProperty("counts").GetProperty("claims").GetInt32());
        Assert.Equal(held.CaseId.ToString(), (await MockedApiFactory.ReadJsonAsync(await admin.GetAsync($"/api/v1/cases/{held.CaseId}"))).GetProperty("caseId").GetString());
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/v1/cases/{closed.CaseId}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync($"/api/v1/cases/{open.CaseId}")).StatusCode);
        Assert.Empty(FilesFor(host, tenant, closed.CaseId));
        Assert.Contains(ClaimReference.For(held.CaseId), record.GetProperty("heldClaims").EnumerateArray().Select(item => item.GetString()));
    }

    [Fact]
    public async Task OldConversationsAreDeletedButNotThoseOfAHeldClaimOrARecentOne()
    {
        var time = new ManualTime();
        using var host = With(time);
        var tenant = MockedApiFactory.NewTenant();
        using var admin = host.CreateScopedClient(tenant);
        await SaveTurnAsync(host, tenant, "256700000001", "old chat");
        await SetRetentionAsync(admin, new Dictionary<string, int> { ["conversations"] = 3 });
        time.Advance(TimeSpan.FromDays(50));
        await SaveTurnAsync(host, tenant, "256700000002", "newer chat");

        Assert.Equal(0, (await MockedApiFactory.ReadJsonAsync(await admin.PostAsync("/api/v1/admin/data/retention/run", null))).GetArrayLength());
        time.Advance(TimeSpan.FromDays(60));
        var records = await MockedApiFactory.ReadJsonAsync(await admin.PostAsync("/api/v1/admin/data/retention/run", null));

        var record = Assert.Single(records.EnumerateArray());
        Assert.Equal(2, record.GetProperty("counts").GetProperty("whatsapp-conversations").GetInt32());
        Assert.Equal(0, await TurnCountAsync(host, tenant));
    }

    [Fact]
    public async Task RetentionInOneWorkspaceNeverDeletesAnotherWorkspacesData()
    {
        var time = new ManualTime();
        using var host = With(time);
        var (claim, owner) = await ClaimWithDocumentAsync(host);
        using var _ = owner;
        using var other = host.CreateScopedClient(MockedApiFactory.NewTenant());
        await SetRetentionAsync(other, new Dictionary<string, int> { ["documents"] = 1, ["closed-claims"] = 1, ["conversations"] = 1 });
        time.Advance(TimeSpan.FromDays(400));

        await other.PostAsync("/api/v1/admin/data/retention/run", null);

        Assert.NotEmpty(FilesFor(host, claim.TenantId, claim.CaseId));
        Assert.Equal(1, (await MockedApiFactory.ReadJsonAsync(await owner.GetAsync($"/api/v1/cases/{claim.CaseId}/documents"))).GetArrayLength());
        Assert.Empty((await DeletionsAsync(owner)).EnumerateArray());
    }

    [Fact]
    public async Task TheScheduledPassVisitsOnlyWorkspacesThatSetAPeriod()
    {
        var time = new ManualTime();
        using var host = With(time);
        var (withPeriod, adminA) = await ClaimWithDocumentAsync(host);
        using var _a = adminA;
        var (withoutPeriod, adminB) = await ClaimWithDocumentAsync(host);
        using var _b = adminB;
        await SetRetentionAsync(adminA, new Dictionary<string, int> { ["documents"] = 1 });
        time.Advance(TimeSpan.FromDays(100));

        using (var services = host.Services.CreateScope())
        {
            var records = await services.ServiceProvider.GetRequiredService<InsuranceIntelligence.Application.DataLifecycle.DataLifecycleService>().RunRetentionAllAsync();
            Assert.Contains(records, record => record.Scope.TenantId == withPeriod.TenantId);
            Assert.DoesNotContain(records, record => record.Scope.TenantId == withoutPeriod.TenantId);
        }

        Assert.DoesNotContain(FilesFor(host, withPeriod.TenantId, withPeriod.CaseId), file => file.EndsWith("police.pdf", StringComparison.Ordinal));
        Assert.NotEmpty(FilesFor(host, withoutPeriod.TenantId, withoutPeriod.CaseId));
    }

    [Theory]
    [InlineData("documents", -1)]
    [InlineData("documents", 241)]
    [InlineData("audit-trail", 12)]
    public async Task ARetentionPeriodThatMakesNoSenseIsRefused(string key, int months)
    {
        using var host = With();
        using var admin = host.CreateScopedClient(MockedApiFactory.NewTenant());

        var response = await admin.PutAsJsonAsync("/api/v1/admin/tenant-settings", new { retentionMonths = new Dictionary<string, int> { [key] = months } });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RetentionPeriodsAreSavedReadBackAndAnAdministratorCanSetThemBackToKeep()
    {
        using var host = With();
        using var admin = host.CreateScopedClient(MockedApiFactory.NewTenant());

        await SetRetentionAsync(admin, new Dictionary<string, int> { ["documents"] = 36, ["communications"] = 0 });
        var saved = await MockedApiFactory.ReadJsonAsync(await admin.GetAsync("/api/v1/admin/tenant-settings"));
        await SetRetentionAsync(admin, new Dictionary<string, int> { ["documents"] = 0 });
        var cleared = await MockedApiFactory.ReadJsonAsync(await admin.GetAsync("/api/v1/admin/tenant-settings"));

        Assert.Equal(36, saved.GetProperty("retentionMonths").GetProperty("documents").GetInt32());
        Assert.Equal(4, saved.GetProperty("retentionClasses").GetArrayLength());
        Assert.Equal(JsonValueKind.Null, cleared.GetProperty("retentionMonths").ValueKind);
    }

    // ---- the files themselves

    [Fact]
    public async Task TheLocalStoreDeletesOnlyTheNamedClaimsFilesAndRefusesAKeyFromAnotherWorkspace()
    {
        using var host = With();
        var (first, adminFirst) = await ClaimWithDocumentAsync(host);
        using var _a = adminFirst;
        var (second, adminSecond) = await ClaimWithDocumentAsync(host);
        using var _b = adminSecond;
        using var services = host.Services.CreateScope();
        var storage = services.ServiceProvider.GetRequiredService<IDocumentStorageProvider>();

        var deleted = await storage.DeleteCaseObjectsAsync(TenantScope.Create(first.TenantId, "workspace-main"), first.CaseId);
        var again = await storage.DeleteCaseObjectsAsync(TenantScope.Create(first.TenantId, "workspace-main"), first.CaseId);

        Assert.True(deleted >= 1);
        Assert.Equal(0, again);
        Assert.Empty(FilesFor(host, first.TenantId, first.CaseId));
        Assert.NotEmpty(FilesFor(host, second.TenantId, second.CaseId));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => storage.DeleteDocumentObjectsAsync(
            TenantScope.Create(first.TenantId, "workspace-main"), second.CaseId, Guid.NewGuid(), $"{second.TenantId}/workspace-main/{second.CaseId}/x.pdf"));
    }
}
