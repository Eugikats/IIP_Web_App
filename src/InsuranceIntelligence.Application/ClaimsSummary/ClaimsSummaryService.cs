using InsuranceIntelligence.Application.Agents;
using InsuranceIntelligence.Application.Agents.Tools;
using InsuranceIntelligence.Application.Ai;
using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Domain.Agents;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Documents;
using InsuranceIntelligence.Domain.Knowledge;
using InsuranceIntelligence.Domain.Policies;

namespace InsuranceIntelligence.Application.ClaimsSummary;

/// <summary>
/// WO-10: closes the one real agent-catalog gap found during the build (docs §6.2 — Claims
/// Summary Agent had no implementation of any kind). Summarizes claim facts, timeline, documents,
/// and policy context from tools only — every statement traces to a retrieved source, never model
/// memory (AGENTS.md I6). Output is explicitly informational: "Required before decision use," not
/// a recommendation, and never itself a claim decision (I4).
/// </summary>
public sealed record ClaimsSummaryResult(
    Guid CaseId,
    string Summary,
    IReadOnlyCollection<string> OpenQuestions,
    IReadOnlyCollection<SourceCitation> KnowledgeSources,
    IReadOnlyCollection<string> EvidenceRefs,
    decimal Confidence,
    Guid AgentActionLogId,
    string CorrelationId);

public interface IClaimsSummaryService
{
    /// <summary>Caller must already have checked the claims-summary agent gate (matching the
    /// established pattern in AssistantEndpoints/CustomerUpdateEndpoints) and resolved/authorized
    /// the case before calling. Throws CaseNotFoundException if the case is not visible in scope.</summary>
    Task<ClaimsSummaryResult> SummarizeAsync(
        Guid caseId,
        TenantScope scope,
        string? userId,
        string correlationId,
        CancellationToken cancellationToken);
}

public sealed class CaseNotFoundException : Exception;

