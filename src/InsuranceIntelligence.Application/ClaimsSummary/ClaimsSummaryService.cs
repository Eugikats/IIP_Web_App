using InsuranceIntelligence.Application.Agents;
using InsuranceIntelligence.Application.Agents.Tools;
using InsuranceIntelligence.Application.Ai;
using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Domain.Agents;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.CaseReview;
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
///
/// This is also the "Case Review" pipeline stage (Intake/FNOL -&gt; Document Checker -&gt; Case Review
/// -&gt; Approval): <see cref="RiskSignals"/> are the fraud/underwriting flags that stage produces,
/// drafted by the same model call, from the same governed tool reads, on the same "advisory only"
/// terms as the summary itself.
/// </summary>
public sealed record ClaimsSummaryResult(
    Guid CaseId,
    string Summary,
    IReadOnlyCollection<string> OpenQuestions,
    IReadOnlyCollection<CaseReviewSignal> RiskSignals,
    IReadOnlyCollection<SourceCitation> KnowledgeSources,
    IReadOnlyCollection<string> EvidenceRefs,
    decimal Confidence,
    Guid AgentActionLogId,
    Guid CaseReviewId,
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
    IAgentRunRecorder agentRunRecorder,
    ICaseReviewRepository caseReviewRepository) : IClaimsSummaryService
{
    private const string AgentKey = "claims-summary";
    private const string PromptVersion = "claims-summary-v2";

    // Machine-plumbing keys AzureDocumentIntelligenceProvider always includes alongside real
    // extracted content (see AzureDocumentIntelligenceProvider.BuildExtraction) -- useful for
    // storage/debugging, not for a human-readable case review, so excluded from the prompt.
    private static readonly HashSet<string> NonSubstantiveExtractedFieldKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "sourceFileName", "objectKey", "modelId", "contentType", "pageCount"
    };

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

        // Case Review consumes documents as-is: any document that has finished processing is fair
        // input, whether or not a human has approved its extraction yet (ExtractionApproved is not
        // a gate here). Only genuinely unprocessed/failed documents are called out as open questions.
        var fairInputDocuments = documents
            .Where(d => d.ProcessingStatus is DocumentProcessingStatus.Processed or DocumentProcessingStatus.ExtractionApproved)
            .ToArray();

        var openQuestions = documents.Count == 0
            ? new List<string> { "No documents have been uploaded for this claim yet." }
            : documents
                .Where(d => d.ProcessingStatus is DocumentProcessingStatus.Uploaded or DocumentProcessingStatus.Processing or DocumentProcessingStatus.Failed)
                .Select(d => $"{d.DocumentType} ({d.OriginalFileName}) has not finished processing yet ({d.ProcessingStatus}).")
                .ToList();

        var prompt = BuildPrompt(claimCase, fairInputDocuments, policy, retrieval, knowledgeQuery);
        var chatResult = await chatCompletionProvider.CompleteAsync(
            new ChatCompletionRequest(prompt, scope.TenantId, scope.WorkspaceId, correlationId, AgentKey),
            cancellationToken);

        var (summary, riskSignals) = ParseModelOutput(chatResult.Content);

        var evidenceRefs = new List<string> { $"case:{claimCase.CaseId}" };
        evidenceRefs.AddRange(fairInputDocuments.Select(d => $"document:{d.DocumentId}"));
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
            ConfidenceScore = AiConfidenceScore.Create(fairInputDocuments.Length > 0 && policy is not null ? 0.78m : 0.55m),
            RiskFlag = riskSignals.Count > 0 ? "needs_review" : openQuestions.Count > 0 ? "needs_review" : "none",
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

        var caseReview = new CaseReviewSummary
        {
            CaseReviewId = Guid.NewGuid(),
            Scope = scope,
            CaseId = caseId,
            Overview = summary,
            OpenQuestions = openQuestions,
            RiskSignals = riskSignals,
            EvidenceRefs = evidenceRefs,
            Confidence = actionLog.ConfidenceScore.Value,
            GeneratedByProvider = chatResult.Provider,
            GeneratedByModel = chatResult.Deployment,
            GeneratedByUserId = userId ?? "unknown",
            GeneratedAt = actionLog.CreatedAt,
            AgentActionLogId = actionLog.AgentActionLogId,
            CorrelationId = correlationId,
        };
        await caseReviewRepository.SaveAsync(caseReview, cancellationToken);
        await auditRepository.WriteAsync(AuditEvent.CaseReviewGenerated(caseReview, userId), cancellationToken);

        await agentRunRecorder.RecordAsync(
            scope, AgentKey, "claims_summary_request", [$"case:{caseId}"],
            AgentRunStatus.Completed, startedAt, correlationId, userId, caseId, errorCategory: null, toolTrace, cancellationToken);

        return new ClaimsSummaryResult(
            caseId,
            summary,
            openQuestions,
            riskSignals,
            citations,
            evidenceRefs,
            actionLog.ConfidenceScore.Value,
            actionLog.AgentActionLogId,
            caseReview.CaseReviewId,
            correlationId);
    }

    /// <summary>
    /// Splits the model's raw completion into the human-readable summary and any trailing
    /// "RISK_SIGNAL|Category|note" lines it was asked to append (see BuildPrompt). Never throws on
    /// a malformed or absent trailer -- a model that returns plain text with no risk lines simply
    /// yields an unmodified summary and an empty signal list.
    /// </summary>
    private static (string Summary, IReadOnlyCollection<CaseReviewSignal> Signals) ParseModelOutput(string content)
    {
        var signals = new List<CaseReviewSignal>();
        var summaryLines = new List<string>();

        foreach (var rawLine in content.Replace("\r\n", "\n").Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.StartsWith("RISK_SIGNAL|", StringComparison.OrdinalIgnoreCase))
            {
                var parts = line.Split('|', 3);
                if (parts.Length == 3
                    && Enum.TryParse<CaseReviewSignalCategory>(parts[1].Trim(), ignoreCase: true, out var category)
                    && !string.IsNullOrWhiteSpace(parts[2]))
                {
                    signals.Add(new CaseReviewSignal(category, parts[2].Trim()));
                }

                continue;
            }

            summaryLines.Add(rawLine);
        }

        return (string.Join('\n', summaryLines).Trim(), signals);
    }

    private static string BuildPrompt(
        ClaimCase claimCase,
        IReadOnlyCollection<CaseDocument> fairInputDocuments,
        Policy? policy,
        RetrievalResult? retrieval,
        string knowledgeQuery)
    {
        var documentLines = fairInputDocuments.Count == 0
            ? "No processed documents available yet."
            : string.Join('\n', fairInputDocuments.Select(DescribeDocument));

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
            "Refer to the claim only by the reference shown after CLAIM below. Never print internal identifiers.",
            "Write plain sentences. Do not use em dashes.",
            string.Empty,
            $"CLAIM: {ClaimReference.For(claimCase.CaseId)}",
            $"Customer: {claimCase.CustomerName}",
            $"Product line: {claimCase.ProductLine} | Claim type: {claimCase.ClaimType} | Status: {claimCase.Status}",
            $"Incident date: {claimCase.IncidentDate} | Description: {claimCase.IncidentDescription}",
            $"Completeness: {claimCase.CompletenessScore}%",
            string.Empty,
            "DOCUMENTS (extracted fields shown with the extraction confidence, where available):",
            documentLines,
            string.Empty,
            "POLICY:",
            policyLine,
            string.Empty,
            $"KNOWLEDGE ({knowledgeQuery}):",
            knowledgeLines,
            string.Empty,
            "After the summary, note anything in the material above that a human reviewer should check",
            "for possible fraud or underwriting concern -- inconsistent facts, mismatched dates, values",
            "that look unusual for this claim type, coverage questions, and similar. These are",
            "observations for a human to verify, never a fraud or underwriting determination -- do not",
            "use words like \"confirmed\", \"fraudulent\", or \"denied\". If you have none, add no lines.",
            "For each concern, add ONE line in exactly this format, after the summary, nothing else on the line:",
            "RISK_SIGNAL|Fraud|<one sentence, grounded only in the material above>",
            "RISK_SIGNAL|Underwriting|<one sentence, grounded only in the material above>",
        ]);
    }

    private static string DescribeDocument(CaseDocument document)
    {
        var fields = document.Extraction.Fields
            .Where(field => !NonSubstantiveExtractedFieldKeys.Contains(field.Key))
            .OrderByDescending(field => document.Extraction.FieldConfidences.TryGetValue(field.Key, out var confidence) ? confidence : 0m)
            .Take(8)
            .Select(field => document.Extraction.FieldConfidences.TryGetValue(field.Key, out var confidence)
                ? $"{field.Key}={field.Value} (confidence {confidence:0.00})"
                : $"{field.Key}={field.Value}")
            .ToArray();

        var fieldsLine = fields.Length == 0
            ? "no extracted fields"
            : string.Join(", ", fields);

        return $"- {document.DocumentType} ({document.OriginalFileName}), status: {document.ProcessingStatus}, "
            + $"classification confidence: {document.Classification.ConfidenceScore:0.00}, human-verified: {document.Extraction.Approved}, "
            + $"fields: {fieldsLine}";
    }
}
