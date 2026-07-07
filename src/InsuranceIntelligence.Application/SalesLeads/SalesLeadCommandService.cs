using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Audit;

namespace InsuranceIntelligence.Application.SalesLeads;

/// <summary>
/// Manual sales-lead capture. Permission-gated (<see cref="Permission.ManageSalesLeads"/>),
/// tenant-scoped, and audited. A new lead is persisted with a "New" quote status and a
/// standard required-information checklist so the acquisition queue has real, actionable work.
/// </summary>
public sealed class SalesLeadCommandService(
    ITenantContextAccessor tenantContextAccessor,
    IPermissionService permissionService,
    ISalesLeadWriteRepository repository,
    IAuditWriter auditWriter) : ISalesLeadCommandService
{
    private static readonly string[] DefaultRequiredInformation =
        ["Confirm contact details", "Confirm insurable interest", "Capture risk details for quote"];

    public async Task<SalesLeadDetailDto> CreateLeadAsync(
        CreateSalesLeadRequest request,
        CancellationToken cancellationToken = default)
    {
        await permissionService.RequirePermissionAsync(Permission.ManageSalesLeads, cancellationToken);

        if (string.IsNullOrWhiteSpace(request.CustomerName))
        {
            throw new SalesLeadQueryValidationException(nameof(request.CustomerName), "Customer name is required.");
        }

        if (string.IsNullOrWhiteSpace(request.CustomerNeed))
        {
            throw new SalesLeadQueryValidationException(nameof(request.CustomerNeed), "Describe what the customer needs.");
        }

        var context = tenantContextAccessor.Current;
        var now = DateTimeOffset.UtcNow;
        var leadId = $"LEAD-{now:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";

        var lead = new SalesLeadDetailReadModel(
            LeadId: leadId,
            LeadSource: string.IsNullOrWhiteSpace(request.LeadSource) ? "Manual capture" : request.LeadSource.Trim(),
            CustomerNeed: request.CustomerNeed.Trim(),
            ConversationSummary: $"Lead captured manually by {context.UserId} on {now:yyyy-MM-dd}.",
            ProductFit: string.IsNullOrWhiteSpace(request.ProductFit) ? "To be assessed" : request.ProductFit.Trim(),
            QuoteStatus: "New",
            RequiredInformation: DefaultRequiredInformation,
            FollowUpActions: ["Contact customer to confirm details", "Prepare indicative quote for human review"],
            AssignedSalesOfficer: string.IsNullOrWhiteSpace(request.AssignedSalesOfficer) ? null : request.AssignedSalesOfficer.Trim(),
            ConversionProbability: null,
            CustomerName: request.CustomerName.Trim(),
            ContactInfo: string.IsNullOrWhiteSpace(request.ContactInfo) ? null : request.ContactInfo.Trim(),
            CreatedAt: now,
            CreatedBy: context.UserId);

        await repository.CreateAsync(context.TenantId, context.WorkspaceId, lead, cancellationToken);

        await auditWriter.WriteAsync(
            new AuditEvent(
                Guid.NewGuid(),
                context.Scope,
                CaseId: null,
                DocumentId: null,
                UserId: context.UserId,
                ActorType.User,
                "sales_lead.created",
                now,
                BeforeStatus: null,
                AfterStatus: "New",
                EvidenceIds: [leadId],
                ModelProvider: "SalesLeads",
                Confidence: null,
                ValidationResult: "recorded",
                ApprovalId: null,
                AuditReason: $"Manual sales lead captured for {lead.CustomerName}.",
                CorrelationId: Guid.NewGuid().ToString("N")),
            cancellationToken);

        return SalesLeadQueryService.ToDetailDto(lead);
    }
}
