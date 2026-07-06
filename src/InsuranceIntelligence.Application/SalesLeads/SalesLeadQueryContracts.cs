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
/// Manual sales-lead creation — the Gate 0 origination path (staff capture a walk-in /
/// phone / referral lead). Channel ingestion and integration-fabric import are later-gate
/// sources that will write into the same store; until then those sources are honest-gated
/// and this manual path is the only origination.
/// </summary>
public interface ISalesLeadCommandService
{
    Task<SalesLeadDetailDto> CreateLeadAsync(
        CreateSalesLeadRequest request,
        CancellationToken cancellationToken = default);
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
    Task CreateAsync(
        string tenantId,
        string workspaceId,
        SalesLeadDetailReadModel lead,
        CancellationToken cancellationToken = default);
}

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
    string? CreatedBy = null);

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
    string? CreatedBy = null);

public sealed record SalesLeadListItemDto(
    string LeadId,
    string CustomerName,
    string LeadSource,
    string ProductFit,
    string QuoteStatus,
    string? AssignedSalesOfficer,
    DateTimeOffset? CreatedAt);

public sealed record CreateSalesLeadRequest(
    string? CustomerName,
    string? LeadSource,
    string? CustomerNeed,
    string? ProductFit,
    string? AssignedSalesOfficer,
    string? ContactInfo);

public sealed class SalesLeadQueryValidationException(string field, string message) : InvalidOperationException(message)
{
    public string Field { get; } = field;
}
