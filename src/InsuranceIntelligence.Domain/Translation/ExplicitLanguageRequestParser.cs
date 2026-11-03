using System.Text.RegularExpressions;

namespace InsuranceIntelligence.Domain.Translation;

/// <summary>Step 2 of the language-resolution order (build plan §6.B): a customer directly naming
/// a language ("reply in Luganda", "Kiswahili tafadhali") always wins, current and explicit beats
/// a remembered default or a detected guess.</summary>
public static class ExplicitLanguageRequestParser
{
    public static SupportedLanguageCode? Parse(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        if (ContainsLanguageName(text, "English"))
        {
            return SupportedLanguageCode.En;
        }

        // The model card's prompt names are the canonical phrases for Sunflower. Longer names are
        // checked first so a language name containing another name cannot win accidentally.
        var mentioned = SupportedLanguage.All
            .Where(language => language.Code != SupportedLanguageCode.En)
            .OrderByDescending(language => language.SunbirdLanguageName.Length)
            .FirstOrDefault(language =>
                ContainsLanguageName(text, language.SunbirdLanguageName)
                || ContainsLanguageName(text, language.DisplayName));
        return mentioned?.Code;
    }

    private static bool ContainsLanguageName(string text, string languageName) =>
        Regex.IsMatch(
            text,
            $"(?<![\\p{{L}}\\p{{N}}]){Regex.Escape(languageName)}(?![\\p{{L}}\\p{{N}}])",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
}
