namespace InsuranceIntelligence.Domain.Translation;

/// <summary>Language identifiers supported by the Sunflower text model's published translation
/// evaluation table. Runtime enablement is tenant scoped and still requires a validated provider.
/// Speech support is represented separately because the provider models and IIP adapters cover a
/// different set of languages.</summary>
public enum SupportedLanguageCode
{
    En,
    Lg,
    Sw,
    Af,
    Zu,
    Pcm,
    Xh,
    Fr,
    Ny,
    Ln,
    So,
    Ig,
    St,
    Mg,
    Kin,
    Tn,
    Ha,
    Sn,
    Nde,
    Yo,
    Am,
    Nyo,
    Run,
    Ewe,
    Xog,
    Luo,
    Nyn,
    Kik,
    Ach,
    Cgg,
    Orm,
    Ttj,
    Ruc,
    Bem,
    Laj,
    Aka,
    Gwr,
    Kdi,
    Rub,
    Lgg,
    Myx,
    Teo,
    Nuj,
    Kab,
    Lsm,
    Tlj,
    Koo,
    Bam,
    Adh,
    Wol,
    Alz,
    Bfa,
    Keo,
    Dag,
    Kdj,
    Ber,
    Rwm,
    Ful,
    Mhi,
    Luc,
    Luy,
    Kln,
    Led,
    Dga,
    Kpz,
    Pok,
    Ikx,
    Kau,
    Din,
    Kpo,
}

public enum TranslationQualityTier
{
    Good,
    Moderate,
    Basic,
}

