namespace InsuranceIntelligence.Domain.Translation;

/// <summary>Step 4 of the language-resolution order (build plan §6.B): when nothing else resolved
/// the customer's language, the assistant asks in English and remembers whichever number comes
/// back. Deliberately three fixed digits, not a free-text reply, so the answer is unambiguous and
/// never needs a translation of its own to be understood.</summary>
public static class LanguageChoicePrompt
{
    public const string Text = "Reply 1 for English, 2 for Luganda, 3 for Kiswahili";

    public static SupportedLanguageCode? ParseAnswer(string messageText) =>
        messageText.Trim() switch
        {
            "1" => SupportedLanguageCode.En,
            "2" => SupportedLanguageCode.Lg,
            "3" => SupportedLanguageCode.Sw,
            _ => null,
        };
}
