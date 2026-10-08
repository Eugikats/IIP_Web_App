namespace InsuranceIntelligence.Domain.Translation;

/// <summary>Step 3 of the language-resolution order (build plan §6.B): a word-list scorer for
/// English, Luganda and Kiswahili, combined with <see cref="TranslationQualityJudge.LooksLikeEnglish"/>.
/// A modest, high-precision word list, not a language model, deliberately: getting a customer's
/// language wrong is worse than admitting uncertainty and asking, which is exactly what a low
/// confidence score here lets <see cref="LanguageResolutionOrder"/> do.</summary>
public static class LanguageWordListScorer
{
    private static readonly HashSet<string> LugandaWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "oli", "otya", "gyebale", "webale", "nkwagala", "nina", "nfunye", "akabenje", "nedda",
        "kale", "nnyo", "abaana", "ensimbi", "omusango", "nnyabo", "ssebo", "mpulira", "bwoba",
        "tewali", "wano", "leero", "nsonga", "kakati", "nsaba", "nnyinza", "bino", "nze", "ggwe",
        "bakyala", "mukama", "olwaki", "mutya", "gyendi", "gyenva",
        // Function words and everyday claim words. A real sentence is mostly these, and the list
        // above alone scored a plain Luganda sentence below the detection threshold.
        "yange", "gwange", "bwange", "zange", "lwange", "kwange", "naye", "nti", "mu", "ku",
        "nga", "nnina", "jjo", "enkya", "makya", "ekiro", "motoka", "poliisi", "ssente", "yinsuwa",
        "ddwaliro", "musawo", "nsonyiwa", "nkusaba", "mbeera", "empapula", "ebiwandiiko", "omuntu",
        "abantu", "bwe", "oba", "leero", "mumbeeko", "nsaba", "kyange", "ebyange", "ekyange",
        // Everyday greetings and claim words the field transcript showed falling through.
        "gyebaleko", "kabenje", "mbadde", "ngude", "njagala", "kulopa", "nnyinza", "wasuze", "osiibye",
        "ssebo", "bulungi", "mwattu", "nkoze", "nfunye", "nnumiziddwa", "mmotoka", "ndwadde", "nnwadde",
        "ntunda", "katale", "akatale", "dduuka", "nkola", "nvuga",
    };

    private static readonly HashSet<string> KiswahiliWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "habari", "jambo", "mambo", "asante", "tafadhali", "ndiyo", "hapana", "bima", "ajali",
        "pole", "naomba", "nataka", "gari", "polisi", "hospitali", "fedha", "malipo", "kesi",
        "leo", "sasa", "karibu", "samahani", "nzuri", "mzuri", "wewe", "mimi", "sisi", "hii",
        "hiyo", "kwa", "nini", "wapi", "lini", "gani", "namba",
        "na", "ya", "wa", "ni", "la", "yangu", "langu", "wangu", "yako", "nimepata", "nina", "sera",
        "barabara", "asubuhi", "jana", "jioni", "usiku", "kwenye", "nisaidie", "madai", "dai", "daktari",
        "wizi", "moto", "kifo", "fidia", "kesho", "sijui", "naweza", "unaweza", "tuma", "picha",
        "nimepoteza", "imeharibika", "gari", "nyumba", "mtu", "watu", "kitu", "kama", "lakini", "pia",
    };

    private const double NoSignalConfidence = 0.0;
    private const double EnglishFallbackConfidence = 0.6;

    // A longer message with no Luganda or Kiswahili in it that reads as plain English is English with
    // confidence: this is what lets a customer who wrote in Luganda go back to English.
    private const double LongEnglishConfidence = 0.85;
    private const int LongEnglishWords = 4;

    public static LanguageResolution Score(string text)
    {
        var words = Tokenize(text);
        if (words.Length == 0)
        {
            return new LanguageResolution(SupportedLanguageCode.En, LanguageResolutionSource.Detected, NoSignalConfidence);
        }

        var lugandaHits = words.Count(LugandaWords.Contains);
        var kiswahiliHits = words.Count(KiswahiliWords.Contains);

        if (lugandaHits == 0 && kiswahiliHits == 0)
        {
            var looksEnglish = TranslationQualityJudge.LooksLikeEnglish(text);
            var confidence = !looksEnglish ? NoSignalConfidence : words.Length >= LongEnglishWords ? LongEnglishConfidence : EnglishFallbackConfidence;
            return new LanguageResolution(SupportedLanguageCode.En, LanguageResolutionSource.Detected, confidence);
        }

        var (language, hits) = lugandaHits >= kiswahiliHits
            ? (SupportedLanguageCode.Lg, lugandaHits)
            : (SupportedLanguageCode.Sw, kiswahiliHits);

        var score = Math.Min(1.0, (hits / (double)words.Length) * 2.0);
        return new LanguageResolution(language, LanguageResolutionSource.Detected, score);
    }

    private static string[] Tokenize(string text) =>
        text.Split([' ', '\t', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries)
            .Select(word => word.Trim('.', ',', '!', '?', ';', ':', '"', '\''))
            .Where(word => word.Length > 0)
            .ToArray();
}
