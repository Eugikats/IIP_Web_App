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

        var lowered = text.ToLowerInvariant();
        if (lowered.Contains("luganda"))
        {
            return SupportedLanguageCode.Lg;
        }

        if (lowered.Contains("kiswahili") || lowered.Contains("swahili"))
        {
            return SupportedLanguageCode.Sw;
        }

        if (lowered.Contains("english"))
        {
            return SupportedLanguageCode.En;
        }

        return null;
    }
}
