using InsuranceIntelligence.Application.Agents;
using InsuranceIntelligence.Application.Ai;
using InsuranceIntelligence.Application.Claims;
using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Domain.Agents;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.CaseReview;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Documents;
using InsuranceIntelligence.Domain.Fraud;
using InsuranceIntelligence.Domain.Knowledge;
using InsuranceIntelligence.Domain.Policies;

namespace InsuranceIntelligence.Application.Fraud;

public sealed record AssessmentSourceDto(string Kind, string Id, string Label);

public sealed record RiskFactorDto(
    string Code,
    string Source,
    string SourceLabel,
    string Strength,
    string Reason,
    AssessmentSourceDto Reference,
    string Reading,
    string ReadingLabel,
    bool Counts);

public sealed record RiskScoreDto(int Value, string Band, string BandLabel, IReadOnlyList<RiskFactorDto> Factors);

public sealed record RecommendationReasonDto(string Text, AssessmentSourceDto Source);

public sealed record RecommendationDto(
    string Kind,
    string Label,
    string Headline,
    IReadOnlyList<RecommendationReasonDto> Reasons,
    IReadOnlyList<string> SuggestedRequests);

public sealed record AssessmentResponseDto(string Kind, string? Note, string? ByUserId, DateTimeOffset? At);

public sealed record ClaimAssessmentDto(
    Guid CaseId,
    string ClaimReference,
    RiskScoreDto Score,
    RecommendationDto Recommendation,
    string? Narrative,
    string? NarrativeProvider,
    bool NarrativeIsMock,
    bool Prepared,
    bool Stale,
    DateTimeOffset? GeneratedAt,
    string? GeneratedBy,
    AssessmentResponseDto? Response,
    string Notice);

public enum AssessmentOutcome
{
    Done,
    NotFound,
    Invalid,
}

public sealed record AssessmentResult(AssessmentOutcome Outcome, string? Message = null, ClaimAssessmentDto? Assessment = null);

public interface IClaimAssessmentService
{
    /// <summary>The claim's review score and recommendation as they stand now, in the caller's workspace.
    /// The score and recommendation are worked out from the claim as it is at this moment; the written
    /// explanation is the one prepared for exactly these facts, if there is one.</summary>
    Task<ClaimAssessmentDto?> GetAsync(TenantScope scope, Guid caseId, CancellationToken cancellationToken = default);

    /// <summary>Works the assessment out and keeps it, with a written explanation when the claim-recommendation agent is on.</summary>
    Task<ClaimAssessmentDto?> PrepareAsync(TenantScope scope, Guid caseId, string actorId, string correlationId, CancellationToken cancellationToken = default);

    /// <summary>A person's response to the recommendation: agree, or disagree with a reason. Audited.</summary>
    Task<AssessmentResult> RespondAsync(TenantScope scope, Guid caseId, string? response, string? note, string actorId, string correlationId, CancellationToken cancellationToken = default);
}

