using InsuranceIntelligence.Domain.Acquisition;

namespace InsuranceIntelligence.Application.SalesLeads;

/// <summary>
/// Merges several records of the same person into one. Before one record per phone number was the
/// rule, every conversation step created its own lead, so a person could appear four times. The oldest
/// record is kept and everything recorded on the others is added to it; nothing is dropped.
/// </summary>
public static class LeadMerge
{
    private static readonly HashSet<string> GenericNames = new(StringComparer.OrdinalIgnoreCase) { "", "WhatsApp customer" };

    /// <summary>The leads must be the same person, oldest first. The first one's id is the one kept.</summary>
    public static SalesLeadDetailReadModel Merge(IReadOnlyList<SalesLeadDetailReadModel> sameBodyOldestFirst)
    {
        var leads = sameBodyOldestFirst;
        if (leads.Count == 0)
        {
            throw new ArgumentException("There is nothing to merge.", nameof(sameBodyOldestFirst));
        }

        var keep = leads[0];

        var facts = leads.Aggregate<SalesLeadDetailReadModel, IReadOnlyList<ProspectFact>>(
            [], (merged, lead) => ProspectFacts.Merge(merged, lead.Facts.OrderBy(fact => fact.RecordedAt)));

        var interests = leads
            .SelectMany(lead => lead.Interests)
            .GroupBy(interest => interest.Product, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(interest => interest.RecordedAt).First())
            .ToArray();

        var assessedFit = leads
            .Select(lead => lead.ProductFit)
            .LastOrDefault(fit => !string.IsNullOrWhiteSpace(fit) && fit is not ("To be assessed" or "To be chosen"));

        return keep with
        {
            CustomerName = leads.Select(lead => lead.CustomerName).FirstOrDefault(name => !GenericNames.Contains(name)) ?? keep.CustomerName,
            ProductFit = assessedFit ?? keep.ProductFit,
            AssignedSalesOfficer = leads.Select(lead => lead.AssignedSalesOfficer).FirstOrDefault(officer => !string.IsNullOrWhiteSpace(officer)),
            Phone = leads.Select(lead => lead.Phone).FirstOrDefault(phone => !string.IsNullOrWhiteSpace(phone)),
            PrimaryChannel = leads.Select(lead => lead.PrimaryChannel).FirstOrDefault(channel => !string.IsNullOrWhiteSpace(channel)),
            ThreadId = leads.Select(lead => lead.ThreadId).LastOrDefault(thread => !string.IsNullOrWhiteSpace(thread)),
            LastInteractionAt = leads.Max(lead => lead.LastInteractionAt ?? lead.CreatedAt),
            Facts = facts,
            Interests = interests,
            Consent = leads.Select(lead => lead.Consent).Where(consent => consent is not null).OrderByDescending(consent => consent!.At).FirstOrDefault(),
            Notes = leads.SelectMany(lead => lead.Notes).DistinctBy(note => note.NoteId).OrderBy(note => note.At).ToArray(),
            Activity = leads.SelectMany(lead => lead.Activity).OrderBy(entry => entry.At).ToArray(),
            AppliedFor = leads.Select(lead => lead.AppliedFor).LastOrDefault(applied => !string.IsNullOrWhiteSpace(applied)),
            HandedToPersonAt = leads.Max(lead => lead.HandedToPersonAt),
            Insights = leads.Select(lead => lead.Insights).Where(insights => insights is not null).OrderByDescending(insights => insights!.GeneratedAt).FirstOrDefault(),
            BankCustomer = leads.Any(lead => lead.BankCustomer == true) ? true : leads.Any(lead => lead.BankCustomer == false) ? false : null,
        };
    }
}
