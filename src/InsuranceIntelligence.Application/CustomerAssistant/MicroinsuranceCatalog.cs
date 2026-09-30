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
    public const string ShopLine = "Small Business Shop";
    public const string TransitLine = "Goods in Transit";
    public const string MaternityLine = "Mama Care Maternity";

    /// <summary>One level of cover a customer can choose: the most it pays, and its monthly price.</summary>
    public sealed record CoverLevel(decimal SumInsured, decimal MonthlyPremium);

    public enum OccupationKind
    {
        BodaRider,
        Trader,
        Other,
    }

    public sealed record MicroProduct(string Name, string ProductLine, string Explanation)
    {
        /// <summary>Short code used in policy numbers (NSA/{Code}/{year}/{sequence}).</summary>
        public string Code { get; init; } = "MI";
        public decimal MonthlyPremium { get; init; }
        public decimal SumInsured { get; init; }
        public string WhoItIsFor { get; init; } = string.Empty;
        public string WaitingPeriod { get; init; } = string.Empty;
        public string PapersForAClaim { get; init; } = string.Empty;

        /// <summary>The levels a customer can choose between, smallest first. A product with one
        /// fixed price has a single level.</summary>
        public IReadOnlyList<CoverLevel> Levels { get; init; } = [];

        public IReadOnlyList<CoverLevel> ChoosableLevels => Levels.Count > 0 ? Levels : [new CoverLevel(SumInsured, MonthlyPremium)];

        /// <summary>The level for an amount the customer asked for: the smallest level that covers
        /// it, or the largest when it is more than any level offers.</summary>
        public CoverLevel LevelFor(decimal? wantedSumInsured) =>
            wantedSumInsured is not { } wanted || wanted <= 0
                ? ChoosableLevels[^1]
                : ChoosableLevels.FirstOrDefault(level => level.SumInsured >= wanted) ?? ChoosableLevels[^1];
    }

    public static readonly MicroProduct HospitalCash = new(
        "Nile Shield Hospital Cash",
        HospitalCashLine,
        "Nile Shield Hospital Cash pays you UGX 40,000 for each night you spend in hospital, up to 10 nights a year. " +
        "If you die, it pays your family UGX 1,000,000 for the funeral. It costs UGX 1,000 a month. " +
        "Accidents are covered from the day you join, and illness after 30 days.")
    {
        Code = "HC",
        MonthlyPremium = 1_000m,
        SumInsured = 1_400_000m,
        WhoItIsFor = "Anyone aged 18 to 65, whatever their work.",
        WaitingPeriod = "Accidents from the day you join; illness after 30 days; pregnancy after 9 months.",
        PapersForAClaim = "The hospital discharge note or admission letter; for a funeral, the burial permit or an LC1 letter.",
    };

    public static readonly MicroProduct BodaAccident = new(
        "Nile Shield Boda Rider Accident",
        BodaAccidentLine,
        "Nile Shield Boda Rider Accident pays you UGX 50,000 for each day an accident stops you working, up to 15 days. " +
        "If an accident leaves you disabled for life, or you die, it pays UGX 2,500,000. " +
        "It costs UGX 6,000 a month, and you are covered from the day you join.")
    {
        Code = "BA",
        MonthlyPremium = 6_000m,
        SumInsured = 3_250_000m,
        WhoItIsFor = "Boda boda riders aged 18 to 60 with a riding permit.",
        WaitingPeriod = "None: covered from the day you join.",
        PapersForAClaim = "The police report of the accident and the medical form from the clinic or hospital.",
    };

    public static readonly MicroProduct StallStock = new(
        "Nile Shield Market Stall Stock",
        StallStockLine,
        "Nile Shield Market Stall Stock pays for the goods in your stall or shop if they are burnt in a fire or stolen in a break-in, up to UGX 3,000,000. " +
        "It costs UGX 5,000 a month, and cover starts 14 days after you join.")
    {
        Code = "MS",
        MonthlyPremium = 5_000m,
        SumInsured = 3_000_000m,
        WhoItIsFor = "Market vendors and stall owners.",
        WaitingPeriod = "14 days after you join.",
        PapersForAClaim = "The police or fire brigade report and a list of the goods you lost with what they cost.",
    };

    public static readonly MicroProduct ShopCover = new(
        "Nile Shield Small Business Shop Cover",
        ShopLine,
        "Nile Shield Small Business Shop Cover pays for your stock, shelves and equipment if they are burnt in a fire, damaged by lightning, or stolen in a break-in. " +
        "You choose the level that matches what your stock is worth: up to UGX 2,000,000 for UGX 5,000 a month, up to UGX 4,000,000 for UGX 9,000 a month, or up to UGX 8,000,000 for UGX 15,000 a month. " +
        "You pay the first UGX 100,000 of each claim yourself, and cover starts 7 days after you join.")
    {
        Code = "SB",
        MonthlyPremium = 15_000m,
        SumInsured = 8_000_000m,
        Levels = [new(2_000_000m, 5_000m), new(4_000_000m, 9_000m), new(8_000_000m, 15_000m)],
        WhoItIsFor = "Owners of a shop, boutique, salon, pharmacy, hardware or similar small business with a fixed lockable premises.",
        WaitingPeriod = "7 days after you join.",
        PapersForAClaim = "The police report (or fire brigade report for a fire), a list of what was lost or damaged with its value, and photos of the premises.",
    };

    public static readonly MicroProduct GoodsInTransit = new(
        "Nile Shield Goods in Transit",
        TransitLine,
        "Nile Shield Goods in Transit pays for stock you are carrying from your supplier to your shop or stall, or delivering to a customer, if it is stolen or destroyed in a road accident on the way. " +
        "Up to UGX 1,000,000 per trip for UGX 3,000 a month, or up to UGX 2,000,000 per trip for UGX 5,000 a month. Cover starts 7 days after you join.")
    {
        Code = "GT",
        MonthlyPremium = 5_000m,
        SumInsured = 2_000_000m,
        Levels = [new(1_000_000m, 3_000m), new(2_000_000m, 5_000m)],
        WhoItIsFor = "Traders who carry stock from suppliers (for example Kikuubo or Owino) by boda, taxi or pick-up, and anyone who delivers goods to customers.",
        WaitingPeriod = "7 days after you join.",
        PapersForAClaim = "The police report, the supplier's receipt or delivery note for the goods, and a short account of the trip.",
    };

    public static readonly MicroProduct MamaCare = new(
        "Nile Shield Mama Care Maternity",
        MaternityLine,
        "Nile Shield Mama Care pays towards the cost of giving birth at a registered hospital or health centre: up to UGX 600,000 for a normal delivery, " +
        "or up to UGX 2,000,000 for a caesarean section, paid to you or straight to the hospital. It costs UGX 12,000 a month. " +
        "Delivery is covered once you have had the cover for 10 months, so it is best taken before or at the very start of a pregnancy.")
    {
        Code = "MC",
        MonthlyPremium = 12_000m,
        SumInsured = 2_000_000m,
        WhoItIsFor = "Women aged 18 to 45, or a husband or partner covering the mother of their child.",
        WaitingPeriod = "10 months for childbirth; accidents during pregnancy from the day you join.",
        PapersForAClaim = "The hospital discharge summary or birth notification, and the hospital bill or receipts.",
    };

    public static readonly IReadOnlyList<MicroProduct> All = [HospitalCash, BodaAccident, StallStock, ShopCover, GoodsInTransit, MamaCare];

    public static MicroProduct? FindByLineOrName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var wanted = value.Trim();
        return All.FirstOrDefault(p =>
            string.Equals(p.ProductLine, wanted, StringComparison.OrdinalIgnoreCase)
            || string.Equals(p.Name, wanted, StringComparison.OrdinalIgnoreCase)
            || string.Equals(p.Code, wanted, StringComparison.OrdinalIgnoreCase)
            || p.Name.Contains(wanted, StringComparison.OrdinalIgnoreCase)
            || wanted.Contains(p.ProductLine, StringComparison.OrdinalIgnoreCase));
    }

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
