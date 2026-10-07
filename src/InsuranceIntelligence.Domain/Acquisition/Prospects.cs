using System.Text.RegularExpressions;

namespace InsuranceIntelligence.Domain.Acquisition;

/// <summary>Where a fact about a prospect came from. Shown next to the fact, so staff can tell what
/// the customer said from what a list or a colleague supplied.</summary>
public enum ProspectFactSource
{
    Customer,
    ImportedList,
    Staff,
}

/// <summary>How keen a prospect is on one cover, from what they said.</summary>
public enum InterestStrength
{
    NotNow,
    Mentioned,
    Interested,
    WantsIt,
}

/// <summary>Where a prospect is in the journey. Derived by <see cref="LeadStages.Derive"/> from what is
/// recorded, never set by a model.</summary>
public enum LeadStage
{
    New,
    GettingToKnow,
    Interested,
    Considering,
    ReadyToApply,
    Applied,
    WithAPerson,
    NotNow,
}

public enum ContactConsentStatus
{
    NotRecorded,
    Agreed,
    Declined,
    Withdrawn,
}

/// <summary>What the insurer already has with this person.</summary>
public enum ProspectRelationship
{
    NewToInsurer,
    Registered,
    ExistingCustomer,
}

/// <summary>One thing known about a prospect, with where it came from and, for something the customer
/// said in a conversation, the message it came from.</summary>
public sealed record ProspectFact(
    string Key,
    string Value,
    ProspectFactSource Source,
    DateTimeOffset RecordedAt,
    Guid? TurnId = null,
    string? RecordedBy = null);

public sealed record ProspectInterest(
    string Product,
    InterestStrength Strength,
    string Reason,
    DateTimeOffset RecordedAt,
    Guid? TurnId = null);

public sealed record ProspectActivity(DateTimeOffset At, string Text, string Actor);

public sealed record ProspectNote(Guid NoteId, string Text, string AuthorUserId, string AuthorName, DateTimeOffset At);

/// <summary>Agreement to be contacted about cover, kept with the exact words the person was asked, so
/// it can be shown to a regulator as given. Separate from the basis for answering someone who wrote
/// first.</summary>
public sealed record ContactConsent(
    ContactConsentStatus Status,
    DateTimeOffset At,
    string Wording,
    string Channel,
    Guid? TurnId = null);

/// <summary>
/// The facts IIP keeps about a prospect. Only these keys are stored: a model asking to record anything
/// else is refused, which keeps sensitive attributes (religion, ethnicity, health conditions, disability,
/// political views) out of the record and out of every score.
/// </summary>
public static partial class ProspectFacts
{
    public sealed record Field(string Key, string Label, bool Discovery);

    public static readonly IReadOnlyList<Field> Fields =
    [
        new("occupation", "Work", true),
        new("business", "What they sell or do", false),
        new("area", "Area", true),
        new("income_band", "Income or turnover", true),
        new("dependants", "Who depends on them", true),
        new("existing_cover", "Cover they already have", true),
        new("worries", "What worries them", true),
        new("monthly_budget", "What they could pay a month", true),
        new("timing", "When they want cover", true),
        new("age_band", "Age", false),
        new("email", "Email", false),
    ];

    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "the", "and", "for", "with", "has", "have", "had", "about", "they", "their", "them", "that", "this",
        "are", "was", "were", "who", "what", "from", "per", "month", "monthly", "year", "years", "ugx", "shs",
        "some", "any", "not", "but", "also", "very", "just", "she", "his", "her", "him",
    };

    public static Field? Find(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        var normalised = key.Trim().ToLowerInvariant().Replace(' ', '_').Replace('-', '_');
        return Fields.FirstOrDefault(field => field.Key == normalised);
    }

    public static string Label(string key) => Find(key)?.Label ?? key;

    /// <summary>The discovery questions not yet answered, in the order a conversation would reach them.</summary>
    public static IReadOnlyList<Field> Missing(IEnumerable<ProspectFact> facts)
    {
        var known = facts.Select(fact => fact.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return Fields.Where(field => field.Discovery && !known.Contains(field.Key)).ToArray();
    }

    /// <summary>The latest value of each fact: a new value for a key replaces the old one.</summary>
    public static IReadOnlyList<ProspectFact> Merge(IEnumerable<ProspectFact> existing, IEnumerable<ProspectFact> incoming)
    {
        var byKey = existing.ToDictionary(fact => fact.Key, StringComparer.OrdinalIgnoreCase);
        foreach (var fact in incoming)
        {
            byKey[fact.Key] = fact;
        }

        return Fields
            .Where(field => byKey.ContainsKey(field.Key))
            .Select(field => byKey[field.Key])
            .ToArray();
    }

    /// <summary>
    /// True when the value echoes something the customer actually wrote: at least one meaningful word or
    /// number of it appears in their messages. Stops a model recording a fact the customer never gave.
    /// "300k" and "300,000" count as the same number.
    /// </summary>
    public static bool IsGroundedIn(string value, IEnumerable<string?> customerTexts)
    {
        var corpus = Normalise(string.Join(" \n ", customerTexts.Where(text => !string.IsNullOrWhiteSpace(text))));
        if (corpus.Length == 0)
        {
            return false;
        }

        var tokens = WordPattern().Matches(Normalise(value))
            .Select(match => match.Value)
            .Where(token => token.Any(char.IsDigit) || (token.Length >= 3 && !StopWords.Contains(token)))
            .ToArray();
        return tokens.Length > 0 && tokens.Any(token => corpus.Contains(token, StringComparison.Ordinal));
    }

    private static string Normalise(string text)
    {
        var lower = text.ToLowerInvariant();
        lower = DigitGroupPattern().Replace(lower, "$1$2");
        lower = ThousandsPattern().Replace(lower, match => match.Groups[1].Value + "000");
        lower = MillionsPattern().Replace(lower, match => match.Groups[1].Value + "000000");
        return lower;
    }

    [GeneratedRegex(@"[\p{L}\p{N}]+")]
    private static partial Regex WordPattern();

    [GeneratedRegex(@"(\d),(\d{3})")]
    private static partial Regex DigitGroupPattern();

    [GeneratedRegex(@"\b(\d+)\s?(k|thousand)\b")]
    private static partial Regex ThousandsPattern();

    [GeneratedRegex(@"\b(\d+)\s?(m|million)\b")]
    private static partial Regex MillionsPattern();
}

