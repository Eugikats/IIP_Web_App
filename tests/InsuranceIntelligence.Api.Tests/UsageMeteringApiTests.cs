using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using InsuranceIntelligence.Api.Tests.Support;
using InsuranceIntelligence.Application.Ai;
using InsuranceIntelligence.Application.Billing;
using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Domain.Billing;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Knowledge;
using InsuranceIntelligence.Infrastructure.Billing;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace InsuranceIntelligence.Api.Tests;

/// <summary>
/// Build-loop item 31: every call to a model is counted against the workspace that caused it, documents and storage are
/// counted, and the cost comes from the configured model's own price rather than a fixed one.
/// </summary>
public sealed class UsageMeteringApiTests(MockedApiFactory factory) : IClassFixture<MockedApiFactory>
{
    private static JsonElement Meter(JsonElement billing, string key) =>
        billing.GetProperty("meters").EnumerateArray().Single(meter => meter.GetProperty("key").GetString() == key);

    private static async Task<JsonElement> BillingAsync(HttpClient staff) =>
        await MockedApiFactory.ReadJsonAsync(await staff.GetAsync("/api/v1/admin/billing"));

    private sealed class CapturingRecorder : IWorkspaceAiUsageRecorder
    {
        public List<WorkspaceAiUsageEntry> Entries { get; } = [];

        public Task RecordAsync(WorkspaceAiUsageEntry entry, CancellationToken cancellationToken = default)
        {
            Entries.Add(entry);
            return Task.CompletedTask;
        }
    }

    private sealed class FixedTenant(string tenant, string workspace) : ITenantContextAccessor
    {
        public bool HasCurrent => true;
        public TenantContext Current => new(tenant, workspace, "someone", ["Admin"]);
    }

    private sealed class NoTenant : ITenantContextAccessor
    {
        public bool HasCurrent => false;
        public TenantContext Current => throw new InvalidOperationException("No tenant.");
    }

    private sealed class StubChat(ChatCompletionResult result) : IChatCompletionProvider
    {
        public Task<ChatCompletionResult> CompleteAsync(ChatCompletionRequest request, CancellationToken cancellationToken = default) => Task.FromResult(result);
    }

    private sealed class StubTools(AgentChatStep step) : IToolCallingChatProvider
    {
        public Task<AgentChatStep> NextAsync(IReadOnlyList<AgentChatMessage> messages, IReadOnlyList<AgentToolSpec> tools, string? correlationId, CancellationToken cancellationToken = default) => Task.FromResult(step);
    }

    private sealed class StubEmbedding(ModelUsage? usage) : IEmbeddingProvider
    {
        public Task<IReadOnlyCollection<float>> EmbedAsync(string text, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyCollection<float>>([1f, 2f]);

        public Task<EmbeddingResult> EmbedWithUsageAsync(string text, CancellationToken cancellationToken = default) => Task.FromResult(new EmbeddingResult([1f, 2f], usage));
    }

    private static ChatCompletionResult Completed(int? prompt = 100, int? completion = 20) =>
        new("text", "AzureOpenAI", "gpt-test", DateTimeOffset.UtcNow, "gpt-test-2026", prompt, completion);

    // ---- prices

    [Fact]
    public void ACallIsPricedByTheDeploymentsOwnConfiguredPrice()
    {
        var model = new ConfiguredAiCostModel(AiPricingOptions.Parse("""{"gpt-test":{"promptPer1K":0.002,"completionPer1K":0.008}}"""));

        Assert.Equal(0.0004m, model.Estimate("AzureOpenAI", "gpt-test", null, 100, 25));
        Assert.Equal(0m, model.Estimate("AzureOpenAI", "gpt-test", null, 0, 0));
    }

    [Fact]
    public void AModelWithNoConfiguredPriceIsNotPricedRatherThanGuessed()
    {
        var model = new ConfiguredAiCostModel(AiPricingOptions.Parse("""{"gpt-test":{"promptPer1K":0.002,"completionPer1K":0.008}}"""));

        Assert.Null(model.Estimate("AzureOpenAI", "some-other-deployment", "some-other-2026", 100, 25));
        Assert.Null(new ConfiguredAiCostModel(AiPricingOptions.None).Estimate("AzureOpenAI", "gpt-test", null, 100, 25));
    }

