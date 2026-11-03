using System.Security.Claims;
using System.Text.Encodings.Web;
using InsuranceIntelligence.Application.Organisations;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace InsuranceIntelligence.Api.Auth;

/// <summary>Signs in a system that presents "X-Api-Key". The key decides which organisation it acts in;
/// a header naming another organisation is ignored, not obeyed.</summary>
public sealed class ApiKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    ApiKeyService service)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public new const string Scheme = "ApiKey";
    public const string HeaderName = "X-Api-Key";
    public const string OrganisationItemKey = "iip.apikey.organisation";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var presented = Request.Headers[HeaderName].ToString();
        var key = await service.AuthenticateAsync(presented, DateTimeOffset.UtcNow, Context.RequestAborted);
        if (key is null)
        {
            // The same answer for a wrong, revoked or expired key.
            return AuthenticateResult.Fail("The API key is not valid.");
        }

        Context.Items[OrganisationItemKey] = key.OrganisationId;
        var identity = new ClaimsIdentity(
            [new Claim("oid", key.UserId), new Claim(ClaimTypes.NameIdentifier, key.UserId), new Claim("name", key.Name)], Scheme);
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme));
    }
}
