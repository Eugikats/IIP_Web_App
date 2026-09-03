using InsuranceIntelligence.Api.Claims;
using InsuranceIntelligence.Api.Local;
using InsuranceIntelligence.Application.Agents;
using InsuranceIntelligence.Application.Agents.Tools;
using InsuranceIntelligence.Application.Ai;
using InsuranceIntelligence.Application.ClaimsSummary;
using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Documents;
using InsuranceIntelligence.Domain.Knowledge;
using InsuranceIntelligence.Domain.Policies;
using InsuranceIntelligence.Domain.Agents;
using InsuranceIntelligence.LocalTestDoubles;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace InsuranceIntelligence.Api.Tests;

/// <summary>
/// WO-10: the claims-summary agent closes the one real gap in the agent catalog. Covers: every
/// statement is grounded (the prompt sent to the model contains only real retrieved facts, proven
/// by capturing it), the kill switch actually stops it, tenant isolation, and the full audit/
/// AgentRun/AgentActionLog trail.
/// </summary>
public sealed class ClaimsSummaryTests
{
    private static readonly TenantScope TenantA = TenantScope.Create("tenant-a", "workspace-a");
    private static readonly TenantScope TenantB = TenantScope.Create("tenant-b", "workspace-a");

    [Fact]
    public async Task SummarizeAsync_GroundsPromptInRealCaseDocumentPolicyAndKnowledgeFacts()
    {
        var deps = CreateDependencies();
        var claimCase = await SeedCaseAsync(deps.CaseRepository, TenantA, customerName: "Amina Okello", productLine: "Motor");
        await deps.PolicyRepository.UpsertAsync(new Policy(
            "policy-1", TenantA.TenantId, TenantA.WorkspaceId, claimCase.PolicyNumber, "Motor", "Comprehensive",
            CustomerId: null, BranchId: null, EffectiveFrom: null, EffectiveTo: null));
        var capturingProvider = new CapturingChatCompletionProvider("A concise, grounded claim summary.");
        var knowledgeDocumentId = Guid.NewGuid();
        var chunk = new KnowledgeChunk
        {
            ChunkId = "chunk-1",
            KnowledgeDocumentId = knowledgeDocumentId,
            Scope = TenantA,
            DocumentName = "Motor Comprehensive Wording",
            ProductLine = "Motor",
            Version = "v3.2",
            ChunkIndex = 0,
            Content = "Own-damage collisions are covered subject to the excess.",
            SourceReference = new KnowledgeSourceReference(knowledgeDocumentId, "chunk-1", "Motor Comprehensive Wording", "Motor", "v3.2", "obj/motor.pdf"),
        };
        var service = BuildService(deps, capturingProvider, retrievalChunks: [chunk]);

        var result = await service.SummarizeAsync(claimCase.CaseId, TenantA, "user-claims-1", "corr-summary-001", CancellationToken.None);

        Assert.Contains("Amina Okello", capturingProvider.LastPrompt);
        Assert.Contains(claimCase.PolicyNumber, capturingProvider.LastPrompt);
        Assert.Contains("Own-damage collisions are covered", capturingProvider.LastPrompt);
        Assert.Contains("Do not approve, deny, settle, repudiate", capturingProvider.LastPrompt);
        Assert.Equal("A concise, grounded claim summary.", result.Summary);
        Assert.NotEmpty(result.KnowledgeSources);
        Assert.Contains(result.EvidenceRefs, r => r == $"case:{claimCase.CaseId}");
        Assert.Contains(result.EvidenceRefs, r => r.StartsWith("policy:"));
    }

    [Fact]
    public async Task SummarizeAsync_WritesAgentActionLogAuditAndAgentRun()
    {
        var deps = CreateDependencies();
        var claimCase = await SeedCaseAsync(deps.CaseRepository, TenantA);
        var service = BuildService(deps, new CapturingChatCompletionProvider("Summary."), retrievalChunks: []);

        var result = await service.SummarizeAsync(claimCase.CaseId, TenantA, "user-claims-1", "corr-summary-002", CancellationToken.None);

        var actionLogs = await deps.ActionLogRepository.ListForCaseAsync(claimCase.CaseId, TenantA);
        var auditEvents = await deps.AuditRepository.ListForCaseAsync(claimCase.CaseId);
        var runs = await deps.AgentRunRepository.ListForCaseAsync(claimCase.CaseId, TenantA);

        Assert.Contains(actionLogs, log => log.AgentActionLogId == result.AgentActionLogId && !log.CanTriggerDecision);
        Assert.Contains(auditEvents, evt => evt.ActionName == "assistant.command_center.answered");
        var run = Assert.Single(runs);
        Assert.Equal("claims-summary", run.AgentKey);
        Assert.Equal(AgentRunStatus.Completed, run.Status);
        Assert.Contains(run.ToolTrace, t => t.ToolName == "claim.read" && t.Outcome == "ok");
        Assert.Contains(run.ToolTrace, t => t.ToolName == "document.read" && t.Outcome == "ok");
        Assert.Contains(run.ToolTrace, t => t.ToolName == "knowledge.search" && t.Outcome == "ok");
    }

