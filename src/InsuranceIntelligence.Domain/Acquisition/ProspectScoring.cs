using System.Globalization;
using System.Text.RegularExpressions;

namespace InsuranceIntelligence.Domain.Acquisition;

public enum ReadinessBand
{
    Cold,
    Early,
    Warm,
    Qualified,
    HighPriority,
}

/// <summary>One cover that suits a prospect, with the plain reasons it does. Reasons are built from
/// recorded facts only, so a person can check each one against the record.</summary>
public sealed record ProductFit(
    string Product,
    string ProductLine,
    string Insurer,
    int Score,
    IReadOnlyList<string> Reasons,
    Guid? CitationDocumentId = null,
    string? CitationTitle = null);

/// <summary>The result of scoring one prospect. A score orders follow-up; it never withholds
/// information from a customer and is never an underwriting or pricing signal.</summary>
public sealed record ProspectTriage(
    DateTimeOffset ScoredAt,
    int Score,
    ReadinessBand Band,
    string Confidence,
    IReadOnlyList<ProductFit> Fits,
    IReadOnlyList<string> Signals,
    IReadOnlyList<string> Missing);

/// <summary>What the insurer has said makes a cover suit someone. Owned by the insurer; the defaults
/// come from each product's own description.</summary>
public sealed record ProductTargetProfile(
    string Product,
    string ProductLine,
    string Insurer,
    decimal MonthlyPremium,
    IReadOnlyList<string> OccupationWords,
    IReadOnlyList<string> BankWords,
    IReadOnlyList<string> LifeWords,
    IReadOnlyList<string> NeedWords,
    int BaseFit = 0,
    string? WhoItIsFor = null);

public sealed record ProspectInputs(
    IReadOnlyList<ProspectFact> Facts,
    IReadOnlyList<ProspectInterest> Interests,
    bool? BankCustomer,
    bool HoldsCover,
    bool Registered,
    ContactConsentStatus Consent,
    bool HasPhone,
    DateTimeOffset? LastInteractionAt,
    int CustomerMessages,
    bool Applied);

/// <summary>
/// Scores a prospect from fixed rules over recorded facts. Five parts add up to 100: need (35),
/// how well a cover fits (25), capacity to pay (15), relationship with the bank and insurer (15) and
/// whether they can be contacted (10). Sensitive attributes are never inputs, because they are never
/// recorded. The same record always scores the same, so a result can be explained and tested.
/// </summary>
public static partial class ProspectScoring
{
    public static ProspectTriage Score(ProspectInputs input, IReadOnlyList<ProductTargetProfile> profiles, DateTimeOffset now)
    {
        var text = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var fact in input.Facts)
        {
            text[fact.Key] = fact.Value;
        }

        string Fact(string key) => text.GetValueOrDefault(key) ?? string.Empty;
        var corpus = string.Join(' ', input.Facts.Select(fact => fact.Value)).ToLowerInvariant();
        var signals = new List<string>();

        // Need: what they asked about and how keen they are.
        var strongest = input.Interests.Count == 0 ? (InterestStrength?)null : input.Interests.Max(interest => interest.Strength);
        var need = strongest switch
        {
            InterestStrength.WantsIt => 35,
            InterestStrength.Interested => 25,
            InterestStrength.Mentioned => 12,
            _ => 0,
        };
        if (strongest is InterestStrength.WantsIt) signals.Add("Said they want a cover");
        else if (strongest is InterestStrength.Interested) signals.Add("Interested in a cover");
        else if (strongest is InterestStrength.Mentioned) signals.Add("Asked about a cover");
        if (!string.IsNullOrWhiteSpace(Fact("worries")))
        {
            need = Math.Min(35, need + 5);
            signals.Add("Described a worry a cover would address");
        }

        var onlyNotNow = input.Interests.Count > 0 && input.Interests.All(interest => interest.Strength == InterestStrength.NotNow);

        // Fit: the covers that suit them, best first.
        var fits = profiles
            .Select(profile => FitFor(profile, input, corpus, Fact))
            .Where(fit => fit.Score >= 20)
            .OrderByDescending(fit => fit.Score)
            .ThenBy(fit => fit.Product, StringComparer.OrdinalIgnoreCase)
            .Take(2)
            .ToArray();
        var fit = fits.Length == 0 ? 0 : (int)Math.Round(fits[0].Score / 100d * 25);

        // Capacity: can they pay the monthly price of the cover that fits best.
        var capacity = 0;
        var topPremium = fits.Length == 0 ? 0m : profiles.First(profile => profile.Product == fits[0].Product).MonthlyPremium;
        var budget = ParseUgx(Fact("monthly_budget"));
        if (budget is { } stated && topPremium > 0)
        {
            capacity = stated >= topPremium ? 15 : 6;
            signals.Add(stated >= topPremium ? "Can pay the monthly price" : "Budget is below the monthly price of the best-fitting cover");
        }
        else if (!string.IsNullOrWhiteSpace(Fact("income_band")))
        {
            capacity = 8;
            signals.Add("Income is known");
        }

        // Relationship: they already trust the bank or the insurer.
        var relationship = 0;
        if (input.BankCustomer == true)
        {
            relationship += 8;
            signals.Add("Banks with the partner bank");
        }