/// <summary>
/// The review score and the recommendation for a claim (build-loop item 18). The score combines the
/// fixed fraud rules, the authenticity checks on the claim's pictures and what a model noticed while
/// summarising the claim; every reason is listed with where it came from and what a person has done
/// with it, and a reason a person set aside no longer counts. The recommendation is worked out from
/// the same facts plus the claim's missing and unreadable documents, and every reason in it names its
/// source. A model may add a short written explanation, which is kept only if it uses no decision or
/// accusation wording. Nothing here changes a claim or decides anything: a person agrees or disagrees,
/// and that response is recorded.
/// </summary>
public sealed class ClaimAssessmentService(
    ICaseRepository caseRepository,
    ICaseDocumentRepository documentRepository,
    IClaimsReadRepository claimsReadRepository,
    IPolicyRepository policyRepository,
    IFraudSignalDispositionRepository fraudDispositionRepository,
    IAuthenticityReportRepository authenticityRepository,
    ICaseReviewRepository caseReviewRepository,
    IFieldProposalRepository proposalRepository,
    IDocumentChecklistProvider checklistProvider,
    IClaimAssessmentRepository assessmentRepository,
    IAgentGate agentGate,
    IChatCompletionProvider chatCompletionProvider,
    IAgentRunRecorder agentRunRecorder,
    IAgentActionLogRepository actionLogRepository,
    IAuditRepository auditRepository) : IClaimAssessmentService
{
    public const string AgentKey = "claim-recommendation";
    public const string NarrativeMarker = "TASK: claim-recommendation-narrative";
    private const string PromptVersion = "claim-recommendation-v1";
    private const int AnalysisWindow = 500;

    /// <summary>A claim with few documents is not a reason to suspect it. The fixed rule that notes it stays
    /// on the fraud signals page, but here missing documents belong to the recommendation ("ask for more"),
    /// not to the score.</summary>
    private const string LowCompletenessRuleId = "R-LOW-COMPLETENESS";

    private static readonly System.Text.RegularExpressions.Regex GuidPattern =
        new("[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}", System.Text.RegularExpressions.RegexOptions.Compiled);

    /// <summary>A person reads claim references, never internal identifiers.</summary>
    private static string ReadableIds(string text) =>
        GuidPattern.Replace(text, match => Guid.TryParse(match.Value, out var id) ? ClaimReference.For(id) : match.Value);

    private sealed record Gathered(ClaimCase Claim, RiskScore Score, ClaimRecommendation Recommendation, string InputsHash);

    public async Task<ClaimAssessmentDto?> GetAsync(TenantScope scope, Guid caseId, CancellationToken cancellationToken = default)
    {
        var gathered = await GatherAsync(scope, caseId, cancellationToken);
        if (gathered is null)
        {
            return null;
        }

        var stored = await assessmentRepository.GetLatestAsync(scope, caseId, cancellationToken);
        if (stored is not null && stored.InputsHash == gathered.InputsHash)
        {
            return ToDto(gathered.Claim, stored.Score, stored.Recommendation, stored, prepared: true, stale: false);
        }

        // Nothing prepared for these facts: the score and recommendation are still shown, since they are
        // worked out on the spot, but there is no written explanation and no response yet.
        return ToDto(gathered.Claim, gathered.Score, gathered.Recommendation, null, prepared: false, stale: stored is not null);
    }

    public async Task<ClaimAssessmentDto?> PrepareAsync(TenantScope scope, Guid caseId, string actorId, string correlationId, CancellationToken cancellationToken = default)
    {
        var gathered = await GatherAsync(scope, caseId, cancellationToken);
        if (gathered is null)
        {
            return null;
        }

        var existing = await assessmentRepository.GetLatestAsync(scope, caseId, cancellationToken);
        if (existing is not null && existing.InputsHash == gathered.InputsHash && existing.Narrative is not null)
        {
            return ToDto(gathered.Claim, existing.Score, existing.Recommendation, existing, prepared: true, stale: false);
        }

        var (narrative, provider) = await WriteNarrativeAsync(scope, gathered, actorId, correlationId, cancellationToken);
        var assessment = new ClaimAssessment
        {
            AssessmentId = Guid.NewGuid(),
            Scope = scope,
            CaseId = caseId,
            Score = gathered.Score,
            Recommendation = gathered.Recommendation,
            Narrative = narrative,
            NarrativeProvider = provider,
            InputsHash = gathered.InputsHash,
            GeneratedAt = DateTimeOffset.UtcNow,
            GeneratedBy = actorId,
            CorrelationId = correlationId,
            // A response belongs to the facts it was given on; the same facts keep it.
            Response = existing is not null && existing.InputsHash == gathered.InputsHash ? existing.Response : null,
            ResponseNote = existing is not null && existing.InputsHash == gathered.InputsHash ? existing.ResponseNote : null,
            ResponseByUserId = existing is not null && existing.InputsHash == gathered.InputsHash ? existing.ResponseByUserId : null,
            ResponseAt = existing is not null && existing.InputsHash == gathered.InputsHash ? existing.ResponseAt : null,
        };
        await assessmentRepository.SaveAsync(assessment, cancellationToken);
        await auditRepository.WriteAsync(
            AuditEvent.ClaimAssessmentPrepared(scope, caseId, actorId, gathered.Recommendation.Kind.ToString(), gathered.Score.Value, correlationId),
            cancellationToken);
        return ToDto(gathered.Claim, assessment.Score, assessment.Recommendation, assessment, prepared: true, stale: false);
    }

    public async Task<AssessmentResult> RespondAsync(TenantScope scope, Guid caseId, string? response, string? note, string actorId, string correlationId, CancellationToken cancellationToken = default)
    {
        if (!Enum.TryParse<AssessmentResponseKind>(response?.Trim(), ignoreCase: true, out var kind) || !Enum.IsDefined(kind))
        {
            return new AssessmentResult(AssessmentOutcome.Invalid, "Choose Agree or Disagree.");
        }

        var trimmed = note?.Trim();
        if (kind == AssessmentResponseKind.Disagree && string.IsNullOrWhiteSpace(trimmed))
        {
            return new AssessmentResult(AssessmentOutcome.Invalid, "Say in a sentence why you disagree, so the reason is on record.");
        }

        if (trimmed is { Length: > 1000 })
        {
            return new AssessmentResult(AssessmentOutcome.Invalid, "The note can be at most 1,000 characters.");
        }

        var gathered = await GatherAsync(scope, caseId, cancellationToken);
        if (gathered is null)
        {
            return new AssessmentResult(AssessmentOutcome.NotFound, "Claim was not found.");
        }

        var stored = await assessmentRepository.GetLatestAsync(scope, caseId, cancellationToken);
        if (stored is null || stored.InputsHash != gathered.InputsHash)
        {
            // Respond to what is on screen: keep these facts as the assessment before recording the response.
            await PrepareAsync(scope, caseId, actorId, correlationId, cancellationToken);
            stored = await assessmentRepository.GetLatestAsync(scope, caseId, cancellationToken);
        }

        if (stored is null)
        {
            return new AssessmentResult(AssessmentOutcome.NotFound, "Claim was not found.");
        }

        var updated = stored with
        {
            Response = kind,
            ResponseNote = string.IsNullOrWhiteSpace(trimmed) ? null : trimmed,
            ResponseByUserId = actorId,
            ResponseAt = DateTimeOffset.UtcNow,
        };
        await assessmentRepository.SaveAsync(updated, cancellationToken);
        await auditRepository.WriteAsync(
            AuditEvent.ClaimAssessmentResponse(scope, caseId, actorId, kind.ToString(), updated.Recommendation.Kind.ToString(), correlationId),
            cancellationToken);
        return new AssessmentResult(AssessmentOutcome.Done, null, ToDto(gathered.Claim, updated.Score, updated.Recommendation, updated, prepared: true, stale: false));
    }

    private async Task<Gathered?> GatherAsync(TenantScope scope, Guid caseId, CancellationToken cancellationToken)
    {
        var claim = await caseRepository.GetAsync(caseId, cancellationToken);
        if (claim is null || claim.Scope != scope)
        {
            return null;
        }

        var documents = (await documentRepository.ListForCaseAsync(caseId, scope, cancellationToken)).Where(document => !document.IsSuperseded).ToArray();
        var documentsById = documents.ToDictionary(document => document.DocumentId);
        var factors = new List<RiskFactor>();

        // Fixed rules over the workspace's claims, kept to the ones this claim is part of.
        var claims = (await claimsReadRepository.ListAsync(new ClaimsQuery(Take: AnalysisWindow), cancellationToken)).ToList();
        if (claims.All(other => other.CaseId != caseId))
        {
            claims.Add(claim);
        }

        var policiesByNumber = (await policyRepository.ListAsync(scope, cancellationToken))
            .Where(policy => !string.IsNullOrWhiteSpace(policy.PolicyNumber))
            .GroupBy(policy => policy.PolicyNumber.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var inputs = claims
            .Select(other => FraudQueryService.ToFraudInput(
                other,
                string.IsNullOrWhiteSpace(other.PolicyNumber) ? null : policiesByNumber.GetValueOrDefault(other.PolicyNumber.Trim())))
            .ToList();
        var dispositions = (await fraudDispositionRepository.ListAsync(scope, cancellationToken)).ToDictionary(disposition => disposition.SignalId);
        foreach (var signal in FraudRuleEngine.Evaluate(inputs).Where(signal => signal.RelatedCaseIds.Contains(caseId) && signal.RuleId != LowCompletenessRuleId))
        {
            var signalId = FraudSignalDisposition.DeterministicId(signal.RuleId, signal.RelatedCaseIds);
            var disposition = dispositions.GetValueOrDefault(signalId);
            factors.Add(new RiskFactor(
                $"rule:{signal.RuleId}",
                RiskFactorSource.RuleEngine,
                ClaimRiskScorer.WeightFor(signal.Severity),
                ReadableIds($"{signal.Title}. {signal.Description}"),
                new AssessmentSource(SourceKind.Signal, signalId.ToString(), signal.Title),
                ClaimRiskScorer.ReadingFor(disposition),
                disposition?.OutcomeNotes));
        }

        // The authenticity check of each picture still on the claim.
        foreach (var report in await authenticityRepository.ListForCaseAsync(scope, caseId, cancellationToken))
        {
            if (!documentsById.TryGetValue(report.DocumentId, out var document))
            {
                continue;
            }

            foreach (var finding in report.Findings.Where(finding => finding.Severity > AuthenticitySeverity.Info))
            {
                factors.Add(new RiskFactor(
                    $"picture:{report.DocumentId:N}:{finding.Code}",
                    RiskFactorSource.PictureCheck,
                    ClaimRiskScorer.WeightFor(finding.Severity),
                    $"{document.DisplayName ?? document.OriginalFileName}: {finding.Reason}",
                    new AssessmentSource(SourceKind.Picture, report.DocumentId.ToString(), document.DisplayName ?? document.OriginalFileName),
                    ClaimRiskScorer.ReadingFor(report.Disposition),
                    report.DispositionNote));
            }
        }

        // What the model noticed while summarising the claim. Fraud notes only; underwriting notes are not about this.
        var review = await caseReviewRepository.GetLatestForCaseAsync(caseId, scope, cancellationToken);
        if (review is not null)
        {
            var index = 0;
            foreach (var note in review.RiskSignals.Where(signal => signal.Category == CaseReviewSignalCategory.Fraud))
            {
                factors.Add(new RiskFactor(
                    $"model:{++index}",
                    RiskFactorSource.ModelReading,
                    ClaimRiskScorer.ModelFactorWeight,
                    note.Note,
                    new AssessmentSource(SourceKind.ModelReading, review.CaseReviewId.ToString(), "The claim summary"),
                    HumanReading.NotLookedAt));
            }
        }

        var score = ClaimRiskScorer.Score(factors);

        var checklist = await checklistProvider.BuildChecklistAsync(claim, documents, cancellationToken);
        var unreadable = documents
            .Where(document => document.ProcessingStatus == DocumentProcessingStatus.Failed)
            .Select(document => (document.DocumentId, document.DisplayName ?? document.OriginalFileName))
            .ToArray();
        var pending = (await proposalRepository.ListForCaseAsync(scope, caseId, cancellationToken)).Count(proposal => proposal.Status == FieldProposalStatus.Proposed);

        var recommendation = ClaimRecommender.Recommend(new RecommendationFacts(
            score,
            checklist.MissingRequiredDocuments.Where(missing => missing.Required).ToArray(),
            unreadable,
            pending,
            caseId));
        return new Gathered(claim, score, recommendation, ClaimAssessment.HashInputs(score, recommendation));
    }

    private async Task<(string? Narrative, string? Provider)> WriteNarrativeAsync(
        TenantScope scope, Gathered gathered, string actorId, string correlationId, CancellationToken cancellationToken)
    {
        var gate = await agentGate.CheckAsync(scope, AgentKey, cancellationToken);
        if (!gate.Enabled)
        {
            return (null, null);
        }

        var startedAt = DateTimeOffset.UtcNow;
        var caseId = gathered.Claim.CaseId;
        try
        {
            var prompt = BuildPrompt(gathered);
            var result = await chatCompletionProvider.CompleteAsync(
                new ChatCompletionRequest(prompt, scope.TenantId, scope.WorkspaceId, correlationId, AgentKey), cancellationToken);
            var text = DisplayText.WithoutEmDashes((result.Content ?? string.Empty).Trim());
            var accepted = text.Length is > 0 and <= 1200 && AdvisoryLanguage.IsAcceptable(text);

            await actionLogRepository.SaveAsync(new AgentActionLog
            {
                AgentActionLogId = Guid.NewGuid(),
                Scope = scope,
                CaseId = caseId,
                AgentName = AgentKey,
                AssistantMode = AssistantMode.ClaimsSummary,
                InputSummary = $"Recommendation explanation for case {caseId}",
                RetrievedSources = [],
                ModelOutput = accepted ? text : "(not kept: wording was not acceptable)",
                ConfidenceScore = AiConfidenceScore.Create(0.6m),
                RiskFlag = gathered.Recommendation.Kind == RecommendationKind.ReadyForDecision ? "none" : "needs_review",
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
            await agentRunRecorder.RecordAsync(
                scope, AgentKey, "claim_recommendation", [$"case:{caseId}"],
                accepted ? AgentRunStatus.Completed : AgentRunStatus.Failed, startedAt, correlationId, actorId, caseId,
                accepted ? null : "wording_not_acceptable", null, cancellationToken);
            return accepted ? (text, result.Provider) : (null, null);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            // The model being unavailable never takes the score or the recommendation with it.
            await agentRunRecorder.RecordAsync(
                scope, AgentKey, "claim_recommendation", [$"case:{caseId}"],
                AgentRunStatus.Failed, startedAt, correlationId, actorId, caseId, "model_unavailable", null, cancellationToken);
            return (null, null);
        }
    }

    private static string BuildPrompt(Gathered gathered)
    {
        var lines = new List<string>
        {
            NarrativeMarker,
            "You help a claims supervisor at a Ugandan insurer read a claim. Write two or three plain sentences that explain",
            "the recommendation below using ONLY the facts listed. Do not add facts. Do not approve, reject, deny, settle or pay.",
            "Do not say anyone committed fraud or lied; the points below are things for a person to check, not conclusions.",
            "Do not use em dashes. Do not name internal identifiers.",
            string.Empty,
            $"CLAIM: {ClaimReference.For(gathered.Claim.CaseId)}",
            $"Review score: {gathered.Score.Value} of 100 ({ClaimRiskScorer.LabelFor(gathered.Score.Band)})",
            $"Recommendation: {ClaimRecommender.LabelFor(gathered.Recommendation.Kind)}. {gathered.Recommendation.Headline}",
        };
        foreach (var reason in gathered.Recommendation.Reasons)
        {
            lines.Add($"FACT: {reason.Text} [{reason.Source.Label}]");
        }

        return string.Join('\n', lines);
    }

    private static ClaimAssessmentDto ToDto(ClaimCase claim, RiskScore score, ClaimRecommendation recommendation, ClaimAssessment? stored, bool prepared, bool stale) =>
        new(
            claim.CaseId,
            ClaimReference.For(claim.CaseId),
            new RiskScoreDto(
                score.Value,
                score.Band.ToString(),
                ClaimRiskScorer.LabelFor(score.Band),
                score.Factors.Select(ToDto).ToArray()),
            new RecommendationDto(
                recommendation.Kind.ToString(),
                ClaimRecommender.LabelFor(recommendation.Kind),
                recommendation.Headline,
                recommendation.Reasons.Select(reason => new RecommendationReasonDto(reason.Text, ToDto(reason.Source))).ToArray(),
                recommendation.SuggestedRequests),
            stored?.Narrative,
            stored?.NarrativeProvider,
            string.Equals(stored?.NarrativeProvider, "Mock", StringComparison.OrdinalIgnoreCase),
            prepared,
            stale,
            stored?.GeneratedAt,
            stored?.GeneratedBy,
            stored?.Response is { } response
                ? new AssessmentResponseDto(response.ToString(), stored.ResponseNote, stored.ResponseByUserId, stored.ResponseAt)
                : null,
            "Prepared by the platform to help a person. It does not approve, reject or settle anything, and it is not a finding that anyone has done wrong.");

    private static AssessmentSourceDto ToDto(AssessmentSource source) => new(source.Kind.ToString(), source.Id, source.Label);

    private static RiskFactorDto ToDto(RiskFactor factor) =>
        new(
            factor.Code,
            factor.Source.ToString(),
            factor.Source switch
            {
                RiskFactorSource.RuleEngine => "Fixed rule",
                RiskFactorSource.PictureCheck => "Picture check",
                _ => "Model reading",
            },
            factor.Weight >= 0.35m ? "Strong" : factor.Weight >= 0.15m ? "Moderate" : "Light",
            factor.Reason,
            ToDto(factor.Reference),
            factor.Reading.ToString(),
            factor.Reading switch
            {
                HumanReading.NotLookedAt => "Not looked at yet",
                HumanReading.BeingWorked => "Being worked",
                HumanReading.SetAside => "Set aside by a person",
                HumanReading.FollowUpRequested => "Follow-up requested",
                HumanReading.ConcernRaised => "Concern raised by a person",
                _ => factor.Reading.ToString(),
            },
            factor.Counts);
}
