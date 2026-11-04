using System.Security.Cryptography;
using InsuranceIntelligence.Api.Workflow;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.ClientPortal;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Infrastructure.ClientPortal;
using InsuranceIntelligence.Infrastructure.Configuration;

namespace InsuranceIntelligence.Api.ClientPortal;

/// <summary>What a customer's pages show about the insurer. Public by design: nothing here is private to the insurer.</summary>
public sealed record PortalBrandingResponse(
    string? Slug, string? DisplayName, string? AccentColour, string? LogoUrl, string? SupportPhone, string? SupportEmail);

/// <summary>An administrator's view of the workspace's customer portal identity. Never carries a secret.</summary>
public sealed record PortalIdentityResponse(
    string? Slug,
    string? DisplayName,
    string? AccentColour,
    string? LogoUrl,
    string? SupportPhone,
    string? SupportEmail,
    string? Hostname,
    bool HostnameVerified,
    string? HostnameChallenge,
    string ChallengePath,
    bool SmsWebhookConfigured,
    string? ExampleLink);

public sealed record SavePortalIdentityRequest(
    string? Slug, string? DisplayName, string? AccentColour, string? LogoUrl, string? SupportPhone, string? SupportEmail, string? Hostname);

/// <summary>
/// The customer portal identity (build-loop item 29): the short name and web address a workspace's customers arrive on, what their
/// pages show, and the secret address the SMS gateway reports delivery to. Reads and changes need the settings permission and are
/// limited to the caller's own workspace; short names and web addresses are unique across the deployment.
/// </summary>
public static class PortalIdentityEndpoints
{
    public static IResult GetBranding(HttpContext httpContext, IPortalDirectory directory)
    {
        if (!ClientPortalScopeReader.TryGetFromHeaders(httpContext, out var scope, out var error))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["tenantScope"] = [error] });
        }

        var identity = directory.ForScope(scope);
        return Results.Ok(identity is null
            ? new PortalBrandingResponse(null, null, null, null, null, null)
            : new PortalBrandingResponse(identity.Slug, identity.DisplayName, identity.AccentColour, identity.LogoUrl, identity.SupportPhone, identity.SupportEmail));
    }

    public static async Task<IResult> Get(
        HttpContext httpContext,
        IPortalIdentityRepository repository,
        InsuranceIntelligence.Application.ClientPortal.IPortalAddress portalAddress,
        CancellationToken cancellationToken)
    {
        if (!WorkflowEndpoints.TryResolveScope(httpContext, out var scope, out _, out var error))
        {
            return Validation("tenantScope", error);
        }

        return Results.Ok(ToResponse(await repository.GetAsync(scope, cancellationToken), portalAddress, scope));
    }

    public static async Task<IResult> Put(
        SavePortalIdentityRequest request,
        HttpContext httpContext,
        IPortalIdentityRepository repository,
        IPortalDirectory directory,
        IAuditRepository auditRepository,
        InsuranceIntelligence.Application.ClientPortal.IPortalAddress portalAddress,
        CancellationToken cancellationToken)
    {
        if (!WorkflowEndpoints.TryResolveScope(httpContext, out var scope, out var userId, out var error))
        {
            return Validation("tenantScope", error);
        }

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Validation("userId", "Authenticated user context is required to change the customer portal.");
        }

        var slug = (request.Slug ?? string.Empty).Trim();
        if (PortalIdentity.ValidateSlug(slug) is { } slugProblem) return Validation("slug", slugProblem);
        if (PortalIdentity.ValidateDisplayName(request.DisplayName) is { } nameProblem) return Validation("displayName", nameProblem);
        var colour = NullIfBlank(request.AccentColour);
        if (colour is not null && PortalIdentity.ValidateAccentColour(colour) is { } colourProblem) return Validation("accentColour", colourProblem);
        var logo = NullIfBlank(request.LogoUrl);
        if (logo is not null && PortalIdentity.ValidateLogoUrl(logo) is { } logoProblem) return Validation("logoUrl", logoProblem);
        var hostname = NullIfBlank(request.Hostname)?.ToLowerInvariant();
        if (hostname is not null && PortalIdentity.ValidateHostname(hostname) is { } hostProblem) return Validation("hostname", hostProblem);
        var phone = NullIfBlank(request.SupportPhone);
        var email = NullIfBlank(request.SupportEmail);
        if (phone is { Length: > 40 }) return Validation("supportPhone", "The support phone number can be at most 40 characters.");
        if (email is not null && !(email.Length <= 120 && System.Net.Mail.MailAddress.TryCreate(email, out var parsed) && parsed.Address == email))
        {
            return Validation("supportEmail", "Enter a valid support email address.");
        }

        var others = (await repository.ListAllAsync(cancellationToken)).Where(item => !item.Scope.Matches(scope)).ToArray();
        if (others.Any(item => string.Equals(item.Slug, slug, StringComparison.Ordinal)))
        {
            return Results.Conflict(new { message = "That short name is already taken. Choose another." });
        }

        if (hostname is not null && others.Any(item => string.Equals(item.Hostname, hostname, StringComparison.Ordinal)))
        {
            return Results.Conflict(new { message = "That web address already belongs to another workspace." });
        }

        var current = await repository.GetAsync(scope, cancellationToken);
        var hostnameChanged = !string.Equals(current?.Hostname, hostname, StringComparison.Ordinal);
        var updated = new PortalIdentity
        {
            Scope = scope,
            Slug = slug,
            DisplayName = request.DisplayName!.Trim(),
            AccentColour = colour,
            LogoUrl = logo,
            SupportPhone = phone,
            SupportEmail = email,
            Hostname = hostname,
            // A new address must be proved again before customers are sent there.
            HostnameChallenge = hostname is null ? null : hostnameChanged ? NewChallenge() : current?.HostnameChallenge,
            HostnameVerifiedAt = hostnameChanged ? null : current?.HostnameVerifiedAt,
            SmsWebhookSecretHash = current?.SmsWebhookSecretHash,
            UpdatedByUserId = userId,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        await repository.SaveAsync(updated, cancellationToken);
        await directory.RefreshAsync(cancellationToken);
        await auditRepository.WriteAsync(Audit(scope, userId, "portal.identity.updated", hostnameChanged ? "hostname changed" : "details changed", httpContext.TraceIdentifier), cancellationToken);
        return Results.Ok(ToResponse(updated, portalAddress, scope));
    }

    public static async Task<IResult> VerifyHostname(
        HttpContext httpContext,
        IPortalIdentityRepository repository,
        IPortalDirectory directory,
        IHostnameVerifier verifier,
        IAuditRepository auditRepository,
        InsuranceIntelligence.Application.ClientPortal.IPortalAddress portalAddress,
        CancellationToken cancellationToken)
    {
        if (!WorkflowEndpoints.TryResolveScope(httpContext, out var scope, out var userId, out var error))
        {
            return Validation("tenantScope", error);
        }

        var identity = await repository.GetAsync(scope, cancellationToken);
        if (identity?.Hostname is null || identity.HostnameChallenge is null)
        {
            return Validation("hostname", "Save a web address first, then verify it.");
        }

        if (identity.HostnameIsVerified)
        {
            return Results.Ok(ToResponse(identity, portalAddress, scope));
        }

        var check = await verifier.VerifyAsync(identity.Hostname, identity.HostnameChallenge, cancellationToken);
        if (!check.Verified)
        {
            return Results.BadRequest(new { verified = false, message = check.SafeReason });
        }

        var verified = identity with { HostnameVerifiedAt = DateTimeOffset.UtcNow, HostnameChallenge = null, UpdatedByUserId = userId, UpdatedAt = DateTimeOffset.UtcNow };
        await repository.SaveAsync(verified, cancellationToken);
        await directory.RefreshAsync(cancellationToken);
        await auditRepository.WriteAsync(Audit(scope, userId, "portal.hostname.verified", "web address verified", httpContext.TraceIdentifier), cancellationToken);
        return Results.Ok(ToResponse(verified, portalAddress, scope));
    }

    /// <summary>Makes (or replaces) the secret address the SMS gateway reports delivery to. The address is shown once and only its
    /// hash is kept, so it cannot be read back; replacing it stops the old address working at once.</summary>
    public static async Task<IResult> RotateSmsWebhook(
        HttpContext httpContext,
        IPortalIdentityRepository repository,
        IPortalDirectory directory,
        IAuditRepository auditRepository,
        InfrastructureOptions options,
        CancellationToken cancellationToken)
    {
        if (!WorkflowEndpoints.TryResolveScope(httpContext, out var scope, out var userId, out var error))
        {
            return Validation("tenantScope", error);
        }

        var identity = await repository.GetAsync(scope, cancellationToken);
        if (identity is null)
        {
            return Validation("slug", "Save the customer portal details first.");
        }

        if (string.IsNullOrWhiteSpace(options.ApiPublicBaseUrl))
        {
            return Results.Conflict(new { message = "The platform's public address is not configured, so a callback address cannot be made." });
        }

        var secret = ClaimantTokenGenerator.GenerateRawToken();
        await repository.SaveAsync(identity with { SmsWebhookSecretHash = PortalIdentity.HashSecret(secret), UpdatedByUserId = userId, UpdatedAt = DateTimeOffset.UtcNow }, cancellationToken);
        await directory.RefreshAsync(cancellationToken);
        await auditRepository.WriteAsync(Audit(scope, userId, "portal.sms_webhook.rotated", "SMS delivery callback address replaced", httpContext.TraceIdentifier), cancellationToken);
        return Results.Ok(new { callbackUrl = $"{options.ApiPublicBaseUrl!.TrimEnd('/')}/api/v1/integrations/sms/status/{secret}", shownOnce = true });
    }

    private static PortalIdentityResponse ToResponse(
        PortalIdentity? identity, InsuranceIntelligence.Application.ClientPortal.IPortalAddress portalAddress, TenantScope scope)
    {
        if (identity is null)
        {
            return new PortalIdentityResponse(null, null, null, null, null, null, null, false, null, IHostnameVerifier.ChallengePath, false, null);
        }

        var baseUrl = portalAddress.BaseUrlFor(scope);
        return new PortalIdentityResponse(
            identity.Slug, identity.DisplayName, identity.AccentColour, identity.LogoUrl, identity.SupportPhone, identity.SupportEmail,
            identity.Hostname, identity.HostnameIsVerified, identity.HostnameChallenge, IHostnameVerifier.ChallengePath,
            identity.SmsWebhookSecretHash is not null,
            baseUrl is null ? null : $"{baseUrl}/access/verify?token=example{portalAddress.LinkQueryFor(scope)}");
    }

    private static string NewChallenge() => "iip-" + Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static AuditEvent Audit(TenantScope scope, string userId, string action, string reason, string correlationId) =>
        new(
            Guid.NewGuid(), scope, null, null, userId, ActorType.User, action, DateTimeOffset.UtcNow,
            BeforeStatus: null, AfterStatus: null, EvidenceIds: [], ModelProvider: null, Confidence: null,
            ValidationResult: "accepted", ApprovalId: null, AuditReason: reason, CorrelationId: correlationId);

    private static IResult Validation(string key, string message) =>
        Results.ValidationProblem(new Dictionary<string, string[]> { [key] = [message] });
}