        if (input.HoldsCover)
        {
            relationship += 7;
            signals.Add("Already holds cover with the insurer");
        }
        else if (input.Registered)
        {
            relationship += 3;
        }

        // Contactability: they have agreed, and we can reach them.
        var contact = 0;
        switch (input.Consent)
        {
            case ContactConsentStatus.Agreed:
                contact += 6;
                signals.Add("Agreed to be contacted");
                break;
            case ContactConsentStatus.Declined:
            case ContactConsentStatus.Withdrawn:
                signals.Add("Does not want to be contacted");
                break;
        }

        if (input.HasPhone) contact += 2;
        if (input.LastInteractionAt is { } last && now - last <= TimeSpan.FromDays(7)) contact += 2;

        var score = Math.Clamp(need + fit + capacity + relationship + contact, 0, 100);
        if (onlyNotNow) score = Math.Min(score, 20);
        if (input.Consent is ContactConsentStatus.Declined or ContactConsentStatus.Withdrawn) score = Math.Min(score, 44);

        var known = ProspectFacts.Fields.Count(field => field.Discovery && !string.IsNullOrWhiteSpace(Fact(field.Key)));
        var confidence = known >= 5 || input.CustomerMessages >= 6 ? "High" : known >= 3 || input.CustomerMessages >= 3 ? "Medium" : "Low";

        return new ProspectTriage(
            now,
            score,
            BandFor(score),
            confidence,
            fits,
            signals,
            ProspectFacts.Missing(input.Facts).Select(field => field.Label).ToArray());
    }

    public static ReadinessBand BandFor(int score) => score switch
    {
        >= 80 => ReadinessBand.HighPriority,
        >= 65 => ReadinessBand.Qualified,
        >= 45 => ReadinessBand.Warm,
        >= 25 => ReadinessBand.Early,
        _ => ReadinessBand.Cold,
    };

    public static string Label(ReadinessBand band) => band switch
    {
        ReadinessBand.HighPriority => "High priority",
        ReadinessBand.Qualified => "Qualified",
        ReadinessBand.Warm => "Warm",
        ReadinessBand.Early => "Early interest",
        _ => "Cold",
    };

    private static ProductFit FitFor(ProductTargetProfile profile, ProspectInputs input, string corpus, Func<string, string> fact)
    {
        var score = profile.BaseFit;
        var reasons = new List<string>();

        var work = $"{fact("occupation")} {fact("business")}".ToLowerInvariant();
        if (Matches(work, profile.OccupationWords) is { } workWord)
        {
            score += 45;
            reasons.Add($"Works as {Trim(fact("occupation"), fact("business"))}, which this cover is for ({workWord})");
        }

        var bank = fact("bank_products").ToLowerInvariant();
        if (Matches(bank, profile.BankWords) is { } bankWord)
        {
            score += 40;
            reasons.Add($"Holds {Trim(fact("bank_products"))} with the bank ({bankWord})");
        }

        var life = $"{fact("dependants")} {fact("existing_cover")} {corpus}".ToLowerInvariant();
        if (Matches(life, profile.LifeWords) is { } lifeWord)
        {
            score += 20;
            reasons.Add(string.IsNullOrWhiteSpace(fact("dependants"))
                ? $"Their situation points to this cover ({lifeWord})"
                : $"Has {fact("dependants")}, which this cover protects");
        }

        var needText = $"{fact("worries")} {fact("timing")}".ToLowerInvariant();
        if (Matches(needText, profile.NeedWords) is { } needWord)
        {
            score += 25;
            reasons.Add($"Described a worry this cover answers ({needWord})");
        }

        var asked = input.Interests.FirstOrDefault(interest => string.Equals(interest.Product, profile.Product, StringComparison.OrdinalIgnoreCase));
        if (asked is not null && asked.Strength != InterestStrength.NotNow)
        {
            score += asked.Strength == InterestStrength.WantsIt ? 40 : asked.Strength == InterestStrength.Interested ? 30 : 20;
            reasons.Add(asked.Strength == InterestStrength.WantsIt ? "Said they want it" : "Asked about it");
        }

        if (reasons.Count == 0 && profile.BaseFit > 0 && profile.WhoItIsFor is { } who)
        {
            reasons.Add(who);
        }

        return new ProductFit(profile.Product, profile.ProductLine, profile.Insurer, Math.Clamp(score, 0, 100), reasons);
    }

    private static string? Matches(string text, IReadOnlyList<string> words)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        foreach (var word in words)
        {
            if (text.Contains(word, StringComparison.OrdinalIgnoreCase)) return word;
        }

        return null;
    }

    private static string Trim(params string[] values) =>
        string.Join(", ", values.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim().TrimEnd('.')));

    /// <summary>The first money amount in text such as "maybe 10,000 a month" or "UGX 300k", in shillings.</summary>
    public static decimal? ParseUgx(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var match = AmountPattern().Match(text.ToLowerInvariant().Replace(",", string.Empty));
        if (!match.Success || !decimal.TryParse(match.Groups[1].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var number)) return null;
        return match.Groups[2].Value switch
        {
            "k" or "thousand" => number * 1_000m,
            "m" or "million" => number * 1_000_000m,
            _ => number,
        };
    }

    [GeneratedRegex(@"(\d+(?:\.\d+)?)\s?(k|thousand|m|million)?\b")]
    private static partial Regex AmountPattern();
}
