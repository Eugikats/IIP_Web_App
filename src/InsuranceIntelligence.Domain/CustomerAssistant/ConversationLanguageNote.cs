using InsuranceIntelligence.Domain.Translation;

namespace InsuranceIntelligence.Domain.CustomerAssistant;

/// <summary>
/// What a member of staff needs to know about the language of a conversation, in plain words:
/// which language the customer is using, what their message was understood to say, and whether the
/// reply they received was a machine translation. Built from the stored turn, so it says what
/// happened, never what was intended. Nothing here is shown for an English conversation.
/// </summary>
public static class ConversationLanguageNote
{
    private const int MaxEnglishReadLength = 140;

    public sealed record Result(string? Language, string? Note);

    public static Result For(CustomerConversationTurn? turn)
    {
        if (turn?.CustomerLanguage is not { Length: > 0 } iso)
        {
            return new Result(null, null);
        }

        var language = SupportedLanguage.FromIso(iso)?.DisplayName ?? iso;
        var parts = new List<string>();

        if (turn.InboundTranslationStatus == TranslationStatus.Translated && !string.IsNullOrWhiteSpace(turn.InboundMessageText))
        {
            parts.Add($"Understood as: \"{Shorten(turn.InboundMessageText)}\".");
        }
        else if (turn.InboundTranslationStatus == TranslationStatus.FellBackToEnglish)
        {
            parts.Add("The message could not be translated, so a person needs to read the customer's own words.");
        }

        switch (turn.OutboundTranslationStatus)
        {
            case TranslationStatus.Translated:
                parts.Add($"Reply sent in {language}. It was translated by a computer and has not been checked by a native speaker.");
                break;
            case TranslationStatus.FellBackToEnglish:
                parts.Add("Reply sent in English because a translation could not be shown.");
                break;
        }

        return new Result(language, parts.Count == 0 ? null : string.Join(' ', parts));
    }

    private static string Shorten(string text)
    {
        var oneLine = text.Replace('\n', ' ').Trim();
        return oneLine.Length <= MaxEnglishReadLength ? oneLine : oneLine[..MaxEnglishReadLength].TrimEnd() + "...";
    }
}
