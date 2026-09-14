using InsuranceIntelligence.Api.Azure;
using InsuranceIntelligence.Infrastructure.Configuration;

namespace InsuranceIntelligence.Api.Auth;

/// <summary>
/// Builds the operator-facing environment/review-mode descriptor from the runtime
/// configuration. This is the single authoritative source of the review-mode signal
/// the portal uses to render its environment banner, so a synthetic demo environment
/// can never be visually mistaken for live production. The server derives it from its
/// own profile and seed state, it is not something the browser can spoof.
/// </summary>
public static class EnvironmentContextFactory
{
    public static UiEnvironmentContext Build(InfrastructureOptions options, string environmentName, string source)
    {
        var reviewMode = AzureProviderStartupValidator.ResolveReviewMode(options, environmentName);
        var syntheticData = AzureProviderStartupValidator.UsesSyntheticData(options, environmentName);
        var name = reviewMode switch
        {
            "Production" => "Production",
            "Demo" => "Demo / Review sandbox",
            _ => "Local Development"
        };
        var dataClassification = syntheticData
            ? "Synthetic, not real customer data"
            : "Live customer data";
        var mode = options.AuthMode.Equals("Development", StringComparison.OrdinalIgnoreCase)
            ? "LocalTest"
            : "Authenticated";

        return new UiEnvironmentContext(name, mode, source, reviewMode, syntheticData, dataClassification);
    }
}