public sealed class ClaimsSummaryService(
    Agents.Tools.IAgentToolRegistry agentToolRegistry,
    IChatCompletionProvider chatCompletionProvider,
    IAgentActionLogRepository actionLogRepository,
    IAuditRepository auditRepository,
    IAgentRunRecorder agentRunRecorder) : IClaimsSummaryService
{
    private const string AgentKey = "claims-summary";
    private const string PromptVersion = "claims-summary-v1";

    public async Task<ClaimsSummaryResult> SummarizeAsync(
        Guid caseId,
        TenantScope scope,
        string? userId,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var startedAt = DateTimeOffset.UtcNow;
        var toolTrace = new List<AgentRunToolTraceEntry>();

        async Task<T?> CallToolAsync<T>(string toolName, IReadOnlyDictionary<string, string>? parameters) where T : class
        {
            var result = await agentToolRegistry.ExecuteAsync(
                toolName,
                new AgentToolInvocation(scope, AgentKey, userId, parameters),
                cancellationToken);
            toolTrace.Add(new AgentRunToolTraceEntry(toolName, DateTimeOffset.UtcNow, result.Success ? "ok" : "denied"));
            return result.Success ? result.Data as T : null;
        }

        var claimCase = await CallToolAsync<ClaimCase>("claim.read", new Dictionary<string, string> { ["caseId"] = caseId.ToString() });
        if (claimCase is null)
        {
            await agentRunRecorder.RecordAsync(
                scope, AgentKey, "claims_summary_request", [$"case:{caseId}"],
                AgentRunStatus.Failed, startedAt, correlationId, userId, caseId, "case_not_found", toolTrace, cancellationToken);
            throw new CaseNotFoundException();
        }

        var documents = await CallToolAsync<IReadOnlyCollection<CaseDocument>>(
            "document.read", new Dictionary<string, string> { ["caseId"] = caseId.ToString() })
            ?? Array.Empty<CaseDocument>();

        var policy = await CallToolAsync<Policy>(
            "policy.read", new Dictionary<string, string> { ["policyNumber"] = claimCase.PolicyNumber });

        var knowledgeQuery = $"Coverage and handling guidance for {claimCase.ProductLine} {claimCase.ClaimType} claims";
        var retrieval = await CallToolAsync<RetrievalResult>(
            "knowledge.search", new Dictionary<string, string> { ["query"] = knowledgeQuery, ["productLine"] = claimCase.ProductLine });

        var openQuestions = documents.Count == 0
            ? new List<string> { "No documents have been uploaded for this claim yet." }
            : documents
                .Where(d => d.ProcessingStatus != DocumentProcessingStatus.Processed || !d.Extraction.Approved)
                .Select(d => $"{d.DocumentType} ({d.OriginalFileName}) has not been reviewed and approved yet.")
                .ToList();

        var prompt = BuildPrompt(claimCase, documents, policy, retrieval, knowledgeQuery);
        var chatResult = await chatCompletionProvider.CompleteAsync(
            new ChatCompletionRequest(prompt, scope.TenantId, scope.WorkspaceId, correlationId),
            cancellationToken);

        var evidenceRefs = new List<string> { $"case:{claimCase.CaseId}" };
        evidenceRefs.AddRange(documents.Select(d => $"document:{d.DocumentId}"));
        if (policy is not null)
        {
            evidenceRefs.Add($"policy:{policy.PolicyId}");
        }

        var citations = retrieval?.Citations ?? Array.Empty<SourceCitation>();

        var actionLog = new AgentActionLog
        {
            AgentActionLogId = Guid.NewGuid(),
            Scope = scope,
            CaseId = caseId,
            AgentName = AgentKey,
            AssistantMode = AssistantMode.ClaimsSummary,
            InputSummary = $"Claims summary for case {caseId}",
            RetrievedSources = citations,
            ModelOutput = chatResult.Content,
            ConfidenceScore = AiConfidenceScore.Create(documents.Count > 0 && policy is not null ? 0.78m : 0.55m),
            RiskFlag = openQuestions.Count > 0 ? "needs_review" : "none",
            HumanOverride = false,
            CreatedAt = DateTimeOffset.UtcNow,
            CorrelationId = correlationId,
            UserId = userId ?? "unknown",
            ModelProvider = chatResult.Provider,
            ModelDeployment = chatResult.Deployment,
            PromptVersion = PromptVersion,
            Visibility = "Internal",
            CanTriggerDecision = false,
        };

        await actionLogRepository.SaveAsync(actionLog, cancellationToken);
        await auditRepository.WriteAsync(AuditEvent.CommandCenterAssistantAnswered(actionLog), cancellationToken);
        await agentRunRecorder.RecordAsync(
            scope, AgentKey, "claims_summary_request", [$"case:{caseId}"],
            AgentRunStatus.Completed, startedAt, correlationId, userId, caseId, errorCategory: null, toolTrace, cancellationToken);

        return new ClaimsSummaryResult(
            caseId,
            chatResult.Content,
            openQuestions,
            citations,
            evidenceRefs,
            actionLog.ConfidenceScore.Value,
            actionLog.AgentActionLogId,
            correlationId);
    }

    private static string BuildPrompt(
        ClaimCase claimCase,
        IReadOnlyCollection<CaseDocument> documents,
        Policy? policy,
        RetrievalResult? retrieval,
        string knowledgeQuery)
    {
        var documentLines = documents.Count == 0
            ? "No documents uploaded."
            : string.Join('\n', documents.Select(d => $"- {d.DocumentType} ({d.OriginalFileName}), status: {d.ProcessingStatus}, approved: {d.Extraction.Approved}"));

        var policyLine = policy is null
            ? "No matching policy record found in this workspace."
            : $"Policy {policy.PolicyNumber}, product line {policy.ProductLine}, cover type {policy.CoverType ?? "unspecified"}.";

        var knowledgeLines = retrieval is null || retrieval.Chunks.Count == 0
            ? "No approved knowledge-base guidance retrieved for this claim type."
            : string.Join("\n\n", retrieval.Chunks.Select((chunk, index) =>
                $"[{index + 1}] {chunk.DocumentName} (v{chunk.Version}):\n{chunk.Content}"));

        return string.Join('\n', [
            "You are an insurance claims-summary assistant for staff at a Ugandan composite insurer.",
            "Summarize this claim using ONLY the facts, documents, policy, and knowledge excerpts below.",
            "Never state a fact that is not present in the material below. If something is unknown, say so plainly.",
            "Do not approve, deny, settle, repudiate, or recommend a decision. This summary prepares a human reviewer; it is not itself a decision.",
            "Cite knowledge excerpts you use by their bracketed number, e.g. [1]. Be concise and factual.",
            string.Empty,
            $"CLAIM: {claimCase.CaseId}",
            $"Customer: {claimCase.CustomerName}",
            $"Product line: {claimCase.ProductLine} | Claim type: {claimCase.ClaimType} | Status: {claimCase.Status}",
            $"Incident date: {claimCase.IncidentDate} | Description: {claimCase.IncidentDescription}",
            $"Completeness: {claimCase.CompletenessScore}%",
            string.Empty,
            "DOCUMENTS:",
            documentLines,
            string.Empty,
            "POLICY:",
            policyLine,
            string.Empty,
            $"KNOWLEDGE ({knowledgeQuery}):",
            knowledgeLines,
        ]);
    }
}
