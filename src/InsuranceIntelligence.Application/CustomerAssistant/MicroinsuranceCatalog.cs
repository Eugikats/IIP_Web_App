namespace InsuranceIntelligence.Application.CustomerAssistant;

/// <summary>
/// The three microinsurance products Kato explains to someone without cover, and which of them fit
/// the work a person does. The explanations are the plain-language first paragraph of each
/// product's key information summary in the knowledge base (see DemoSeedService), kept here as
/// fixed text so what a participant reads never depends on a model's wording.
///
/// All sums insured are within the UGX 10 million microinsurance ceiling of the IRA's Interim
/// Micro Insurance Guidelines, 2016. These are trial products: Kato explains, records interest for a
/// licensed person to follow up, never completes a sale and never compares insurers.
/// </summary>
public static class MicroinsuranceCatalog
{
    public const string HospitalCashLine = "Hospital Cash";
    public const string BodaAccidentLine = "Boda Rider Accident";
    public const string StallStockLine = "Market Stall Stock";

    public enum OccupationKind
    {
        BodaRider,
        Trader,
        Other,
    }

    public sealed record MicroProduct(string Name, string ProductLine, string Explanation);

    public static readonly MicroProduct HospitalCash = new(
        "Nile Shield Hospital Cash",
        HospitalCashLine,
        "Nile Shield Hospital Cash pays you UGX 40,000 for each night you spend in hospital, up to 10 nights a year. " +
        "If you die, it pays your family UGX 1,000,000 for the funeral. It costs UGX 1,000 a month. " +
        "Accidents are covered from the day you join, and illness after 30 days.");

    public static readonly MicroProduct BodaAccident = new(
        "Nile Shield Boda Rider Accident",
        BodaAccidentLine,
        "Nile Shield Boda Rider Accident pays you UGX 50,000 for each day an accident stops you working, up to 15 days. " +
        "If an accident leaves you disabled for life, or you die, it pays UGX 2,500,000. " +
        "It costs UGX 6,000 a month, and you are covered from the day you join.");

    public static readonly MicroProduct StallStock = new(
        "Nile Shield Market Stall Stock",
        StallStockLine,
        "Nile Shield Market Stall Stock pays for the goods in your stall or shop if they are burnt in a fire or stolen in a break-in, up to UGX 3,000,000. " +
        "It costs UGX 5,000 a month, and cover starts 14 days after you join.");

    public static readonly IReadOnlyList<MicroProduct> All = [HospitalCash, BodaAccident, StallStock];

    private static readonly HashSet<string> BodaWords = new(StringComparer.Ordinal)
    {
        "boda", "bodaboda", "bodas", "rider", "riders", "ride", "riding", "motorcycle", "motorbike", "piki", "pikipiki",
        "safeboda", "nvuga", "okuvuga", "mvuga", "rideboda",
    };

    private static readonly HashSet<string> TraderWords = new(StringComparer.Ordinal)
    {
        "sell", "sells", "selling", "seller", "trader", "traders", "trade", "trading", "market", "stall", "shop",
        "shopkeeper", "kiosk", "vendor", "hawker", "retail", "retailer", "wholesale", "business", "businessman", "businesswoman",
        "ntunda", "ntundira", "tunda", "katale", "akatale", "dduuka", "duuka", "duka", "soko", "biashara", "nauza", "muuzaji", "mfanyabiashara",
    };

    /// <summary>The kind of work a message describes, from what the customer wrote or its English
    /// translation, or null when it names no work at all.</summary>
    public static OccupationKind? OccupationFrom(string original, string english)
    {
        var words = Words(original).Concat(Words(english)).ToHashSet(StringComparer.Ordinal);
        if (words.Overlaps(BodaWords))
        {
            return OccupationKind.BodaRider;
        }

        if (words.Overlaps(TraderWords))
        {
            return OccupationKind.Trader;
        }

        return LooksLikeWorkDescription(words) ? OccupationKind.Other : null;
    }

    public static IReadOnlyList<MicroProduct> ProductsFor(OccupationKind kind) => kind switch
    {
        OccupationKind.BodaRider => [BodaAccident, HospitalCash],
        OccupationKind.Trader => [StallStock, HospitalCash],
        _ => [HospitalCash],
    };

    public static string Explain(OccupationKind kind) => kind switch
    {
        OccupationKind.BodaRider => $"For a boda rider, two covers fit well. The first: {BodaAccident.Explanation} The second: {HospitalCash.Explanation}",
        OccupationKind.Trader => $"For your trade, two covers fit well. The first: {StallStock.Explanation} The second: {HospitalCash.Explanation}",
        _ => $"One cover fits almost everyone. {HospitalCash.Explanation}",
    };

    // "I work as a tailor", "I'm a teacher", "nkola..." name some work without naming a boda or a trade.
    private static bool LooksLikeWorkDescription(HashSet<string> words) =>
        words.Overlaps(["work", "job", "farmer", "teacher", "tailor", "builder", "driver", "carpenter", "mechanic", "cleaner", "nurse", "student", "nkola", "kazi", "nafanya", "casual", "salon", "hairdresser", "fisherman", "farming", "digging"]);

    private static IEnumerable<string> Words(string text) =>
        new string((text ?? string.Empty).ToLowerInvariant().Select(c => char.IsLetter(c) ? c : ' ').ToArray())
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);
}
