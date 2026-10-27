namespace InsuranceIntelligence.Application.BankEvents;

/// <summary>
/// Something that happened to a customer at the bank, which may be a reason to offer them cover. The bank tells IIP;
/// IIP cannot see it for itself. Each type says why it matters, what it suggests, and when.
/// </summary>
public sealed record BankEventType(string Code, string Label, string Reason, string SuggestedOffer, string SuggestedTiming, bool ProtectsAnAsset = false);

public static class BankEventCatalog
{
    public static readonly IReadOnlyList<BankEventType> All =
    [
        new("loan_disbursed", "Loan disbursed", "A debt now exists that a family could inherit.", "Credit life (Loan Protector)", "2 to 3 days after"),
        new("vehicle_loan_disbursed", "Vehicle or equipment loan disbursed", "The bank financed an asset that is at risk.", "Motor or equipment cover", "Before first use, within 2 days", ProtectsAnAsset: true),
        new("business_loan_disbursed", "Business or working-capital loan disbursed", "Stock and premises are at risk.", "Shop and stock cover", "3 days after", ProtectsAnAsset: true),
        new("salary_account_opened", "Salary account opened", "A new earner with dependants.", "Funeral or personal accident cover", "7 days after"),
        new("school_fees_paid", "School fees paid", "Education is the family's biggest cost.", "School fees protector", "Two weeks before term"),
        new("savings_milestone", "Savings milestone reached", "Someone building savings.", "Education or endowment cover", "The same week"),
        new("standing_order_started", "Standing order started", "Someone committing to regular saving.", "Education or endowment cover", "The same week"),
        new("loan_repaid", "Loan repaid", "A natural moment to talk.", "A new cover, or a thank you and a new offer", "At repayment"),
        new("mortgage_taken", "Mortgage taken", "A large debt and an asset.", "Mortgage life and property cover", "Within a week", ProtectsAnAsset: true),
    ];

    public static BankEventType? Find(string? codeOrLabel)
    {
        if (string.IsNullOrWhiteSpace(codeOrLabel))
        {
            return null;
        }

        var wanted = codeOrLabel.Trim();
        var asCode = wanted.ToLowerInvariant().Replace(' ', '_').Replace('-', '_');
        return All.FirstOrDefault(type => type.Code == asCode || string.Equals(type.Label, wanted, StringComparison.OrdinalIgnoreCase));
    }
}

public enum EventSource
{
    /// <summary>A person at the bank, or at IIP for a demonstration, entered it on the form.</summary>
    Recorded,

    /// <summary>It came in a file the bank supplied.</summary>
    FileImport,

    /// <summary>The bank's system sent it. Not built yet.</summary>
    Feed,
}

public enum EventProcessingState
{
    /// <summary>Waiting for the rules to look at it.</summary>
    Pending,

    /// <summary>The rules have looked at it. What they decided is in the decision log.</summary>
    Processed,
}

/// <summary>
/// One event. It carries who (a phone number), what, when, and only the few safe details a rule needs. Balances,
/// transaction lists and account numbers are not accepted: the bank keeps those.
/// </summary>
public sealed record WorkspaceEvent(
    string EventId,
    string EventType,
    string Phone,
    DateTimeOffset OccurredAt,
    DateTimeOffset ReceivedAt,
    EventSource Source,
    string? SourceReference,
    IReadOnlyDictionary<string, string> Details,
    string RecordedBy,
    EventProcessingState State = EventProcessingState.Pending,
    DateTimeOffset? ProcessedAt = null)
{
    /// <summary>The only details an event may carry. Anything else is dropped on the way in and reported.</summary>
    public static readonly IReadOnlySet<string> AllowedDetails =
        new HashSet<string>(StringComparer.Ordinal) { "amount", "term_months", "product_code", "branch" };

    public decimal? Amount => Details.TryGetValue("amount", out var value) && decimal.TryParse(value, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var amount) ? amount : null;

    public int? TermMonths => Details.TryGetValue("term_months", out var value) && int.TryParse(value, out var months) ? months : null;
}

/// <summary>What a person or a file supplies for one event, before it is checked.</summary>
public sealed record EventInput(
    string? EventType,
    string? Phone,
    DateTimeOffset? OccurredAt,
    string? ExternalId = null,
    decimal? Amount = null,
    int? TermMonths = null,
    string? ProductCode = null,
    string? Branch = null);

public sealed class EventException(string message) : Exception(message);

public sealed record EventRecordResult(WorkspaceEvent Event, bool Created);

public sealed record EventRowProblem(int Row, string Reason);

public sealed record EventImportResult(
    int RowsRead,
    int Created,
    int Duplicates,
    IReadOnlyList<EventRowProblem> Rejected,
    IReadOnlyList<string> IgnoredColumns);
