using System.Text;
using InsuranceIntelligence.Application.Agents;
using InsuranceIntelligence.Application.Ai;
using InsuranceIntelligence.Application.Claims;
using InsuranceIntelligence.Domain.Agents;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Documents;
using InsuranceIntelligence.Domain.Fraud;
using InsuranceIntelligence.Domain.Knowledge;
using InsuranceIntelligence.Domain.Policies;

namespace InsuranceIntelligence.Application.Fraud;

public sealed record FraudAgentFactDto(string Id, string Text);

public sealed record FraudAgentFindingDto(
    string IndicatorCode,
    string IndicatorLabel,
    string Line,
    string Title,
    string Observation,
    string WhatToCheck,
    string Severity,
    string SeverityLabel,
    IReadOnlyList<FraudAgentFactDto> Facts);

public sealed record FraudAgentReviewDto(
    Guid CaseId,
    string ClaimReference,
    bool AgentEnabled,
    bool Ran,
    bool Stale,
    IReadOnlyList<FraudAgentFindingDto> Findings,
    int LeftOut,
    DateTimeOffset? GeneratedAt,
    string? GeneratedBy,
    string? Provider,
    bool IsMock,
    string PlaybookVersion,
    string Notice);

public enum FraudAgentOutcome
{
    Done,
    NotFound,
    Disabled,
    Unavailable,
}

public sealed record FraudAgentRunResult(FraudAgentOutcome Outcome, string? Message = null, FraudAgentReviewDto? Review = null);

public interface IFraudAgentService
{
    /// <summary>The latest review of the claim, and whether the claim has changed since it was written. Reads only; never calls the model.</summary>
    Task<FraudAgentReviewDto?> GetAsync(TenantScope scope, Guid caseId, CancellationToken cancellationToken = default);

    /// <summary>The latest review, only when the claim has not changed since it was written. Used by the review score.</summary>
    Task<FraudAgentReview?> GetCurrentAsync(TenantScope scope, Guid caseId, CancellationToken cancellationToken = default);

    /// <summary>Has the fraud agent read the claim against the playbook and keep what it found. Audited. Advisory only.</summary>
    Task<FraudAgentRunResult> RunAsync(TenantScope scope, Guid caseId, string actorId, string correlationId, CancellationToken cancellationToken = default);
}

