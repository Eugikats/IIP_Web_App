namespace InsuranceIntelligence.Domain.Translation;

/// <summary>The launch language set (build plan §"Decisions settled 2026-09-25"). English is
/// always on; Luganda and Kiswahili are the two translated languages the AfriSLM model covers.
/// Adding a language later means adding a value here and a model that actually covers it, never
/// widening this enum ahead of a model that can serve it.</summary>
public enum SupportedLanguageCode
{
    En,
    Lg,
    Sw,
}

public sealed record SupportedLanguage(SupportedLanguageCode Code, string Iso, string DisplayName)
{
    public static readonly SupportedLanguage English = new(SupportedLanguageCode.En, "en", "English");
    public static readonly SupportedLanguage Luganda = new(SupportedLanguageCode.Lg, "lg", "Luganda");
    public static readonly SupportedLanguage Kiswahili = new(SupportedLanguageCode.Sw, "sw", "Kiswahili");

    public static readonly IReadOnlyList<SupportedLanguage> All = [English, Luganda, Kiswahili];

    public static SupportedLanguage? FromIso(string? iso) =>
        string.IsNullOrWhiteSpace(iso) ? null : All.FirstOrDefault(language => string.Equals(language.Iso, iso, StringComparison.OrdinalIgnoreCase));

    public static SupportedLanguage For(SupportedLanguageCode code) => All.First(language => language.Code == code);
}

/// <summary>Which of the launch languages a tenant has turned on. English is always enabled and
/// is never stored in <see cref="EnabledCodes"/>; a tenant that has enabled nothing beyond English
/// still gets an honest "not configured" surface for the other two, not a silent fallback.</summary>
public sealed record TenantLanguageCatalog(IReadOnlyCollection<SupportedLanguageCode> EnabledCodes)
{
    public static readonly TenantLanguageCatalog EnglishOnly = new([]);

    public bool IsEnabled(SupportedLanguageCode code) =>
        code == SupportedLanguageCode.En || EnabledCodes.Contains(code);
}
