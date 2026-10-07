using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Domain.Acquisition;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.CustomerAssistant;
using InsuranceIntelligence.Domain.Customers;
using InsuranceIntelligence.Domain.Policies;

namespace InsuranceIntelligence.Application.SalesLeads;

/// <summary>
/// The acquisition list and a lead's details. What is shown is derived, never stored as a label: the
/// stage comes from <see cref="LeadStages.Derive"/>, the relationship from the insurer's own customer
/// and policy records for the lead's phone number, and officers are shown by name.
/// </summary>
public sealed class SalesLeadQueryService(
    ITenantContextAccessor tenantContextAccessor,
    IPermissionService permissionService,
    ISalesLeadReadRepository repository,
    ICustomerRepository? customerRepository = null,
    IPolicyRepository? policyRepository = null,
    IUserAccountRepository? userAccountRepository = null,
    ICustomerConversationRepository? conversationRepository = null) : ISalesLeadQueryService
{
    private static readonly HashSet<string> Unassessed = new(StringComparer.OrdinalIgnoreCase) { "To be assessed", "To be chosen", "" };

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
        if (lead is null)
        {
            return null;
        }

        var lookup = await LoadLookupAsync(context.Scope, cancellationToken);
        var customerTurns = 0;
        if (conversationRepository is not null && !string.IsNullOrWhiteSpace(lead.ThreadId))
        {
            customerTurns = (await conversationRepository.ListForThreadAsync(context.Scope, lead.ThreadId, cancellationToken))
                .Count(turn => !string.IsNullOrWhiteSpace(turn.InboundMessageText));
        }

        return ToDetailDto(lead, lookup, customerTurns);
    }

    public async Task<SalesLeadDetailDto?> GetLeadByThreadAsync(string threadId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(threadId))
        {
            throw new SalesLeadQueryValidationException("threadId", "Thread ID is required.");
        }

        await permissionService.RequirePermissionAsync(Permission.ViewSalesLeads, cancellationToken);
        var context = tenantContextAccessor.Current;
        var leads = await repository.ListLeadsAsync(context.TenantId, context.WorkspaceId, cancellationToken);
        var trimmed = threadId.Trim();
        // A thread id ends with the customer's number, so a lead whose own thread has not been
        // recorded yet is still found by it.
        var lead = leads.FirstOrDefault(item => string.Equals(item.ThreadId, trimmed, StringComparison.Ordinal))
            ?? leads.FirstOrDefault(item => !string.IsNullOrWhiteSpace(item.Phone) && trimmed.EndsWith(item.Phone, StringComparison.Ordinal));
        return lead is null ? null : await GetLeadDetailAsync(lead.LeadId, cancellationToken);
    }

    public async Task<IReadOnlyList<SalesLeadListItemDto>> ListLeadsAsync(
        CancellationToken cancellationToken = default)
    {
        await permissionService.RequirePermissionAsync(Permission.ViewSalesLeads, cancellationToken);

        var context = tenantContextAccessor.Current;
        var leads = await repository.ListLeadsAsync(context.TenantId, context.WorkspaceId, cancellationToken);
        var lookup = await LoadLookupAsync(context.Scope, cancellationToken);

        return leads
            .OrderByDescending(lead => lead.LastInteractionAt ?? lead.CreatedAt ?? DateTimeOffset.MinValue)
            .Select(lead =>
            {
                var relationship = lookup.RelationshipFor(lead.Phone ?? lead.ContactInfo);
                var stage = StageOf(lead);
                return new SalesLeadListItemDto(
                    lead.LeadId,
                    lead.CustomerName,
                    lead.LeadSource,
                    lead.ProductFit,
                    lead.QuoteStatus,
                    lead.AssignedSalesOfficer,
                    lead.CreatedAt)
                {
                    MaskedContact = ContactMask.Mask(lead.Phone ?? lead.ContactInfo),
                    Occupation = FactValue(lead, "occupation"),
                    Relationship = relationship.ToString(),
                    RelationshipLabel = LeadStages.Label(relationship),
                    PrimaryChannel = lead.PrimaryChannel ?? ChannelFromSource(lead.LeadSource),
                    ProductInterest = ProductInterestOf(lead),
                    Stage = stage.ToString(),
                    StageLabel = LeadStages.Label(stage),
                    AssignedOfficerName = lookup.NameOf(lead.AssignedSalesOfficer),
                    LastInteractionAt = lead.LastInteractionAt ?? lead.CreatedAt,
                    ConsentStatus = (lead.Consent?.Status ?? ContactConsentStatus.NotRecorded).ToString(),
                    BankCustomer = lead.BankCustomer,
                    BankCustomerLabel = BankCustomerLabel(lead.BankCustomer),
                    ConversationId = string.IsNullOrWhiteSpace(lead.ThreadId) ? null : lead.ThreadId,
                };
            })
            .ToArray();
    }

    internal static LeadStage StageOf(SalesLeadDetailReadModel lead) =>
        LeadStages.Derive(lead.Facts, lead.Interests, lead.AppliedFor is not null, lead.HandedToPersonAt is not null);

    internal static string? ProductInterestOf(SalesLeadDetailReadModel lead)
    {
        var strongest = lead.Interests
            .Where(interest => interest.Strength != InterestStrength.NotNow)
            .OrderByDescending(interest => interest.Strength)
            .ThenByDescending(interest => interest.RecordedAt)
            .FirstOrDefault();
        return strongest?.Product
            ?? lead.AppliedFor
            ?? (Unassessed.Contains(lead.ProductFit.Trim()) ? null : lead.ProductFit);
    }

    internal static SalesLeadDetailDto ToDetailDto(SalesLeadDetailReadModel lead) =>
        ToDetailDto(lead, LeadLookup.Empty, customerTurns: 0);

    private static SalesLeadDetailDto ToDetailDto(SalesLeadDetailReadModel lead, LeadLookup lookup, int customerTurns)
    {
        var relationship = lookup.RelationshipFor(lead.Phone ?? lead.ContactInfo);
        var stage = StageOf(lead);
        var consent = lead.Consent;
        return new SalesLeadDetailDto(
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
            lead.CreatedBy)
        {
            MaskedContact = ContactMask.Mask(lead.Phone ?? lead.ContactInfo),
            PrimaryChannel = lead.PrimaryChannel ?? ChannelFromSource(lead.LeadSource),
            LastInteractionAt = lead.LastInteractionAt ?? lead.CreatedAt,
            Stage = stage.ToString(),
            StageLabel = LeadStages.Label(stage),
            Relationship = relationship.ToString(),
            RelationshipLabel = LeadStages.Label(relationship),
            AssignedOfficerName = lookup.NameOf(lead.AssignedSalesOfficer),
            Occupation = FactValue(lead, "occupation"),
            ProductInterest = ProductInterestOf(lead),
            AppliedFor = lead.AppliedFor,
            BankCustomer = lead.BankCustomer,
            BankCustomerLabel = BankCustomerLabel(lead.BankCustomer),
            HasConversation = !string.IsNullOrWhiteSpace(lead.ThreadId),
            ConversationId = lead.ThreadId,
            Consent = new LeadConsentDto(
                (consent?.Status ?? ContactConsentStatus.NotRecorded).ToString(),
                LeadStages.Label(consent?.Status ?? ContactConsentStatus.NotRecorded),
                consent?.At,
                consent?.Wording,
                consent?.Channel),
            Facts = lead.Facts
                .Select(fact => new LeadFactDto(fact.Key, ProspectFacts.Label(fact.Key), fact.Value, SourceLabel(fact.Source), fact.RecordedAt))
                .ToArray(),
            Missing = ProspectFacts.Missing(lead.Facts).Select(field => field.Label).ToArray(),
            Interests = lead.Interests
                .OrderByDescending(interest => interest.Strength)
                .Select(interest => new LeadInterestDto(
                    interest.Product, interest.Strength.ToString(), LeadStages.Label(interest.Strength), interest.Reason, interest.RecordedAt))
                .ToArray(),
            Notes = lead.Notes.OrderByDescending(note => note.At).ToArray(),
            Activity = lead.Activity.OrderByDescending(entry => entry.At).ToArray(),
            Policies = lookup.PoliciesFor(lead.Phone ?? lead.ContactInfo),
            Insights = lead.Insights,
            InsightsStale = customerTurns > 0 && (lead.Insights is null || lead.Insights.MessageCount < customerTurns),
        };
    }

    internal static string BankCustomerLabel(bool? bankCustomer) => bankCustomer switch
    {
        true => "Yes",
        false => "No",
        null => "Not known",
    };

    private static string SourceLabel(ProspectFactSource source) => source switch
    {
        ProspectFactSource.Customer => "Said by the customer",
        ProspectFactSource.ImportedList => "From an imported list",
        ProspectFactSource.Staff => "Added by staff",
        _ => source.ToString(),
    };

    private static string? FactValue(SalesLeadDetailReadModel lead, string key) =>
        lead.Facts.FirstOrDefault(fact => string.Equals(fact.Key, key, StringComparison.OrdinalIgnoreCase))?.Value;

    private static string ChannelFromSource(string leadSource) =>
        leadSource.StartsWith("WhatsApp", StringComparison.OrdinalIgnoreCase) ? "WhatsApp"
        : leadSource.Contains("email", StringComparison.OrdinalIgnoreCase) ? "Email"
        : "Manual";

    private async Task<LeadLookup> LoadLookupAsync(TenantScope scope, CancellationToken cancellationToken)
    {
        var customers = customerRepository is null ? [] : await customerRepository.ListAsync(scope, cancellationToken);
        var policies = policyRepository is null ? [] : await policyRepository.ListAsync(scope, cancellationToken);
        var users = userAccountRepository is null ? [] : await userAccountRepository.ListAsync(scope, cancellationToken);
        return new LeadLookup(customers.ToArray(), policies.ToArray(), users.ToDictionary(user => user.UserId, user => user.Name, StringComparer.OrdinalIgnoreCase));
    }

    private sealed class LeadLookup(Customer[] customers, Policy[] policies, IReadOnlyDictionary<string, string> userNames)
    {
        public static readonly LeadLookup Empty = new([], [], new Dictionary<string, string>());

        public ProspectRelationship RelationshipFor(string? phone)
        {
            var onPhone = CustomersOn(phone);
            if (onPhone.Length == 0)
            {
                return ProspectRelationship.NewToInsurer;
            }

            var ids = onPhone.Select(customer => customer.CustomerId).ToHashSet(StringComparer.OrdinalIgnoreCase);
            return policies.Any(policy => policy.CustomerId is not null && ids.Contains(policy.CustomerId))
                ? ProspectRelationship.ExistingCustomer
                : ProspectRelationship.Registered;
        }

        public IReadOnlyList<LeadPolicyDto> PoliciesFor(string? phone)
        {
            var ids = CustomersOn(phone).Select(customer => customer.CustomerId).ToHashSet(StringComparer.OrdinalIgnoreCase);
            return policies
                .Where(policy => policy.CustomerId is not null && ids.Contains(policy.CustomerId))
                .Select(policy => new LeadPolicyDto(policy.PolicyNumber, policy.ProductLine, policy.EffectiveTo))
                .ToArray();
        }

        /// <summary>A stored user id becomes the person's name. Leads saved before officers were
        /// chosen from the user list hold a typed name, which is shown as it was typed.</summary>
        public string? NameOf(string? officer) =>
            string.IsNullOrWhiteSpace(officer) ? null : userNames.TryGetValue(officer, out var name) ? name : officer;

        private Customer[] CustomersOn(string? phone) =>
            string.IsNullOrWhiteSpace(phone)
                ? []
                : customers.Where(customer => PhoneNumbers.SameNumber(customer.Phone, phone)).ToArray();
    }
}
