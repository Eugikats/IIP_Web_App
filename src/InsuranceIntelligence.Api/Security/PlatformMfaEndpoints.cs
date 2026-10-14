using System.Security.Claims;
using System.Text.Json;
using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Application.Security;

namespace InsuranceIntelligence.Api.Security;

/// <summary>
/// The second step after sign-in: set up an authenticator app, codes or backup codes, verify, and (for an
/// administrator) set the organisation's policy. Modelled on CIP's platform MFA. Nothing here returns a secret
/// after set-up; the authenticator secret is shown once, when it is created.
/// </summary>
public static class PlatformMfaEndpoints
{
    public sealed record BeginTotpRequest(string? Label);

    public sealed record ConfirmTotpRequest(Guid Id, string? Code);

    public sealed record SendCodeRequest(string? Channel, string? Purpose, string? Phone);

    public sealed record ConfirmCodeRequest(string? Channel, string? Code);

    public sealed record VerifyRequest(string? Method, string? Code);

    public sealed record PolicyRequest(bool Required, bool AllowSnooze, int MinMethods, IReadOnlyList<string>? EnabledMethods);

    public sealed record PasskeyResultRequest(JsonElement Response, string? Nickname);

    // ---- passkeys. The browser's Origin header names the portal address the passkey belongs to.

    public static async Task<IResult> PasskeyRegistrationOptions(HttpContext http, IPasskeyService passkeys, ITenantContextAccessor tenant, CancellationToken cancellationToken)
    {
        var result = await passkeys.RegistrationOptionsAsync(Origin(http), AccountName(http), http.User.FindFirst("name")?.Value ?? AccountName(http), cancellationToken);
        return RespondRaw(result);
    }

    public static async Task<IResult> PasskeyRegistrationResult(PasskeyResultRequest request, HttpContext http, IPasskeyService passkeys, CancellationToken cancellationToken) =>
        Respond(await passkeys.RegistrationResultAsync(Origin(http), request.Response.GetRawText(), request.Nickname, cancellationToken), _ => new { ok = true });

    public static async Task<IResult> PasskeyAssertionOptions(HttpContext http, IPasskeyService passkeys, CancellationToken cancellationToken) =>
        RespondRaw(await passkeys.AssertionOptionsAsync(Origin(http), cancellationToken));

    public static async Task<IResult> PasskeyAssertionResult(PasskeyResultRequest request, HttpContext http, IPasskeyService passkeys, CancellationToken cancellationToken) =>
        Respond(await passkeys.AssertionResultAsync(Origin(http), request.Response.GetRawText(), cancellationToken), _ => new { ok = true });

    private static string Origin(HttpContext http) => http.Request.Headers.Origin.ToString();

    /// <summary>The options are already JSON, so they are returned as they are rather than quoted a second time.</summary>
    private static IResult RespondRaw(MfaResult<string> result) =>
        result.Ok ? Results.Content(result.Value!, "application/json") : Respond(result, value => value);

    public static async Task<IResult> GetStatus(IPlatformMfaService service, CancellationToken cancellationToken) =>
        Results.Ok(await service.GetStatusAsync(cancellationToken));

    public static async Task<IResult> BeginTotp(BeginTotpRequest request, HttpContext http, IPlatformMfaService service, CancellationToken cancellationToken)
    {
        var result = await service.BeginTotpAsync(request.Label ?? string.Empty, AccountName(http), cancellationToken);
        return Respond(result, value => new { id = value.Id, secret = value.Secret, provisioningUri = value.ProvisioningUri });
    }

    public static async Task<IResult> ConfirmTotp(ConfirmTotpRequest request, IPlatformMfaService service, CancellationToken cancellationToken) =>
        Respond(await service.ConfirmTotpAsync(request.Id, request.Code ?? string.Empty, cancellationToken), _ => new { ok = true });

    public static async Task<IResult> RemoveCredential(string method, Guid id, IPlatformMfaService service, CancellationToken cancellationToken) =>
        Respond(await service.RemoveCredentialAsync(method, id, cancellationToken), _ => new { ok = true });

    public static async Task<IResult> RotateBackupCodes(IPlatformMfaService service, CancellationToken cancellationToken) =>
        Respond(await service.RotateBackupCodesAsync(cancellationToken), codes => new { codes });

    public static async Task<IResult> SendCode(SendCodeRequest request, HttpContext http, IPlatformMfaService service, CancellationToken cancellationToken) =>
        Respond(
            await service.SendCodeAsync(request.Channel ?? string.Empty, request.Purpose ?? string.Empty, request.Phone, AccountEmail(http), cancellationToken),
            sent => new { channel = sent.Channel, destination = sent.DestinationMasked, expiresAt = sent.ExpiresAt });

    public static async Task<IResult> ConfirmCode(ConfirmCodeRequest request, IPlatformMfaService service, CancellationToken cancellationToken) =>
        Respond(await service.ConfirmCodeEnrolmentAsync(request.Channel ?? string.Empty, request.Code ?? string.Empty, cancellationToken), _ => new { ok = true });

