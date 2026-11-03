using InsuranceIntelligence.Domain.Translation;

namespace InsuranceIntelligence.Application.CustomerAssistant;

/// <summary>Which languages the WhatsApp assistant may converse in besides English, and whether it
/// may machine translate a reply when no human-reviewed translation exists. Passed in rather than
/// read from configuration because the Application layer does not see Infrastructure.</summary>
public interface IAssistantLanguageSettings
{
    Task<IReadOnlyCollection<SupportedLanguageCode>> GetEnabledLanguagesAsync(
        InsuranceIntelligence.Domain.Common.TenantScope scope,
        CancellationToken cancellationToken = default);

    bool AllowUnreviewedTranslations { get; }
}

/// <summary>English only. What the assistant runs on when nothing else is configured.</summary>
public sealed class EnglishOnlyAssistantLanguageSettings : IAssistantLanguageSettings
{
    public Task<IReadOnlyCollection<SupportedLanguageCode>> GetEnabledLanguagesAsync(
        InsuranceIntelligence.Domain.Common.TenantScope scope,
        CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyCollection<SupportedLanguageCode>>([]);

    public bool AllowUnreviewedTranslations => false;
}
