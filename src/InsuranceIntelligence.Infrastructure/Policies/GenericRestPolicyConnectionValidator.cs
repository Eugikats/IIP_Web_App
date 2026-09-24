using InsuranceIntelligence.Domain.Integrations;

namespace InsuranceIntelligence.Infrastructure.Policies;

/// <summary>
/// Validates a PolicySystem connection's settings structurally, not by calling a live endpoint,
/// unlike AfricasTalkingConnectionValidator/EgoSmsConnectionValidator, which can prove credentials
/// against a real, known API shape (a balance check that spends no money), there is no known live
/// partner PAS contract here to probe: a generic REST adapter's actual path and payload shape
/// depend entirely on which partner eventually connects. Validating a base URL is well-formed and
/// an auth header is present when a value is, is the honest amount of proof this pass can offer
/// without inventing a fake partner contract to call.
/// </summary>
public sealed class GenericRestPolicyConnectionValidator : IConnectionValidator
{
    public ConnectionType ConnectionType => ConnectionType.PolicySystem;
    public string ProviderKey => "GenericRest";

    public Task<ConnectionValidationResult> ValidateAsync(
        IReadOnlyDictionary<string, string> settings,
        CancellationToken cancellationToken = default)
    {
        var baseUrl = settings.GetValueOrDefault("baseUrl");
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return Task.FromResult(new ConnectionValidationResult(false, "A base URL is required."));
        }

        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var parsed) || (parsed.Scheme != Uri.UriSchemeHttps && parsed.Scheme != Uri.UriSchemeHttp))
        {
            return Task.FromResult(new ConnectionValidationResult(false, "The base URL must be a valid absolute http(s) URL."));
        }

        var authHeaderName = settings.GetValueOrDefault("authHeaderName");
        var authHeaderValue = settings.GetValueOrDefault("authHeaderValue");
        if (!string.IsNullOrWhiteSpace(authHeaderName) && string.IsNullOrWhiteSpace(authHeaderValue))
        {
            return Task.FromResult(new ConnectionValidationResult(false, "An auth header value is required when an auth header name is set."));
        }

        return Task.FromResult(new ConnectionValidationResult(true, null));
    }
}
