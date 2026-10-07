using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Domain.Acquisition;
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
    IAuditWriter auditWriter,
    ISalesLeadReadRepository? readRepository = null,
    IUserAccountRepository? userAccountRepository = null) : ISalesLeadCommandService
{
    private const int MaxNoteLength = 2000;

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

        var contactDigits = new string((lead.ContactInfo ?? string.Empty).Where(char.IsDigit).ToArray());
        lead = lead with
        {
            Phone = lead.ContactInfo is not null && !lead.ContactInfo.Contains('@') && contactDigits.Length >= 9 ? contactDigits : null,
            PrimaryChannel = "Manual",
            LastInteractionAt = now,
            Activity = [new ProspectActivity(now, $"Captured by staff ({lead.LeadSource})", await NameOfAsync(context.UserId, cancellationToken) ?? "A colleague")],
        };

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

    public async Task<bool> AddNoteAsync(string leadId, AddLeadNoteRequest request, CancellationToken cancellationToken = default)
    {
        await permissionService.RequirePermissionAsync(Permission.ManageSalesLeads, cancellationToken);
        var text = request.Text?.Trim() ?? string.Empty;
        if (text.Length == 0)
        {
            throw new SalesLeadQueryValidationException(nameof(request.Text), "Write the note first.");
        }

        if (text.Length > MaxNoteLength)
        {
            throw new SalesLeadQueryValidationException(nameof(request.Text), $"Notes can be up to {MaxNoteLength:N0} characters.");
        }

        var context = tenantContextAccessor.Current;
        var lead = await LoadAsync(leadId, cancellationToken);
        if (lead is null)
        {
            return false;
        }

        var now = DateTimeOffset.UtcNow;
        var author = await NameOfAsync(context.UserId, cancellationToken) ?? "A colleague";
        var updated = lead with
        {
            Notes = [.. lead.Notes, new ProspectNote(Guid.NewGuid(), text, context.UserId, author, now)],
            Activity = [.. lead.Activity, new ProspectActivity(now, "Added a note", author)],
        };
        await repository.CreateAsync(context.TenantId, context.WorkspaceId, updated, cancellationToken);
        await AuditAsync("sales_lead.note_added", lead.LeadId, null, "Internal note added to a lead. Note text omitted.", cancellationToken);
        return true;
    }

    public async Task<bool> AssignAsync(string leadId, AssignLeadRequest request, CancellationToken cancellationToken = default)
    {
        await permissionService.RequirePermissionAsync(Permission.ManageSalesLeads, cancellationToken);
        var context = tenantContextAccessor.Current;
        var lead = await LoadAsync(leadId, cancellationToken);
        if (lead is null)
        {
            return false;
        }

        var userId = string.IsNullOrWhiteSpace(request.UserId) ? null : request.UserId.Trim();
        string? assigneeName = null;
        if (userId is not null)
        {
            assigneeName = await NameOfAsync(userId, cancellationToken)
                ?? throw new SalesLeadQueryValidationException(nameof(request.UserId), "Choose someone from this workspace.");
        }

        var now = DateTimeOffset.UtcNow;
        var actor = await NameOfAsync(context.UserId, cancellationToken) ?? "A colleague";
        var updated = lead with
        {
            AssignedSalesOfficer = userId,
            Activity = [.. lead.Activity, new ProspectActivity(now, assigneeName is null ? "Unassigned the lead" : $"Assigned to {assigneeName}", actor)],
        };
        await repository.CreateAsync(context.TenantId, context.WorkspaceId, updated, cancellationToken);
        await AuditAsync("sales_lead.assigned", lead.LeadId, userId is null ? "Unassigned" : "Assigned", "Lead owner changed.", cancellationToken);
        return true;
    }

    private async Task<SalesLeadDetailReadModel?> LoadAsync(string leadId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(leadId))
        {
            throw new SalesLeadQueryValidationException("leadId", "Lead ID is required.");
        }

        if (readRepository is null)
        {
            return null;
        }

        var context = tenantContextAccessor.Current;
        return await readRepository.GetLeadDetailAsync(context.TenantId, context.WorkspaceId, leadId.Trim(), cancellationToken);
    }

    private async Task<string?> NameOfAsync(string userId, CancellationToken cancellationToken)
    {
        if (userAccountRepository is null)
        {
            return null;
        }

        var user = await userAccountRepository.GetAsync(userId, tenantContextAccessor.Current.Scope, cancellationToken);
        return user?.Name;
    }

    private Task AuditAsync(string action, string leadId, string? afterStatus, string reason, CancellationToken cancellationToken)
    {
        var context = tenantContextAccessor.Current;
        return auditWriter.WriteAsync(
            new AuditEvent(
                Guid.NewGuid(), context.Scope, CaseId: null, DocumentId: null, UserId: context.UserId, ActorType.User, action,
                DateTimeOffset.UtcNow, BeforeStatus: null, AfterStatus: afterStatus, EvidenceIds: [leadId], ModelProvider: "SalesLeads",
                Confidence: null, ValidationResult: "recorded", ApprovalId: null, AuditReason: reason, CorrelationId: Guid.NewGuid().ToString("N")),
            cancellationToken);
    }
}
