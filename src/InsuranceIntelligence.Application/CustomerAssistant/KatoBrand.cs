namespace InsuranceIntelligence.Application.CustomerAssistant;

/// <summary>
/// Whose assistant Kato is. By default he is Nile Shield Assurance's assistant and sells only its
/// covers. Configured for a bank (bancassurance), he is the bank's assistant and sells the covers of
/// the bank's partner insurers, always saying which insurer provides each one, because the bank is the
/// distributor and the insurer is the one that pays. Set from configuration (Assistant:Organisation,
/// Assistant:ShortName, Assistant:PartnerInsurers).
/// </summary>
public sealed record KatoBrand(string Organisation, string ShortName, IReadOnlyList<string> PartnerInsurers)
{
    public static readonly KatoBrand NileShield = new(MicroinsuranceCatalog.NileShield, "Nile Shield", []);

    /// <summary>The fallback replies and greetings are fixed text; they read the brand from here, set
    /// once at start-up from configuration.</summary>
    public static KatoBrand Current { get; set; } = NileShield;

    public bool IsBancassurance => PartnerInsurers.Count > 0;

    /// <summary>The covers Kato may explain and take applications for.</summary>
    public IReadOnlyList<MicroinsuranceCatalog.MicroProduct> Products =>
        MicroinsuranceCatalog.All
            .Where(product => IsBancassurance
                ? PartnerInsurers.Contains(product.Insurer, StringComparer.OrdinalIgnoreCase)
                : string.Equals(product.Insurer, Organisation, StringComparison.OrdinalIgnoreCase))
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
