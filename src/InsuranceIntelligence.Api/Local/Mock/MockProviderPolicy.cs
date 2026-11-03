using InsuranceIntelligence.Infrastructure.Configuration;

namespace InsuranceIntelligence.Api.Local.Mock;

/// <summary>
/// The single rule for when a Mock provider adapter may be registered. Mock adapters are
/// deterministic fakes for local runs and tests. They are active only when the provider setting is
/// "Mock" AND the runtime is a developer runtime (Development, LocalTest or Test). Under any other
/// profile a "Mock" setting resolves to the NotConfigured adapter, so the API fails closed rather
/// than producing fake output, and <c>AzureProviderStartupValidator.ValidateProduction</c>
/// additionally refuses to boot Staging, Production, Demo and ManagedApplicationReviewer with any
/// provider set to Mock.
/// </summary>
public static class MockProviderPolicy
{
    public static bool IsLocalDeveloperRuntime(string runtimeProfile) =>
        runtimeProfile.Equals("LocalTest", StringComparison.OrdinalIgnoreCase)
        || runtimeProfile.Equals("Development", StringComparison.OrdinalIgnoreCase)
        || runtimeProfile.Equals("Test", StringComparison.OrdinalIgnoreCase);

    /// <summary>True when the named provider setting is "Mock" and Mock adapters are permitted.</summary>
    public static bool IsActive(string providerSetting, string runtimeProfile) =>
        ProviderModeSupport.IsMockMode(providerSetting) && IsLocalDeveloperRuntime(runtimeProfile);
}