/// <summary>The lead stage, derived from what is recorded. A rule, not a model's opinion, so the same
/// record always shows the same stage and it can be tested.</summary>
public static class LeadStages
{
    public static LeadStage Derive(
        IReadOnlyCollection<ProspectFact> facts,
        IReadOnlyCollection<ProspectInterest> interests,
        bool applied,
        bool withAPerson)
    {
        if (withAPerson)
        {
            return LeadStage.WithAPerson;
        }

        if (applied)
        {
            return LeadStage.Applied;
        }

        var discovered = facts.Count(fact => ProspectFacts.Find(fact.Key)?.Discovery == true);
        if (interests.Count > 0)
        {
            var strongest = interests.Max(interest => interest.Strength);
            switch (strongest)
            {
                case InterestStrength.NotNow:
                    return LeadStage.NotNow;
                case InterestStrength.WantsIt:
                    return LeadStage.ReadyToApply;
                case InterestStrength.Interested:
                    return discovered >= 3 ? LeadStage.Considering : LeadStage.Interested;
            }
        }

        return discovered > 0 || interests.Count > 0 ? LeadStage.GettingToKnow : LeadStage.New;
    }

    public static string Label(LeadStage stage) => stage switch
    {
        LeadStage.New => "New",
        LeadStage.GettingToKnow => "Getting to know them",
        LeadStage.Interested => "Interested",
        LeadStage.Considering => "Considering",
        LeadStage.ReadyToApply => "Ready to apply",
        LeadStage.Applied => "Applied",
        LeadStage.WithAPerson => "With a person",
        LeadStage.NotNow => "Not now",
        _ => stage.ToString(),
    };

    public static string Label(ProspectRelationship relationship) => relationship switch
    {
        ProspectRelationship.NewToInsurer => "New to insurer",
        ProspectRelationship.Registered => "Registered",
        ProspectRelationship.ExistingCustomer => "Existing customer",
        _ => relationship.ToString(),
    };

    public static string Label(InterestStrength strength) => strength switch
    {
        InterestStrength.NotNow => "Not now",
        InterestStrength.Mentioned => "Asked about it",
        InterestStrength.Interested => "Interested",
        InterestStrength.WantsIt => "Wants it",
        _ => strength.ToString(),
    };

    public static string Label(ContactConsentStatus status) => status switch
    {
        ContactConsentStatus.NotRecorded => "Not recorded",
        ContactConsentStatus.Agreed => "Agreed",
        ContactConsentStatus.Declined => "Declined",
        ContactConsentStatus.Withdrawn => "Withdrawn",
        _ => status.ToString(),
    };
}

/// <summary>A phone number as staff lists show it: the country code's first two digits and the last
/// two, for example 25***89. The full number is never sent to a list.</summary>
public static class ContactMask
{
    public static string Mask(string? phone)
    {
        var digits = new string((phone ?? string.Empty).Where(char.IsDigit).ToArray());
        return digits.Length < 6 ? "Hidden" : $"{digits[..2]}***{digits[^2..]}";
    }
}
