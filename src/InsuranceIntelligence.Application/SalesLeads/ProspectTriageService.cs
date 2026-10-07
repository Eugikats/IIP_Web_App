using InsuranceIntelligence.Application.Agents;
using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Application.CustomerAssistant;
using InsuranceIntelligence.Domain.Acquisition;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Agents;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Customers;
using InsuranceIntelligence.Domain.Knowledge;
using InsuranceIntelligence.Domain.Policies;

namespace InsuranceIntelligence.Application.SalesLeads;

/// <summary>
/// The triage agent. It reads each prospect's recorded facts, scores them with <see cref="ProspectScoring"/>
/// and stores the two covers that fit best, each with reasons quoted from the record and a link to the
/// insurer's own key information summary. It prepares a ranking for staff; it never contacts anyone,
/// decides cover or sets a price.
/// </summary>
public sealed class ProspectTriageService(
    ITenantContextAccessor tenantContextAccessor,
    IPermissionService permissionService,
    ISalesLeadReadRepository readRepository,
    ISalesLeadWriteRepository writeRepository,
    IAuditWriter auditWriter,
    ICustomerRepository? customerRepository = null,
    IPolicyRepository? policyRepository = null,
    IAgentRunRecorder? agentRunRecorder = null,
    IKnowledgeDocumentRepository? knowledgeRepository = null) : IProspectTriageService
{
    public const string AgentKey = "prospect-triage";

    public async Task<TriageRunResult> RunAsync(string? leadId, CancellationToken cancellationToken = default)
    {
        await permissionService.RequirePermissionAsync(Permission.ManageSalesLeads, cancellationToken);
        var context = tenantContextAccessor.Current;
        var now = DateTimeOffset.UtcNow;

        var leads = await readRepository.ListLeadsAsync(context.TenantId, context.WorkspaceId, cancellationToken);
        if (!string.IsNullOrWhiteSpace(leadId))
        {
            leads = leads.Where(lead => string.Equals(lead.LeadId, leadId.Trim(), StringComparison.OrdinalIgnoreCase)).ToArray();
        }

        var profiles = ProductTargeting.ProfilesFor(KatoBrand.Current.Products);
        var customers = customerRepository is null ? [] : (await customerRepository.ListAsync(context.Scope, cancellationToken)).ToArray();
        var policies = policyRepository is null ? [] : (await policyRepository.ListAsync(context.Scope, cancellationToken)).ToArray();
        var documents = knowledgeRepository is null
            ? []
            : (await knowledgeRepository.ListAsync(context.Scope, cancellationToken))
                .Where(document => document.IsActiveOn(DateOnly.FromDateTime(now.UtcDateTime)))
                .ToArray();

        var scored = 0;
        var skipped = 0;
        foreach (var lead in leads)
        {
            var phone = lead.Phone ?? lead.ContactInfo;
            var onPhone = string.IsNullOrWhiteSpace(phone)
                ? []
                : customers.Where(customer => PhoneNumbers.SameNumber(customer.Phone, phone)).ToArray();
            var ids = onPhone.Select(customer => customer.CustomerId).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var holdsCover = policies.Any(policy => policy.CustomerId is not null && ids.Contains(policy.CustomerId));

            var inputs = new ProspectInputs(
                lead.Facts,
                lead.Interests,
                lead.BankCustomer,
                holdsCover,
                onPhone.Length > 0,
                lead.Consent?.Status ?? ContactConsentStatus.NotRecorded,
                !string.IsNullOrWhiteSpace(phone) && !phone.Contains('@'),
                lead.LastInteractionAt ?? lead.CreatedAt,
                lead.Insights?.MessageCount ?? 0,
                lead.AppliedFor is not null);

            var triage = ProspectScoring.Score(inputs, profiles, now);
            triage = triage with { Fits = triage.Fits.Select(fit => Cite(fit, documents)).ToArray() };

            if (triage.Fits.Count == 0 && triage.Signals.Count == 0)
            {
                skipped++;
            }
            else
            {
                scored++;
            }

            await writeRepository.CreateAsync(context.TenantId, context.WorkspaceId, lead with { Triage = triage }, cancellationToken);
        }

        await auditWriter.WriteAsync(
            new AuditEvent(
                Guid.NewGuid(), context.Scope, CaseId: null, DocumentId: null, UserId: context.UserId, ActorType.User, "prospect_triage.run",
                now, BeforeStatus: null, AfterStatus: null, EvidenceIds: [], ModelProvider: "ProspectTriage",
                Confidence: null, ValidationResult: "recorded", ApprovalId: null,
                AuditReason: $"Scored {scored} prospects ({skipped} with too little recorded to rank).",
                CorrelationId: Guid.NewGuid().ToString("N")),
            cancellationToken);

        if (agentRunRecorder is not null)
        {
            await agentRunRecorder.RecordAsync(
                context.Scope, AgentKey, string.IsNullOrWhiteSpace(leadId) ? "triage_all" : "triage_one",
                string.IsNullOrWhiteSpace(leadId) ? [] : [$"lead:{leadId.Trim()}"], AgentRunStatus.Completed, now,
                Guid.NewGuid().ToString("N"), context.UserId, caseId: null, cancellationToken: cancellationToken);
        }

        return new TriageRunResult(scored, skipped, now);
    }

    /// <summary>Links a fit to the insurer's approved summary for that cover, so a person can open the
    /// source of what the cover is for.</summary>
    private static ProductFit Cite(ProductFit fit, IReadOnlyList<KnowledgeDocument> documents)
    {
        var document = documents
            .Where(candidate => string.Equals(candidate.ProductLine, fit.ProductLine, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(candidate => candidate.UpdatedAt)
            .FirstOrDefault();
        return document is null ? fit : fit with { CitationDocumentId = document.KnowledgeDocumentId, CitationTitle = document.DocumentName };
    }
}