    [Fact]
    public void ThePriceCanBeFoundFromTheReportedModelVersionAndTheLongestNameWins()
    {
        var model = new ConfiguredAiCostModel(AiPricingOptions.Parse("""{"gpt-4o":{"promptPer1K":1,"completionPer1K":1},"gpt-4o-mini":{"promptPer1K":0.1,"completionPer1K":0.1}}"""));

        Assert.Equal(0.2m, model.Estimate("AzureOpenAI", "my-deployment", "gpt-4o-mini-2024-07-18", 1000, 1000));
        Assert.Equal(2m, model.Estimate("AzureOpenAI", "my-deployment", "gpt-4o-2024-08-06", 1000, 1000));
    }

    [Fact]
    public void AMockModelIsFreeAndACallWithNoTokenCountHasNoCost()
    {
        var model = new ConfiguredAiCostModel(AiPricingOptions.None);

        Assert.Equal(0m, model.Estimate("Mock", "mock-chat", null, 50, 5));
        Assert.Null(model.Estimate("AzureOpenAI", "gpt-test", null, null, null));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("[1,2]")]
    [InlineData("""{"gpt":{"promptPer1K":-1,"completionPer1K":1}}""")]
    [InlineData("""{"gpt":{"promptPer1K":"x","completionPer1K":1}}""")]
    public void MalformedPricingMeansNoPricesNotAnError(string? json) => Assert.Empty(AiPricingOptions.Parse(json).Prices);

    // ---- the wrappers

    [Fact]
    public async Task EveryChatCallIsRecordedAgainstTheWorkspaceThatMadeIt()
    {
        var recorder = new CapturingRecorder();
        var chat = new MeteredChatCompletionProvider(new StubChat(Completed()), recorder, new NoTenant());

        var result = await chat.CompleteAsync(new ChatCompletionRequest("hi", "tenant-a", "workspace-a", "corr-1", "claims-summary"));

        Assert.Equal("text", result.Content);
        var entry = Assert.Single(recorder.Entries);
        Assert.Equal(TenantScope.Create("tenant-a", "workspace-a"), entry.Scope);
        Assert.Equal("chat:claims-summary", entry.Operation);
        Assert.Equal((100, 20), (entry.PromptTokens, entry.CompletionTokens));
        Assert.Equal("gpt-test", entry.Deployment);
    }

    [Fact]
    public async Task ARequestForAnotherTenantIsNeverRecordedAgainstTheRequestsTenant()
    {
        var recorder = new CapturingRecorder();
        var chat = new MeteredChatCompletionProvider(new StubChat(Completed()), recorder, new FixedTenant("tenant-a", "workspace-a"));

        await chat.CompleteAsync(new ChatCompletionRequest("hi", "tenant-b", "workspace-b"));

        Assert.Equal(TenantScope.Create("tenant-b", "workspace-b"), Assert.Single(recorder.Entries).Scope);
    }

    [Fact]
    public async Task AToolCallingStepIsRecordedFromTheRequestsTenantAndNothingIsRecordedWithoutOne()
    {
        var step = new AgentChatStep("ok", [], new ModelUsage("AzureOpenAI", "gpt-test", null, 300, 40));
        var recorder = new CapturingRecorder();

        var result = await new MeteredToolCallingChatProvider(new StubTools(step), recorder, new FixedTenant("tenant-a", "workspace-a")).NextAsync([], [], "corr");
        await new MeteredToolCallingChatProvider(new StubTools(step), recorder, new NoTenant()).NextAsync([], [], "corr");

        Assert.Equal("ok", result.Content);
        var entry = Assert.Single(recorder.Entries);
        Assert.Equal(TenantScope.Create("tenant-a", "workspace-a"), entry.Scope);
        Assert.Equal((300, 40), (entry.PromptTokens, entry.CompletionTokens));
    }

    [Fact]
    public async Task AnEmbeddingIsRecordedWhenTheProviderReportsWhatItUsed()
    {
        var recorder = new CapturingRecorder();
        var embedder = new MeteredEmbeddingProvider(new StubEmbedding(new ModelUsage("AzureOpenAI", "embed-test", null, 12, 0)), recorder, new FixedTenant("tenant-a", "workspace-a"));

        var vector = await embedder.EmbedAsync("some text");

        Assert.Equal(2, vector.Count);
        var entry = Assert.Single(recorder.Entries);
        Assert.Equal(("embedding", 12), (entry.Operation, entry.PromptTokens));
    }

