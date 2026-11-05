namespace InsuranceIntelligence.Application.CustomerAssistant;

/// <summary>
/// Whose assistant Kato is. By default he is Nile Shield Assurance's assistant and sells only its
/// covers. Configured for a bank (bancassurance), he is the bank's assistant and sells the covers of
/// the bank's partner insurers, always saying which insurer provides each one, because the bank is the
/// distributor and the insurer is the one that pays.
///
/// With several workspaces on one deployment (build-loop item 27) each workspace has its own brand and its
/// own covers: its administrator names the organisation in the operating settings, and the covers are the
/// workspace's published products that carry an offer. <see cref="Use"/> binds that brand to the work being
/// done for one workspace, so the fixed replies and greetings that read <see cref="Current"/> speak for the
/// right organisation and no other. The deployment's own default (Assistant:Organisation, Assistant:ShortName,
/// Assistant:PartnerInsurers) is what <see cref="Current"/> returns outside any such binding.
/// </summary>
public sealed record KatoBrand(
    string Organisation,
    string ShortName,
    IReadOnlyList<string> PartnerInsurers,
    LineFocus LineFocus = LineFocus.Both)
{
    public static readonly KatoBrand NileShield = new(MicroinsuranceCatalog.NileShield, "Nile Shield", []);

    private static readonly AsyncLocal<KatoBrand?> Bound = new();
    private static KatoBrand deploymentDefault = NileShield;

    /// <summary>The brand for the work in hand: the workspace's, when one is bound (see <see cref="Use"/>), otherwise
    /// the deployment's default. Set at start-up from configuration.</summary>
    public static KatoBrand Current
    {
        get => Bound.Value ?? deploymentDefault;
        set => deploymentDefault = value;
    }

    /// <summary>The deployment's own default, whatever workspace is bound.</summary>
    public static KatoBrand Default => deploymentDefault;

    /// <summary>Makes this the brand for everything done on this flow of execution until the result is disposed. A
    /// value set here does not leak to other requests or jobs running at the same time.</summary>
    public static IDisposable Use(KatoBrand brand)
    {
        var previous = Bound.Value;
        Bound.Value = brand;
        return new Restore(previous);
    }

    private sealed class Restore(KatoBrand? previous) : IDisposable
    {
        public void Dispose() => Bound.Value = previous;
    }

    /// <summary>The covers a workspace has configured for the assistant to sell. When set, these and only these are
    /// the covers. When null the built-in demonstration catalogue is filtered by insurer, as before.</summary>
    public IReadOnlyList<MicroinsuranceCatalog.MicroProduct>? Offered { get; init; }

    /// <summary>Workspace-approved description of the organisation's non-insurance services.</summary>
    public string? About { get; init; }

    public bool IsBancassurance => PartnerInsurers.Count > 0;

    /// <summary>The covers Kato may explain and take applications for.</summary>
    public IReadOnlyList<MicroinsuranceCatalog.MicroProduct> Products =>
        (Offered ?? MicroinsuranceCatalog.Offered
            .Where(product => IsBancassurance
                ? PartnerInsurers.Contains(product.Insurer, StringComparer.OrdinalIgnoreCase)
                : string.Equals(product.Insurer, Organisation, StringComparison.OrdinalIgnoreCase))
            .ToArray())
            .Where(product => LineFocus.Allows(product.InsuranceClass))
            .ToArray();

    /// <summary>One of this brand's covers, by product line, name or code. Null when it is not one Kato sells.</summary>
    public MicroinsuranceCatalog.MicroProduct? Find(string? value) => MicroinsuranceCatalog.FindIn(Products, value);

    /// <summary>The covers that fit the kind of work someone does, from this brand's own covers. The usual pairings
    /// (a boda rider with accident and hospital cash, a trader with stall stock and hospital cash) apply when the
    /// brand sells those lines; otherwise it offers its first two covers.</summary>
    public IReadOnlyList<MicroinsuranceCatalog.MicroProduct> ProductsFor(MicroinsuranceCatalog.OccupationKind kind)
    {
        var all = Products;
        var preferred = PreferredLines(kind)
            .Select(line => all.FirstOrDefault(product => string.Equals(product.ProductLine, line, StringComparison.OrdinalIgnoreCase)))
            .OfType<MicroinsuranceCatalog.MicroProduct>()
            .ToArray();
        return preferred.Length > 0 ? preferred : all.Take(2).ToArray();
    }

    /// <summary>Plain words about the covers that fit, built from the workspace's own explanations.</summary>
    public string Explain(MicroinsuranceCatalog.OccupationKind kind)
    {
        var picks = ProductsFor(kind);
        // The wording that speaks of this kind of work is only honest when the covers were chosen for it.
        var chosenForKind = PreferredLines(kind).Any(line => picks.Any(pick => string.Equals(pick.ProductLine, line, StringComparison.OrdinalIgnoreCase)));
        return (chosenForKind ? kind : MicroinsuranceCatalog.OccupationKind.Other, picks.Count) switch
        {
            (_, 0) => string.Empty,
            (MicroinsuranceCatalog.OccupationKind.BodaRider, 2) => $"For a boda rider, two covers fit well. The first: {picks[0].Explanation} The second: {picks[1].Explanation}",
            (MicroinsuranceCatalog.OccupationKind.Trader, 2) => $"For your trade, two covers fit well. The first: {picks[0].Explanation} The second: {picks[1].Explanation}",
            (MicroinsuranceCatalog.OccupationKind.Other, 1) when string.Equals(picks[0].ProductLine, MicroinsuranceCatalog.HospitalCashLine, StringComparison.OrdinalIgnoreCase) =>
                $"One cover fits almost everyone. {picks[0].Explanation}",
            (_, 1) => chosenForKind ? $"One cover fits well. {picks[0].Explanation}" : $"One cover may fit. {picks[0].Explanation}",
            _ => $"Two covers may fit. The first: {picks[0].Explanation} The second: {picks[1].Explanation}",
        };
    }

    private static string[] PreferredLines(MicroinsuranceCatalog.OccupationKind kind) => kind switch
    {
        MicroinsuranceCatalog.OccupationKind.BodaRider => [MicroinsuranceCatalog.BodaAccidentLine, MicroinsuranceCatalog.HospitalCashLine],
        MicroinsuranceCatalog.OccupationKind.Trader => [MicroinsuranceCatalog.StallStockLine, MicroinsuranceCatalog.HospitalCashLine],
        _ => [MicroinsuranceCatalog.HospitalCashLine],
    };

    public static KatoBrand FromSettings(string? organisation, string? shortName, string? partnerInsurers)
    {
        if (string.IsNullOrWhiteSpace(organisation))
        {
            return NileShield;
        }

        var partners = (partnerInsurers ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToArray();
        return new KatoBrand(organisation.Trim(), string.IsNullOrWhiteSpace(shortName) ? organisation.Trim() : shortName.Trim(), partners);
    }
}

/// <summary>Which insurance lines the workspace offers through the assistant.</summary>
public enum LineFocus
{
    Both,
    Life,
    General,
}

public enum InsuranceClass
{
    Life,
    General,
}

public static class LineFocusExtensions
{
    public static bool Allows(this LineFocus focus, InsuranceClass insuranceClass) =>
        focus == LineFocus.Both
        || (focus == LineFocus.Life && insuranceClass == InsuranceClass.Life)
        || (focus == LineFocus.General && insuranceClass == InsuranceClass.General);
}
