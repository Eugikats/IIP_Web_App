using System.Text.Json;
using InsuranceIntelligence.Api.CommandCenter;
using InsuranceIntelligence.Api.Local;
using InsuranceIntelligence.Application.Ai;
using InsuranceIntelligence.Application.CommandCenter;
using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.LocalTestDoubles;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Workflow;
using InsuranceIntelligence.Infrastructure.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace InsuranceIntelligence.Api.Tests;

public sealed class CommandCenterQueryEndpointsTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly TenantScope Scope = TenantScope.Create("tenant-demo", "workspace-claims");

    [Fact]
    public async Task DefaultBriefingReturnsGovernedBlocksAndWritesAudit()
    {
        var harness = new Harness();
        await harness.SeedIncompleteCaseAsync("Amina Okello", SlaState.Breached);
        await harness.SeedPendingApprovalAsync();

        var (statusCode, body) = await harness.RunAsync("What needs my attention today?");

        Assert.Equal(StatusCodes.Status200OK, statusCode);

        // Envelope guardrail: preparation only, never a decision trigger.
        Assert.False(body.GetProperty("canTriggerDecision").GetBoolean());
        Assert.Equal("Deterministic", body.GetProperty("modelProvider").GetString());
        Assert.Equal("command-center-query-v1", body.GetProperty("promptVersion").GetString());
        Assert.Equal("Internal", body.GetProperty("visibility").GetString());

        var types = BlockTypes(body);
        Assert.Contains("metric_pills", types);
        Assert.Contains("document_gap_summary", types);
        Assert.Contains("approval_queue", types);

        // Document gap surfaces the incomplete case by customer name.
        var gapBlock = Block(body, "document_gap_summary");
        var gapCustomers = gapBlock.GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("customer").GetString());
        Assert.Contains("Amina Okello", gapCustomers);

        // Every proposed action is draft-only: requiresHumanApproval is always true.
        foreach (var actionBlock in Blocks(body, "action_proposal"))
        {
            foreach (var action in actionBlock.GetProperty("actions").EnumerateArray())
            {
                Assert.True(action.GetProperty("requiresHumanApproval").GetBoolean());
            }
        }

        // The query is audited: an action log + audit event are written server-side.
        var actionLogId = body.GetProperty("agentActionLogId").GetGuid();
        var actionLogs = await harness.ActionLogRepository.ListAsync(Scope);
        var auditEvents = await harness.AuditRepository.ListAsync(Scope);
        Assert.Contains(actionLogs, log => log.AgentActionLogId == actionLogId && !log.CanTriggerDecision);
        Assert.Contains(auditEvents, evt => evt.ActionName == "assistant.command_center.answered");
    }

    [Fact]
    public async Task ApprovalIntentReturnsApprovalQueue()
    {
        var harness = new Harness();
        await harness.SeedIncompleteCaseAsync("Brian Mukasa", SlaState.OnTrack);
        await harness.SeedPendingApprovalAsync();

        var (statusCode, body) = await harness.RunAsync("Show me pending approvals to sign off.");

        Assert.Equal(StatusCodes.Status200OK, statusCode);
        var approvalBlock = Block(body, "approval_queue");
        Assert.NotEmpty(approvalBlock.GetProperty("items").EnumerateArray());
        Assert.All(
            approvalBlock.GetProperty("items").EnumerateArray(),
            item => Assert.Equal("/approvals", item.GetProperty("href").GetString()));
    }

    [Fact]
    public async Task ReadOnlyBriefingDoesNotRequireAiProviderBlocks()
    {
        var harness = new Harness();
        await harness.SeedIncompleteCaseAsync("Carol Nabirye", SlaState.OnTrack);

        var (statusCode, body) = await harness.RunAsync("What needs my attention today?");

        Assert.Equal(StatusCodes.Status200OK, statusCode);
        Assert.Empty(Blocks(body, "not_configured"));
    }

    [Fact]
    public async Task AiDependentIntentReturnsNotConfiguredAndWritesAudit()
    {
        var harness = new Harness();
        await harness.SeedIncompleteCaseAsync("Doreen Akello", SlaState.OnTrack);

        var (statusCode, body) = await harness.RunAsync("Prepare a management summary for today's claims.");

        Assert.Equal(StatusCodes.Status200OK, statusCode);

        var block = Block(body, "not_configured");
        Assert.Equal("Azure OpenAI", block.GetProperty("provider").GetString());
        Assert.False(body.GetProperty("canTriggerDecision").GetBoolean());

        var actionLogId = body.GetProperty("agentActionLogId").GetGuid();
        var actionLogs = await harness.ActionLogRepository.ListAsync(Scope);
        var auditEvents = await harness.AuditRepository.ListAsync(Scope);
        Assert.Contains(actionLogs, log =>
            log.AgentActionLogId == actionLogId
            && log.RiskFlag == "provider_not_configured"
            && !log.CanTriggerDecision);
        Assert.Contains(auditEvents, evt => evt.ActionName == "assistant.command_center.answered");
    }

    [Fact]
    public async Task EmptyQuestionIsRejected()
    {
        var harness = new Harness();

        var (statusCode, _) = await harness.RunAsync("   ");

        Assert.Equal(StatusCodes.Status400BadRequest, statusCode);
    }

    [Fact]
    public async Task QueryRequiresCommandCenterPermission()
    {
        var harness = new Harness(role: "Broker");

        var (statusCode, _) = await harness.RunAsync("What needs my attention today?");

        Assert.Equal(StatusCodes.Status403Forbidden, statusCode);
    }

    [Fact]
    public async Task BriefingRequiresClaimsReadPermission()
    {
        var harness = new Harness(role: "PublisherSupport");

        var (statusCode, _) = await harness.RunAsync("What needs my attention today?");

        Assert.Equal(StatusCodes.Status403Forbidden, statusCode);
    }

    [Fact]
    public async Task AiOutputValidatorRemovesDecisionAndCustomerUnsafeLanguage()
    {
        var validator = new DeterministicAiOutputValidator();
        var context = new TenantContext("tenant-demo", "workspace-claims", "user-claims-1", ["ClaimsSupervisor"]);

        var output = await validator.ValidateCustomerSafeDraftAsync(
            "The claim is denied. Internal note: fraud flag and reserve are visible.",
            context);

        Assert.True(output.RequiresHumanReview);
        Assert.Contains("requires human review", output.Preview);
        Assert.DoesNotContain("claim is denied", output.Preview, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("fraud flag", output.Preview, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("reserve", output.Preview, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("consequential_decision_language_removed", output.GuardrailsApplied);
        Assert.Contains("customer_unsafe_internal_detail_removed", output.GuardrailsApplied);
    }

    [Fact]
    public async Task NotificationDraftServicePersistsCustomerUpdateDraft()
    {
        var repository = new InMemoryCustomerUpdateRepository();
        var service = new CustomerUpdateNotificationDraftService(repository);
        var caseId = Guid.NewGuid();

        var result = await service.CreateDraftAsync(
            new NotificationDraftCreateRequest(
                "tenant-demo",
                "workspace-claims",
                "claim",
                caseId.ToString(),
                "Email",
                "Please review the missing documents for your claim.",
                RequiresApproval: true,
                "user-claims-1",
                "corr-draft-001"));

        var saved = await repository.GetAsync(Guid.Parse(result.DraftId), Scope);

        Assert.NotNull(saved);
        Assert.Equal(CustomerUpdateStatus.PendingApproval, saved!.Status);
        Assert.True(saved.RequiresApproval);
        Assert.True(saved.Sensitive);
        Assert.Equal(caseId, saved.CaseId);
        Assert.Equal("Please review the missing documents for your claim.", saved.MessageBody);
    }

    [Fact]
    public async Task CustomerUpdateDraftIntentPersistsDraftBeforeReturningBlock()
    {
        var harness = new Harness(
            providerHealthCheck: new ReadyProviderHealthCheck(),
            chatCompletionProvider: new CapturingChatCompletionProvider(
                "The claim is denied. Internal note: fraud flag and reserve are visible."));
        var claimCase = await harness.SeedIncompleteCaseAsync("Evelyn Nansubuga", SlaState.OnTrack);

        var (statusCode, body) = await harness.RunAsync($"Draft customer update for claim {claimCase.CaseId}");

        Assert.Equal(StatusCodes.Status200OK, statusCode);
        Assert.False(body.GetProperty("canTriggerDecision").GetBoolean());

        var draftBlock = Block(body, "draft_communication");
        Assert.True(draftBlock.GetProperty("requiresApproval").GetBoolean());
        var draftId = Guid.Parse(draftBlock.GetProperty("draftId").GetString()!);
        var preview = draftBlock.GetProperty("preview").GetString()!;
        Assert.Contains("requires human review", preview);
        Assert.DoesNotContain("fraud flag", preview, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("reserve", preview, StringComparison.OrdinalIgnoreCase);

        var saved = await harness.CustomerUpdateRepository.GetAsync(draftId, Scope);
        Assert.NotNull(saved);
        Assert.Equal(preview, saved!.MessageBody);
        Assert.Equal(CustomerUpdateStatus.PendingApproval, saved.Status);
        Assert.True(saved.RequiresApproval);

        var actionLogId = body.GetProperty("agentActionLogId").GetGuid();
        var actionLogs = await harness.ActionLogRepository.ListAsync(Scope);
        var actionLog = Assert.Single(actionLogs, log => log.AgentActionLogId == actionLogId);
        Assert.Equal(claimCase.CaseId, actionLog.CaseId);
        Assert.Equal("AzureOpenAI", actionLog.ModelProvider);
        Assert.False(actionLog.CanTriggerDecision);
        Assert.DoesNotContain(preview, actionLog.ModelOutput, StringComparison.Ordinal);
        Assert.Contains(draftId.ToString(), actionLog.ModelOutput, StringComparison.Ordinal);

        var actions = Block(body, "action_proposal").GetProperty("actions").EnumerateArray();
        Assert.All(actions, action => Assert.True(action.GetProperty("requiresHumanApproval").GetBoolean()));
    }

    private static IReadOnlyList<string> BlockTypes(JsonElement body) =>
        body.GetProperty("blocks").EnumerateArray()
            .Select(block => block.GetProperty("type").GetString()!)
            .ToList();

    private static IEnumerable<JsonElement> Blocks(JsonElement body, string type) =>
        body.GetProperty("blocks").EnumerateArray()
            .Where(block => block.GetProperty("type").GetString() == type);

    private static JsonElement Block(JsonElement body, string type) => Blocks(body, type).First();

    private enum SlaState
    {
        OnTrack,
        Breached,
    }

    private sealed class Harness
    {
        private readonly InfrastructureOptions _options;
        private readonly string _role;
        private readonly IProviderHealthCheck _providerHealthCheck;
        private readonly IChatCompletionProvider _chatCompletionProvider;

        public Harness(
            InfrastructureOptions? options = null,
            string role = "ClaimsSupervisor",
            IProviderHealthCheck? providerHealthCheck = null,
            IChatCompletionProvider? chatCompletionProvider = null)
        {
            _options = options ?? new InfrastructureOptions { AuthMode = "Development" };
            _role = role;
            _providerHealthCheck = providerHealthCheck ?? new LocalTestProviderHealthCheck();
            _chatCompletionProvider = chatCompletionProvider ?? new CapturingChatCompletionProvider("Configured chat provider was not invoked.");
        }

        public InMemoryCaseRepository CaseRepository { get; } = new();
        public InMemoryCaseDocumentRepository DocumentRepository { get; } = new();
        public InMemoryKnowledgeDocumentRepository KnowledgeRepository { get; } = new();
        public InMemoryAgentActionLogRepository ActionLogRepository { get; } = new();
        public InMemoryApprovalRepository ApprovalRepository { get; } = new();
        public InMemoryWorkflowTaskRepository WorkflowTaskRepository { get; } = new();
        public InMemoryCustomerUpdateRepository CustomerUpdateRepository { get; } = new();
        public InMemoryAuditRepository AuditRepository { get; } = new();
        public StaticDocumentChecklistProvider ChecklistProvider { get; } = new(new CompletenessScoringService());
        public BasicSlaCalculator SlaCalculator { get; } = new();
        private TenantContextAccessor TenantContextAccessor { get; } = new();

        public async Task<ClaimCase> SeedIncompleteCaseAsync(string customer, SlaState slaState)
        {
            var now = DateTimeOffset.UtcNow;
            var slaDueAt = slaState == SlaState.Breached ? now.AddHours(-6) : now.AddDays(3);
            var claimCase = new ClaimCase
            {
                CaseId = Guid.NewGuid(),
                Scope = Scope,
                CustomerName = customer,
                PolicyNumber = "POL-1001",
                ProductLine = "Motor",
                ClaimType = "Accident",
                Channel = "WebPortal",
                IncidentDate = new DateOnly(2026, 5, 24),
                IncidentDescription = "Minor collision reported at branch intake.",
                Status = CaseStatus.MissingDocuments,
                CompletenessScore = 40m,
                SlaDueAt = slaDueAt,
                AssignedUserId = "user-claims-1",
                CreatedByUserId = "user-claims-1",
                CreatedAt = now,
                UpdatedAt = now,
                CorrelationId = "corr-query-001",
            };

            await CaseRepository.SaveAsync(claimCase);
            return claimCase;
        }

        public async Task SeedPendingApprovalAsync()
        {
            var now = DateTimeOffset.UtcNow;
            await ApprovalRepository.SaveAsync(new ApprovalRequest
            {
                ApprovalId = Guid.NewGuid(),
                Scope = Scope,
                CaseId = Guid.NewGuid(),
                ApprovalType = "SupervisorApproval",
                Status = ApprovalStatus.Pending,
                RiskLevel = "High",
                EvidenceSummary = "Supervisor review required before proceeding.",
                SourceReferences = ["case-file"],
                RequestedByUserId = "user-claims-1",
                CreatedAt = now,
                UpdatedAt = now,
                CorrelationId = "corr-query-001",
            });
        }

        public async Task<(int? StatusCode, JsonElement Body)> RunAsync(string question)
        {
            var permissionService = new PermissionService(TenantContextAccessor, new LocalTestPermissionReadRepository());
            var queryService = new CommandCenterQueryService(
                TenantContextAccessor,
                permissionService,
                CaseRepository,
                DocumentRepository,
                ChecklistProvider,
                KnowledgeRepository,
                ActionLogRepository,
                ApprovalRepository,
                WorkflowTaskRepository,
                CustomerUpdateRepository,
                AuditRepository,
                SlaCalculator,
                new DeterministicThenAiIntentClassifier(),
                new ProviderRequirementResolver(),
                _providerHealthCheck,
                _chatCompletionProvider,
                new DeterministicAiOutputValidator(),
                new CustomerUpdateNotificationDraftService(CustomerUpdateRepository));

            var result = await CommandCenterQueryEndpoints.RunQuery(
                new CommandCenterQueryRequest(question),
                queryService,
                TenantContextAccessor,
                _options,
                ScopedContext(),
                CancellationToken.None);

            return await ExecuteAsync(result);
        }

        private DefaultHttpContext ScopedContext()
        {
            var context = new DefaultHttpContext { TraceIdentifier = "trace-query-001" };
            context.Request.Headers["X-Tenant-Id"] = "tenant-demo";
            context.Request.Headers["X-Workspace-Id"] = "workspace-claims";
            context.Request.Headers["X-User-Id"] = "user-claims-1";
            context.Request.Headers["x-demo-user-id"] = "user-claims-1";
            context.Request.Headers["x-demo-role"] = _role;
            context.Request.Headers["X-Correlation-ID"] = "corr-query-001";
            return context;
        }

        private static async Task<(int? StatusCode, JsonElement Body)> ExecuteAsync(IResult result)
        {
            var httpContext = new DefaultHttpContext
            {
                RequestServices = new ServiceCollection()
                    .AddLogging()
                    .AddProblemDetails()
                    .BuildServiceProvider(),
            };
            httpContext.Response.Body = new MemoryStream();

            await result.ExecuteAsync(httpContext);

            httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
            var body = httpContext.Response.Body.Length == 0
                ? default
                : (await JsonSerializer.DeserializeAsync<JsonElement>(httpContext.Response.Body, JsonOptions));

            return (httpContext.Response.StatusCode, body);
        }
    }

    private sealed class ReadyProviderHealthCheck : IProviderHealthCheck
    {
        public Task<ProviderStatus> CheckAsync(string providerCode, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ProviderStatus(providerCode, "ready", "Provider is configured for this local test.", DateTimeOffset.UtcNow));
    }

    private sealed class CapturingChatCompletionProvider(string content) : IChatCompletionProvider
    {
        public Task<ChatCompletionResult> CompleteAsync(
            ChatCompletionRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new ChatCompletionResult(
                content,
                "AzureOpenAI",
                "chat-test",
                DateTimeOffset.UtcNow));
    }
}
