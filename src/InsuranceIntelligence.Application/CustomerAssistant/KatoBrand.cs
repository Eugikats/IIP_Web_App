namespace InsuranceIntelligence.Application.CustomerAssistant;

/// <summary>
/// Whose assistant Kato is. By default he is Nile Shield Assurance's assistant and sells only its
/// covers. Configured for a bank (bancassurance), he is the bank's assistant and sells the covers of
/// the bank's partner insurers, always saying which insurer provides each one, because the bank is the
/// distributor and the insurer is the one that pays. Set from configuration (Assistant:Organisation,
/// Assistant:ShortName, Assistant:PartnerInsurers).
/// </summary>
public sealed record KatoBrand(
    string Organisation,
    string ShortName,
    IReadOnlyList<string> PartnerInsurers,
    LineFocus LineFocus = LineFocus.Both)
{
    public static readonly KatoBrand NileShield = new(MicroinsuranceCatalog.NileShield, "Nile Shield", []);

    private static KatoBrand startupDefault = NileShield;
    private static readonly AsyncLocal<KatoBrand?> Scoped = new();

    /// <summary>
    /// The brand in force. Inside <see cref="Use"/> it is the workspace's own brand; outside it is the
    /// start-up default from configuration. The fallback replies and greetings are fixed text that
    /// reads the brand from here, so one process can speak as several organisations at once.
    /// Setting it changes the start-up default only.
    /// </summary>
    public static KatoBrand Current
    {
        get => Scoped.Value ?? startupDefault;
        set => startupDefault = value;
    }

    /// <summary>Makes <paramref name="brand"/> the brand for everything awaited from here until the
    /// returned scope is disposed. Call it from the method that does the work, not from a helper that
    /// returns first, because an async helper's change does not reach its caller.</summary>
    public static IDisposable Use(KatoBrand brand)
    {
        var previous = Scoped.Value;
        Scoped.Value = brand;
        return new Restore(previous);
    }

    private sealed class Restore(KatoBrand? previous) : IDisposable
    {
        public void Dispose() => Scoped.Value = previous;
    }

    public bool IsBancassurance => PartnerInsurers.Count > 0;

    /// <summary>The covers Kato may explain and take applications for.</summary>
    public IReadOnlyList<MicroinsuranceCatalog.MicroProduct> Products =>
        MicroinsuranceCatalog.All
            .Where(product => (IsBancassurance
                    ? PartnerInsurers.Contains(product.Insurer, StringComparer.OrdinalIgnoreCase)
                    : string.Equals(product.Insurer, Organisation, StringComparison.OrdinalIgnoreCase))
                && LineFocus.Allows(product.InsuranceClass))
            .ToArray();

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

/// <summary>Which class of insurance a workspace sells: life, general (non-life), or both.</summary>
public enum LineFocus
{
    Both,
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

public enum InsuranceClass
{
    Life,
    General,
}
