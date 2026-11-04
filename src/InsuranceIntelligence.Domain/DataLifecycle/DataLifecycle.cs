using System.Security.Cryptography;
using System.Text;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Customers;

namespace InsuranceIntelligence.Domain.DataLifecycle;

/// <summary>
/// How long an insurer keeps each kind of record before the platform deletes it (build-loop item 34). Nothing is deleted unless the insurer
/// sets a period; a period of zero or no entry means keep it.
/// </summary>
public static class RetentionClasses
{
    public const string ClosedClaims = "closed-claims";
    public const string Documents = "documents";
    public const string Communications = "communications";
    public const string Conversations = "conversations";

    public const int MaxMonths = 240;

    public static readonly IReadOnlyList<string> All = [ClosedClaims, Documents, Communications, Conversations];

    public static string Label(string retentionClass) => retentionClass switch
    {
        ClosedClaims => "Closed and settled claims",
        Documents => "Claim documents and their stored files",
        Communications => "Messages sent to and received from customers about claims",
        Conversations => "WhatsApp conversations",
        _ => retentionClass,
    };

    /// <summary>Why this set of periods cannot be saved, in words an administrator can act on.</summary>
    public static IReadOnlyList<string> Validate(IReadOnlyDictionary<string, int>? months)
    {
        var problems = new List<string>();
        foreach (var (key, value) in months ?? new Dictionary<string, int>())
        {
            if (!All.Contains(key))
            {
                problems.Add($"'{key}' is not a kind of record that can have a retention period.");
            }
            else if (value is < 0 or > MaxMonths)
            {
                problems.Add($"{Label(key)}: the period must be between 0 (keep) and {MaxMonths} months.");
            }
        }

        return problems;
    }

    public static bool SameAs(IReadOnlyDictionary<string, int>? left, IReadOnlyDictionary<string, int>? right)
    {
        static Dictionary<string, int> Normal(IReadOnlyDictionary<string, int>? source) =>
            (source ?? new Dictionary<string, int>()).Where(pair => pair.Value > 0).ToDictionary(pair => pair.Key, pair => pair.Value);
        var a = Normal(left);
        var b = Normal(right);
        return a.Count == b.Count && a.All(pair => b.TryGetValue(pair.Key, out var other) && other == pair.Value);
    }

    public static int MonthsFor(IReadOnlyDictionary<string, int>? months, string retentionClass) =>
        months is not null && months.TryGetValue(retentionClass, out var value) && value > 0 ? value : 0;
}

/// <summary>Who a request to erase personal data is about. At least one of the three must be given.</summary>
public sealed record ErasureSubject(string? Phone, string? Email, string? PolicyNumber)
{
    public string? NormalisedEmail => string.IsNullOrWhiteSpace(Email) ? null : Email.Trim().ToLowerInvariant();

    public string? NormalisedPolicy => string.IsNullOrWhiteSpace(PolicyNumber) ? null : PolicyNumber.Trim();

    public string? PhoneDigits
    {
        get
        {
            var digits = new string((Phone ?? string.Empty).Where(char.IsDigit).ToArray());
            return digits.Length >= 9 ? digits : null;
        }
    }

    public bool IsEmpty => PhoneDigits is null && NormalisedEmail is null && NormalisedPolicy is null;

    /// <summary>The last nine digits, which are the same however the number was written (with or without the country code).</summary>
    public string? PhoneTail => PhoneDigits is { } digits ? digits[^9..] : null;

    /// <summary>What was asked for, as a short fingerprint. The record of an erasure keeps this and never the phone or email itself.</summary>
    public string Fingerprint()
    {
        var text = $"{PhoneTail}|{NormalisedEmail}|{NormalisedPolicy?.ToUpperInvariant()}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..16].ToLowerInvariant();
    }

    public bool Matches(string? phone, string? email, string? policyNumber) =>
        (Phone is not null && PhoneNumbers.SameNumber(phone, Phone))
        || (NormalisedEmail is { } wanted && string.Equals(email?.Trim(), wanted, StringComparison.OrdinalIgnoreCase))
        || (NormalisedPolicy is { } policy && string.Equals(policyNumber?.Trim(), policy, StringComparison.OrdinalIgnoreCase));

    /// <summary>The same person's other contact details, taken from the claims found for them, so their chats and mail are found too.</summary>
    public ErasureSubject WithContactsFrom(IEnumerable<(string? Phone, string? Email)> contacts)
    {
        var phones = contacts.Select(contact => contact.Phone).Where(phone => !string.IsNullOrWhiteSpace(phone)).ToList();
        var emails = contacts.Select(contact => contact.Email).Where(email => !string.IsNullOrWhiteSpace(email)).ToList();
        return new ErasureSubject(Phone ?? phones.FirstOrDefault(), Email ?? emails.FirstOrDefault(), PolicyNumber);
    }
}

