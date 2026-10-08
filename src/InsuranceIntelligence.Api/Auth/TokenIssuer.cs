using Microsoft.IdentityModel.JsonWebTokens;

namespace InsuranceIntelligence.Api.Auth;

/// <summary>
/// Which directory issued the bearer token on a request, read only to choose which check validates it.
/// Nothing here trusts the token: the chosen scheme then verifies its signature, issuer, audience and lifetime.
/// </summary>
public static class TokenIssuer
{
    public const string ExternalIdScheme = "ExternalId";

    public static bool IsExternalId(HttpContext context) =>
        IssuerHost(context) is { } host && IsExternalIdHost(host);

    /// <summary>Entra External ID tenants sign people in on a ciamlogin.com address.</summary>
    public static bool IsExternalIdHost(string host) =>
        host.Equals("ciamlogin.com", StringComparison.OrdinalIgnoreCase)
        || host.EndsWith(".ciamlogin.com", StringComparison.OrdinalIgnoreCase);

    private static string? IssuerHost(HttpContext context)
    {
        var header = context.Request.Headers.Authorization.ToString();
        if (!header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var token = header["Bearer ".Length..].Trim();
        var handler = new JsonWebTokenHandler();
        if (!handler.CanReadToken(token))
        {
            return null;
        }

        try
        {
            var issuer = handler.ReadJsonWebToken(token).Issuer;
            return Uri.TryCreate(issuer, UriKind.Absolute, out var uri) ? uri.Host : null;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }
}
