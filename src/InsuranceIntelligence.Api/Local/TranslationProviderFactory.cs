using InsuranceIntelligence.Application.Translation;
using InsuranceIntelligence.Infrastructure.Configuration;
using InsuranceIntelligence.Infrastructure.Translation;

namespace InsuranceIntelligence.Api.Local;

/// <summary>
/// Builds a live ITranslationProvider from a provider key and tenant settings, either a saved
/// IntegrationConnection (Create), or the deployment's environment configuration (CreateFallback).
/// Mirrors SmsProviderFactory. Lives in InsuranceIntelligence.Api rather than Infrastructure
/// because CreateFallback's Mock branch depends on MockTranslationProvider, which is deliberately
/// confined to this project (see MockTranslationProvider's own doc comment): a compile-time
/// guarantee a test double can never reach a real deployment, not just a runtime check.
/// </summary>
public sealed class TranslationProviderFactory(InfrastructureOptions options, IHttpClientFactory httpClientFactory) : ITranslationProviderFactory
{
    public IReadOnlyCollection<string> SupportedProviderKeys { get; } = ["Sunbird"];

    public ITranslationProvider? Create(string providerKey, IReadOnlyDictionary<string, string> settings)
    {
        if (string.Equals(providerKey, "Sunbird", StringComparison.OrdinalIgnoreCase))
        {
            return SunbirdSettings.TryParse(settings, out var sunbirdSettings, out _) && sunbirdSettings is not null
                ? new SunbirdTranslationProvider(sunbirdSettings, httpClientFactory.CreateClient(nameof(SunbirdTranslationProvider)))
                : null;
        }

        return null;
    }

    public ITranslationProvider CreateFallback()
    {
        // AzureProviderStartupValidator.ValidateProduction refuses Mock under every deployed
        // profile; this fallback only ever serves Mock in Development/Demo, same guarantee as
        // today's direct Program.cs DI switch.
        if (options.TranslationProvider.Equals("Mock", StringComparison.OrdinalIgnoreCase))
        {
            return new MockTranslationProvider();
        }

        if (options.TranslationProvider.Equals("AfriSlm", StringComparison.OrdinalIgnoreCase))
        {
            return new AfriSlmTranslationProvider(
                options.Translation,
                httpClientFactory.CreateClient(nameof(AfriSlmTranslationProvider)));
        }

        return new NotConfiguredTranslationProvider();
    }
}
