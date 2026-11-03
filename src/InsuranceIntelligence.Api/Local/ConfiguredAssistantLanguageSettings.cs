using InsuranceIntelligence.Application.CustomerAssistant;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Translation;
using InsuranceIntelligence.Infrastructure.Configuration;

namespace InsuranceIntelligence.Api.Local;

/// <summary>Loads the enabled language set from the tenant configuration. The deployment setting
/// is retained only for workspaces that have not yet saved their tenant-level choice.</summary>
public sealed class ConfiguredAssistantLanguageSettings(
    ITenantConfigurationRepository repository,
    TranslationOptions options) : IAssistantLanguageSettings
{
    private readonly HashSet<SupportedLanguageCode> legacyDeploymentLanguages = options.EnabledLanguages
        .Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(SupportedLanguage.FromIso)
        .OfType<SupportedLanguage>()
        .Select(language => language.Code)
        .Where(code => code != SupportedLanguageCode.En)
        .ToHashSet();

    public bool AllowUnreviewedTranslations { get; } = options.AllowUnreviewedTranslations;

    public async Task<IReadOnlyCollection<SupportedLanguageCode>> GetEnabledLanguagesAsync(
        TenantScope scope,
        CancellationToken cancellationToken = default)
    {
        var configuration = await repository.GetAsync(scope, cancellationToken);
        if (configuration.AssistantLanguages is null)
        {
            return legacyDeploymentLanguages.ToArray();
        }

        return configuration.AssistantLanguages
            .Select(SupportedLanguage.FromIso)
            .OfType<SupportedLanguage>()
            .Select(language => language.Code)
            .Where(code => code != SupportedLanguageCode.En)
            .Distinct()
            .ToArray();
    }
}
