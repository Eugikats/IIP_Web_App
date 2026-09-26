using InsuranceIntelligence.Domain.Translation;

namespace InsuranceIntelligence.Application.CustomerAssistant;

/// <summary>Which languages the WhatsApp assistant may converse in besides English, and whether it
/// may machine translate a reply when no human-reviewed translation exists. Passed in rather than
/// read from configuration because the Application layer does not see Infrastructure.</summary>
public interface IAssistantLanguageSettings
{
    IReadOnlyCollection<SupportedLanguageCode> EnabledLanguages { get; }

    bool AllowUnreviewedTranslations { get; }
}

/// <summary>English only. What the assistant runs on when nothing else is configured.</summary>
public sealed class EnglishOnlyAssistantLanguageSettings : IAssistantLanguageSettings
{
    public IReadOnlyCollection<SupportedLanguageCode> EnabledLanguages => [];

    public bool AllowUnreviewedTranslations => false;
}
