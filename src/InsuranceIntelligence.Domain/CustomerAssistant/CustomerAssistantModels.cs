using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Domain.CustomerAssistant;

/// <summary>
/// The six-intent set for the customer-facing WhatsApp assistant (blueprint §8.3). Deliberately
/// small — every intent except GeneralQuestion results in a human or a structured flow, never a
/// free-form AI conversation about it.
/// </summary>
public enum CustomerIntent
{
    GeneralQuestion,
    ProductInterest,
    NewClaim,
    ExistingClaimStatus,
    HumanRequested,
    Unknown,
}

public interface ICustomerIntentClassifier
{
    CustomerIntent Classify(string messageText);
}

/// <summary>What actually happened to one inbound customer message — always recorded, regardless
/// of outcome (blueprint §8.3 "conversation persistence"). This is both the audit trail and the
/// "why did the system say that?" answer.</summary>
public enum CustomerAssistantOutcome
{
    AutoReplied,
    QueuedForHumanApproval,
    CaseOpened,
    LeadCreated,
    Escalated,
    AwaitingMoreInformation,
    Declined,
}

public sealed record CustomerConversationTurn
{
    public required Guid TurnId { get; init; }
    public required TenantScope Scope { get; init; }
    public required string ThreadId { get; init; }
    public required string ContactWaId { get; init; }
    public required string InboundMessageText { get; init; }
    public required CustomerIntent ClassifiedIntent { get; init; }
    public string? OutboundReplyText { get; init; }
    public required CustomerAssistantOutcome Outcome { get; init; }
    public IReadOnlyCollection<string> CitationTitles { get; init; } = [];
    public Guid? CaseId { get; init; }
    public string? LeadId { get; init; }
    public required DateTimeOffset OccurredAt { get; init; }
    public required string CorrelationId { get; init; }
}

public interface ICustomerConversationRepository
{
    Task SaveAsync(CustomerConversationTurn turn, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<CustomerConversationTurn>> ListForThreadAsync(TenantScope scope, string threadId, CancellationToken cancellationToken = default);
}

/// <summary>
/// The five-condition auto-reply gate (blueprint §8.3) — mirrors OutboundSmsGuard's shape exactly:
/// a static, pure function every caller must pass through before a customer-facing AI-composed
/// answer is sent without human review. Everything that fails this gate is drafted and logged,
/// never sent. Note this governs only AI-composed GeneralQuestion answers; fixed, non-disclosing
/// template acknowledgements (a claim reference number, "please share your policy number", a lead
/// confirmation) are a structurally different, lower-risk category and are not subject to it —
/// the same distinction OutboundSmsGuard already draws between AI-drafted content and low-risk
/// transactional sends.
/// </summary>
public static class CustomerAutoReplyGate
{
    public static bool IsAutoReplyPermitted(
        CustomerIntent intent,
        bool hasApprovedCitation,
        bool carriesCustomerSpecificAssertion,
        int replyLength,
        int? maxResponseCharacters,
        bool tenantOptedIntoAutoReply) =>
        intent == CustomerIntent.GeneralQuestion
        && hasApprovedCitation
        && !carriesCustomerSpecificAssertion
        && (maxResponseCharacters is null || replyLength <= maxResponseCharacters)
        && tenantOptedIntoAutoReply;
}
