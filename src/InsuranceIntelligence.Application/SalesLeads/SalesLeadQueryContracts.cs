namespace InsuranceIntelligence.Application.SalesLeads;

public interface ISalesLeadQueryService
{
    Task<SalesLeadDetailDto?> GetLeadDetailAsync(
        string leadId,
        CancellationToken cancellationToken = default);
}

public interface ISalesLeadReadRepository
{
    Task<SalesLeadDetailReadModel?> GetLeadDetailAsync(
        string tenantId,
        string workspaceId,
        string leadId,
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
    decimal? ConversionProbability);

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
    decimal? ConversionProbability);

public sealed class SalesLeadQueryValidationException(string field, string message) : InvalidOperationException(message)
{
    public string Field { get; } = field;
}