/// <summary>
/// The fraud agent. It reads one claim, with what the platform already knows about it (the claim's
/// own details, its documents, the policy, the fixed rules that fired, the picture checks), against a
/// written playbook of what insurance fraud looks like in Uganda, and writes findings that each cite
/// the facts they rest on. This is what makes fraud review more than a fixed list of checks: the fixed
/// rules still run and are shown to it as facts, and the agent can notice what they cannot, such as a
/// repair estimate that does not fit the photos or a story that changes between documents.
///
/// It never accuses, decides or blocks anything. A finding that cites no real fact, or that uses
/// decision or accusation wording, is dropped before it is kept. When the agent is switched off or the
/// model is unavailable, the fixed rules and the review score carry on as before.
/// </summary>
public sealed class FraudAgentService(
    ICaseRepository caseRepository,
    ICaseDocumentRepository documentRepository,
    IClaimsReadRepository claimsReadRepository,
    IPolicyRepository policyRepository,
    IAuthenticityReportRepository authenticityRepository,
    IFraudAgentReviewRepository reviewRepository,
    IAgentGate agentGate,
    IChatCompletionProvider chatCompletionProvider,
    IAgentRunRecorder agentRunRecorder,
    IAgentActionLogRepository actionLogRepository,
    IAuditRepository auditRepository) : IFraudAgentService
{
    public const string AgentKey = "risk-fraud-signal";
    public const string TaskMarker = "TASK: fraud-agent-review";
    private const string PromptVersion = "fraud-agent-review-v1";
    private const int AnalysisWindow = 500;
    private const int MaxDocuments = 12;
    private const int MaxDetailFacts = 15;
    private const int MaxDescription = 900;

    private sealed record Gathered(ClaimCase Claim, IReadOnlyList<FraudAgentFact> Facts, string FactsHash);

    public async Task<FraudAgentReviewDto?> GetAsync(TenantScope scope, Guid caseId, CancellationToken cancellationToken = default)
    {
        var gathered = await GatherAsync(scope, caseId, cancellationToken);
        if (gathered is null)
        {
            return null;
        }

        var enabled = (await agentGate.CheckAsync(scope, AgentKey, cancellationToken)).Enabled;
        var stored = await reviewRepository.GetLatestAsync(scope, caseId, cancellationToken);
        return ToDto(gathered.Claim, stored, enabled, stale: stored is not null && stored.FactsHash != gathered.FactsHash);
    }

    public async Task<FraudAgentReview?> GetCurrentAsync(TenantScope scope, Guid caseId, CancellationToken cancellationToken = default)
    {
        var stored = await reviewRepository.GetLatestAsync(scope, caseId, cancellationToken);
        if (stored is null)
        {
            return null;
        }

        var gathered = await GatherAsync(scope, caseId, cancellationToken);
        return gathered is not null && gathered.FactsHash == stored.FactsHash ? stored : null;
    }

    public async Task<FraudAgentRunResult> RunAsync(TenantScope scope, Guid caseId, string actorId, string correlationId, CancellationToken cancellationToken = default)
    {
        var gathered = await GatherAsync(scope, caseId, cancellationToken);
        if (gathered is null)
        {
            return new FraudAgentRunResult(FraudAgentOutcome.NotFound, "Claim was not found.");
        }

        var gate = await agentGate.CheckAsync(scope, AgentKey, cancellationToken);
        if (!gate.Enabled)
        {
            return new FraudAgentRunResult(FraudAgentOutcome.Disabled, gate.RefusalMessage);
        }

        var startedAt = DateTimeOffset.UtcNow;
        try
        {
            var prompt = BuildPrompt(gathered);
            var result = await chatCompletionProvider.CompleteAsync(
                new ChatCompletionRequest(prompt, scope.TenantId, scope.WorkspaceId, correlationId, AgentKey), cancellationToken);
            var validIds = gathered.Facts.Select(fact => fact.Id).ToHashSet(StringComparer.Ordinal);
            var parsed = FraudAgentOutput.Parse(result.Content, validIds);

            await actionLogRepository.SaveAsync(new AgentActionLog
            {
                AgentActionLogId = Guid.NewGuid(),
                Scope = scope,
                CaseId = caseId,
                AgentName = AgentKey,
                AssistantMode = AssistantMode.ClaimsSummary,
                InputSummary = $"Fraud review for case {caseId} against {FraudPlaybook.Version}",
                RetrievedSources = [],
                ModelOutput = parsed.Readable
                    ? $"{parsed.Findings.Count} finding(s) kept, {parsed.LeftOut} left out."
                    : "(not kept: the reply could not be read)",
                ConfidenceScore = AiConfidenceScore.Create(0.55m),
                RiskFlag = parsed.Findings.Count > 0 ? "needs_review" : "none",
                HumanOverride = false,
                CreatedAt = DateTimeOffset.UtcNow,
                CorrelationId = correlationId,
                UserId = actorId,
                ModelProvider = result.Provider,
                ModelDeployment = result.Deployment,
                PromptVersion = PromptVersion,
                Visibility = "Internal",
                CanTriggerDecision = false,
            }, cancellationToken);

            if (!parsed.Readable)
            {
                await agentRunRecorder.RecordAsync(
                    scope, AgentKey, "fraud_agent_review", [$"case:{caseId}"],
                    AgentRunStatus.Failed, startedAt, correlationId, actorId, caseId, "reply_not_readable", null, cancellationToken);
                return new FraudAgentRunResult(FraudAgentOutcome.Unavailable, "The fraud agent answered in a form that could not be read, so nothing was kept. Try again.");
            }

            var review = new FraudAgentReview
            {
                ReviewId = Guid.NewGuid(),
                Scope = scope,
                CaseId = caseId,
                Findings = parsed.Findings,
                Facts = gathered.Facts,
                FactsHash = gathered.FactsHash,
                PlaybookVersion = FraudPlaybook.Version,
                Provider = result.Provider,
                Deployment = result.Deployment,
                GeneratedAt = DateTimeOffset.UtcNow,
                GeneratedBy = actorId,
                CorrelationId = correlationId,
                LeftOut = parsed.LeftOut,
            };
            await reviewRepository.SaveAsync(review, cancellationToken);
            await auditRepository.WriteAsync(
                AuditEvent.FraudAgentReviewPrepared(scope, caseId, actorId, review.Findings.Count, review.LeftOut, review.Provider, correlationId),
                cancellationToken);
            await agentRunRecorder.RecordAsync(
                scope, AgentKey, "fraud_agent_review", [$"case:{caseId}"],
                AgentRunStatus.Completed, startedAt, correlationId, actorId, caseId, null, null, cancellationToken);
            return new FraudAgentRunResult(FraudAgentOutcome.Done, null, ToDto(gathered.Claim, review, agentEnabled: true, stale: false));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            // The model being unavailable never takes the fixed rules or the review score with it.
            await agentRunRecorder.RecordAsync(
                scope, AgentKey, "fraud_agent_review", [$"case:{caseId}"],
                AgentRunStatus.Failed, startedAt, correlationId, actorId, caseId, "model_unavailable", null, cancellationToken);
            return new FraudAgentRunResult(FraudAgentOutcome.Unavailable, "The fraud agent could not run just now. The fixed rules and the review score are unaffected. Try again shortly.");
        }
    }

    private async Task<Gathered?> GatherAsync(TenantScope scope, Guid caseId, CancellationToken cancellationToken)
    {
        var claim = await caseRepository.GetAsync(caseId, cancellationToken);
        if (claim is null || claim.Scope != scope)
        {
            return null;
        }

        var facts = new List<FraudAgentFact>();
        void Add(string text) => facts.Add(new FraudAgentFact($"F{facts.Count + 1}", text));

        var reference = ClaimReference.For(claim.CaseId);
        Add($"Claim {reference}: product line {claim.ProductLine}, claim type {claim.ClaimType}, reported through {claim.Channel}, status {claim.Status}.");
        var recorded = DateOnly.FromDateTime(claim.CreatedAt.UtcDateTime);
        var reportingDelay = recorded.DayNumber - claim.IncidentDate.DayNumber;
        Add($"Incident date {claim.IncidentDate:yyyy-MM-dd}; the claim was recorded on {recorded:yyyy-MM-dd}, {reportingDelay} day(s) after the incident.");
        var description = claim.IncidentDescription?.Trim() ?? string.Empty;
        Add($"Incident as described: {(description.Length > MaxDescription ? description[..MaxDescription] + "..." : description)}");
        if (claim.ClaimedAmount is { } amount)
        {
            Add($"Amount claimed: {amount:N0}.");
        }

        Add($"Documentation completeness: {claim.CompletenessScore:0}%.");

        foreach (var pair in Domain.Products.ClaimFieldValues.For(claim).OrderBy(pair => pair.Key, StringComparer.Ordinal).Take(MaxDetailFacts))
        {
            Add($"Detail given: {Domain.Products.ClaimFieldValues.LabelFor(pair.Key)} is {pair.Value}.");
        }

        Policy? policy = string.IsNullOrWhiteSpace(claim.PolicyNumber)
            ? null
            : await policyRepository.GetByPolicyNumberAsync(claim.PolicyNumber.Trim(), scope, cancellationToken);
        if (policy is null)
        {
            Add("No policy record in this workspace matches the policy number on the claim.");
        }
        else
        {
            var inception = policy.EffectiveFrom is { } from
                ? $"in force from {from:yyyy-MM-dd}, so the incident was {claim.IncidentDate.DayNumber - from.DayNumber} day(s) after the cover began"
                : "with no start date recorded";
            var sum = policy.SumInsured is { } insured ? $", sum insured {insured:N0}" : string.Empty;
            Add($"Policy: product line {policy.ProductLine}, cover type {policy.CoverType ?? "unspecified"}, {inception}{sum}.");
        }

        foreach (var note in claim.AdvisoryRiskSignals)
        {
            Add($"Noted when the claim was opened: {note}");
        }

        var documents = (await documentRepository.ListForCaseAsync(caseId, scope, cancellationToken)).Where(document => !document.IsSuperseded).ToArray();
        foreach (var document in documents.Take(MaxDocuments))
        {
            Add(DescribeDocument(document));
        }

        if (documents.Length == 0)
        {
            Add("No documents are on the claim yet.");
        }

        // The fixed rules that fired for this claim, so the agent builds on them and does not repeat them as news.
        var claims = (await claimsReadRepository.ListAsync(new ClaimsQuery(Take: AnalysisWindow), cancellationToken)).ToList();
        if (claims.All(other => other.CaseId != caseId))
        {
            claims.Add(claim);
        }

        var policiesByNumber = (await policyRepository.ListAsync(scope, cancellationToken))
            .Where(item => !string.IsNullOrWhiteSpace(item.PolicyNumber))
            .GroupBy(item => item.PolicyNumber.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var inputs = claims
            .Select(other => FraudQueryService.ToFraudInput(
                other,
                string.IsNullOrWhiteSpace(other.PolicyNumber) ? null : policiesByNumber.GetValueOrDefault(other.PolicyNumber.Trim())))
            .ToList();
        foreach (var signal in FraudRuleEngine.Evaluate(inputs).Where(signal => signal.RelatedCaseIds.Contains(caseId)))
        {
            Add($"Fixed rule {signal.RuleId} fired: {signal.Title}. {signal.Description}");
        }

        var documentsById = documents.ToDictionary(document => document.DocumentId);
        foreach (var report in await authenticityRepository.ListForCaseAsync(scope, caseId, cancellationToken))
        {
            if (!documentsById.TryGetValue(report.DocumentId, out var document))
            {
                continue;
            }

            foreach (var finding in report.Findings.Where(finding => finding.Severity > AuthenticitySeverity.Info))
            {
                Add($"Picture check on {document.DisplayName ?? document.OriginalFileName} ({finding.Severity}): {finding.Reason}");
            }
        }

        // Claim references, never internal ids, are all a person or a model should see.
        var readable = facts.Select(fact => fact with { Text = ReadableIds(fact.Text) }).ToArray();
        return new Gathered(claim, readable, FraudAgentReview.HashFacts(readable));
    }

    private static readonly System.Text.RegularExpressions.Regex GuidPattern =
        new("[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}", System.Text.RegularExpressions.RegexOptions.Compiled);

    private static string ReadableIds(string text) =>
        GuidPattern.Replace(text, match => Guid.TryParse(match.Value, out var id) ? ClaimReference.For(id) : match.Value);

    private static readonly HashSet<string> NonSubstantiveExtractedFieldKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "sourceFileName", "objectKey", "modelId", "contentType", "pageCount",
    };

    private static string DescribeDocument(CaseDocument document)
    {
        var fields = document.Extraction.Fields
            .Where(field => !NonSubstantiveExtractedFieldKeys.Contains(field.Key))
            .OrderBy(field => field.Key, StringComparer.Ordinal)
            .Take(8)
            .Select(field => document.Extraction.FieldConfidences.TryGetValue(field.Key, out var confidence)
                ? $"{field.Key}={field.Value} (confidence {confidence:0.00})"
                : $"{field.Key}={field.Value}")
            .ToArray();
        var fieldsLine = fields.Length == 0 ? "no fields were read" : string.Join(", ", fields);
        return $"Document {document.DisplayName ?? document.OriginalFileName} ({document.DocumentType}), status {document.ProcessingStatus}; {fieldsLine}.";
    }

    private static string BuildPrompt(Gathered gathered)
    {
        var builder = new StringBuilder();
        builder.AppendLine(TaskMarker);
        builder.AppendLine("You are the fraud review agent for staff at a Ugandan insurer. A person will read what you write before anything else happens.");
        builder.AppendLine("Read the numbered FACTS about one claim against the PLAYBOOK, and list what a person should look at more closely.");
        builder.AppendLine();
        builder.AppendLine("RULES:");
        builder.AppendLine("- Use ONLY the facts. Never invent a fact, a date, a name or a number. If the facts do not point to a concern, return an empty list. Returning nothing is a good answer.");
        builder.AppendLine("- Every finding must cite the fact numbers it rests on, for example [\"F2\",\"F5\"]. A finding with no real fact number is thrown away.");
        builder.AppendLine("- An indicator is a reason to look, not proof. Weigh the ordinary explanation given in the playbook before you raise one, and say so in the observation when the ordinary explanation is likely.");
        builder.AppendLine("- Do not say anyone committed fraud, lied or is guilty. Do not approve, reject, deny, settle or pay. Describe what the facts show and what a person should check.");
        builder.AppendLine("- Do not repeat a fixed rule as if it were new. Use it as a fact and add what it does not say.");
        builder.AppendLine("- A single fact supports at most severity \"potential\". Use \"investigate\" only when two or more different facts point the same way.");
        builder.AppendLine("- Plain sentences. Do not use em dashes. At most six findings, strongest first.");
        builder.AppendLine();
        builder.AppendLine(FraudPlaybook.LocalContext);
        builder.AppendLine();
        builder.AppendLine(FraudPlaybook.RenderForPrompt());
        builder.AppendLine();
        builder.AppendLine("FACTS:");
        foreach (var fact in gathered.Facts)
        {
            builder.Append(fact.Id).Append(": ").AppendLine(fact.Text);
        }

        builder.AppendLine();
        builder.AppendLine("REPLY with JSON only, in exactly this shape and nothing else:");
        builder.AppendLine("{\"findings\":[{\"indicator\":\"<code from the playbook or OTHER>\",\"title\":\"<short title>\",\"observation\":\"<what the facts show, one or two sentences>\",\"check\":\"<what a person should verify>\",\"severity\":\"advisory|potential|investigate\",\"facts\":[\"F1\"]}]}");
        return builder.ToString();
    }

    private static FraudAgentReviewDto ToDto(ClaimCase claim, FraudAgentReview? review, bool agentEnabled, bool stale)
    {
        var factsById = review?.Facts.ToDictionary(fact => fact.Id, StringComparer.Ordinal) ?? new Dictionary<string, FraudAgentFact>();
        var findings = (review?.Findings ?? []).Select(finding =>
        {
            var indicator = FraudPlaybook.Find(finding.IndicatorCode);
            return new FraudAgentFindingDto(
                finding.IndicatorCode,
                indicator?.Label ?? "Other concern",
                indicator?.Line ?? "Any",
                finding.Title,
                finding.Observation,
                finding.WhatToCheck,
                finding.Severity.ToString(),
                finding.Severity switch
                {
                    FraudSignalSeverity.RequiresInvestigation => "Worth investigating",
                    FraudSignalSeverity.PotentialAnomaly => "Worth a closer look",
                    _ => "Worth noting",
                },
                finding.FactIds
                    .Where(factsById.ContainsKey)
                    .Select(id => new FraudAgentFactDto(id, factsById[id].Text))
                    .ToArray());
        }).ToArray();

        return new FraudAgentReviewDto(
            claim.CaseId,
            ClaimReference.For(claim.CaseId),
            agentEnabled,
            Ran: review is not null,
            stale,
            findings,
            review?.LeftOut ?? 0,
            review?.GeneratedAt,
            review?.GeneratedBy,
            review?.Provider,
            string.Equals(review?.Provider, "Mock", StringComparison.OrdinalIgnoreCase),
            review?.PlaybookVersion ?? FraudPlaybook.Version,
            "Prepared by the fraud agent to help a person. These are things worth checking, not findings that anyone has done wrong. A person decides what happens next.");
    }
}
