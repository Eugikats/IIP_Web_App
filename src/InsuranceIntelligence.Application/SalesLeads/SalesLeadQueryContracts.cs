using InsuranceIntelligence.Domain.Acquisition;

namespace InsuranceIntelligence.Application.SalesLeads;

public interface ISalesLeadQueryService
{
    Task<SalesLeadDetailDto?> GetLeadDetailAsync(
        string leadId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SalesLeadListItemDto>> ListLeadsAsync(
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Manual sales-lead creation, the Gate 0 origination path (staff capture a walk-in /
/// phone / referral lead). Channel ingestion and integration-fabric import are later-gate
/// sources that will write into the same store; until then those sources are honest-gated
/// and this manual path is the only origination.
/// </summary>
public interface ISalesLeadCommandService
{
    Task<SalesLeadDetailDto> CreateLeadAsync(
        CreateSalesLeadRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>False when the lead is not in this workspace.</summary>
    Task<bool> AddNoteAsync(string leadId, AddLeadNoteRequest request, CancellationToken cancellationToken = default);

    /// <summary>False when the lead is not in this workspace. A null user id unassigns.</summary>
    Task<bool> AssignAsync(string leadId, AssignLeadRequest request, CancellationToken cancellationToken = default);
}

public interface ISalesLeadReadRepository
{
    Task<SalesLeadDetailReadModel?> GetLeadDetailAsync(
        string tenantId,
        string workspaceId,
        string leadId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SalesLeadDetailReadModel>> ListLeadsAsync(
        string tenantId,
        string workspaceId,
        CancellationToken cancellationToken = default);
}

public interface ISalesLeadWriteRepository
{
    /// <summary>Saves the lead, replacing any earlier version with the same lead id.</summary>
    Task CreateAsync(
        string tenantId,
        string workspaceId,
        SalesLeadDetailReadModel lead,
        CancellationToken cancellationToken = default);

    /// <summary>The lead for this phone number in the workspace, if there is one. One prospect per
    /// person: a second conversation from the same number updates this record.</summary>
    Task<SalesLeadDetailReadModel?> FindByPhoneAsync(
        string tenantId,
        string workspaceId,
        string phone,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// The stored lead, which is IIP's prospect record. The positional fields are the original Gate 0
/// lead; the properties after them were added for acquisition and are optional, so leads saved before
/// them still read back.
/// </summary>
public sealed record SalesLeadDetailReadModel(
    string LeadId,
    string LeadSource,
    string CustomerNeed,
    string ConversationSummary,
    string ProductFit,
    string QuoteStatus,
    IReadOnlyList<string> RequiredInformation,
    IReadOnlyList<string> FollowUpActions,
    string? AssignedSalesOfficer,
    decimal? ConversionProbability,
    string CustomerName = "",
    string? ContactInfo = null,
    DateTimeOffset? CreatedAt = null,
    string? CreatedBy = null)
{
    /// <summary>Digits only, international form, for example 256776678989.</summary>
    public string? Phone { get; init; }

    public string? PrimaryChannel { get; init; }

    /// <summary>The WhatsApp conversation, when the lead came from one.</summary>
    public string? ThreadId { get; init; }

    public DateTimeOffset? LastInteractionAt { get; init; }

    public IReadOnlyList<ProspectFact> Facts { get; init; } = [];

    public IReadOnlyList<ProspectInterest> Interests { get; init; } = [];

    public ContactConsent? Consent { get; init; }

    public IReadOnlyList<ProspectNote> Notes { get; init; } = [];

    public IReadOnlyList<ProspectActivity> Activity { get; init; } = [];

    /// <summary>The cover applied for, once they apply.</summary>
    public string? AppliedFor { get; init; }

    /// <summary>Set when Kato handed the conversation to a person.</summary>
    public DateTimeOffset? HandedToPersonAt { get; init; }

    public ConversationInsights? Insights { get; init; }

    /// <summary>Whether the person already banks with the partner bank (bancassurance). Null when
    /// nobody has said: shown as "Not known", never guessed. Set by staff or by an imported bank list.</summary>
    public bool? BankCustomer { get; init; }
}

/// <summary>What the conversation insights agent made of a conversation. Every point carries the tags
/// of the messages it came from; a point without one is never stored.</summary>
public sealed record ConversationInsights(
    DateTimeOffset GeneratedAt,
    int MessageCount,
    bool EnoughConversation,
    InsightPoint? Summary,
    IReadOnlyList<InsightInterest> Interests,
    IReadOnlyList<InsightPoint> Problems,
    IReadOnlyList<InsightPoint> Objections,
    string? Tone,
    IReadOnlyList<string> Unknowns,
    InsightPoint? NextStep,
    IReadOnlyList<InsightSource> Sources,
    Guid? AgentActionLogId = null);

public sealed record InsightPoint(string Text, IReadOnlyList<string> Refs);

public sealed record InsightInterest(string Product, string Strength, string Text, IReadOnlyList<string> Refs);

/// <summary>A message an insight can cite: M3 is the third customer message in the conversation.</summary>
public sealed record InsightSource(string Tag, Guid TurnId, DateTimeOffset At, string Excerpt);

public sealed record SalesLeadDetailDto(
    string LeadId,
    string LeadSource,
    string CustomerNeed,
    string ConversationSummary,
    string ProductFit,
    string QuoteStatus,
    IReadOnlyList<string> RequiredInformation,
    IReadOnlyList<string> FollowUpActions,
    string? AssignedSalesOfficer,
    decimal? ConversionProbability,
    string CustomerName = "",
    string? ContactInfo = null,
    DateTimeOffset? CreatedAt = null,
    string? CreatedBy = null)
{
    public string MaskedContact { get; init; } = "Hidden";
    public string? PrimaryChannel { get; init; }
    public DateTimeOffset? LastInteractionAt { get; init; }
    public string Stage { get; init; } = nameof(LeadStage.New);
    public string StageLabel { get; init; } = "New";
    public string Relationship { get; init; } = nameof(ProspectRelationship.NewToInsurer);
    public string RelationshipLabel { get; init; } = "New to insurer";
    public string? AssignedOfficerName { get; init; }
    public string? Occupation { get; init; }
    public string? ProductInterest { get; init; }
    public string? AppliedFor { get; init; }
    public bool? BankCustomer { get; init; }
    public string BankCustomerLabel { get; init; } = "Not known";
    public bool HasConversation { get; init; }

    /// <summary>The WhatsApp conversation, so the screen can open it. Not shown as text.</summary>
    public string? ConversationId { get; init; }
    public LeadConsentDto Consent { get; init; } = new("NotRecorded", "Not recorded", null, null, null);
    public IReadOnlyList<LeadFactDto> Facts { get; init; } = [];
    public IReadOnlyList<string> Missing { get; init; } = [];
    public IReadOnlyList<LeadInterestDto> Interests { get; init; } = [];
    public IReadOnlyList<ProspectNote> Notes { get; init; } = [];
    public IReadOnlyList<ProspectActivity> Activity { get; init; } = [];
    public IReadOnlyList<LeadPolicyDto> Policies { get; init; } = [];
    public ConversationInsights? Insights { get; init; }

    /// <summary>True when the conversation has moved on since the insights were prepared.</summary>
    public bool InsightsStale { get; init; }
}

public sealed record LeadFactDto(string Key, string Label, string Value, string Source, DateTimeOffset RecordedAt);

public sealed record LeadInterestDto(string Product, string Strength, string StrengthLabel, string Reason, DateTimeOffset RecordedAt);

public sealed record LeadConsentDto(string Status, string StatusLabel, DateTimeOffset? At, string? Wording, string? Channel);

public sealed record LeadPolicyDto(string PolicyNumber, string ProductLine, DateOnly? EffectiveTo);

public sealed record SalesLeadListItemDto(
    string LeadId,
    string CustomerName,
    string LeadSource,
    string ProductFit,
    string QuoteStatus,
    string? AssignedSalesOfficer,
    DateTimeOffset? CreatedAt)
{
    public string MaskedContact { get; init; } = "Hidden";
    public string? Occupation { get; init; }
    public string Relationship { get; init; } = nameof(ProspectRelationship.NewToInsurer);
    public string RelationshipLabel { get; init; } = "New to insurer";
    public string? PrimaryChannel { get; init; }
    public string? ProductInterest { get; init; }
    public string Stage { get; init; } = nameof(LeadStage.New);
    public string StageLabel { get; init; } = "New";
    public string? AssignedOfficerName { get; init; }
    public DateTimeOffset? LastInteractionAt { get; init; }
    public string ConsentStatus { get; init; } = nameof(ContactConsentStatus.NotRecorded);
    public bool? BankCustomer { get; init; }
    public string BankCustomerLabel { get; init; } = "Not known";

    /// <summary>The WhatsApp conversation, so a row can open it. Null for leads with no conversation.</summary>
    public string? ConversationId { get; init; }
}

public sealed record CreateSalesLeadRequest(
    string? CustomerName,
    string? LeadSource,
    string? CustomerNeed,
    string? ProductFit,
    string? AssignedSalesOfficer,
    string? ContactInfo,
    bool? BankCustomer = null);

public sealed record AddLeadNoteRequest(string? Text);

public sealed record AssignLeadRequest(string? UserId);

public sealed class SalesLeadQueryValidationException(string field, string message) : InvalidOperationException(message)
{
    public string Field { get; } = field;
}
