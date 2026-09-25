using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Domain.Workflow;

/// <summary>How this draft's MessageBody came to exist (outcome D): the audit trail must be able
/// to show which of the three happened for any given message.</summary>
public enum CustomerUpdateCreationMethod
{
    AiDrafted,
    ManuallyWritten,
    Template
}

public sealed record CustomerUpdateDraft
{
    public required Guid UpdateId { get; init; }
    public required TenantScope Scope { get; init; }
    public required Guid CaseId { get; init; }
    public required CustomerUpdateReasonCategory ReasonCategory { get; init; }
    public required CommunicationChannel Channel { get; init; }
    public required CustomerUpdateStatus Status { get; init; }
    public required string MessageBody { get; init; }

    /// <summary>A short, plain-language form that fits an SMS, carried on every message because
    /// most phones in this market are not smartphones (outcome D). Falls back to MessageBody itself
    /// where no distinct short form was supplied.
    ///
    /// Deliberately not `required`: drafts are stored as JSON, and every draft saved before
    /// outcome D has no such field. A required member made each of those rows throw on read, which
    /// took down every screen that lists drafts (My Work, the update queue, operations summary,
    /// command center). Old rows read back with the full message as their short form.</summary>
    public string ShortMessageBody
    {
        get => string.IsNullOrEmpty(shortMessageBody) ? MessageBody : shortMessageBody;
        init => shortMessageBody = value;
    }

    private readonly string? shortMessageBody;

    public string Language { get; init; } = "en";

    /// <summary>Not `required` for the same stored-JSON reason as ShortMessageBody. A draft saved
    /// before outcome D has no value and reads back as AiDrafted, which is what every draft of
    /// that era was.</summary>
    public CustomerUpdateCreationMethod CreationMethod { get; init; } = CustomerUpdateCreationMethod.AiDrafted;

    /// <summary>The model's original text, kept alongside MessageBody once a person edits it, so
    /// the audit trail shows both (outcome D). Null when CreationMethod is not AiDrafted, or when an
    /// AI draft has never been edited (MessageBody still equals the original).</summary>
    public string? OriginalMessageBody { get; init; }
    public string? EditedByUserId { get; init; }
    public DateTimeOffset? EditedAt { get; init; }

    /// <summary>The template this message started from, if CreationMethod is Template.</summary>
    public string? SourceTemplateKey { get; init; }

    public required bool RequiresApproval { get; init; }
    public required bool Sensitive { get; init; }
    public string? DeliveryStatus { get; init; }
    public required string CreatedByUserId { get; init; }
    public string? ApprovedByUserId { get; init; }
    public string? SentByUserId { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required DateTimeOffset UpdatedAt { get; init; }
    public DateTimeOffset? ApprovedAt { get; init; }
    public DateTimeOffset? SentAt { get; init; }
    public required string CorrelationId { get; init; }
}
