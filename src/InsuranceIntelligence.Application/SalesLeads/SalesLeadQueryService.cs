using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Domain.Administration;

namespace InsuranceIntelligence.Application.SalesLeads;

public sealed class SalesLeadQueryService(
    ITenantContextAccessor tenantContextAccessor,
    IPermissionService permissionService,
    ISalesLeadReadRepository repository) : ISalesLeadQueryService
{
    public async Task<SalesLeadDetailDto?> GetLeadDetailAsync(
        string leadId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(leadId))
        {
            throw new SalesLeadQueryValidationException("leadId", "Lead ID is required.");
        }

        await permissionService.RequirePermissionAsync(Permission.ViewSalesLeads, cancellationToken);

        var context = tenantContextAccessor.Current;
        var lead = await repository.GetLeadDetailAsync(
            context.TenantId,
            context.WorkspaceId,
            leadId.Trim(),
            cancellationToken);

        return lead is null ? null : ToDetailDto(lead);
    }

    public async Task<IReadOnlyList<SalesLeadListItemDto>> ListLeadsAsync(
        CancellationToken cancellationToken = default)
    {
        await permissionService.RequirePermissionAsync(Permission.ViewSalesLeads, cancellationToken);

        var context = tenantContextAccessor.Current;
        var leads = await repository.ListLeadsAsync(context.TenantId, context.WorkspaceId, cancellationToken);

        return leads
            .OrderByDescending(lead => lead.CreatedAt ?? DateTimeOffset.MinValue)
            .Select(lead => new SalesLeadListItemDto(
                lead.LeadId,
                lead.CustomerName,
                lead.LeadSource,
                lead.ProductFit,
                lead.QuoteStatus,
                lead.AssignedSalesOfficer,
                lead.CreatedAt))
            .ToArray();
    }

    internal static SalesLeadDetailDto ToDetailDto(SalesLeadDetailReadModel lead) =>
        new(
            lead.LeadId,
            lead.LeadSource,
            lead.CustomerNeed,
            lead.ConversationSummary,
            lead.ProductFit,
            lead.QuoteStatus,
            lead.RequiredInformation,
            lead.FollowUpActions,
            lead.AssignedSalesOfficer,
            lead.ConversionProbability,
            lead.CustomerName,
            lead.ContactInfo,
            lead.CreatedAt,
            lead.CreatedBy);
}
