using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Domain.Contestability;

/// <summary>Lifecycle of a customer-initiated request for a human to re-review a claim.</summary>
public enum ClaimReviewRequestStatus
{
    Open,
    Acknowledged,
    Resolved,
}

/// <summary>Who raised the re-review request (directly or on the claimant's behalf).</summary>
public enum ClaimReviewRequestOrigin
{
    Customer,
    FieldAgent,
    Staff,
}

/// <summary>
/// A contestability record: a claimant (directly, or via a field agent / SMS) asks that a human
/// re-review their claim. Creating one only flags work for a human, it is never itself a decision,
/// so it sits inside the human-supervised boundary without an approval guard. Traceable and auditable.
/// </summary>
public sealed record ClaimReviewRequest(
    Guid RequestId,
    TenantScope Scope,
    Guid CaseId,
    ClaimReviewRequestOrigin Origin,
    string RequestedByUserId,
    string Reason,
    ClaimReviewRequestStatus Status,
    DateTimeOffset RequestedAt,
    DateTimeOffset? ResolvedAt,
    string? ResolvedByUserId,
    string? ResolutionNote,
    string CorrelationId)
{
    public const int MaxReasonLength = 1000;

    public static ClaimReviewRequest Create(
        TenantScope scope,
        Guid caseId,
        ClaimReviewRequestOrigin origin,
        string requestedByUserId,
        string reason,
        string correlationId)
    {
        var trimmedReason = (reason ?? string.Empty).Trim();
        if (trimmedReason.Length == 0)
        {
            throw new ArgumentException("A re-review request requires a reason.", nameof(reason));
        }

        if (trimmedReason.Length > MaxReasonLength)
        {
            trimmedReason = trimmedReason[..MaxReasonLength];
        }

        return new ClaimReviewRequest(
            Guid.NewGuid(),
            scope,
            caseId,
            origin,
            string.IsNullOrWhiteSpace(requestedByUserId) ? "client-portal-user" : requestedByUserId,
            trimmedReason,
            ClaimReviewRequestStatus.Open,
            DateTimeOffset.UtcNow,
            ResolvedAt: null,
            ResolvedByUserId: null,
            ResolutionNote: null,
            correlationId);
    }

    public ClaimReviewRequest Acknowledge() =>
        Status == ClaimReviewRequestStatus.Open
            ? this with { Status = ClaimReviewRequestStatus.Acknowledged }
            : this;

    public ClaimReviewRequest Resolve(string resolvedByUserId, string? resolutionNote) =>
        Status == ClaimReviewRequestStatus.Resolved
            ? this
            : this with
            {
                Status = ClaimReviewRequestStatus.Resolved,
                ResolvedAt = DateTimeOffset.UtcNow,
                ResolvedByUserId = resolvedByUserId,
                ResolutionNote = resolutionNote,
            };
}