public sealed record SupportedLanguage(
    SupportedLanguageCode Code,
    string Iso,
    string DisplayName,
    string SunbirdLanguageName,
    string SunbirdModelCode,
    TranslationQualityTier TranslationTier,
    bool VoiceInputAvailable = false,
    bool VoiceOutputAvailable = false)
{
    public static readonly SupportedLanguage English = new(SupportedLanguageCode.En, "en", "English", "English", "eng", TranslationQualityTier.Good);
    public static readonly SupportedLanguage Luganda = new(SupportedLanguageCode.Lg, "lg", "Luganda", "Luganda", "lug", TranslationQualityTier.Good, true, true);
    public static readonly SupportedLanguage Kiswahili = new(SupportedLanguageCode.Sw, "sw", "Kiswahili", "Swahili", "swa", TranslationQualityTier.Good, true, true);

    // Quality tiers follow the Sunflower-Qwen3.5-9B model card. Model coverage does not imply
    // insurer-grade terminology accuracy; workspace enablement remains an explicit decision.
    public static readonly IReadOnlyList<SupportedLanguage> All =
    [
        English, Luganda, Kiswahili,
        new(SupportedLanguageCode.Af, "af", "Afrikaans", "Afrikaans", "afr", TranslationQualityTier.Good),
        new(SupportedLanguageCode.Zu, "zu", "Zulu", "Zulu", "zul", TranslationQualityTier.Good),
        new(SupportedLanguageCode.Pcm, "pcm", "Nigerian Pidgin", "Nigerian Pidgin", "pcm", TranslationQualityTier.Good),
        new(SupportedLanguageCode.Xh, "xh", "Xhosa", "Xhosa", "xho", TranslationQualityTier.Good),
        new(SupportedLanguageCode.Fr, "fr", "French", "French", "fra", TranslationQualityTier.Good),
        new(SupportedLanguageCode.Ny, "ny", "Chichewa", "Chichewa", "nya", TranslationQualityTier.Good),
        new(SupportedLanguageCode.Ln, "ln", "Lingala", "Lingala", "lin", TranslationQualityTier.Good),
        new(SupportedLanguageCode.So, "so", "Somali", "Somali", "som", TranslationQualityTier.Good),
        new(SupportedLanguageCode.Ig, "ig", "Igbo", "Igbo", "ibo", TranslationQualityTier.Good),
        new(SupportedLanguageCode.St, "st", "Sotho", "Sotho", "sot", TranslationQualityTier.Good),
        new(SupportedLanguageCode.Mg, "mg", "Malagasy", "Malagasy", "mlg", TranslationQualityTier.Good),
        new(SupportedLanguageCode.Kin, "rw", "Kinyarwanda", "Kinyarwanda", "kin", TranslationQualityTier.Good),
        new(SupportedLanguageCode.Tn, "tn", "Tswana", "Tswana", "tsn", TranslationQualityTier.Good),
        new(SupportedLanguageCode.Ha, "ha", "Hausa", "Hausa", "hau", TranslationQualityTier.Good),
        new(SupportedLanguageCode.Sn, "sn", "Shona", "Shona", "sna", TranslationQualityTier.Good),
        new(SupportedLanguageCode.Nde, "nd", "Ndebele", "Ndebele", "nbl", TranslationQualityTier.Good),
        new(SupportedLanguageCode.Yo, "yo", "Yoruba", "Yoruba", "yor", TranslationQualityTier.Good),
        new(SupportedLanguageCode.Am, "am", "Amharic", "Amharic", "amh", TranslationQualityTier.Good),
        new(SupportedLanguageCode.Nyo, "nyo", "Runyoro", "Runyoro", "nyo", TranslationQualityTier.Good),
        new(SupportedLanguageCode.Run, "run", "Kirundi", "Kirundi", "run", TranslationQualityTier.Good),
        new(SupportedLanguageCode.Ewe, "ee", "Ewe", "Ewe", "ewe", TranslationQualityTier.Good),
        new(SupportedLanguageCode.Xog, "xog", "Lusoga", "Lusoga", "xog", TranslationQualityTier.Good),
        new(SupportedLanguageCode.Luo, "luo", "Luo", "Luo", "luo", TranslationQualityTier.Good),
        new(SupportedLanguageCode.Nyn, "nyn", "Runyankole", "Runyankole", "nyn", TranslationQualityTier.Good),
        new(SupportedLanguageCode.Kik, "ki", "Kikuyu", "Kikuyu", "kik", TranslationQualityTier.Moderate),
        new(SupportedLanguageCode.Ach, "ach", "Acholi", "Acholi", "ach", TranslationQualityTier.Moderate),
        new(SupportedLanguageCode.Cgg, "cgg", "Rukiga", "Rukiga", "cgg", TranslationQualityTier.Moderate),
        new(SupportedLanguageCode.Orm, "om", "Oromo", "Oromo", "orm", TranslationQualityTier.Moderate),
        new(SupportedLanguageCode.Ttj, "ttj", "Rutooro", "Rutooro", "ttj", TranslationQualityTier.Moderate),
        new(SupportedLanguageCode.Ruc, "ruc", "Ruruuli", "Ruruuli", "ruc", TranslationQualityTier.Moderate),
        new(SupportedLanguageCode.Bem, "bem", "Bemba", "Bemba", "bem", TranslationQualityTier.Moderate),
        new(SupportedLanguageCode.Laj, "laj", "Lango", "Lango", "laj", TranslationQualityTier.Moderate),
        new(SupportedLanguageCode.Aka, "ak", "Akan", "Akan", "aka", TranslationQualityTier.Moderate),
        new(SupportedLanguageCode.Gwr, "gwr", "Lugwere", "Lugwere", "gwr", TranslationQualityTier.Moderate),
        new(SupportedLanguageCode.Kdi, "kdi", "Kumam", "Kumam", "kdi", TranslationQualityTier.Moderate),
        new(SupportedLanguageCode.Rub, "rub", "Lugungu", "Lugungu", "rub", TranslationQualityTier.Moderate),
        new(SupportedLanguageCode.Lgg, "lgg", "Lugbara", "Lugbara", "lgg", TranslationQualityTier.Moderate),
        new(SupportedLanguageCode.Myx, "myx", "Lumasaba", "Lumasaba", "myx", TranslationQualityTier.Moderate),
        new(SupportedLanguageCode.Teo, "teo", "Ateso", "Ateso", "teo", TranslationQualityTier.Moderate),
        new(SupportedLanguageCode.Nuj, "nuj", "Lunyole", "Lunyole", "nuj", TranslationQualityTier.Moderate),
        new(SupportedLanguageCode.Kab, "kab", "Kabyle", "Kabyle", "kab", TranslationQualityTier.Basic),
        new(SupportedLanguageCode.Lsm, "lsm", "Samia", "Samia", "lsm", TranslationQualityTier.Basic),
        new(SupportedLanguageCode.Tlj, "tlj", "Lubwisi", "Lubwisi", "tlj", TranslationQualityTier.Basic),
        new(SupportedLanguageCode.Koo, "koo", "Rukonjo", "Rukonjo", "koo", TranslationQualityTier.Basic),
        new(SupportedLanguageCode.Bam, "bm", "Bambara", "Bambara", "bam", TranslationQualityTier.Basic),
        new(SupportedLanguageCode.Adh, "adh", "Jopadhola", "Jopadhola", "adh", TranslationQualityTier.Basic),
        new(SupportedLanguageCode.Wol, "wo", "Wolof", "Wolof", "wol", TranslationQualityTier.Basic),
        new(SupportedLanguageCode.Alz, "alz", "Alur", "Alur", "alz", TranslationQualityTier.Basic),
        new(SupportedLanguageCode.Bfa, "bfa", "Bari", "Bari", "bfa", TranslationQualityTier.Basic),
        new(SupportedLanguageCode.Keo, "keo", "Kakwa", "Kakwa", "keo", TranslationQualityTier.Basic),
        new(SupportedLanguageCode.Dag, "dag", "Dagbani", "Dagbani", "dag", TranslationQualityTier.Basic),
        new(SupportedLanguageCode.Kdj, "kdj", "Karamojong", "Karamojong", "kdj", TranslationQualityTier.Basic),
        new(SupportedLanguageCode.Ber, "ber", "Berber", "Berber", "ber", TranslationQualityTier.Basic),
        new(SupportedLanguageCode.Rwm, "rwm", "Kwamba", "Kwamba", "rwm", TranslationQualityTier.Basic),
        new(SupportedLanguageCode.Ful, "ful", "Fulani", "Fulani", "ful", TranslationQualityTier.Basic),
        new(SupportedLanguageCode.Mhi, "mhi", "Ma'di", "Ma'di", "mhi", TranslationQualityTier.Basic),
        new(SupportedLanguageCode.Luc, "luc", "Aringa", "Aringa", "luc", TranslationQualityTier.Basic),
        new(SupportedLanguageCode.Luy, "luy", "Luhya", "Luhya", "luy", TranslationQualityTier.Basic),
        new(SupportedLanguageCode.Kln, "kln", "Kalenjin", "Kalenjin", "kln", TranslationQualityTier.Basic),
        new(SupportedLanguageCode.Led, "led", "Lendu", "Lendu", "led", TranslationQualityTier.Basic),
        new(SupportedLanguageCode.Dga, "dga", "Dagaare", "Dagaare", "dga", TranslationQualityTier.Basic),
        new(SupportedLanguageCode.Kpz, "kpz", "Kupsabiny", "Kupsabiny", "kpz", TranslationQualityTier.Basic),
        new(SupportedLanguageCode.Pok, "pok", "Pokot", "Pokot", "pok", TranslationQualityTier.Basic),
        new(SupportedLanguageCode.Ikx, "ikx", "Ik", "Ik", "ikx", TranslationQualityTier.Basic),
        new(SupportedLanguageCode.Kau, "kau", "Kanuri", "Kanuri", "kau", TranslationQualityTier.Basic),
        new(SupportedLanguageCode.Din, "din", "Dinka", "Dinka", "din", TranslationQualityTier.Basic),
        new(SupportedLanguageCode.Kpo, "kpo", "Ikposo", "Ikposo", "kpo", TranslationQualityTier.Basic),
    ];

    public static SupportedLanguage? FromIso(string? iso) =>
        string.IsNullOrWhiteSpace(iso) ? null : All.FirstOrDefault(language =>
            string.Equals(language.Iso, iso, StringComparison.OrdinalIgnoreCase)
            || string.Equals(language.SunbirdModelCode, iso, StringComparison.OrdinalIgnoreCase));

    public static SupportedLanguage For(SupportedLanguageCode code) => All.First(language => language.Code == code);
}

/// <summary>Which model-supported languages an insurer has chosen to use. English is always on.</summary>
public sealed record TenantLanguageCatalog(IReadOnlyCollection<SupportedLanguageCode> EnabledCodes)
{
    public static readonly TenantLanguageCatalog EnglishOnly = new([]);

    public bool IsEnabled(SupportedLanguageCode code) => code == SupportedLanguageCode.En || EnabledCodes.Contains(code);
}
