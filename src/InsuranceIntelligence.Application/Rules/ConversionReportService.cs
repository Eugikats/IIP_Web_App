using InsuranceIntelligence.Application.CustomerAssistant;
using InsuranceIntelligence.Application.SalesLeads;
using InsuranceIntelligence.Application.Workspaces;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Customers;
using InsuranceIntelligence.Domain.Policies;

namespace InsuranceIntelligence.Application.Rules;

/// <summary>One insurer's row: what was offered, how people answered, and how many went on to apply or hold a policy.</summary>
public sealed record InsurerConversion(string Insurer, int Offers, int Replied, int AskedForDetails, int ConvertedAfterOffer);

public sealed record ConvertedPerson(string? CustomerName, string Phone, string Cover, string Insurer, string How, DateTimeOffset OfferedAt, string RuleId = "");

public sealed record ConversionReport(
    IReadOnlyList<InsurerConversion> Insurers,
    InsurerConversion Total,
    IReadOnlyList<ConvertedPerson> Converted,
    int Applications,
    string Note);

/// <summary>
/// What the offers led to, for the bank's own people. It counts conversions by insurer and nothing else: IIP does not hold what a
/// bank earns from an insurer, so the bank applies its own rates to these numbers. Commission never enters the choice of which
/// cover to offer, and this view is never shown to a customer. A person counts as converted when they applied for a cover from
/// the insurer that was offered, or hold a policy from that insurer that began after the offer.
/// </summary>
public sealed class ConversionReportService(
    DecisionLog log,
    OutcomeService outcomes,
    ISalesLeadReadRepository leads,
    ICustomerRepository customers,
    IPolicyRepository policies,
    WorkspaceBrandService brands)
{
    public const string Note =
        "These are conversions only. The bank applies its own commission rates to them; IIP does not record rates, and what a cover pays the bank never affects which cover is offered.";

    public async Task<ConversionReport> BuildAsync(TenantScope scope, CancellationToken cancellationToken = default)
    {
        var brand = await brands.ResolveAsync(scope, cancellationToken);
        var offers = (await log.ListAsync(scope, take: 5000, cancellationToken: cancellationToken))
            .Where(decision => decision.Kind == "offer" && decision.WentOut && decision.Insurer is not null)
            .ToArray();
        var replies = await outcomes.ListAsync(scope, take: 5000, cancellationToken: cancellationToken);
        var allLeads = await leads.ListLeadsAsync(scope.TenantId, scope.WorkspaceId, cancellationToken);
        var customerPhones = (await customers.ListAsync(scope, cancellationToken))
            .Where(customer => !string.IsNullOrWhiteSpace(customer.Phone))
            .ToDictionary(customer => customer.CustomerId, customer => customer.Phone!, StringComparer.OrdinalIgnoreCase);
        var allPolicies = await policies.ListAsync(scope, cancellationToken);

        bool SamePerson(string? a, string? b) => PhoneNumbers.SameNumber(a, b);

        var converted = new List<ConvertedPerson>();
        foreach (var offer in offers.GroupBy(item => (item.Phone, item.Insurer)).Select(group => group.OrderBy(item => item.DecidedAt).First()))
        {
            var lead = allLeads.FirstOrDefault(candidate => SamePerson(candidate.Phone ?? candidate.ContactInfo, offer.Phone));
            var applied = lead?.AppliedFor is { Length: > 0 } appliedFor ? brand.Find(appliedFor) : null;
            if (applied is not null && applied.Insurer == offer.Insurer)
            {
                converted.Add(new ConvertedPerson(lead!.CustomerName, offer.Phone, applied.Name, applied.Insurer, "Applied for the cover", offer.DecidedAt, offer.RuleId));
                continue;
            }

            var policy = allPolicies.FirstOrDefault(candidate =>
                candidate.CustomerId is not null && customerPhones.TryGetValue(candidate.CustomerId, out var phone) && SamePerson(phone, offer.Phone)
                && candidate.EffectiveFrom is { } from && from >= DateOnly.FromDateTime(offer.DecidedAt.UtcDateTime)
                && MicroinsuranceCatalog.Offered.Any(product => product.ProductLine == candidate.ProductLine && product.PolicyPrefix == candidate.PolicyNumber.Split('/', 2)[0] && product.Insurer == offer.Insurer));
            if (policy is not null)
            {
                converted.Add(new ConvertedPerson(lead?.CustomerName, offer.Phone, policy.ProductLine, offer.Insurer!, "Holds a policy that began after the offer", offer.DecidedAt, offer.RuleId));
            }
        }

        var insurers = offers.Select(item => item.Insurer!).Concat(replies.Select(item => item.Insurer)).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        var rows = insurers.Select(insurer => new InsurerConversion(
            insurer,
            offers.Count(item => item.Insurer == insurer),
            replies.Count(item => item.Insurer == insurer),
            replies.Count(item => item.Insurer == insurer && item.Kind is OutcomeKind.Interested or OutcomeKind.RenewalRequested),
            converted.Count(item => item.Insurer == insurer))).ToArray();
        var total = new InsurerConversion("All insurers", rows.Sum(row => row.Offers), rows.Sum(row => row.Replied), rows.Sum(row => row.AskedForDetails), rows.Sum(row => row.ConvertedAfterOffer));
        var applications = allLeads.Count(lead => !string.IsNullOrWhiteSpace(lead.AppliedFor));
        return new ConversionReport(rows, total, converted.OrderByDescending(item => item.OfferedAt).ToArray(), applications, Note);
    }
}