    [Fact]
    public async Task SummarizeAsync_IsolatesByTenant()
    {
        var deps = CreateDependencies();
        var claimCase = await SeedCaseAsync(deps.CaseRepository, TenantA);
        var service = BuildService(deps, new CapturingChatCompletionProvider("Summary."), retrievalChunks: []);

        await Assert.ThrowsAsync<CaseNotFoundException>(
            () => service.SummarizeAsync(claimCase.CaseId, TenantB, "user-claims-1", "corr-summary-003", CancellationToken.None));
    }

    [Fact]
    public async Task SummarizeCaseEndpoint_DisabledAgent_RefusesInsteadOfSummarizingAndNeverCallsModel()
    {
        var deps = CreateDependencies();
        var claimCase = await SeedCaseAsync(deps.CaseRepository, TenantA);
        var service = BuildService(deps, new ThrowsIfCalledChatCompletionProvider(), retrievalChunks: []);
        var context = ScopedContext();

        var result = await ClaimsSummaryEndpoints.SummarizeCase(
            claimCase.CaseId,
            deps.CaseRepository,
            service,
            new LocalTestAgentGate("claims-summary"),
            context,
            CancellationToken.None);
        var (statusCode, body) = await ExecuteAsync<System.Text.Json.JsonElement>(result);

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, statusCode);
        Assert.Equal("AgentDisabled", body.GetProperty("status").GetString());
        Assert.Equal("claims-summary", body.GetProperty("agentKey").GetString());
    }

    [Fact]
    public async Task SummarizeCaseEndpoint_EnabledAgent_ReturnsSummary()
    {
        var deps = CreateDependencies();
        var claimCase = await SeedCaseAsync(deps.CaseRepository, TenantA);
        var service = BuildService(deps, new CapturingChatCompletionProvider("Endpoint summary."), retrievalChunks: []);
        var context = ScopedContext();

        var result = await ClaimsSummaryEndpoints.SummarizeCase(
            claimCase.CaseId,
            deps.CaseRepository,
            service,
            new LocalTestAgentGate(),
            context,
            CancellationToken.None);
        var (statusCode, body) = await ExecuteAsync<ClaimsSummaryResponse>(result);

        Assert.Equal(StatusCodes.Status200OK, statusCode);
        Assert.Equal("Endpoint summary.", body!.Summary);
    }

    [Fact]
    public async Task SummarizeCaseEndpoint_ReturnsNotFoundForTenantWorkspaceMismatch()
    {
        var deps = CreateDependencies();
        var claimCase = await SeedCaseAsync(deps.CaseRepository, TenantA);
        var service = BuildService(deps, new ThrowsIfCalledChatCompletionProvider(), retrievalChunks: []);
        var context = ScopedContext(workspaceId: "wrong-workspace");

        var result = await ClaimsSummaryEndpoints.SummarizeCase(
            claimCase.CaseId,
            deps.CaseRepository,
            service,
            new LocalTestAgentGate(),
            context,
            CancellationToken.None);
        var (statusCode, _) = await ExecuteAsync<System.Text.Json.JsonElement>(result);

        Assert.Equal(StatusCodes.Status404NotFound, statusCode);
    }

    private static ClaimsSummaryService BuildService(
        Dependencies deps,
        IChatCompletionProvider chatCompletionProvider,
        IReadOnlyCollection<KnowledgeChunk> retrievalChunks)
    {
        var loader = new ClaimSnapshotLoader(deps.CaseRepository, deps.DocumentRepository, new StaticDocumentChecklistProvider(new CompletenessScoringService()), new BasicSlaCalculator());
        var permissionService = new AllowAllPermissionService();
        var registry = new AgentToolRegistry(
        [
            new ClaimReadTool(deps.CaseRepository, permissionService),
            new DocumentReadTool(deps.DocumentRepository, permissionService),
            new PolicyReadTool(deps.PolicyRepository, permissionService),
            new KnowledgeSearchTool(new StaticRetrievalProvider(retrievalChunks), permissionService),
            new ClaimsAttentionReadTool(loader, permissionService),
        ]);

        return new ClaimsSummaryService(
            registry,
            chatCompletionProvider,
            deps.ActionLogRepository,
            deps.AuditRepository,
            new AgentRunRecorder(deps.AgentRunRepository));
    }

    private static Dependencies CreateDependencies() => new(
        new InMemoryCaseRepository(),
        new InMemoryCaseDocumentRepository(),
        new InMemoryPolicyRepository(),
        new InMemoryAgentActionLogRepository(),
        new InMemoryAuditRepository(),
        new InMemoryAgentRunRepository());

    private static async Task<ClaimCase> SeedCaseAsync(
        ICaseRepository repository,
        TenantScope scope,
        string customerName = "Amina Okello",
        string productLine = "Motor")
    {
        var claimCase = new ClaimCase
        {
            CaseId = Guid.NewGuid(),
            Scope = scope,
            CustomerName = customerName,
            PolicyNumber = "POL-1001",
            ProductLine = productLine,
            ClaimType = "Accident",
            Channel = "WebPortal",
            IncidentDate = new DateOnly(2026, 5, 24),
            IncidentDescription = "Minor collision reported at branch intake.",
            Status = CaseStatus.MissingDocuments,
            CompletenessScore = 40m,
            SlaDueAt = DateTimeOffset.UtcNow.AddDays(3),
            AssignedUserId = "user-claims-1",
            CreatedByUserId = "user-claims-1",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            CorrelationId = "corr-summary-seed",
        };
        await repository.SaveAsync(claimCase);
        return claimCase;
    }

    private static DefaultHttpContext ScopedContext(string? workspaceId = "workspace-a")
    {
        var context = new DefaultHttpContext { TraceIdentifier = "trace-summary-001" };
        context.Request.Headers["X-Tenant-Id"] = "tenant-a";
        context.Request.Headers["X-Workspace-Id"] = workspaceId;
        context.Request.Headers["X-User-Id"] = "user-claims-1";
        context.Request.Headers["x-demo-user-id"] = "user-claims-1";
        context.Request.Headers["x-demo-role"] = "ClaimsOfficer";
        context.Request.Headers["X-Correlation-ID"] = "corr-summary-endpoint";
        return context;
    }

    private static async Task<(int? StatusCode, T? Body)> ExecuteAsync<T>(IResult result)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.RequestServices = new Microsoft.Extensions.DependencyInjection.ServiceCollection()
            .AddLogging()
            .AddProblemDetails()
            .BuildServiceProvider();
        httpContext.Response.Body = new MemoryStream();

        await result.ExecuteAsync(httpContext);

        httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        var body = httpContext.Response.Body.Length == 0
            ? default
            : await System.Text.Json.JsonSerializer.DeserializeAsync<T>(httpContext.Response.Body, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));

        return (httpContext.Response.StatusCode, body);
    }

    private sealed record Dependencies(
        InMemoryCaseRepository CaseRepository,
        InMemoryCaseDocumentRepository DocumentRepository,
        InMemoryPolicyRepository PolicyRepository,
        InMemoryAgentActionLogRepository ActionLogRepository,
        InMemoryAuditRepository AuditRepository,
        InMemoryAgentRunRepository AgentRunRepository);

    private sealed class AllowAllPermissionService : IPermissionService
    {
        public Task<bool> HasPermissionAsync(Permission permission, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task RequirePermissionAsync(Permission permission, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class StaticRetrievalProvider(IReadOnlyCollection<KnowledgeChunk> chunks) : IRetrievalProvider
    {
        public Task<RetrievalResult> RetrieveAsync(AssistantQuestion question, CancellationToken cancellationToken = default)
        {
            var citations = chunks.Select(chunk => new SourceCitation(
                chunk.KnowledgeDocumentId, chunk.ChunkId, chunk.DocumentName, chunk.ProductLine, chunk.Version, chunk.Content, chunk.DocumentName)).ToArray();
            return Task.FromResult(new RetrievalResult(question.Question, question.ProductLine, chunks, citations));
        }
    }

    private sealed class CapturingChatCompletionProvider(string content) : IChatCompletionProvider
    {
        public string LastPrompt { get; private set; } = string.Empty;

        public Task<ChatCompletionResult> CompleteAsync(ChatCompletionRequest request, CancellationToken cancellationToken = default)
        {
            LastPrompt = request.Prompt;
            return Task.FromResult(new ChatCompletionResult(content, "AzureOpenAI", "chat-test", DateTimeOffset.UtcNow));
        }
    }

    private sealed class ThrowsIfCalledChatCompletionProvider : IChatCompletionProvider
    {
        public Task<ChatCompletionResult> CompleteAsync(ChatCompletionRequest request, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("The chat completion provider was invoked despite the agent being disabled.");
    }
}
