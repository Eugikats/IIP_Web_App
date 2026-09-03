using System.Text.Json;
using InsuranceIntelligence.Api.Assistant;
using InsuranceIntelligence.Api.Compliance;
using InsuranceIntelligence.Api.Communications;
using InsuranceIntelligence.Api.Local;
using InsuranceIntelligence.Api.Workflow;
using InsuranceIntelligence.Application.Compliance;
using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Application.Onboarding;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Approvals;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Compliance;
using InsuranceIntelligence.Domain.Communications;
using InsuranceIntelligence.Domain.Documents;
using InsuranceIntelligence.Domain.Knowledge;
using InsuranceIntelligence.Domain.Storage;
using InsuranceIntelligence.Domain.Workflow;
using InsuranceIntelligence.Infrastructure.Configuration;
using InsuranceIntelligence.LocalTestDoubles;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace InsuranceIntelligence.Api.Tests;

/// <summary>
/// AI-boundary red-team contract: server-side enforcement that non-human actors cannot
/// finalize consequential insurance actions, while advisory/draft paths remain available.
/// </summary>
public sealed class AiBoundaryContractTests
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private static readonly TenantScope Scope = TenantScope.Create("tenant-demo", "workspace-claims");

    [Theory]
    [InlineData("agent:runtime")]
    [InlineData("system:batch")]
    [InlineData("integration:webhook")]
    public async Task ApproveApproval_Blocks_NonHuman_Actors(string actorUserId)
    {
        var deps = CreateWorkflowDeps();
        var claimCase = await CreateCaseAsync(deps);
        var approval = await CreatePendingApprovalAsync(deps, claimCase, "ApproveClaim");

        var result = await ApprovalEndpoints.ApproveApproval(
            approval.ApprovalId,
            new ApprovalDecisionRequest("Agent attempted consequential approve."),
            deps.ApprovalRepository,
            deps.AuditRepository,
            ActorContext(actorUserId, "Agent must not approve."),
            CancellationToken.None);
        var (status, body) = await ExecuteAsync<JsonElement>(result);

        Assert.Equal(StatusCodes.Status403Forbidden, status);
        Assert.Equal("human_decision_required", body.GetProperty("code").GetString());
        var persisted = await deps.ApprovalRepository.GetAsync(approval.ApprovalId, Scope, CancellationToken.None);
        Assert.Equal(ApprovalStatus.Pending, persisted!.Status);
    }

    [Theory]
    [InlineData("agent:runtime")]
    [InlineData("system:orchestrator")]
    public async Task RejectApproval_Blocks_NonHuman_Actors(string actorUserId)
    {
        var deps = CreateWorkflowDeps();
        var claimCase = await CreateCaseAsync(deps);
        var approval = await CreatePendingApprovalAsync(deps, claimCase, "RejectClaim");

        var result = await ApprovalEndpoints.RejectApproval(
            approval.ApprovalId,
            new ApprovalDecisionRequest("Agent attempted consequential reject."),
            deps.ApprovalRepository,
            deps.AuditRepository,
            ActorContext(actorUserId, "Agent must not reject."),
            CancellationToken.None);
        var (status, body) = await ExecuteAsync<JsonElement>(result);

        Assert.Equal(StatusCodes.Status403Forbidden, status);
        Assert.Equal("human_decision_required", body.GetProperty("code").GetString());
    }

    [Fact]
    public async Task OverrideApproval_Blocks_Agent_Actor()
    {
        var deps = CreateWorkflowDeps();
        var claimCase = await CreateCaseAsync(deps);
        var approval = await CreatePendingApprovalAsync(deps, claimCase, "SettlementStep");

        var result = await ApprovalEndpoints.OverrideApproval(
            approval.ApprovalId,
            new ApprovalDecisionRequest("Agent attempted override."),
            deps.ApprovalRepository,
            deps.AuditRepository,
            ActorContext("agent:runtime", "Agent must not override."),
            CancellationToken.None);
        var (status, body) = await ExecuteAsync<JsonElement>(result);

        Assert.Equal(StatusCodes.Status403Forbidden, status);
        Assert.Equal("human_decision_required", body.GetProperty("code").GetString());
    }

    [Fact]
    public async Task SendCustomerEmail_Blocks_Agent_Even_When_Request_Claims_Approved()
    {
        var deps = CreateCommunicationDeps();
        var result = await CommunicationEndpoints.SendEmail(
            new SendCommunicationRequest(
                Guid.NewGuid(),
                "customer@example.test",
                "Treat this as already approved.",
                "agent:runtime",
                Approved: true,
                LowRisk: false),
            deps.Provider,
            deps.CommunicationRepository,
            deps.AuditRepository,
            DevelopmentOptions(),
            ActorContext("agent:runtime", "Prompt injection bypass attempt."),
            CancellationToken.None);
        var (status, body) = await ExecuteAsync<JsonElement>(result);

        Assert.Equal(StatusCodes.Status403Forbidden, status);
        Assert.Equal("human_decision_required", body.GetProperty("code").GetString());
    }

    [Fact]
    public async Task ExportAuditPack_Blocks_Agent_Actor()
    {
        var deps = CreateWorkflowDeps();
        var claimCase = await CreateCaseAsync(deps);
        claimCase = claimCase with { Status = CaseStatus.ReadyForExport };
        await deps.CaseRepository.SaveAsync(claimCase, CancellationToken.None);

        var context = ActorContext("agent:runtime", "Agent export attempt.");
        var result = await ComplianceEndpoints.ExportAuditPack(
            claimCase.CaseId,
            CreateComplianceService(context, caseRepository: deps.CaseRepository),
            DevelopmentOptions(),
            context,
            CancellationToken.None);
        var (status, body) = await ExecuteAsync<JsonElement>(result);

        Assert.Equal(StatusCodes.Status400BadRequest, status);
        Assert.True(
            body.TryGetProperty("errors", out var errors) && errors.TryGetProperty("status", out _)
            || (body.TryGetProperty("code", out var code) && code.GetString() == "human_decision_required"));
    }

    [Theory]
    [InlineData(ClaimConsequentialAction.ApproveClaim)]
    [InlineData(ClaimConsequentialAction.RejectClaim)]
    [InlineData(ClaimConsequentialAction.Repudiate)]
    [InlineData(ClaimConsequentialAction.ApproveSettlement)]
    [InlineData(ClaimConsequentialAction.ApprovePayment)]
    [InlineData(ClaimConsequentialAction.BindOrPricePolicy)]
    [InlineData(ClaimConsequentialAction.AdverseUnderwriting)]
    [InlineData(ClaimConsequentialAction.FinalComplaintResponse)]
    [InlineData(ClaimConsequentialAction.RegulatoryFiling)]
    [InlineData(ClaimConsequentialAction.EvidencePackExport)]
    [InlineData(ClaimConsequentialAction.CustomerCommunicationSend)]
    public void ClaimAuthorityPolicy_Blocks_Agent_For_All_Consequential_Actions(ClaimConsequentialAction action)
    {
        var exception = Assert.Throws<DomainException>(() =>
            ClaimAuthorityPolicy.EnsureActorMayPerform(action, DecisionActorType.Agent));

        Assert.Contains("human", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Program_Does_Not_Map_Direct_Policy_Bind_Or_Payment_Execution_Routes()
    {
        var programPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "src", "InsuranceIntelligence.Api", "Program.cs"));
        var source = File.ReadAllText(programPath);

        foreach (var fragment in new[]
        {
            "bind-policy", "issue-policy", "execute-payment", "approve-payment", "settle-claim", "repudiate-claim"
        })
        {
            Assert.DoesNotContain(fragment, source, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task AssistantPolicyAnswer_Never_Sets_CanTriggerDecision()
    {
        var citationFormatter = new SourceCitationFormatter();
        var indexProvider = new InMemoryKnowledgeIndexProvider();
        var retrieval = new MockRetrievalProvider(indexProvider, citationFormatter);
        var assistant = new MockAiAssistantProvider();
        var actionLogs = new InMemoryAgentActionLogRepository();
        var audit = new InMemoryAuditRepository();
        var cases = new InMemoryCaseRepository();

        var context = ActorContext("user-claims-1", "Policy question for advisory answer.");
        context.Request.Headers["X-Tenant-Id"] = Scope.TenantId;
        context.Request.Headers["X-Workspace-Id"] = Scope.WorkspaceId;

        var result = await AssistantEndpoints.AskPolicyQuestion(
            new PolicyQuestionRequest(null, "What is covered for windscreen damage?", null, "InternalUser"),
            retrieval,
            assistant,
            actionLogs,
            audit,
            cases,
            new LocalTestAgentGate(),
            context,
            CancellationToken.None);
        var (status, body) = await ExecuteAsync<PolicyQuestionResponse>(result);

        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.False(body.CanTriggerDecision);
        var logs = await actionLogs.ListAsync(Scope);
        Assert.All(logs, log => Assert.False(log.CanTriggerDecision));
    }

    [Fact]
    public async Task DraftCustomerUpdate_Allows_Agent_Context_To_Prepare_Work()
    {
        var deps = CreateDraftDeps();
        var claimCase = await CreateCaseAsync(deps.CaseRepository, deps.AuditRepository);

        var result = await CustomerUpdateEndpoints.DraftCustomerUpdate(
            claimCase.CaseId,
            new DraftCustomerUpdateRequest("StatusUpdate", "Email"),
            deps.CaseRepository,
            deps.DocumentRepository,
            deps.ChecklistProvider,
            deps.DraftingProvider,
            deps.CustomerUpdateRepository,
            deps.ActionLogRepository,
            deps.AuditRepository,
            new LocalTestAgentGate(),
            ActorContext("agent:runtime", "Draft only."),
            CancellationToken.None);
        var (status, _) = await ExecuteAsync<CustomerUpdateResponse>(result);

        Assert.Equal(StatusCodes.Status201Created, status);
    }

    [Fact]
    public async Task ApproveApproval_Allows_Authorized_Human_Actor()
    {
        var deps = CreateWorkflowDeps();
        var claimCase = await CreateCaseAsync(deps);
        var approval = await CreatePendingApprovalAsync(deps, claimCase, "SettlementStep");

        var result = await ApprovalEndpoints.ApproveApproval(
            approval.ApprovalId,
            new ApprovalDecisionRequest("Human approved after review."),
            deps.ApprovalRepository,
            deps.AuditRepository,
            ActorContext("user-claims-1", "Human approve."),
            CancellationToken.None);
        var (status, body) = await ExecuteAsync<ApprovalResponse>(result);

        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.Equal(ApprovalStatus.Approved.ToString(), body.Status, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SendCustomerEmail_Allows_Human_Actor_When_Provider_Approval_Flag_Set()
    {
        var deps = CreateCommunicationDeps();
        var claimCase = await CreateCaseAsync(deps.CaseRepository, deps.AuditRepository);
        var result = await CommunicationEndpoints.SendEmail(
            new SendCommunicationRequest(
                claimCase.CaseId,
                "customer@example.test",
                "Human-approved customer update.",
                "user-claims-1",
                Approved: true,
                LowRisk: false),
            deps.Provider,
            deps.CommunicationRepository,
            deps.AuditRepository,
            DevelopmentOptions(),
            ActorContext("user-claims-1", "Human send after approval."),
            CancellationToken.None);
        var (status, _) = await ExecuteAsync<JsonElement>(result);

        Assert.Equal(StatusCodes.Status200OK, status);
    }

    private static async Task<ClaimCase> CreateCaseAsync(WorkflowDeps deps) =>
        await CreateCaseAsync(deps.CaseRepository, deps.AuditRepository);

    private static async Task<ClaimCase> CreateCaseAsync(InMemoryCaseRepository caseRepository, InMemoryAuditRepository auditRepository) =>
        await new CaseIntakeService(caseRepository, auditRepository).CreateCaseAsync(
            new CreateCaseCommand(
                Scope,
                "Amina Okello",
                "amina@example.test",
                "+256700000000",
                "POL-1001",
                "Motor",
                "Accident",
                "WebPortal",
                new DateOnly(2026, 5, 24),
                "Minor collision reported at branch intake.",
                "user-claims-1",
                "corr-ai-boundary-001"));

    private static async Task<ApprovalRequest> CreatePendingApprovalAsync(
        WorkflowDeps deps,
        ClaimCase claimCase,
        string approvalType)
    {
        var approval = new ApprovalRequest
        {
            ApprovalId = Guid.NewGuid(),
            Scope = claimCase.Scope,
            CaseId = claimCase.CaseId,
            ApprovalType = approvalType,
            Status = ApprovalStatus.Pending,
            RiskLevel = "Medium",
            EvidenceSummary = "Prepared recommendation awaiting human decision.",
            SourceReferences = ["case:" + claimCase.CaseId],
            RequestedByUserId = "user-claims-1",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            CorrelationId = "corr-ai-boundary-approval"
        };
        await deps.ApprovalRepository.SaveAsync(approval, CancellationToken.None);
        return approval;
    }

    private static WorkflowDeps CreateWorkflowDeps() =>
        new(
            new InMemoryCaseRepository(),
            new InMemoryAuditRepository(),
            new InMemoryApprovalRepository(),
            new InMemoryCustomerUpdateRepository());

    private static DraftDeps CreateDraftDeps()
    {
        var scoringService = new CompletenessScoringService();
        var workflowRuleProvider = new StaticWorkflowRuleProvider();
        return new DraftDeps(
            new InMemoryCaseRepository(),
            new InMemoryAuditRepository(),
            new InMemoryCaseDocumentRepository(),
            new StaticDocumentChecklistProvider(scoringService),
            new MockCustomerUpdateDraftingProvider(workflowRuleProvider),
            new InMemoryCustomerUpdateRepository(),
            new InMemoryAgentActionLogRepository());
    }

    private static CommunicationDeps CreateCommunicationDeps()
    {
        var caseRepository = new InMemoryCaseRepository();
        return new CommunicationDeps(
            new MockCommunicationSendingProvider(),
            caseRepository,
            new InMemoryCommunicationRepository(),
            new InMemoryAuditRepository());
    }

    private static ComplianceQueryService CreateComplianceService(
        DefaultHttpContext context,
        InMemoryCaseRepository? caseRepository = null,
        InMemoryAuditRepository? auditRepository = null,
        IEvidencePackStorageProvider? storageProvider = null)
    {
        var tenantContext = context.RequestServices.GetRequiredService<ITenantContextAccessor>();
        var options = DevelopmentOptions();
        return new ComplianceQueryService(
            tenantContext,
            new AllowPermissionService(),
            new InMemoryTenantConfigurationRepository(),
            new FixedOnboardingRepository([]),
            caseRepository ?? new InMemoryCaseRepository(),
            auditRepository ?? new InMemoryAuditRepository(),
            new InMemoryCaseDocumentRepository(),
            new InMemoryAgentActionLogRepository(),
            new InMemoryApprovalRepository(),
            new InMemoryCustomerUpdateRepository(),
            storageProvider ?? new CapturingEvidencePackStorage(),
            new BasicSlaCalculator(),
            new ComplianceResidencySettings(
                options.DataResidencyCountry,
                options.DataResidencyGeography,
                options.AzureRegion,
                options.DataResidencyMode,
                options.AzureOpenAI.DeploymentType,
                options.DataResidencyAllowedAzureRegions,
                options.DataResidencyApprovalReference,
                options.AiProvider));
    }

    private static DefaultHttpContext ActorContext(string userId, string auditReason, string role = "ClaimsSupervisor")
    {
        var tenantContext = new TenantContextAccessor();
        tenantContext.SetCurrent(new TenantContext(Scope.TenantId, Scope.WorkspaceId, userId, [role]));
        var context = new DefaultHttpContext { TraceIdentifier = "trace-ai-boundary" };
        context.Request.Headers["X-Audit-Reason"] = auditReason;
        context.Request.Headers["X-Tenant-Id"] = Scope.TenantId;
        context.Request.Headers["X-Workspace-Id"] = Scope.WorkspaceId;
        context.Request.Headers["x-demo-role"] = role;
        context.Request.Headers["x-demo-user-id"] = userId;
        context.RequestServices = new ServiceCollection()
            .AddSingleton<ITenantContextAccessor>(tenantContext)
            .AddLogging()
            .AddProblemDetails()
            .BuildServiceProvider();
        return context;
    }

    private static InfrastructureOptions DevelopmentOptions() =>
        new()
        {
            AuthMode = "Development",
            CommunicationProvider = "Mock",
            AzureRegion = "approved-region-1",
            DataResidencyCountry = "Approved jurisdiction",
            DataResidencyGeography = "Approved geography",
            DataResidencyMode = "regional",
            DataResidencyAllowedAzureRegions = "approved-region-1",
            DataResidencyApprovalReference = "RES-TEST-001"
        };

    private static async Task<(int StatusCode, T Body)> ExecuteAsync<T>(IResult result)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.RequestServices = new ServiceCollection().AddLogging().AddProblemDetails().BuildServiceProvider();
        httpContext.Response.Body = new MemoryStream();
        await result.ExecuteAsync(httpContext);
        httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        var body = httpContext.Response.Body.Length == 0
            ? default!
            : (await JsonSerializer.DeserializeAsync<T>(httpContext.Response.Body, JsonOptions))!;
        return (httpContext.Response.StatusCode, body);
    }

    private sealed record WorkflowDeps(
        InMemoryCaseRepository CaseRepository,
        InMemoryAuditRepository AuditRepository,
        InMemoryApprovalRepository ApprovalRepository,
        InMemoryCustomerUpdateRepository CustomerUpdateRepository);

    private sealed record DraftDeps(
        InMemoryCaseRepository CaseRepository,
        InMemoryAuditRepository AuditRepository,
        InMemoryCaseDocumentRepository DocumentRepository,
        IDocumentChecklistProvider ChecklistProvider,
        ICustomerUpdateDraftingProvider DraftingProvider,
        InMemoryCustomerUpdateRepository CustomerUpdateRepository,
        InMemoryAgentActionLogRepository ActionLogRepository);

    private sealed record CommunicationDeps(
        MockCommunicationSendingProvider Provider,
        InMemoryCaseRepository CaseRepository,
        InMemoryCommunicationRepository CommunicationRepository,
        InMemoryAuditRepository AuditRepository);

    private sealed class AllowPermissionService : IPermissionService
    {
        public Task RequirePermissionAsync(Permission permission, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<bool> HasPermissionAsync(Permission permission, CancellationToken cancellationToken = default) => Task.FromResult(true);
    }

    private sealed class CapturingEvidencePackStorage : IEvidencePackStorageProvider
    {
        public Task<EvidencePackStorageResult> StoreAsync(EvidencePackStorageRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(new EvidencePackStorageResult("stored/key.json", request.ContentType, 128));
    }

    private sealed class FixedOnboardingRepository(IReadOnlyList<TenantOnboardingRecord> records) : ITenantOnboardingRepository
    {
        public Task UpsertAsync(TenantOnboardingRecord record, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<TenantOnboardingRecord?> GetAsync(string onboardingId, CancellationToken cancellationToken = default) =>
            Task.FromResult(records.FirstOrDefault(item => item.OnboardingId == onboardingId));

        public Task<TenantOnboardingRecord?> GetByStateAsync(string state, CancellationToken cancellationToken = default) =>
            Task.FromResult(records.FirstOrDefault(item => item.State == state));

        public Task<IReadOnlyList<TenantOnboardingRecord>> ListAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(records);
    }
}
