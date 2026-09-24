using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Domain.Cases;

/// <summary>Outcome O: "when a claim closes, ask one satisfaction question, and open a service
/// recovery task on a poor answer." The question itself is sent as an ordinary CustomerUpdateDraft
/// (see CustomerUpdateReasonCategory.SatisfactionQuestion); this record is the officer-observed
/// answer. Capture is human-recorded, not parsed automatically from an inbound reply, matching
/// AGENTS.md's boundary on what a system may determine unattended about a customer's own claim
/// experience.</summary>
public enum SatisfactionRating
{
    Good,
    Neutral,
    Poor
}

public sealed record SatisfactionResponse
{
    public required Guid ResponseId { get; init; }
    public required TenantScope Scope { get; init; }
    public required Guid CaseId { get; init; }
    public required SatisfactionRating Rating { get; init; }
    public string? Notes { get; init; }
    public required string RecordedByUserId { get; init; }
    public required DateTimeOffset RecordedAt { get; init; }
    public required string CorrelationId { get; init; }
}

public interface ISatisfactionResponseRepository
{
    Task SaveAsync(SatisfactionResponse response, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<SatisfactionResponse>> ListForCaseAsync(Guid caseId, TenantScope scope, CancellationToken cancellationToken = default);
}