    public static async Task<IResult> DisableChannel(string channel, IPlatformMfaService service, CancellationToken cancellationToken) =>
        Respond(await service.DisableChannelAsync(channel, cancellationToken), _ => new { ok = true });

    public static async Task<IResult> Verify(VerifyRequest request, IPlatformMfaService service, CancellationToken cancellationToken) =>
        Respond(await service.VerifyAsync(request.Method ?? string.Empty, request.Code ?? string.Empty, cancellationToken), _ => new { ok = true });

    public static async Task<IResult> Snooze(IPlatformMfaService service, CancellationToken cancellationToken) =>
        Respond(await service.SnoozeAsync(cancellationToken), _ => new { ok = true });

    public static async Task<IResult> SignOut(IPlatformMfaService service, CancellationToken cancellationToken)
    {
        await service.SignOutAsync(cancellationToken);
        return Results.Ok(new { ok = true });
    }

    public static async Task<IResult> GetPolicy(IPlatformMfaService service, CancellationToken cancellationToken)
    {
        var policy = await service.GetPolicyAsync(cancellationToken);
        return Results.Ok(new { required = policy.Required, allowSnooze = policy.AllowSnooze, minMethods = policy.MinMethods, enabledMethods = policy.EnabledMethods, updatedAt = policy.UpdatedAt });
    }

    public static async Task<IResult> SetPolicy(PolicyRequest request, IPlatformMfaService service, CancellationToken cancellationToken) =>
        Respond(
            await service.SetPolicyAsync(request.Required, request.AllowSnooze, request.MinMethods, request.EnabledMethods ?? [], cancellationToken),
            policy => new { required = policy.Required, allowSnooze = policy.AllowSnooze, minMethods = policy.MinMethods, enabledMethods = policy.EnabledMethods, updatedAt = policy.UpdatedAt });

    public static async Task<IResult> ResetUser(string userId, IPlatformMfaService service, CancellationToken cancellationToken) =>
        Respond(await service.ResetUserAsync(userId, cancellationToken), _ => new { ok = true });

    public static async Task<IResult> GetMetrics(IPlatformMfaService service, CancellationToken cancellationToken) =>
        Results.Ok(await service.GetMetricsAsync(cancellationToken));

    internal static IResult Respond<T>(MfaResult<T> result, Func<T, object> success)
    {
        if (result.Ok) return Results.Ok(success(result.Value!));
        var body = new { code = result.Code, message = result.Message };
        return result.Code switch
        {
            "needs_verification" => Results.Json(body, statusCode: StatusCodes.Status403Forbidden),
            "locked" => Results.Json(body, statusCode: StatusCodes.Status423Locked),
            "cooldown" => Results.Json(body, statusCode: StatusCodes.Status429TooManyRequests),
            "not_found" or "no_code" => Results.Json(body, statusCode: StatusCodes.Status404NotFound),
            "not_configured" => Results.Json(body, statusCode: StatusCodes.Status409Conflict),
            _ => Results.Json(body, statusCode: StatusCodes.Status400BadRequest),
        };
    }

    internal static string? AccountEmail(HttpContext http) =>
        new[] { "email", ClaimTypes.Email, "preferred_username", "upn" }
            .Select(type => http.User.FindFirst(type)?.Value)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value) && value.Contains('@'));

    internal static string AccountName(HttpContext http) =>
        AccountEmail(http)
        ?? http.User.FindFirst("name")?.Value
        ?? http.User.FindFirst(ClaimTypes.Name)?.Value
        ?? "account";
}

/// <summary>
/// Blocks the API for a signed-in person who has not completed the second step, when their organisation requires
/// it. Does nothing at all when the policy is off, which is the default. People without a membership, public
/// routes and API keys are not affected: they have their own authentication.
/// </summary>
public sealed class PlatformMfaEnforcementMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext http, ITenantContextAccessor tenant, IPlatformMfaService mfa)
    {
        var path = http.Request.Path;
        if (!path.StartsWithSegments("/api/v1") || IsExempt(path) || !tenant.HasCurrent
            || http.Items.ContainsKey(InsuranceIntelligence.Api.Auth.ApiKeyAuthenticationHandler.OrganisationItemKey))
        {
            await next(http);
            return;
        }

        var context = tenant.Current;
        var gate = await mfa.EvaluateGateAsync(context.Scope, context.UserId, http.RequestAborted);
        if (gate == MfaGate.Allowed)
        {
            await next(http);
            return;
        }

        http.Response.StatusCode = StatusCodes.Status403Forbidden;
        await http.Response.WriteAsJsonAsync(gate == MfaGate.EnrolRequired
            ? new { code = "MFA_ENROLL_REQUIRED", message = "Set up a verification method before continuing." }
            : new { code = "MFA_REQUIRED", message = "Verify it is you to continue." });
    }

    /// <summary>What a person must reach to enrol, verify, learn who they are and sign out.</summary>
    private static bool IsExempt(PathString path) =>
        path.StartsWithSegments("/api/v1/security/mfa")
        || path.StartsWithSegments("/api/v1/me")
        || path.StartsWithSegments("/api/v1/organisations")
        || path.StartsWithSegments("/api/v1/invitations")
        || path.StartsWithSegments("/api/v1/admin/db-migrate");
}
