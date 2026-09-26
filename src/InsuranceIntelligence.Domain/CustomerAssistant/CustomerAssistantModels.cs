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
