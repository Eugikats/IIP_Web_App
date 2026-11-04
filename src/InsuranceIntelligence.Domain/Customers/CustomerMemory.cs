using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Documents;
using InsuranceIntelligence.Domain.Products;

namespace InsuranceIntelligence.Domain.Customers;

/// <summary>A detail the customer gave on an earlier claim, offered again so they are not asked twice.</summary>
public sealed record RememberedValue(string Key, string Label, string Value, string FromClaimReference, DateTimeOffset At);

/// <summary>A document the customer sent for an earlier claim, offered for this claim instead of asking again.</summary>
public sealed record DocumentOnFile(Guid DocumentId, string SlotKey, string SlotLabel, string FileName, string FromClaimReference, DateTimeOffset UploadedAt);

public sealed record CustomerMemory(IReadOnlyList<RememberedValue> Values, IReadOnlyList<DocumentOnFile> Documents)
{
    public static readonly CustomerMemory Empty = new([], []);
}

/// <summary>
/// What a platform remembers about a customer between their claims (build-loop item 26), within one workspace and only for the
/// same customer. Only what the workspace has named as worth remembering is offered, a detail is offered for the customer to
/// confirm or change and never filled in silently, and a document is reused only when the customer chooses to. Nothing here
/// crosses a workspace, and a claim belongs to the same customer only when the policy, the phone number or the email matches.
/// </summary>
public static class CustomerMemoryPolicy
{
    /// <summary>The details worth carrying from one claim to the next unless the workspace says otherwise: things about the person
    /// or their vehicle, not about what happened.</summary>
    public static readonly IReadOnlyList<string> DefaultFieldKeys = ["drivingPermitNumber", "preferredCommunicationChannel", "vehicleRegistration"];

    /// <summary>The documents worth reusing unless the workspace says otherwise: proof of who the customer is.</summary>
    public static readonly IReadOnlyList<string> DefaultDocumentKeys = ["CustomerId"];

    public static bool SameCustomer(ClaimCase current, ClaimCase other) =>
        current.CaseId != other.CaseId && SameCustomer(current.PolicyNumber, current.CustomerPhone, current.CustomerEmail, other);

    /// <summary>The same customer when the policy number, the phone number or the email address is the same. Used before a claim
    /// exists, for a customer starting a new one.</summary>
    public static bool SameCustomer(string? policyNumber, string? phone, string? email, ClaimCase other) =>
        (!string.IsNullOrWhiteSpace(policyNumber) && string.Equals(policyNumber.Trim(), other.PolicyNumber?.Trim(), StringComparison.OrdinalIgnoreCase))
        || PhoneNumbers.SameNumber(phone, other.CustomerPhone)
        || (!string.IsNullOrWhiteSpace(email) && string.Equals(email.Trim(), other.CustomerEmail?.Trim(), StringComparison.OrdinalIgnoreCase));

    public static IReadOnlyList<string> KeysOrDefault(IReadOnlyList<string>? configured, IReadOnlyList<string> fallback) =>
        configured is { Count: > 0 } ? configured : fallback;

    /// <summary>The most recent non-empty value of each remembered detail across the customer's earlier claims, leaving out any the
    /// current claim already has an answer for.</summary>
    public static IReadOnlyList<RememberedValue> Values(
        IReadOnlyDictionary<string, string> have,
        IReadOnlyList<ClaimCase> earlierClaimsOfTheSameCustomer,
        IReadOnlyList<string> keys,
        Func<string, string?> labelFor,
        Func<Guid, string> referenceFor)
    {
        var found = new Dictionary<string, RememberedValue>(StringComparer.OrdinalIgnoreCase);
        foreach (var claim in earlierClaimsOfTheSameCustomer.OrderByDescending(claim => claim.CreatedAt))
        {
            var values = ClaimFieldValues.For(claim);
            foreach (var key in keys)
            {
                if (found.ContainsKey(key) || have.ContainsKey(key) || !values.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                // Only details this claim actually asks about are offered.
                if (labelFor(key) is not { } label)
                {
                    continue;
                }

                found[key] = new RememberedValue(key, label, value.Trim(), referenceFor(claim.CaseId), claim.CreatedAt);
            }
        }

        return keys.Where(found.ContainsKey).Select(key => found[key]).ToArray();
    }

    /// <summary>The newest still-valid document of each reusable kind from earlier claims, for the kinds this claim asks for and has
    /// nothing in yet.</summary>
    public static IReadOnlyList<DocumentOnFile> Documents(
        IReadOnlyList<(ClaimCase Claim, IReadOnlyList<CaseDocument> Documents)> earlier,
        IReadOnlyList<string> keys,
        IReadOnlyDictionary<string, string> slotsAskedFor,
        IReadOnlySet<string> slotsAlreadyFilled,
        Func<Guid, string> referenceFor)
    {
        var found = new Dictionary<string, DocumentOnFile>(StringComparer.OrdinalIgnoreCase);
        foreach (var (claim, documents) in earlier.OrderByDescending(item => item.Claim.CreatedAt))
        {
            foreach (var document in documents.Where(document => document.CountsTowardsCompleteness).OrderByDescending(document => document.UploadedAt))
            {
                var key = document.EffectiveKey;
                if (found.ContainsKey(key)
                    || !keys.Contains(key, StringComparer.OrdinalIgnoreCase)
                    || !slotsAskedFor.TryGetValue(key, out var label)
                    || slotsAlreadyFilled.Contains(key))
                {
                    continue;
                }

                found[key] = new DocumentOnFile(document.DocumentId, key, label, document.OriginalFileName, referenceFor(claim.CaseId), document.UploadedAt);
            }
        }

        return found.Values.OrderBy(item => item.SlotLabel, StringComparer.Ordinal).ToArray();
    }
}
