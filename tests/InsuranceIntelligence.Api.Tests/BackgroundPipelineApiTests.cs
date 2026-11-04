using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using InsuranceIntelligence.Api.Jobs;
using InsuranceIntelligence.Api.Tests.Support;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Jobs;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace InsuranceIntelligence.Api.Tests;

/// <summary>Build-loop item 17: an upload returns as soon as the file is stored; a durable pipeline
/// reads it afterwards, retries failures, dead-letters what cannot be done, and survives restarts.
/// The worker is switched off and the tests drive the processor by hand, one job at a time.</summary>
public sealed class BackgroundPipelineApiTests(MockedApiFactory factory) : IClassFixture<MockedApiFactory>
{
    private static readonly string[] Queues = [BackgroundQueues.DocumentProcessing, BackgroundQueues.ClaimReview];

    private WebApplicationFactory<Program> Background(int maxAttempts = 3) =>
        factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.Replace(ServiceDescriptor.Singleton(new BackgroundPipelineOptions
            {
                Inline = false,
                WorkerEnabled = false,
                MaxAttempts = maxAttempts,
                BackoffSeconds = [0],
                ReviewSettleSeconds = 0,
            }))));

    private static async Task<int> DrainAsync(WebApplicationFactory<Program> host, string tenant, string workspace = "workspace-main", int limit = 20)
    {
        var processor = host.Services.GetRequiredService<IBackgroundJobProcessor>();
        var scope = TenantScope.Create(tenant, workspace);
        var ran = 0;
        while (ran < limit && await processor.ProcessNextAsync(Queues, "test-worker", CancellationToken.None, scope))
        {
            ran++;
        }

        return ran;
    }

    private static async Task<bool> StepAsync(WebApplicationFactory<Program> host, string tenant, params string[] queues) =>
        await host.Services.GetRequiredService<IBackgroundJobProcessor>().ProcessNextAsync(queues.Length == 0 ? Queues : queues, "test-worker", CancellationToken.None, TenantScope.Create(tenant, "workspace-main"));

    private static async Task<HttpResponseMessage> UploadAsync(HttpClient claimant, Guid caseId, string slotKey, string fileName, string content)
    {
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes(content));
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(file, "files", fileName);
        form.Add(new StringContent(slotKey), "documentTypeKey");
        var response = await claimant.PostAsync($"/api/v1/client-portal/claims/{caseId}/documents", form);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return response;
    }

    private static JsonElement Row(JsonElement claim, string slot) =>
        claim.GetProperty("requiredDocuments").EnumerateArray().Single(row => row.GetProperty("documentTypeKey").GetString() == slot);

    private static async Task<JsonElement> JobsAsync(HttpClient staff, string query = "") =>
        await MockedApiFactory.ReadJsonAsync(await staff.GetAsync($"/api/v1/admin/background-jobs{query}"));

    private async Task<(WebApplicationFactory<Program> Host, string Tenant, HttpClient Staff, CaseInfo Claim, HttpClient Claimant)> ArrangeAsync(int maxAttempts = 3)
    {
        var host = Background(maxAttempts);
        var tenant = MockedApiFactory.NewTenant();
        var staff = host.CreateScopedClient(tenant);
        var claim = await TestData.CreateCaseAsync(staff, tenant);
        var claimant = await TestData.ClaimantClientAsync(host, staff, claim);
        return (host, tenant, staff, claim, claimant);
    }

    [Fact]
    public async Task AnUploadReturnsAtOnceAsBeingChecked_AndAWorkerReadsItAfterwards()
    {
        var (host, tenant, staff, claim, claimant) = await ArrangeAsync();
        using var _h = host; using var _s = staff; using var _c = claimant;

        var response = await MockedApiFactory.ReadJsonAsync(await UploadAsync(claimant, claim.CaseId, "PoliceAbstract", "police.pdf", "IIP-MOCK-DOC:PoliceAbstract"));

        Assert.Equal("Being checked", Row(response, "PoliceAbstract").GetProperty("status").GetString());
        var documents = await MockedApiFactory.ReadJsonAsync(await staff.GetAsync($"/api/v1/cases/{claim.CaseId}/documents"));
        Assert.Equal("Uploaded", documents[0].GetProperty("processingStatus").GetString());
        var waiting = await JobsAsync(staff);
        Assert.Single(waiting.GetProperty("jobs").EnumerateArray(), job => job.GetProperty("type").GetString() == "document.process" && job.GetProperty("status").GetString() == "Pending");
        Assert.Contains(waiting.GetProperty("jobs").EnumerateArray(), job => job.GetProperty("type").GetString() == "document.process" && job.GetProperty("what").GetString() == "Reading an uploaded document");

        await DrainAsync(host, tenant);

        var after = await MockedApiFactory.ReadJsonAsync(await claimant.GetAsync($"/api/v1/client-portal/claims/{claim.CaseId}"));
        Assert.Equal("Received", Row(after, "PoliceAbstract").GetProperty("status").GetString());
        var done = await JobsAsync(staff);
        Assert.Equal(0, done.GetProperty("pending").GetInt32());
        Assert.True(done.GetProperty("succeeded").GetInt32() >= 2);
        Assert.Contains(done.GetProperty("jobs").EnumerateArray(), job => job.GetProperty("type").GetString() == "claim.review" && job.GetProperty("status").GetString() == "Succeeded");
        var processed = (await MockedApiFactory.ReadJsonAsync(await staff.GetAsync($"/api/v1/cases/{claim.CaseId}/documents")))[0];
        Assert.Equal("Processed", processed.GetProperty("processingStatus").GetString());
        Assert.StartsWith("PoliceAbstract_CLM-", processed.GetProperty("displayName").GetString());
    }

    [Fact]
    public async Task AFailureIsRetriedWhileTheCustomerStillSeesBeingChecked_ThenItSucceeds()
    {
        var (host, tenant, staff, claim, claimant) = await ArrangeAsync();
        using var _h = host; using var _s = staff; using var _c = claimant;
        await UploadAsync(claimant, claim.CaseId, "PoliceAbstract", "police.pdf", "IIP-MOCK-DOC:PoliceAbstract IIP-MOCK-FAIL-ONCE");

        Assert.True(await StepAsync(host, tenant, BackgroundQueues.DocumentProcessing));

        var view = await MockedApiFactory.ReadJsonAsync(await claimant.GetAsync($"/api/v1/client-portal/claims/{claim.CaseId}"));
        Assert.Equal("Being checked", Row(view, "PoliceAbstract").GetProperty("status").GetString());
        var jobs = await JobsAsync(staff, "?status=Pending");
        var retry = jobs.GetProperty("jobs").EnumerateArray().Single(job => job.GetProperty("type").GetString() == "document.process");
        Assert.Equal(1, retry.GetProperty("attempts").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(retry.GetProperty("lastError").GetString()));

        await DrainAsync(host, tenant);

        var after = await MockedApiFactory.ReadJsonAsync(await claimant.GetAsync($"/api/v1/client-portal/claims/{claim.CaseId}"));
        Assert.Equal("Received", Row(after, "PoliceAbstract").GetProperty("status").GetString());
    }

    [Fact]
    public async Task WhatKeepsFailingIsDeadLetteredWithASafeReason_TheDocumentShowsAsUnreadAndAnAdministratorCanSendItBack()
    {
        var (host, tenant, staff, claim, claimant) = await ArrangeAsync(maxAttempts: 3);
        using var _h = host; using var _s = staff; using var _c = claimant;
        await UploadAsync(claimant, claim.CaseId, "PoliceAbstract", "police.pdf", "IIP-MOCK-DOC:PoliceAbstract IIP-MOCK-FAIL-ALWAYS");

        await DrainAsync(host, tenant);

        var dead = await JobsAsync(staff, "?status=DeadLettered");
        var job = dead.GetProperty("jobs").EnumerateArray().Single(candidate => candidate.GetProperty("type").GetString() == "document.process");
        Assert.Equal(3, job.GetProperty("attempts").GetInt32());
        var reason = job.GetProperty("lastError").GetString()!;
        Assert.DoesNotContain("Simulated", reason);
        Assert.DoesNotContain("IIP-MOCK", reason);
        Assert.Equal(1, dead.GetProperty("deadLettered").GetInt32());
        var view = await MockedApiFactory.ReadJsonAsync(await claimant.GetAsync($"/api/v1/client-portal/claims/{claim.CaseId}"));
        Assert.Equal("Could not be read", Row(view, "PoliceAbstract").GetProperty("status").GetString());
        Assert.True(Row(view, "PoliceAbstract").GetProperty("canRetry").GetBoolean());

        var requeued = await staff.PostAsync($"/api/v1/admin/background-jobs/{job.GetProperty("jobId").GetGuid()}/retry", null);

        Assert.Equal(HttpStatusCode.OK, requeued.StatusCode);
        var again = await MockedApiFactory.ReadJsonAsync(requeued);
        Assert.Equal("Pending", again.GetProperty("status").GetString());
        Assert.Equal(0, again.GetProperty("attempts").GetInt32());
        Assert.Contains("background.job.requeued", await staff.GetStringAsync("/api/v1/audit/events?pageSize=200"));
        var twice = await staff.PostAsync($"/api/v1/admin/background-jobs/{job.GetProperty("jobId").GetGuid()}/retry", null);
        Assert.Equal(HttpStatusCode.Conflict, twice.StatusCode);
    }

    [Fact]
    public async Task WorkWaitingWhenTheApiStopsIsStillThereWhenItStartsAgain()
    {
        var (first, tenant, staff, claim, claimant) = await ArrangeAsync();
        using var _s = staff; using var _c = claimant;
        await UploadAsync(claimant, claim.CaseId, "PoliceAbstract", "police.pdf", "IIP-MOCK-DOC:PoliceAbstract");
        first.Dispose();

        using var restarted = Background();
        await DrainAsync(restarted, tenant);

        using var staffAfter = restarted.CreateScopedClient(tenant);
        var document = (await MockedApiFactory.ReadJsonAsync(await staffAfter.GetAsync($"/api/v1/cases/{claim.CaseId}/documents")))[0];
        Assert.Equal("Processed", document.GetProperty("processingStatus").GetString());
    }

    [Fact]
    public async Task AWorkerThatDiesMidJobLosesItsHold_AndAnotherWorkerTakesTheJobOver()
    {
        var (host, tenant, staff, claim, claimant) = await ArrangeAsync();
        using var _h = host; using var _s = staff; using var _c = claimant;
        await UploadAsync(claimant, claim.CaseId, "PoliceAbstract", "police.pdf", "IIP-MOCK-DOC:PoliceAbstract");
        var scope = TenantScope.Create(tenant, "workspace-main");
        await using (var arrange = host.Services.CreateAsyncScope())
        {
            var store = arrange.ServiceProvider.GetRequiredService<IBackgroundJobStore>();
            var taken = await store.LeaseNextAsync([BackgroundQueues.DocumentProcessing], "worker-that-dies", TimeSpan.FromMilliseconds(1), DateTimeOffset.UtcNow, scope);
            Assert.NotNull(taken);
            Assert.Equal(1, taken!.Attempts);
            // While it is held, nobody else can take it.
            Assert.Null(await store.LeaseNextAsync([BackgroundQueues.DocumentProcessing], "other", TimeSpan.FromMinutes(1), taken.LeasedUntil!.Value.AddHours(-1), scope));
        }

        await Task.Delay(50);
        await DrainAsync(host, tenant);

        var finished = await JobsAsync(staff);
        var job = finished.GetProperty("jobs").EnumerateArray().Single(candidate => candidate.GetProperty("type").GetString() == "document.process");
        Assert.Equal("Succeeded", job.GetProperty("status").GetString());
        Assert.Equal(2, job.GetProperty("attempts").GetInt32());
    }

    [Fact]
    public async Task OnlyOneOfSeveralWorkersCanTakeAJob()
    {
        var (host, tenant, staff, claim, claimant) = await ArrangeAsync();
        using var _h = host; using var _s = staff; using var _c = claimant;
        await UploadAsync(claimant, claim.CaseId, "PoliceAbstract", "police.pdf", "IIP-MOCK-DOC:PoliceAbstract");
        var scope = TenantScope.Create(tenant, "workspace-main");

        var attempts = await Task.WhenAll(Enumerable.Range(0, 6).Select(async index =>
        {
            await using var serviceScope = host.Services.CreateAsyncScope();
            return await serviceScope.ServiceProvider.GetRequiredService<IBackgroundJobStore>()
                .LeaseNextAsync([BackgroundQueues.DocumentProcessing], $"worker-{index}", TimeSpan.FromMinutes(5), DateTimeOffset.UtcNow, scope);
        }));

        Assert.Single(attempts, taken => taken is not null);
    }

    [Fact]
    public async Task AskingToProcessTheSameDocumentTwiceQueuesItOnce()
    {
        var (host, tenant, staff, claim, claimant) = await ArrangeAsync();
        using var _h = host; using var _s = staff; using var _c = claimant;
        await UploadAsync(claimant, claim.CaseId, "PoliceAbstract", "police.pdf", "IIP-MOCK-DOC:PoliceAbstract");
        var documentId = (await MockedApiFactory.ReadJsonAsync(await staff.GetAsync($"/api/v1/cases/{claim.CaseId}/documents")))[0].GetProperty("documentId").GetGuid();

        await staff.PostAsync($"/api/v1/cases/{claim.CaseId}/documents/{documentId}/reprocess", null);
        await staff.PostAsync($"/api/v1/cases/{claim.CaseId}/documents/{documentId}/reprocess", null);

        var jobs = (await JobsAsync(staff, "?status=Pending")).GetProperty("jobs").EnumerateArray().Where(job => job.GetProperty("type").GetString() == "document.process").ToArray();
        Assert.Single(jobs);
        _ = tenant;
    }

    [Fact]
    public async Task ADocumentThatIsGoneOrReplacedBeforeItsTurnIsSkippedWithoutError()
    {
        var (host, tenant, staff, claim, claimant) = await ArrangeAsync();
        using var _h = host; using var _s = staff; using var _c = claimant;
        await UploadAsync(claimant, claim.CaseId, "PoliceAbstract", "police.pdf", "IIP-MOCK-DOC:PoliceAbstract");
        await claimant.PostAsJsonAsync($"/api/v1/client-portal/claims/{claim.CaseId}/documents/remove", new { documentTypeKey = "PoliceAbstract" });

        await DrainAsync(host, tenant);

        var jobs = await JobsAsync(staff);
        Assert.Equal(0, jobs.GetProperty("deadLettered").GetInt32());
        Assert.Contains(jobs.GetProperty("jobs").EnumerateArray(), job => job.GetProperty("type").GetString() == "document.process" && job.GetProperty("status").GetString() == "Succeeded");
    }

    [Fact]
    public async Task SeveralDocumentsOnOneClaimAreReviewedOnce()
    {
        var (host, tenant, staff, claim, claimant) = await ArrangeAsync();
        using var _h = host; using var _s = staff; using var _c = claimant;
        await UploadAsync(claimant, claim.CaseId, "PoliceAbstract", "police.pdf", "IIP-MOCK-DOC:PoliceAbstract");
        await UploadAsync(claimant, claim.CaseId, "ClaimForm", "form.pdf", "IIP-MOCK-DOC:ClaimForm");
        await UploadAsync(claimant, claim.CaseId, "CustomerId", "id.pdf", "IIP-MOCK-DOC:CustomerId");

        for (var i = 0; i < 3; i++)
        {
            await StepAsync(host, tenant, BackgroundQueues.DocumentProcessing);
        }

        var reviews = (await JobsAsync(staff, "?status=Pending")).GetProperty("jobs").EnumerateArray().Where(job => job.GetProperty("type").GetString() == "claim.review").ToArray();
        Assert.True(reviews.Length <= 1, $"{reviews.Length} reviews were waiting");
    }

    [Fact]
    public async Task AWorkspaceSeesOnlyItsOwnWorkAndOnlyAdministratorsCanSendJobsBack()
    {
        var (host, tenant, staff, claim, claimant) = await ArrangeAsync(maxAttempts: 1);
        using var _h = host; using var _s = staff; using var _c = claimant;
        await UploadAsync(claimant, claim.CaseId, "PoliceAbstract", "police.pdf", "IIP-MOCK-DOC:PoliceAbstract IIP-MOCK-FAIL-ALWAYS");
        await DrainAsync(host, tenant);
        var jobId = (await JobsAsync(staff, "?status=DeadLettered")).GetProperty("jobs")[0].GetProperty("jobId").GetGuid();
        using var stranger = host.CreateScopedClient(MockedApiFactory.NewTenant());
        using var officer = host.CreateScopedClient(tenant, role: "ClaimsOfficer");
        using var broker = host.CreateScopedClient(tenant, role: "Broker");

        Assert.Equal(0, (await JobsAsync(stranger)).GetProperty("jobs").GetArrayLength());
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.PostAsync($"/api/v1/admin/background-jobs/{jobId}/retry", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await officer.GetAsync("/api/v1/admin/background-jobs")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await officer.PostAsync($"/api/v1/admin/background-jobs/{jobId}/retry", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await officer.GetAsync($"/api/v1/cases/{claim.CaseId}/background-work")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await broker.GetAsync($"/api/v1/cases/{claim.CaseId}/background-work")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync($"/api/v1/cases/{claim.CaseId}/background-work")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await staff.GetAsync("/api/v1/admin/background-jobs?status=Nonsense")).StatusCode);
    }

    [Fact]
    public async Task ATestBuiltInInlineModeStillProcessesBeforeReturning()
    {
        var tenant = MockedApiFactory.NewTenant();
        using var staff = factory.CreateScopedClient(tenant);
        var claim = await TestData.CreateCaseAsync(staff, tenant);
        using var claimant = await TestData.ClaimantClientAsync(factory, staff, claim);

        var response = await MockedApiFactory.ReadJsonAsync(await UploadAsync(claimant, claim.CaseId, "PoliceAbstract", "police.pdf", "IIP-MOCK-DOC:PoliceAbstract"));

        Assert.Equal("Received", Row(response, "PoliceAbstract").GetProperty("status").GetString());
        Assert.Equal(0, (await JobsAsync(staff, "?status=Pending")).GetProperty("jobs").EnumerateArray().Count(job => job.GetProperty("type").GetString() == "document.process"));
    }
}
