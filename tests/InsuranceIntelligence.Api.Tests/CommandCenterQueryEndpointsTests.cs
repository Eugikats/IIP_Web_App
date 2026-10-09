using System.Text.Json;
using InsuranceIntelligence.Api.CommandCenter;
using InsuranceIntelligence.Api.Local;
using InsuranceIntelligence.Application.Agents;
using InsuranceIntelligence.Application.Agents.Tools;
using InsuranceIntelligence.Application.Ai;
using InsuranceIntelligence.Application.CommandCenter;
using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.LocalTestDoubles;
using InsuranceIntelligence.Domain.Agents;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Knowledge;
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
        Assert.NotEqual(Guid.Empty, body.GetProperty("conversationId").GetGuid());
        Assert.NotEqual(Guid.Empty, body.GetProperty("turnId").GetGuid());
        Assert.NotEqual(default, body.GetProperty("dataAsOf").GetDateTimeOffset());
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("snapshotId").GetString()));

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

        // WO-07: the AI execution is also reconstructable as a single AgentRun with correct
        // scope, user, correlation, and outcome, not only as an AgentActionLog/AuditEvent.
        var run = Assert.Single(harness.RecordedAgentRuns);
        Assert.Equal("command-center", run.AgentKey);
        Assert.Equal(AgentRunStatus.Completed, run.Status);
        Assert.Equal(Scope, run.Scope);
        Assert.NotNull(run.CompletedAt);
        Assert.NotEqual(default, run.StartedAt);

        // WO-09: data access is now real tool calls, so the trace is no longer empty, it names
        // the exact WO-08 tools this request's data actually came from.
        var toolNames = run.ToolTrace.Select(entry => entry.ToolName).ToArray();
        Assert.Contains("claims.attention.read", toolNames);
        Assert.Contains("approvals.queue.read", toolNames);
        Assert.Contains("metrics.read", toolNames);
        Assert.All(run.ToolTrace, entry => Assert.Equal("ok", entry.Outcome));
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
            item => Assert.Equal("/app/work-items", item.GetProperty("href").GetString()));
    }

    // Response-block hrefs are a portal navigation contract: the portal only follows links under
    // /app/ (safeInternalWorkHref) and silently drops anything else onto a generic list page.
    // "/cases/{id}" and "/approvals" both shipped here once and both dropped operators on the wrong
    // screen from the AI surface, so every href this contract emits is pinned to a real route.
    [Fact]
    public async Task EveryResponseHrefTargetsARealPortalRoute()
    {
        var harness = new Harness();
        var claimCase = await harness.SeedIncompleteCaseAsync("Brian Mukasa", SlaState.Breached);
        await harness.SeedPendingApprovalAsync();

        var (statusCode, body) = await harness.RunAsync("What needs my attention today?");

        Assert.Equal(StatusCodes.Status200OK, statusCode);

        var hrefs = new List<string>();
        CollectHrefs(body, hrefs);
        Assert.NotEmpty(hrefs);
        Assert.All(hrefs, href => Assert.StartsWith("/app/", href, StringComparison.Ordinal));
        Assert.Contains($"/app/claims/{claimCase.CaseId}", hrefs);

        static void CollectHrefs(JsonElement element, List<string> hrefs)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    foreach (var property in element.EnumerateObject())
                    {
                        if (property.NameEquals("href") || property.NameEquals("targetHref"))
                        {
                            var value = property.Value.GetString();
                            if (!string.IsNullOrWhiteSpace(value)) hrefs.Add(value);
                        }
                        else
                        {
                            CollectHrefs(property.Value, hrefs);
                        }
                    }
                    break;
                case JsonValueKind.Array:
                    foreach (var item in element.EnumerateArray()) CollectHrefs(item, hrefs);
                    break;
            }
        }
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
    public async Task RunQuery_UsesResolvedMembershipContextInsteadOfBrowserAuthorityHeaders()
    {
        var harness = new Harness();
        var membershipScope = TenantScope.Create("tenant-membership", "workspace-membership");
        var spoofedScope = TenantScope.Create("tenant-spoofed", "workspace-spoofed");

        var (statusCode, body) = await harness.RunWithResolvedContextAsync(
            "What needs my attention today?",
            membershipScope,
            "membership-command-user",
            spoofedScope);
        var actionLogId = body.GetProperty("agentActionLogId").GetGuid();
        var membershipLogs = await harness.ActionLogRepository.ListAsync(membershipScope);
        var spoofedLogs = await harness.ActionLogRepository.ListAsync(spoofedScope);

        Assert.Equal(StatusCodes.Status200OK, statusCode);
        Assert.Contains(membershipLogs, log => log.AgentActionLogId == actionLogId);
        Assert.Empty(spoofedLogs);
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
    public async Task DisabledCommandCenterAgent_RefusesInsteadOfAnsweringAndWritesAudit()
    {
        // "What needs my attention today?" reaches the deterministic default path (no provider
        // dependency), so this proves the kill switch gates the whole agent, not only its
        // model-calling branches.
        var harness = new Harness(agentGate: new LocalTestAgentGate("command-center"));
        await harness.SeedIncompleteCaseAsync("Grace Nabirye", SlaState.OnTrack);

        var (statusCode, body) = await harness.RunAsync("What needs my attention today?");

        Assert.Equal(StatusCodes.Status200OK, statusCode);
        var types = BlockTypes(body);
        Assert.DoesNotContain("metric_pills", types);
        Assert.DoesNotContain("claims_table", types);
        var block = Block(body, "error");
        Assert.Equal("agent_disabled", block.GetProperty("code").GetString());
        Assert.Contains("disabled for this workspace", block.GetProperty("safeMessage").GetString());

        var actionLogId = body.GetProperty("agentActionLogId").GetGuid();
        var actionLogs = await harness.ActionLogRepository.ListAsync(Scope);
        var auditEvents = await harness.AuditRepository.ListAsync(Scope);
        Assert.Contains(actionLogs, log =>
            log.AgentActionLogId == actionLogId
            && log.RiskFlag == "agent_disabled"
            && !log.CanTriggerDecision);
        Assert.Contains(auditEvents, evt => evt.ActionName == "assistant.command_center.answered");

        // WO-07: a refusal is still reconstructable as an AgentRun, not just as an AgentActionLog.
        var run = Assert.Single(harness.RecordedAgentRuns);
        Assert.Equal("command-center", run.AgentKey);
        Assert.Equal(AgentRunStatus.Refused, run.Status);
        Assert.Equal("agent_disabled", run.ErrorCategory);
    }

    [Fact]
    public async Task DisabledReportInsightAgent_RefusesBeforeCallingTheModel()
    {
        // Providers must be healthy here specifically so the request reaches the agent gate
        // rather than being refused earlier by the provider-not-configured gate (see
        // AiDependentIntentReturnsNotConfiguredAndWritesAudit for that path). The chat provider
        // throws if invoked, proof the model genuinely never runs, not just that the response
        // happens to look like a refusal.
        var harness = new Harness(
            providerHealthCheck: new AlwaysReadyProviderHealthCheck(),
            chatCompletionProvider: new ThrowsIfCalledChatCompletionProvider(),
            agentGate: new LocalTestAgentGate("report-insight"));
        await harness.SeedIncompleteCaseAsync("Grace Nabirye", SlaState.OnTrack);

        var (statusCode, body) = await harness.RunAsync("Prepare a management summary for today's claims.");

        Assert.Equal(StatusCodes.Status200OK, statusCode);
        var block = Block(body, "error");
        Assert.Equal("agent_disabled", block.GetProperty("code").GetString());

        var actionLogs = await harness.ActionLogRepository.ListAsync(Scope);
        Assert.Contains(actionLogs, log => log.RiskFlag == "agent_disabled");
    }

    [Fact]
    public async Task EnabledReportInsightAgent_StillReachesTheModel()
    {
        // Negative control for the test above: with the same healthy providers and the agent
        // enabled, the request must actually reach the model, proving the prior test's refusal
        // came from the kill switch, not from some other reason the model was never called.
        var harness = new Harness(
            providerHealthCheck: new AlwaysReadyProviderHealthCheck(),
            chatCompletionProvider: new CapturingChatCompletionProvider("Summary content."),
            agentGate: new LocalTestAgentGate());
        await harness.SeedIncompleteCaseAsync("Grace Nabirye", SlaState.OnTrack);

        var (statusCode, body) = await harness.RunAsync("Prepare a management summary for today's claims.");

        Assert.Equal(StatusCodes.Status200OK, statusCode);
        var types = BlockTypes(body);
        Assert.DoesNotContain("error", types);
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

    [Fact]
    public async Task ManagementSummaryIntentUsesAiValidatorAndDoesNotStoreRawSummary()
    {
        var harness = new Harness(
            providerHealthCheck: new ReadyProviderHealthCheck(),
            chatCompletionProvider: new CapturingChatCompletionProvider(
                "Overview: 3 claims need review.\nThe claim is denied.\nNext: keep supervisors focused on SLA breaches."),
            retrievalProvider: new StaticRetrievalProvider());
        await harness.SeedIncompleteCaseAsync("Faridah Ssemanda", SlaState.Breached);
        await harness.SeedPendingApprovalAsync();

        var (statusCode, body) = await harness.RunAsync("Prepare a management summary for today's claims.");

        Assert.Equal(StatusCodes.Status200OK, statusCode);
        Assert.False(body.GetProperty("canTriggerDecision").GetBoolean());
        Assert.Contains("management_summary", BlockTypes(body));
        Assert.Contains("metric_pills", BlockTypes(body));
        Assert.Contains("evidence_list", BlockTypes(body));
        Assert.Equal("AzureOpenAI", body.GetProperty("modelProvider").GetString());

        var summaryBlock = Block(body, "management_summary");
        var sectionBodies = summaryBlock.GetProperty("sections").EnumerateArray()
            .Select(section => section.GetProperty("body").GetString()!)
            .ToArray();
        Assert.Contains(sectionBodies, bodyText => bodyText.Contains("requires human review", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(sectionBodies, bodyText => bodyText.Contains("claim is denied", StringComparison.OrdinalIgnoreCase));

        var actionLogId = body.GetProperty("agentActionLogId").GetGuid();
        var actionLogs = await harness.ActionLogRepository.ListAsync(Scope);
        var actionLog = Assert.Single(actionLogs, log => log.AgentActionLogId == actionLogId);
        Assert.Equal("AzureOpenAI", actionLog.ModelProvider);
        Assert.False(actionLog.CanTriggerDecision);
        Assert.Contains("citation(s)", actionLog.ModelOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("Overview: 3 claims need review", actionLog.ModelOutput, StringComparison.Ordinal);
        Assert.NotEmpty(actionLog.RetrievedSources);
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
        private readonly IRetrievalProvider _retrievalProvider;
        private readonly IAgentGate _agentGate;
        private readonly IAgentRunRecorder _agentRunRecorder = new LocalTestAgentRunRecorder();

        public Harness(
            InfrastructureOptions? options = null,
            string role = "ClaimsSupervisor",
            IProviderHealthCheck? providerHealthCheck = null,
            IChatCompletionProvider? chatCompletionProvider = null,
            IRetrievalProvider? retrievalProvider = null,
            IAgentGate? agentGate = null)
        {
            _options = options ?? new InfrastructureOptions { AuthMode = "Development" };
            _role = role;
            _providerHealthCheck = providerHealthCheck ?? new LocalTestProviderHealthCheck();
            _chatCompletionProvider = chatCompletionProvider ?? new CapturingChatCompletionProvider("Configured chat provider was not invoked.");
            _retrievalProvider = retrievalProvider ?? new EmptyRetrievalProvider();
            _agentGate = agentGate ?? new LocalTestAgentGate();
        }

        public IReadOnlyList<AgentRun> RecordedAgentRuns => ((LocalTestAgentRunRecorder)_agentRunRecorder).Recorded;

        public InMemoryCaseRepository CaseRepository { get; } = new();
        public InMemoryCaseDocumentRepository DocumentRepository { get; } = new();
        public InMemoryKnowledgeDocumentRepository KnowledgeRepository { get; } = new();
        public InMemoryAgentActionLogRepository ActionLogRepository { get; } = new();
        public InMemoryApprovalRepository ApprovalRepository { get; } = new();
        public InMemoryWorkflowTaskRepository WorkflowTaskRepository { get; } = new();
        public InMemoryCustomerUpdateRepository CustomerUpdateRepository { get; } = new();
        public InMemoryAuditRepository AuditRepository { get; } = new();
        public InMemoryCommandCenterConversationRepository ConversationRepository { get; } = new();
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

        /// <summary>WO-09: CommandCenterQueryService now reaches its data through the WO-08 tool
        /// registry rather than repositories directly, so the harness must build one backed by
        /// the exact same in-memory repositories the tests seed via SeedIncompleteCaseAsync /
        /// SeedPendingApprovalAsync, otherwise the tools would see empty state and every
        /// data-dependent assertion in this file would break. Mirrors Program.cs's real
        /// registration set.</summary>
        private InsuranceIntelligence.Application.Agents.Tools.IAgentToolRegistry BuildToolRegistry(IPermissionService permissionService)
        {
            var loader = new ClaimSnapshotLoader(CaseRepository, DocumentRepository, ChecklistProvider, SlaCalculator);
            return new AgentToolRegistry(
            [
                new ClaimsAttentionReadTool(loader, permissionService),
                new ClaimsSlaReadTool(loader, permissionService),
                new DocumentsGapsReadTool(loader, permissionService),
                new ApprovalsQueueReadTool(ApprovalRepository, permissionService),
                new KnowledgeSearchTool(_retrievalProvider, permissionService),
                new AuditReadTool(AuditRepository, permissionService),
                new ProviderHealthReadTool(_providerHealthCheck, permissionService),
                new MetricsReadTool(
                    CaseRepository,
                    DocumentRepository,
                    KnowledgeRepository,
                    ActionLogRepository,
                    ApprovalRepository,
                    WorkflowTaskRepository,
                    CustomerUpdateRepository,
                    SlaCalculator,
                    permissionService),
            ]);
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
                _retrievalProvider,
                ActionLogRepository,
                AuditRepository,
                new DeterministicThenAiIntentClassifier(),
                new ProviderRequirementResolver(),
                _chatCompletionProvider,
                new DeterministicAiOutputValidator(),
                new CustomerUpdateNotificationDraftService(CustomerUpdateRepository),
                _agentGate,
                _agentRunRecorder,
                BuildToolRegistry(permissionService));
            var conversationService = new CommandCenterConversationService(
                TenantContextAccessor,
                permissionService,
                 queryService,
                 ConversationRepository,
                 AuditRepository,
                 new ConversationGroundingService(CaseRepository, DocumentRepository));

            var result = await CommandCenterQueryEndpoints.RunQuery(
                new CommandCenterQueryRequest(question),
                conversationService,
                TenantContextAccessor,
                new AllowAllPermissions(),
                _options,
                ScopedContext(),
                CancellationToken.None);

            return await ExecuteAsync(result);
        }

        public async Task<(int? StatusCode, JsonElement Body)> RunWithResolvedContextAsync(
            string question,
            TenantScope membershipScope,
            string membershipUserId,
            TenantScope spoofedScope)
        {
            TenantContextAccessor.SetCurrent(new TenantContext(
                membershipScope.TenantId,
                membershipScope.WorkspaceId,
                membershipUserId,
                [_role]));
            var permissionService = new PermissionService(TenantContextAccessor, new LocalTestPermissionReadRepository());
            var queryService = new CommandCenterQueryService(
                TenantContextAccessor,
                permissionService,
                CaseRepository,
                DocumentRepository,
                ChecklistProvider,
                _retrievalProvider,
                ActionLogRepository,
                AuditRepository,
                new DeterministicThenAiIntentClassifier(),
                new ProviderRequirementResolver(),
                _chatCompletionProvider,
                new DeterministicAiOutputValidator(),
                new CustomerUpdateNotificationDraftService(CustomerUpdateRepository),
                _agentGate,
                _agentRunRecorder,
                BuildToolRegistry(permissionService));
            var conversationService = new CommandCenterConversationService(
                TenantContextAccessor,
                permissionService,
                 queryService,
                 ConversationRepository,
                 AuditRepository,
                 new ConversationGroundingService(CaseRepository, DocumentRepository));

            var result = await CommandCenterQueryEndpoints.RunQuery(
                new CommandCenterQueryRequest(question),
                conversationService,
                TenantContextAccessor,
                new AllowAllPermissions(),
                _options,
                ScopedContext(spoofedScope, "spoofed-command-user"),
                CancellationToken.None);

            return await ExecuteAsync(result);
        }

        private DefaultHttpContext ScopedContext()
        {
            return ScopedContext(Scope, "user-claims-1");
        }

        private DefaultHttpContext ScopedContext(TenantScope scope, string userId)
        {
            var context = new DefaultHttpContext { TraceIdentifier = "trace-query-001" };
            context.Request.Headers["X-Tenant-Id"] = scope.TenantId;
            context.Request.Headers["X-Workspace-Id"] = scope.WorkspaceId;
            context.Request.Headers["X-User-Id"] = userId;
            context.Request.Headers["x-demo-user-id"] = userId;
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

    /// <summary>
    /// WO-06 proof: if the kill switch is bypassed and the model gets called anyway, this fails
    /// the test loudly instead of silently returning a plausible-looking answer.
    /// </summary>
    private sealed class ThrowsIfCalledChatCompletionProvider : IChatCompletionProvider
    {
        public Task<ChatCompletionResult> CompleteAsync(
            ChatCompletionRequest request,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException(
                "The chat completion provider was invoked despite the agent being disabled, the kill switch was bypassed.");
    }

    private sealed class AlwaysReadyProviderHealthCheck : IProviderHealthCheck
    {
        public Task<ProviderStatus> CheckAsync(string providerCode, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ProviderStatus(providerCode, "ready", string.Empty, DateTimeOffset.UtcNow));
    }

    private sealed class EmptyRetrievalProvider : IRetrievalProvider
    {
        public Task<RetrievalResult> RetrieveAsync(
            AssistantQuestion question,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new RetrievalResult(
                question.Question,
                question.ProductLine,
                Array.Empty<KnowledgeChunk>(),
                Array.Empty<SourceCitation>()));
    }

    private sealed class StaticRetrievalProvider : IRetrievalProvider
    {
        public Task<RetrievalResult> RetrieveAsync(
            AssistantQuestion question,
            CancellationToken cancellationToken = default)
        {
            var documentId = Guid.NewGuid();
            var citation = new SourceCitation(
                documentId,
                "chunk-001",
                "Claims Operations Playbook",
                "Motor",
                "v1",
                "Escalate SLA-breached claims and keep customer communications human-reviewed.",
                "Claims Operations Playbook v1");
            var chunk = new KnowledgeChunk
            {
                KnowledgeDocumentId = documentId,
                Scope = Scope,
                ChunkId = "chunk-001",
                DocumentName = "Claims Operations Playbook",
                ProductLine = "Motor",
                Version = "v1",
                ChunkIndex = 0,
                Content = citation.Excerpt,
                SourceReference = new KnowledgeSourceReference(
                    documentId,
                    "chunk-001",
                    "Claims Operations Playbook",
                    "Motor",
                    "v1",
                    "knowledge/claims-operations-playbook.pdf")
            };

            return Task.FromResult(new RetrievalResult(
                question.Question,
                question.ProductLine,
                [chunk],
                [citation]));
        }
    }

    private sealed class AllowAllPermissions : InsuranceIntelligence.Application.Core.IPermissionService
    {
        public Task<bool> HasPermissionAsync(InsuranceIntelligence.Domain.Administration.Permission permission, CancellationToken cancellationToken = default) => Task.FromResult(true);

        public Task RequirePermissionAsync(InsuranceIntelligence.Domain.Administration.Permission permission, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