public static class DeletionKinds
{
    public const string Retention = "Retention";
    public const string Erasure = "Erasure";
}

/// <summary>
/// The evidence that something was deleted: when, by whom or by which rule, why, and how many of each kind. It never holds the data that was
/// deleted, nor the phone or email asked about (only a fingerprint of them). It is kept after the data is gone.
/// </summary>
public sealed record DeletionRecord
{
    public required Guid DeletionId { get; init; }
    public required TenantScope Scope { get; init; }
    public required string Kind { get; init; }

    /// <summary>For retention, the kind of record the rule covered; for erasure, a fingerprint of who was asked about.</summary>
    public required string Subject { get; init; }

    public required string Reason { get; init; }
    public required string Actor { get; init; }
    public required DateTimeOffset At { get; init; }
    public IReadOnlyDictionary<string, int> Counts { get; init; } = new Dictionary<string, int>();
    public int StoredFilesDeleted { get; init; }

    /// <summary>Claim references (CLM-XXXXXXXX) left alone because of a legal hold.</summary>
    public IReadOnlyList<string> HeldClaims { get; init; } = [];

    /// <summary>Claims that could not be fully deleted (for example the file store was unreachable) and will be tried again.</summary>
    public IReadOnlyList<string> Failures { get; init; } = [];

    public int Total => Counts.Values.Sum();
}

public sealed record DocumentRef(Guid CaseId, Guid DocumentId, string ObjectKey, string? PreviewObjectKey);

public sealed record CaseDataFootprint(IReadOnlyDictionary<string, int> Counts, IReadOnlyList<string> PackObjectKeys)
{
    public int Documents => Counts.TryGetValue(DataCategories.Documents, out var count) ? count : 0;
}

public static class DataCategories
{
    public const string Claims = "claims";
    public const string Documents = "documents";
    public const string Communications = "communications";
    public const string CustomerUpdates = "customer-updates";
    public const string Reviews = "reviews-and-tasks";
    public const string AiRecords = "ai-records";
    public const string Packs = "claims-packs";
    public const string Uploads = "unfinished-uploads";
    public const string Conversations = "whatsapp-conversations";
    public const string Messages = "whatsapp-messages";
    public const string Consents = "consents";
    public const string PortalAccess = "customer-portal-access";
    public const string Profiles = "customer-profiles";
    public const string Leads = "sales-leads";
    public const string Emails = "emails";
}

/// <summary>
/// Reads and deletes what a workspace holds, only ever inside the scope it is given. The service decides what to delete; this carries it out
/// against the database. Deleting stored files is separate (the storage providers), so a claim's rows are removed only after its files are.
/// </summary>
public interface IDataLifecycleStore
{
    Task<CaseDataFootprint> FootprintAsync(TenantScope scope, Guid caseId, CancellationToken cancellationToken = default);

    /// <summary>Deletes everything linked to one claim except the claim itself, and the claim too when asked. Returns how many of each kind went.</summary>
    Task<IReadOnlyDictionary<string, int>> DeleteCaseDataAsync(TenantScope scope, Guid caseId, bool includeClaim, CancellationToken cancellationToken = default);

    /// <summary>Counts, or deletes, what the workspace holds that is not tied to a claim but mentions this person.</summary>
    Task<IReadOnlyDictionary<string, int>> SubjectDataAsync(TenantScope scope, ErasureSubject subject, bool delete, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DocumentRef>> DocumentsBeforeAsync(TenantScope scope, DateTimeOffset cutoff, IReadOnlyCollection<Guid> excludeCases, int take, CancellationToken cancellationToken = default);

    Task<int> DeleteDocumentRowsAsync(TenantScope scope, IReadOnlyCollection<Guid> documentIds, CancellationToken cancellationToken = default);

    Task<int> DeleteCommunicationsBeforeAsync(TenantScope scope, DateTimeOffset cutoff, IReadOnlyCollection<Guid> excludeCases, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<string, int>> DeleteConversationsBeforeAsync(TenantScope scope, DateTimeOffset cutoff, IReadOnlyCollection<Guid> excludeCases, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TenantScope>> ListScopesWithRetentionAsync(CancellationToken cancellationToken = default);

    Task SaveDeletionAsync(DeletionRecord record, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DeletionRecord>> ListDeletionsAsync(TenantScope scope, int take, CancellationToken cancellationToken = default);
}
