using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Domain.CustomerAssistant;

/// <summary>
/// The six-intent set for the customer-facing WhatsApp assistant (blueprint §8.3). Deliberately
/// small, every intent except GeneralQuestion results in a human or a structured flow, never a
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

/// <summary>What actually happened to one inbound customer message, always recorded, regardless
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

    /// <summary>A recognised policyholder is reporting a claim; the assistant asked them what
    /// happened, when and where. Their next message continues the same claim.</summary>
    AwaitingIncidentDetails,

    /// <summary>A recognised policyholder with more than one policy is reporting a claim; the
    /// assistant asked which cover it concerns.</summary>
    AwaitingPolicyChoice,

    /// <summary>The message was unclear; the assistant asked what the customer needs instead of
    /// handing them to a person straight away. A second unclear message in a row escalates.</summary>
    Clarifying,

    /// <summary>A photo or document arrived and the assistant acknowledged it.</summary>
    DocumentReceived,

    /// <summary>The assistant could not tell which language the customer writes in and asked them
    /// to choose. Their next message is the answer. Appended last so stored values keep their meaning.</summary>
    AwaitingLanguageChoice,

    /// <summary>The demo phone sent the hidden reset command. Everything in this thread before this
    /// turn belongs to the previous participant and is ignored from here on.</summary>
    SessionReset,

    /// <summary>The assistant read a claim back to the customer and is waiting for a yes before
    /// opening it. Nothing binding happens until the customer confirms.</summary>
    AwaitingClaimConfirmation,

    /// <summary>A new customer was asked for their name.</summary>
    AwaitingName,

    /// <summary>A customer with no policy was asked what they do for a living.</summary>
    AwaitingOccupation,

    /// <summary>A person took the conversation. Kato stays silent until it is handed back or the
    /// person stops replying. Appended last so stored values keep their meaning.</summary>
    HumanTookOver,

    /// <summary>The person handed the conversation back to Kato.</summary>
    HandedBackToKato,

    /// <summary>A person replied to the customer from the portal. The reply text is the turn's
    /// outbound text, so Kato can read it if he answers later.</summary>
    HumanReplied,

    /// <summary>The customer wrote while a person had the conversation: recorded, not answered by Kato.</summary>
    HeldForPerson,
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
    /// <summary>Model-authored reply held for staff review. It is kept apart from
    /// <see cref="OutboundReplyText"/>, which records the text actually sent to the customer.</summary>
    public string? PendingReplyDraftText { get; init; }
    public required CustomerAssistantOutcome Outcome { get; init; }
    public IReadOnlyCollection<string> CitationTitles { get; init; } = [];
    public Guid? CaseId { get; init; }
    public string? LeadId { get; init; }

    /// <summary>The staff member behind a take-over, hand-back or reply. Null for Kato's own turns.</summary>
    public string? HumanUserId { get; init; }

    /// <summary>The policy this turn concerned, once known (chosen by the customer, matched from
    /// their phone number, or typed). Carries a policy choice forward to the next message of a
    /// claim conversation. Optional so turns saved before it existed still read back.</summary>
    public string? PolicyNumber { get; init; }

    /// <summary>The language the customer is conversing in, as an ISO code, when it is not English.
    /// InboundMessageText and OutboundReplyText are always the English record of the turn.</summary>
    public string? CustomerLanguage { get; init; }

    /// <summary>What the customer actually wrote, kept when the English above is a translation of it.</summary>
    public string? OriginalInboundText { get; init; }

    /// <summary>What the customer actually received, kept when it differs from the English reply.</summary>
    public string? OutboundLocalisedText { get; init; }

    public InsuranceIntelligence.Domain.Translation.TranslationStatus? InboundTranslationStatus { get; init; }

    public InsuranceIntelligence.Domain.Translation.TranslationStatus? OutboundTranslationStatus { get; init; }

    /// <summary>The English description of a claim read back to the customer and not yet
    /// confirmed. Nothing is opened until they say yes.</summary>
    public string? PendingClaimDescription { get; init; }

    /// <summary>The customer's own words for the pending claim, and their language, when the
    /// English above is a translation.</summary>
    public string? PendingClaimOriginalText { get; init; }

    public string? PendingClaimOriginalLanguage { get; init; }

    /// <summary>Set when the customer described an accident that matched an open claim on the same
    /// cover: the detail was added to that claim, and they were asked whether this is a new one.</summary>
    public Guid? OfferedNewIncidentForCaseId { get; init; }

    /// <summary>What a customer without a policy said they do for a living.</summary>
    public string? Occupation { get; init; }

    public required DateTimeOffset OccurredAt { get; init; }
    public required string CorrelationId { get; init; }
}

public interface ICustomerConversationRepository
{
    Task SaveAsync(CustomerConversationTurn turn, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<CustomerConversationTurn>> ListForThreadAsync(TenantScope scope, string threadId, CancellationToken cancellationToken = default);

    /// <summary>Blueprint §14 Phase 6, WO 6.3, real deletion, not a soft-delete flag: retention
    /// enforcement for this data class means the raw conversation content is actually gone.
    /// Returns what was deleted (count, oldest/newest OccurredAt) so the caller can write an
    /// honest audit summary without a second read.</summary>
    Task<InsuranceIntelligence.Domain.Compliance.RetentionDeletionSummary> DeleteOlderThanAsync(TenantScope scope, DateTimeOffset cutoff, CancellationToken cancellationToken = default);
}

/// <summary>
/// The five-condition auto-reply gate (blueprint §8.3), mirrors OutboundSmsGuard's shape exactly:
/// a static, pure function every caller must pass through before a customer-facing AI-composed
/// answer is sent without human review. Everything that fails this gate is drafted and logged,
/// never sent. Note this governs only AI-composed GeneralQuestion answers; fixed, non-disclosing
/// template acknowledgements (a claim reference number, "please share your policy number", a lead
/// confirmation) are a structurally different, lower-risk category and are not subject to it,
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
