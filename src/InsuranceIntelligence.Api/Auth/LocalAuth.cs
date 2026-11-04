using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using InsuranceIntelligence.Application.Identity;
using InsuranceIntelligence.Domain.Common;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace InsuranceIntelligence.Api.Auth;

/// <summary>
/// The platform's own sign-in, for companies without Microsoft 365 (build-loop item 30). It issues short-lived signed access tokens
/// after a password check, and accepts them alongside Microsoft Entra tokens. A token says only who the person is: what they may do
/// still comes from the workspace's membership store on every request (ADR-0005), never from the token.
/// </summary>
public sealed class LocalAuthOptions
{
    public const string SchemeName = "IipLocal";
    public const string PolicySchemeName = "IipAuth";
    public const string Issuer = "iip-local";
    public const string Audience = "iip-api";
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(8);

    public byte[]? SigningKey { get; init; }

    /// <summary>Whether the public sign-up page may create workspaces. Off unless switched on, because a deployment dedicated to one insurer must not let strangers open workspaces.</summary>
    public bool SelfServeSignUp { get; init; }

    public bool Enabled => SigningKey is { Length: >= 32 };

    /// <summary>
    /// The signing key comes from configuration (base64, at least 32 bytes). A developer runtime without one makes a random key for the life of
    /// the process, so sign-in works locally and tokens die with a restart. Anywhere else, no key means the platform's own sign-in is not
    /// configured and says so.
    /// </summary>
    public static LocalAuthOptions From(IConfiguration configuration, bool localDeveloperRuntime)
    {
        byte[]? key = null;
        var configured = configuration["INSURANCEINTELLIGENCE_LOCAL_AUTH_SIGNING_KEY"];
        if (!string.IsNullOrWhiteSpace(configured))
        {
            try
            {
                key = Convert.FromBase64String(configured.Trim());
            }
            catch (FormatException)
            {
                throw new InvalidOperationException("INSURANCEINTELLIGENCE_LOCAL_AUTH_SIGNING_KEY must be base64.");
            }

            if (key.Length < 32)
            {
                throw new InvalidOperationException("INSURANCEINTELLIGENCE_LOCAL_AUTH_SIGNING_KEY must be at least 32 bytes.");
            }
        }
        else if (localDeveloperRuntime)
        {
            key = RandomNumberGenerator.GetBytes(48);
        }

        var signUp = configuration["INSURANCEINTELLIGENCE_SELF_SERVE_SIGNUP"];
        return new LocalAuthOptions
        {
            SigningKey = key,
            SelfServeSignUp = string.IsNullOrWhiteSpace(signUp) ? localDeveloperRuntime : signUp.Trim().Equals("true", StringComparison.OrdinalIgnoreCase),
        };
    }

    public TokenValidationParameters ValidationParameters() => new()
    {
        ValidateIssuer = true,
        ValidIssuer = Issuer,
        ValidateAudience = true,
        ValidAudience = Audience,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = SigningKey is null ? null : new SymmetricSecurityKey(SigningKey),
        ClockSkew = TimeSpan.FromMinutes(1),
        NameClaimType = "name",
        RequireSignedTokens = true,
    };

    /// <summary>Which scheme should read a bearer token: ours for a token we issued, Microsoft Entra's for any other.</summary>
    public static string SelectScheme(HttpContext context, bool entraConfigured)
    {
        var header = context.Request.Headers.Authorization.ToString();
        if (header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            var token = header["Bearer ".Length..].Trim();
            var handler = new JwtSecurityTokenHandler();
            if (handler.CanReadToken(token) && string.Equals(handler.ReadJwtToken(token).Issuer, Issuer, StringComparison.Ordinal))
            {
                return SchemeName;
            }
        }

        return entraConfigured ? JwtBearerDefaults.AuthenticationScheme : SchemeName;
    }
}

public sealed class LocalTokenIssuer(LocalAuthOptions options) : ILocalTokenIssuer
{
    public bool IsConfigured => options.Enabled;

    public (string Token, DateTimeOffset ExpiresAt) Issue(string userId, string email, string name, TenantScope scope, string role)
    {
        if (!options.Enabled)
        {
            throw new InvalidOperationException("The platform's own sign-in is not configured.");
        }

        var now = DateTimeOffset.UtcNow;
        var expires = now + LocalAuthOptions.Lifetime;
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = LocalAuthOptions.Issuer,
            Audience = LocalAuthOptions.Audience,
            NotBefore = now.UtcDateTime,
            Expires = expires.UtcDateTime,
            Subject = new ClaimsIdentity(
            [
                new Claim("sub", userId),
                new Claim("email", email),
                new Claim("name", name),
                // For the screens that show the role; what the person may do is decided from the membership store.
                new Claim("roles", role),
                // Hints only: the membership store decides what this person may do and where.
                new Claim("iip_tenant_id", scope.TenantId),
                new Claim("iip_workspace_id", scope.WorkspaceId),
            ]),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(options.SigningKey!), SecurityAlgorithms.HmacSha256),
        };
        var handler = new JwtSecurityTokenHandler { SetDefaultTimesOnTokenCreation = false };
        return (handler.WriteToken(handler.CreateToken(descriptor)), expires);
    }
}
