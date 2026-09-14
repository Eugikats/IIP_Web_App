namespace InsuranceIntelligence.Domain.Compliance;

/// <summary>What actually happened to an erasure request (blueprint §14 Phase 6, WO 6.4), always
/// truthful, and always recorded, whichever way it goes. Insurance claim records commonly carry a
/// real legal retention obligation a customer's own erasure request cannot override while a claim
/// is active, refusal is not a bug in the feature, it is the feature, and it must be documented,
/// not silent.</summary>
public enum DataSubjectErasureOutcome
{
    /// <summary>The customer's identifying fields were anonymized. Related case, policy, and
    /// communication records are never deleted by this action, those carry their own retention
    /// obligations independent of a customer's erasure request, and this platform's compliance
    /// posture depends on the case record continuing to exist.</summary>
    Erased,

    /// <summary>Refused because at least one case tied to this customer is still open. The
    /// specific reason is always on the result and in the audit trail, never a bare denial.</summary>
    RefusedActiveCase,
}

public sealed record DataSubjectErasureResult(
    string CustomerId,
    DataSubjectErasureOutcome Outcome,
    string Reason,
    DateTimeOffset DecidedAt);

/// <summary>The corrections a rectification request actually wants applied, null means "leave
/// this field as it is," matching ConfirmCaseCorrections' own established shape for a
/// partial-update command.</summary>
public sealed record DataSubjectRectificationRequest(
    string? FullName,
    string? Email,
    string? Phone);
