using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Domain.Compliance;

/// <summary>
/// The legal basis a customer's outbound communication actually rests on (blueprint §14 Phase 6,
/// WO 6.1), distinct from a marketing opt-in checkbox. Named to match how each basis is actually
/// established in this platform, not a generic textbook list.
/// </summary>
public enum ConsentBasis
{
    /// <summary>The customer messaged first (WhatsApp/email/portal), replying within that
    /// conversation is contract-adjacent, not marketing. This is what every hardcoded
    /// "customer_initiated_conversation" string in this codebase was standing in for before this
    /// WO gave it a real, queryable, auditable record.</summary>
    CustomerInitiatedConversation,

    /// <summary>The customer explicitly opted in, a checkbox, a recorded verbal confirmation, a
    /// signed form, for a stated purpose.</summary>
    ExplicitOptIn,

    /// <summary>Necessary to service an active policy or claim the customer already holds
    /// (status updates, missing-document requests, decision-outcome notices).</summary>
    ContractualNecessity,

    /// <summary>A prior consent basis was withdrawn by the customer. Never deleted, kept as its
    /// own row so the withdrawal itself is part of the auditable history.</summary>
    Withdrawn,
}

/// <summary>
/// One consent grant or withdrawal for one customer, for one purpose, on one channel. Append-only
///, same pattern as AgentBehaviourProfile/CustomerConversationTurn: every SaveAsync inserts a new
/// row, so the full consent history for a customer is always reconstructable and a withdrawal can
/// never quietly erase what was previously true. IConsentRepository.GetCurrentAsync resolves the
/// single most recent row for (customer, purpose) as the answer to "is this permitted right now."
/// </summary>
public sealed record ConsentRecord
{
    public required Guid RecordId { get; init; }
    public required TenantScope Scope { get; init; }

    /// <summary>Whatever this platform actually has for the customer at capture time, a phone
    /// number (WhatsApp/SMS), an email address, or a Customer.CustomerId when the message arrived
    /// through an identified, policy-matched sender. Not a foreign key to a customer record that
    /// may not exist yet (e.g. a first-contact WhatsApp message from an unrecognized number).</summary>
    public required string CustomerIdentifier { get; init; }

    public required ConsentBasis Basis { get; init; }

    /// <summary>What the consent covers, e.g. "whatsapp_messaging", "sms_transactional",
    /// "email_claim_updates". A grant for one purpose says nothing about another.</summary>
    public required string Purpose { get; init; }

    public required string Channel { get; init; }

    /// <summary>Null for a grant row. Set on the row that records a withdrawal, see
    /// ConsentEnforcementGuard, which treats the most recent row for (customer, purpose) as
    /// authoritative, so a withdrawal row always overrides any earlier grant.</summary>
    public DateTimeOffset? WithdrawnAt { get; init; }

    public required string RecordedByUserId { get; init; }
    public required DateTimeOffset RecordedAt { get; init; }
    public required string CorrelationId { get; init; }
}

public interface IConsentRepository
{
    Task SaveAsync(ConsentRecord record, CancellationToken cancellationToken = default);

    /// <summary>The most recent record for this (customer, purpose), a grant, or a withdrawal
    /// that supersedes an earlier grant. Null means no consent history exists at all yet.</summary>
    Task<ConsentRecord?> GetCurrentAsync(TenantScope scope, string customerIdentifier, string purpose, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<ConsentRecord>> ListForCustomerAsync(TenantScope scope, string customerIdentifier, CancellationToken cancellationToken = default);
}

/// <summary>
/// Outbound enforcement (WO 6.1), the one gate every real send path this platform has must pass
/// through before it contacts a customer for a given purpose. Mirrors OutboundSmsGuard/
/// CustomerAutoReplyGate's shape: a static, pure function, not a service, so it is trivially
/// unit-testable and cannot itself develop side effects.
/// </summary>
public static class ConsentEnforcementGuard
{
    public static bool IsPermitted(ConsentRecord? current) =>
        current is not null && current.WithdrawnAt is null && current.Basis != ConsentBasis.Withdrawn;
}
