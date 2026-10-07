using InsuranceIntelligence.Application.CustomerAssistant;
using InsuranceIntelligence.Domain.Acquisition;

namespace InsuranceIntelligence.Application.SalesLeads;

/// <summary>
/// What makes each cover suit someone, written down once so a score can be explained and changed in one
/// place. The words are matched against what the prospect said or what an imported list recorded; the
/// reasons shown to staff quote those facts back. Targeting uses work, banking products and family
/// situation only, never age, gender, religion, ethnicity or health.
/// </summary>
public static class ProductTargeting
{
    private static readonly IReadOnlyDictionary<string, Words> ByLine = new Dictionary<string, Words>(StringComparer.OrdinalIgnoreCase)
    {
        [MicroinsuranceCatalog.HospitalCashLine] = new([], [], ["child", "children", "dependant", "family", "wife", "husband"], ["hospital", "sick", "illness", "medical", "health"], 20),
        [MicroinsuranceCatalog.BodaAccidentLine] = new(["boda", "rider", "motorcycle", "piki"], [], [], ["accident", "crash", "injur"], 0),
        [MicroinsuranceCatalog.StallStockLine] = new(["stall", "market", "vendor", "hawker", "kiosk", "trader", "seller"], ["trade loan", "stock loan"], [], ["fire", "theft", "stolen", "flood"], 0),
        [MicroinsuranceCatalog.ShopLine] = new(["shop", "retail", "wholesale", "salon", "restaurant", "boutique", "pharmacy", "owner"], ["business account", "working capital", "overdraft", "stock loan", "equipment loan"], [], ["fire", "theft", "burglar", "stolen"], 0),
        [MicroinsuranceCatalog.TransitLine] = new(["driver", "transport", "trucker", "courier", "delivery", "haul", "wholesale", "fish trader"], ["trade loan", "vehicle loan", "asset loan"], [], ["transit", "road", "goods", "stolen"], 0),
        [MicroinsuranceCatalog.MaternityLine] = new(["midwife"], [], ["pregnan", "expecting", "baby", "maternity"], ["pregnan", "birth", "deliver", "antenatal"], 0),
        [MicroinsuranceCatalog.LoanProtectorLine] = new([], ["loan", "credit", "overdraft", "mortgage"], ["loan"], ["repay", "loan", "debt"], 0),
        [MicroinsuranceCatalog.FuneralLine] = new([], [], ["parents", "elderly", "spouse", "wife", "husband", "children", "grandchildren", "dependant", "family"], ["funeral", "burial", "death", "die"], 15),
        [MicroinsuranceCatalog.SchoolFeesLine] = new(["teacher"], ["school fees"], ["school", "student", "daughter", "fees"], ["school", "fees", "education"], 0),
        [MicroinsuranceCatalog.HomeContentsLine] = new([], ["mortgage", "home loan"], ["home", "house", "rent", "landlord"], ["burglar", "theft", "fire", "flood", "break"], 0),
    };

    private sealed record Words(string[] Work, string[] Bank, string[] Life, string[] Need, int BaseFit);

    /// <summary>One profile for each cover the assistant may sell.</summary>
    public static IReadOnlyList<ProductTargetProfile> ProfilesFor(IEnumerable<MicroinsuranceCatalog.MicroProduct> products) =>
        products.Select(product =>
        {
            var words = ByLine.GetValueOrDefault(product.ProductLine) ?? new([], [], [], [], 0);
            return new ProductTargetProfile(
                product.Name,
                product.ProductLine,
                product.Insurer,
                product.ChoosableLevels[0].MonthlyPremium,
                words.Work,
                words.Bank,
                words.Life,
                words.Need,
                words.BaseFit,
                product.WhoItIsFor);
        }).ToArray();
}
