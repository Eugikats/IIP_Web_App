using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Domain.CaseReview;

/// <summary>What kind of concern an LLM-drafted case-review signal is about.</summary>
public enum CaseReviewSignalCategory
{
    Fraud,
    Underwriting
}

/// <summary>
/// One LLM-drafted observation surfaced during case review. Advisory only: never a fraud or
/// underwriting determination (AGENTS.md invariant — AI must not make adverse underwriting or
/// fraud determinations). A human reviewer decides what, if anything, to do about it. This is
/// deliberately not the same thing as <see cref="InsuranceIntelligence.Domain.Fraud.FraudRuleEngine"/>,
/// which stays the deterministic, rule-based fraud-signal path; this is the narrower, LLM-only
/// enrichment the case-review stage produces alongside its summary.
/// </summary>
public sealed record CaseReviewSignal(CaseReviewSignalCategory Category, string Note);

/// <summary>
/// A persisted case-review summary: the AI-generated claim overview plus any fraud/underwriting
/// signals produced in the same pass, scanned by a human reviewer before the claim moves to
/// approval. Never itself a decision and never gates claim progression — see
/// docs on the governed claim path (FNOL -> evidence intake -> assignment -> review -> approval).
/// </summary>
public sealed record CaseReviewSummary
{
    public required Guid CaseReviewId { get; init; }
    public required TenantScope Scope { get; init; }
    public required Guid CaseId { get; init; }
    public required string Overview { get; init; }
    public required IReadOnlyCollection<string> OpenQuestions { get; init; }
    public required IReadOnlyCollection<CaseReviewSignal> RiskSignals { get; init; }
    public required IReadOnlyCollection<string> EvidenceRefs { get; init; }
    public required decimal Confidence { get; init; }
    public required string GeneratedByProvider { get; init; }
    public string? GeneratedByModel { get; init; }
    public required string GeneratedByUserId { get; init; }
    public required DateTimeOffset GeneratedAt { get; init; }
    public required Guid AgentActionLogId { get; init; }
    public required string CorrelationId { get; init; }
}

public interface ICaseReviewRepository
{
    Task SaveAsync(CaseReviewSummary review, CancellationToken cancellationToken = default);

    Task<CaseReviewSummary?> GetLatestForCaseAsync(Guid caseId, TenantScope scope, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<CaseReviewSummary>> ListForCaseAsync(Guid caseId, TenantScope scope, CancellationToken cancellationToken = default);
}
