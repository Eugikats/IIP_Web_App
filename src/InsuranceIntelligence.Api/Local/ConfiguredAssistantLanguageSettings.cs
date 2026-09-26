using InsuranceIntelligence.Application.CustomerAssistant;
using InsuranceIntelligence.Domain.Translation;
using InsuranceIntelligence.Infrastructure.Configuration;

namespace InsuranceIntelligence.Api.Local;

/// <summary>Reads the assistant's language settings from <see cref="TranslationOptions"/>. An
/// unknown code, and "en", are ignored: English is always available and nothing else is guessed.</summary>
public sealed class ConfiguredAssistantLanguageSettings : IAssistantLanguageSettings
{
    public ConfiguredAssistantLanguageSettings(TranslationOptions options)
    {
        EnabledLanguages = options.EnabledLanguages
            .Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(SupportedLanguage.FromIso)
            .OfType<SupportedLanguage>()
            .Select(language => language.Code)
            .Where(code => code != SupportedLanguageCode.En)
            .Distinct()
            .ToArray();
        AllowUnreviewedTranslations = options.AllowUnreviewedTranslations;
    }

    public IReadOnlyCollection<SupportedLanguageCode> EnabledLanguages { get; }

    public bool AllowUnreviewedTranslations { get; }
}
