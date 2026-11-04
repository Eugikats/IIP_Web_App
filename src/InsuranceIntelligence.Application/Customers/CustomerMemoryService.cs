using InsuranceIntelligence.Application.Products;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Customers;
using InsuranceIntelligence.Domain.Documents;
using InsuranceIntelligence.Domain.Products;

namespace InsuranceIntelligence.Application.Customers;

public interface ICustomerMemoryService
{
    /// <summary>What can be offered again for an existing claim: details this claim asks about and has no answer for, and
    /// documents this claim asks for and has nothing in yet.</summary>
    Task<CustomerMemory> ForClaimAsync(ClaimCase claim, CancellationToken cancellationToken = default);

    /// <summary>What can be offered before a claim exists, to prefill the new-claim form for a verified customer.</summary>
    Task<CustomerMemory> ForNewClaimAsync(TenantScope scope, string policyNumber, string? phone, string? email, string? productLine, string? claimType, CancellationToken cancellationToken = default);

    /// <summary>The bytes of a document on file, if it is one that may be reused for this claim and slot; otherwise null.</summary>
    Task<(DocumentOnFile Offer, CaseDocument Source)?> FindDocumentAsync(ClaimCase claim, Guid sourceDocumentId, string slotKey, CancellationToken cancellationToken = default);
}

/// <summary>
/// Remembers a returning customer between claims (build-loop item 26), inside one workspace. It matches earlier claims to the
/// customer by policy number, phone number or email, offers only the details and documents the workspace has named as worth
/// reusing and that the current claim asks for, and offers them to the customer to accept: nothing is filled in or copied silently.
/// It reads claims by scanning the workspace's claims, which item 35 replaces with an indexed lookup.
/// </summary>
public sealed class CustomerMemoryService(
    ICaseRepository caseRepository,
    ICaseDocumentRepository documentRepository,
    IDocumentChecklistProvider checklistProvider,
    IClaimFieldService claimFieldService,
    ITenantSettingsRepository settingsRepository) : ICustomerMemoryService
{
    public async Task<CustomerMemory> ForClaimAsync(ClaimCase claim, CancellationToken cancellationToken = default)
    {
        var settings = await settingsRepository.GetAsync(claim.Scope, cancellationToken);
        if (!settings.CustomerMemoryEnabled)
        {
            return CustomerMemory.Empty;
        }

        var earlier = (await caseRepository.ListAsync(claim.Scope, cancellationToken))
            .Where(other => other.Scope == claim.Scope && CustomerMemoryPolicy.SameCustomer(claim, other))
            .ToArray();
        if (earlier.Length == 0)
        {
            return CustomerMemory.Empty;
        }

        var form = await claimFieldService.GetForClaimAsync(claim, customerAudience: true, cancellationToken);
        var labels = form.Fields.ToDictionary(field => field.Key, field => field.Label, StringComparer.OrdinalIgnoreCase);
        var have = ClaimFieldValues.For(claim);
        var values = CustomerMemoryPolicy.Values(
            have, earlier, CustomerMemoryPolicy.KeysOrDefault(settings.CustomerMemoryFieldKeys, CustomerMemoryPolicy.DefaultFieldKeys),
            key => labels.GetValueOrDefault(key), ClaimReference.For);

        var mine = await documentRepository.ListForCaseAsync(claim.CaseId, claim.Scope, cancellationToken);
        var checklist = await checklistProvider.BuildChecklistAsync(claim, mine, cancellationToken);
        var asked = checklist.Slots.ToDictionary(slot => slot.Key, slot => slot.Label, StringComparer.OrdinalIgnoreCase);
        var filled = mine.Where(document => document.CountsTowardsCompleteness).Select(document => document.EffectiveKey).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var earlierWithDocuments = new List<(ClaimCase, IReadOnlyList<CaseDocument>)>();
        foreach (var other in earlier)
        {
            earlierWithDocuments.Add((other, (await documentRepository.ListForCaseAsync(other.CaseId, other.Scope, cancellationToken)).Where(document => !document.IsSuperseded).ToArray()));
        }

        var documents = CustomerMemoryPolicy.Documents(
            earlierWithDocuments, CustomerMemoryPolicy.KeysOrDefault(settings.CustomerMemoryDocumentKeys, CustomerMemoryPolicy.DefaultDocumentKeys),
            asked, filled, ClaimReference.For);
        return new CustomerMemory(values, documents);
    }

    public async Task<CustomerMemory> ForNewClaimAsync(TenantScope scope, string policyNumber, string? phone, string? email, string? productLine, string? claimType, CancellationToken cancellationToken = default)
    {
        var settings = await settingsRepository.GetAsync(scope, cancellationToken);
        if (!settings.CustomerMemoryEnabled)
        {
            return CustomerMemory.Empty;
        }

        var earlier = (await caseRepository.ListAsync(scope, cancellationToken))
            .Where(other => other.Scope == scope && CustomerMemoryPolicy.SameCustomer(policyNumber, phone, email, other))
            .ToArray();
        if (earlier.Length == 0)
        {
            return CustomerMemory.Empty;
        }

        var form = await claimFieldService.GetFormAsync(scope, productLine, claimType, customerAudience: true, cancellationToken);
        var labels = form.Fields.ToDictionary(field => field.Key, field => field.Label, StringComparer.OrdinalIgnoreCase);
        var values = CustomerMemoryPolicy.Values(
            new Dictionary<string, string>(), earlier, CustomerMemoryPolicy.KeysOrDefault(settings.CustomerMemoryFieldKeys, CustomerMemoryPolicy.DefaultFieldKeys),
            key => labels.GetValueOrDefault(key), ClaimReference.For);
        return new CustomerMemory(values, []);
    }

    public async Task<(DocumentOnFile Offer, CaseDocument Source)?> FindDocumentAsync(ClaimCase claim, Guid sourceDocumentId, string slotKey, CancellationToken cancellationToken = default)
    {
        var memory = await ForClaimAsync(claim, cancellationToken);
        var offer = memory.Documents.FirstOrDefault(item => item.DocumentId == sourceDocumentId && string.Equals(item.SlotKey, slotKey, StringComparison.OrdinalIgnoreCase));
        if (offer is null)
        {
            return null;
        }

        // The offer names the earlier claim by reference; find the document in this customer's own earlier claims only.
        foreach (var other in (await caseRepository.ListAsync(claim.Scope, cancellationToken)).Where(other => other.Scope == claim.Scope && CustomerMemoryPolicy.SameCustomer(claim, other)))
        {
            var source = await documentRepository.GetAsync(other.CaseId, sourceDocumentId, other.Scope, cancellationToken);
            if (source is not null && !source.IsSuperseded)
            {
                return (offer, source);
            }
        }

        return null;
    }
}
