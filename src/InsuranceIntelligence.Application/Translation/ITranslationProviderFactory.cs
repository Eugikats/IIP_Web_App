namespace InsuranceIntelligence.Application.Translation;

/// <summary>
/// Builds a live ITranslationProvider from a provider key and settings, either a tenant's
/// decrypted IntegrationConnection settings, or (via CreateFallback) the deployment's environment
/// configuration. Mirrors ISmsProviderFactory/IEmailSendingProviderFactory: registering a new
/// translation engine means adding one case here and one IConnectionValidator, nothing else in the
/// resolution chain (TenantAwareTranslationProvider) changes.
/// </summary>
public interface ITranslationProviderFactory
{
    IReadOnlyCollection<string> SupportedProviderKeys { get; }

    ITranslationProvider? Create(string providerKey, IReadOnlyDictionary<string, string> settings);

    ITranslationProvider CreateFallback();
}