    private sealed class ThrowingLog : IAgentActionLogRepository
    {
        public Task SaveAsync(AgentActionLog actionLog, CancellationToken cancellationToken = default) => throw new InvalidOperationException("storage is down");
        public Task<IReadOnlyCollection<AgentActionLog>> ListAsync(TenantScope scope, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyCollection<AgentActionLog>> ListForCaseAsync(Guid caseId, TenantScope scope, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    [Fact]
    public async Task AFaultInCountingNeverStopsTheWorkBeingCounted()
    {
        var recorder = new AgentActionLogUsageRecorder(new ThrowingLog(), new ConfiguredAiCostModel(AiPricingOptions.None), NullLogger<AgentActionLogUsageRecorder>.Instance);
        var chat = new MeteredChatCompletionProvider(new StubChat(Completed()), recorder, new NoTenant());

        var result = await chat.CompleteAsync(new ChatCompletionRequest("hi", "tenant-a", "workspace-a"));

        Assert.Equal("text", result.Content);
    }

    // ---- through the real API

    [Fact]
    public async Task ASummaryTheModelWritesIsCountedOnTheBillingPageAndOnlyForThatWorkspace()
    {
        var tenant = MockedApiFactory.NewTenant();
        var other = MockedApiFactory.NewTenant();
        using var staff = factory.CreateScopedClient(tenant);
        using var otherStaff = factory.CreateScopedClient(other);
        var claim = await TestData.CreateCaseAsync(staff, tenant);

        var before = await BillingAsync(staff);
        Assert.Equal(0, Meter(before, "ai-calls").GetProperty("used").GetInt32());

        var summary = await staff.PostAsync($"/api/v1/cases/{claim.CaseId}/summary", null);
        Assert.True(summary.IsSuccessStatusCode, await summary.Content.ReadAsStringAsync());

        var after = await BillingAsync(staff);
        Assert.True(Meter(after, "ai-calls").GetProperty("used").GetInt32() >= 1);
        Assert.True(Meter(after, "ai-tokens").GetProperty("used").GetInt32() > 0);
        Assert.True(Meter(after, "ai-tokens").GetProperty("metered").GetBoolean());
        // The mock model is free, so its cost is a real zero, not "not configured".
        Assert.True(Meter(after, "ai-cost-usd").GetProperty("metered").GetBoolean());

        var otherBilling = await BillingAsync(otherStaff);
        Assert.Equal(0, Meter(otherBilling, "ai-calls").GetProperty("used").GetInt32());
        Assert.Equal(0, Meter(otherBilling, "ai-tokens").GetProperty("used").GetInt32());
    }

    [Fact]
    public async Task TheBareModelCallRecordsAreNeverListedAmongTheAssistantsActions()
    {
        var tenant = MockedApiFactory.NewTenant();
        using var staff = factory.CreateScopedClient(tenant);
        var claim = await TestData.CreateCaseAsync(staff, tenant);
        await staff.PostAsync($"/api/v1/cases/{claim.CaseId}/summary", null);

        Assert.True(Meter(await BillingAsync(staff), "ai-calls").GetProperty("used").GetInt32() >= 1);
        var actions = await MockedApiFactory.ReadJsonAsync(await staff.GetAsync("/api/v1/assistant/actions"));
        Assert.DoesNotContain(actions.EnumerateArray(), action => action.GetProperty("agentName").GetString()?.StartsWith("chat:", StringComparison.Ordinal) == true);
        Assert.DoesNotContain(actions.EnumerateArray(), action => action.GetProperty("inputSummary").GetString() == "Model call");
    }

    [Fact]
    public async Task ACostIsShownOnlyForAModelWithAConfiguredPrice()
    {
        using var host = factory.WithWebHostBuilder(builder => builder.UseSetting("INSURANCEINTELLIGENCE_AI_PRICING", """{"gpt-priced":{"promptPer1K":1,"completionPer1K":2}}"""));
        var tenant = MockedApiFactory.NewTenant();
        using var staff = host.CreateScopedClient(tenant);
        var scope = TenantScope.Create(tenant, "workspace-main");

        using (var services = host.Services.CreateScope())
        {
            var recorder = services.ServiceProvider.GetRequiredService<IWorkspaceAiUsageRecorder>();
            await recorder.RecordAsync(new WorkspaceAiUsageEntry(scope, "chat:test", "AzureOpenAI", "gpt-unpriced", null, 1000, 1000, "c1"));
        }

        var unpriced = await BillingAsync(staff);
        Assert.Equal(2000, Meter(unpriced, "ai-tokens").GetProperty("used").GetInt32());
        Assert.False(Meter(unpriced, "ai-cost-usd").GetProperty("metered").GetBoolean());

        using (var services = host.Services.CreateScope())
        {
            var recorder = services.ServiceProvider.GetRequiredService<IWorkspaceAiUsageRecorder>();
            await recorder.RecordAsync(new WorkspaceAiUsageEntry(scope, "chat:test", "AzureOpenAI", "gpt-priced", null, 1000, 500, "c2"));
        }

        var priced = await BillingAsync(staff);
        Assert.Equal(3500, Meter(priced, "ai-tokens").GetProperty("used").GetInt32());
        var cost = Meter(priced, "ai-cost-usd");
        Assert.True(cost.GetProperty("metered").GetBoolean());
        Assert.Equal(200, cost.GetProperty("used").GetInt32());
        Assert.Equal("cents", cost.GetProperty("unit").GetString());
    }

    [Fact]
    public async Task DocumentsReceivedAndStorageHeldAreCountedPerWorkspace()
    {
        var tenant = MockedApiFactory.NewTenant();
        var other = MockedApiFactory.NewTenant();
        using var staff = factory.CreateScopedClient(tenant);
        using var otherStaff = factory.CreateScopedClient(other);
        var claim = await TestData.CreateCaseAsync(staff, tenant);
        using var claimant = await TestData.ClaimantClientAsync(factory, staff, claim);

        Assert.Equal(0, Meter(await BillingAsync(staff), "documents").GetProperty("used").GetInt32());

        var payload = new string('x', 300_000) + "IIP-MOCK-DOC:PoliceAbstract";
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes(payload));
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(file, "files", "police.pdf");
        form.Add(new StringContent("PoliceAbstract"), "documentTypeKey");
        var upload = await claimant.PostAsync($"/api/v1/client-portal/claims/{claim.CaseId}/documents", form);
        Assert.True(upload.IsSuccessStatusCode, await upload.Content.ReadAsStringAsync());

        var billing = await BillingAsync(staff);
        Assert.Equal(1, Meter(billing, "documents").GetProperty("used").GetInt32());
        var storage = Meter(billing, "storage");
        Assert.True(storage.GetProperty("metered").GetBoolean());
        Assert.Equal("MB", storage.GetProperty("unit").GetString());

        var otherBilling = await BillingAsync(otherStaff);
        Assert.Equal(0, Meter(otherBilling, "documents").GetProperty("used").GetInt32());
        Assert.Equal(0, Meter(otherBilling, "storage").GetProperty("used").GetInt32());
    }

    [Fact]
    public async Task ARecordFromAnEarlierMonthDoesNotCountTowardsThisMonth()
    {
        var tenant = MockedApiFactory.NewTenant();
        using var staff = factory.CreateScopedClient(tenant);
        var scope = TenantScope.Create(tenant, "workspace-main");

        using (var services = factory.Services.CreateScope())
        {
            var repository = services.ServiceProvider.GetRequiredService<IAgentActionLogRepository>();
            await repository.SaveAsync(new AgentActionLog
            {
                AgentActionLogId = Guid.NewGuid(),
                Scope = scope,
                UserId = AgentActionLogUsageRecorder.MeteringActor,
                AgentName = "chat:old",
                AssistantMode = AssistantMode.OperationalSummary,
                InputSummary = "Model call",
                RetrievedSources = [],
                ModelOutput = string.Empty,
                ConfidenceScore = AiConfidenceScore.Create(0m),
                RiskFlag = "None",
                HumanOverride = false,
                CreatedAt = DateTimeOffset.UtcNow.AddMonths(-2),
                CorrelationId = "old",
                PromptTokens = 5000,
                CompletionTokens = 5000,
                RecordKind = ActionLogKinds.ModelCall,
            });
        }

        Assert.Equal(0, Meter(await BillingAsync(staff), "ai-tokens").GetProperty("used").GetInt32());
    }
}
