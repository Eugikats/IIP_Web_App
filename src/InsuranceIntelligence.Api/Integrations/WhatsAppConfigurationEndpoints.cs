using InsuranceIntelligence.Api.Auth;
using InsuranceIntelligence.Api.Common;
using InsuranceIntelligence.Api.Workflow;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.ClientPortal;
using InsuranceIntelligence.Domain.Integrations;
using InsuranceIntelligence.Infrastructure.Configuration;
using InsuranceIntelligence.Infrastructure.WhatsApp;

namespace InsuranceIntelligence.Api.Integrations;

/// <summary>
/// Self-service WhatsApp Business API connection settings. AccessToken/AppSecret/VerifyToken are
/// write-only from the browser's perspective: GetStatus never returns them, only whether each is
/// present, so a saved secret can never be read back out through this API even by an admin.
/// SaveConfiguration always validates against the real Meta Graph API before persisting anything,
/// a wrong phone number id or expired token fails immediately with Meta's own (sanitized) reason,
/// never a silent "saved" that only breaks on the next webhook. It then registers the webhook
/// itself via Meta's subscribed_apps override (blueprint §4.3), the operator supplies the WABA
/// id; IIP generates the verify token and never asks for it.
/// </summary>
public static class WhatsAppConfigurationEndpoints
{
    public static async Task<IResult> GetStatus(
        IWhatsAppChannelConfigurationRepository repository,
        InfrastructureOptions options,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (DemoAuthorization.Require(httpContext, options, Permission.ConfigureIntegrations) is { } forbidden)
        {
            return forbidden;
        }

        if (!WorkflowEndpoints.TryResolveScope(httpContext, out var scope, out _, out var validationError))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["tenantScope"] = [validationError] });
        }

        var configuration = await repository.GetAsync(scope, cancellationToken);
        if (configuration is null)
        {
            return Results.Ok(WhatsAppConfigurationStatusResponse.NotConnected(BuildCallbackUrl(options)));
        }

        return Results.Ok(ToResponse(configuration));
    }

    public static async Task<IResult> SaveConfiguration(
        WhatsAppSaveConfigurationRequest request,
        IWhatsAppChannelConfigurationRepository repository,
        IWhatsAppCredentialProtector protector,
        IWhatsAppGraphValidationClient validationClient,
        IWhatsAppRuntimeSettingsProvider settingsProvider,
        IAuditRepository auditRepository,
        InfrastructureOptions options,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (DemoAuthorization.Require(httpContext, options, Permission.ConfigureIntegrations) is { } forbidden)
        {
            return forbidden;
        }

        if (!WorkflowEndpoints.TryResolveScope(httpContext, out var scope, out _, out var validationError))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["tenantScope"] = [validationError] });
        }

        var missing = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.PhoneNumberId)) missing["phoneNumberId"] = ["A WhatsApp phone number id is required."];
        if (string.IsNullOrWhiteSpace(request.AccessToken)) missing["accessToken"] = ["A permanent access token is required."];
        if (string.IsNullOrWhiteSpace(request.AppSecret)) missing["appSecret"] = ["The Meta app secret is required to verify inbound webhooks."];
        if (string.IsNullOrWhiteSpace(request.WabaId)) missing["wabaId"] = ["The WhatsApp Business Account id is required to register the webhook, find it in Meta Business Settings next to the phone number."];
        if (missing.Count > 0)
        {
            return Results.ValidationProblem(missing);
        }

        var userId = DemoAuthorization.GetUserId(httpContext, fallback: string.Empty);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["userId"] = ["Authenticated user context is required to configure WhatsApp."]
            });
        }

        var auditReason = AuditReasonReader.Read(httpContext);
        if (string.IsNullOrWhiteSpace(auditReason))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["auditReason"] = ["An audit reason is required to change WhatsApp connection settings."]
            });
        }

        // One WhatsApp number belongs to one insurer. Without this, a second organisation entering
        // someone else's number and token could attract that insurer's customers' messages.
        var phoneNumberId = request.PhoneNumberId.Trim();
        var holders = await repository.ListActiveAsync(cancellationToken);
        if (holders.Any(existing => string.Equals(existing.PhoneNumberId, phoneNumberId, StringComparison.Ordinal)
                && !(string.Equals(existing.Scope.TenantId, scope.TenantId, StringComparison.Ordinal)
                    && string.Equals(existing.Scope.WorkspaceId, scope.WorkspaceId, StringComparison.Ordinal))))
        {
            return Results.Conflict(new
            {
                deliveryStatus = "NumberAlreadyConnected",
                message = "This WhatsApp number is already connected to another organisation. Contact support if it should be moved."
            });
        }

        // Guide-through validation: confirm the credentials actually work against the real Meta
        // API before anything is persisted, never store a connection that is silently broken.
        var validation = await validationClient.ValidateAsync(
            options.WhatsApp.GraphBaseUrl,
            request.PhoneNumberId.Trim(),
            request.AccessToken.Trim(),
            options.WhatsApp.RequestTimeoutSeconds,
            cancellationToken);
        if (!validation.Success)
        {
            return Results.BadRequest(new { deliveryStatus = "ValidationFailed", message = validation.SafeError });
        }

        // The verify token is generated here, not typed by the operator, it is a shared secret
        // between IIP and Meta, never something a human needs to invent or remember (blueprint §4.3
        // point 2). Reuses the same cryptographically random generator as claimant portal tokens.
        var verifyToken = ClaimantTokenGenerator.GenerateRawToken();
        var callbackUrl = BuildCallbackUrl(options);

        var now = DateTimeOffset.UtcNow;
        var configuration = new WhatsAppChannelConfiguration
        {
            Scope = scope,
            PhoneNumberId = request.PhoneNumberId.Trim(),
            WabaId = request.WabaId!.Trim(),
            BusinessDisplayName = string.IsNullOrWhiteSpace(request.BusinessDisplayName) ? validation.VerifiedName : request.BusinessDisplayName!.Trim(),
            EncryptedAccessToken = protector.Protect(request.AccessToken.Trim()),
            EncryptedAppSecret = protector.Protect(request.AppSecret.Trim()),
            EncryptedVerifyToken = protector.Protect(verifyToken),
            IsActive = true,
            ConfiguredByUserId = userId,
            ConfiguredAt = now,
            LastValidatedAt = now,
            LastValidationStatus = "Success",
            LastValidationError = null,
            WebhookCallbackUrl = callbackUrl,
            WebhookRegistrationStatus = callbackUrl is null ? "ManualRequired" : "Pending",
            WebhookRegistrationError = callbackUrl is null
                ? "This deployment has no configured public API base URL (INSURANCEINTELLIGENCE_API_PUBLIC_BASE_URL), the webhook cannot be auto-registered. Ask whoever manages your deployment to set it, or register manually in Meta Business Settings."
                : null,
        };

        // Save and refresh the cache BEFORE asking Meta to register the webhook: Meta calls this
        // same endpoint right back (a GET with hub.challenge and this verify token) to confirm it
        // before accepting the subscription. Registering first, when nothing here yet recognised
        // that verify token, made Meta's own check fail every single time, our own webhook
        // answered 503 "not configured" to Meta's own verification call, and the operator saw
        // "webhook registration failed" on a perfectly correct connection. A retry never helped
        // because every attempt hit the same gap.
        await repository.SaveAsync(configuration, cancellationToken);
        await settingsProvider.RefreshAsync(cancellationToken);

        if (callbackUrl is not null)
        {
            var registration = await validationClient.RegisterWebhookAsync(
                options.WhatsApp.GraphBaseUrl,
                request.WabaId!.Trim(),
                request.AccessToken.Trim(),
                callbackUrl,
                verifyToken,
                options.WhatsApp.RequestTimeoutSeconds,
                cancellationToken);
            configuration = configuration with
            {
                WebhookRegistrationStatus = registration.Success ? "Registered" : "RegistrationFailed",
                WebhookRegistrationError = registration.Success ? null : registration.SafeError,
            };
            await repository.SaveAsync(configuration, cancellationToken);
        }

        await auditRepository.WriteAsync(
            AuditEvent.WhatsAppConfigurationSaved(scope, userId, configuration.PhoneNumberId, auditReason, GetCorrelationId(httpContext)),
            cancellationToken);

        return Results.Ok(ToResponse(configuration));
    }

    public static async Task<IResult> TestConfiguration(
        IWhatsAppChannelConfigurationRepository repository,
        IWhatsAppCredentialProtector protector,
        IWhatsAppGraphValidationClient validationClient,
        InfrastructureOptions options,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (DemoAuthorization.Require(httpContext, options, Permission.ConfigureIntegrations) is { } forbidden)
        {
            return forbidden;
        }

        if (!WorkflowEndpoints.TryResolveScope(httpContext, out var scope, out _, out var validationError))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["tenantScope"] = [validationError] });
        }

        var configuration = await repository.GetAsync(scope, cancellationToken);
        if (configuration is null)
        {
            return Results.NotFound(new { message = "No WhatsApp connection is saved for this workspace yet." });
        }

        var accessToken = protector.Unprotect(configuration.EncryptedAccessToken);
        var validation = await validationClient.ValidateAsync(
            options.WhatsApp.GraphBaseUrl,
            configuration.PhoneNumberId,
            accessToken,
            options.WhatsApp.RequestTimeoutSeconds,
            cancellationToken);

        var updated = configuration with
        {
            LastValidatedAt = DateTimeOffset.UtcNow,
            LastValidationStatus = validation.Success ? "Success" : "Failed",
            LastValidationError = validation.Success ? null : validation.SafeError,
        };
        await repository.SaveAsync(updated, cancellationToken);

        return Results.Ok(new { success = validation.Success, message = validation.SafeError, verifiedName = validation.VerifiedName });
    }

    /// <summary>Asks Meta, read-only, where it sends this number's messages, and says in plain words whether that is IIP.
    /// The reason it exists: on 12 October 2026 a connection looked healthy while Meta's messages went elsewhere.</summary>
    public static async Task<IResult> CheckWithMeta(
        IWhatsAppChannelConfigurationRepository repository,
        IWhatsAppCredentialProtector protector,
        IWhatsAppGraphValidationClient validationClient,
        InfrastructureOptions options,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (DemoAuthorization.Require(httpContext, options, Permission.ConfigureIntegrations) is { } forbidden)
        {
            return forbidden;
        }

        if (!WorkflowEndpoints.TryResolveScope(httpContext, out var scope, out _, out var validationError))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["tenantScope"] = [validationError] });
        }

        var configuration = await repository.GetAsync(scope, cancellationToken);
        if (configuration is null)
        {
            return Results.NotFound(new { message = "No WhatsApp connection is saved for this workspace yet." });
        }

        var facts = await validationClient.ReadDeliverySetupAsync(
            options.WhatsApp.GraphBaseUrl,
            configuration.PhoneNumberId,
            configuration.WabaId ?? string.Empty,
            protector.Unprotect(configuration.EncryptedAccessToken),
            options.WhatsApp.RequestTimeoutSeconds,
            cancellationToken);
        var findings = WhatsAppDeliveryAnalysis.Analyse(configuration.WebhookCallbackUrl ?? BuildCallbackUrl(options), facts);
        return Results.Ok(new { checkedAt = DateTimeOffset.UtcNow, findings });
    }

    public static async Task<IResult> DeleteConfiguration(
        IWhatsAppChannelConfigurationRepository repository,
        IWhatsAppRuntimeSettingsProvider settingsProvider,
        IAuditRepository auditRepository,
        InfrastructureOptions options,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (DemoAuthorization.Require(httpContext, options, Permission.ConfigureIntegrations) is { } forbidden)
        {
            return forbidden;
        }

        if (!WorkflowEndpoints.TryResolveScope(httpContext, out var scope, out _, out var validationError))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["tenantScope"] = [validationError] });
        }

        var userId = DemoAuthorization.GetUserId(httpContext, fallback: string.Empty);
        var auditReason = AuditReasonReader.Read(httpContext);
        if (string.IsNullOrWhiteSpace(auditReason))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["auditReason"] = ["An audit reason is required to disconnect WhatsApp."]
            });
        }

        var existing = await repository.GetAsync(scope, cancellationToken);
        await repository.DeleteAsync(scope, cancellationToken);
        await settingsProvider.RefreshAsync(cancellationToken);

        await auditRepository.WriteAsync(
            AuditEvent.WhatsAppConfigurationRemoved(scope, userId, existing?.PhoneNumberId, auditReason, GetCorrelationId(httpContext)),
            cancellationToken);

        return Results.Ok(new { message = "WhatsApp connection removed." });
    }

    // Built from a deployment-level setting, not derived from the inbound request's Host header,
    // trusting a client-supplied host for a value registered with an external provider would let a
    // spoofed Host header redirect Meta's webhook traffic elsewhere.
    private static string? BuildCallbackUrl(InfrastructureOptions options) =>
        string.IsNullOrWhiteSpace(options.ApiPublicBaseUrl)
            ? null
            : $"{options.ApiPublicBaseUrl!.TrimEnd('/')}/api/v1/integrations/whatsapp/webhook";

    // Note: the ChannelProviderActive/RequiresChannelProviderDeploymentChange fields below are
    // retired as of the Phase 1 DI fix (InfrastructureRegistration.cs registers the real WhatsApp
    // inbound/outbound providers unconditionally now, see the blueprint's Phase 1 status table).
    // A saved, validated connection here is live immediately; no deployment change is ever
    // required. Kept as always-true/always-false rather than removed outright, so any client still
    // reading them gets an honest (if now-constant) answer instead of a breaking API removal.
    private static WhatsAppConfigurationStatusResponse ToResponse(WhatsAppChannelConfiguration configuration) =>
        new(
            Connected: configuration.IsActive,
            PhoneNumberId: configuration.PhoneNumberId,
            WabaId: configuration.WabaId,
            BusinessDisplayName: configuration.BusinessDisplayName,
            ConfiguredByUserId: configuration.ConfiguredByUserId,
            ConfiguredAt: configuration.ConfiguredAt,
            LastValidatedAt: configuration.LastValidatedAt,
            LastValidationStatus: configuration.LastValidationStatus,
            LastValidationError: configuration.LastValidationError,
            WebhookCallbackUrl: configuration.WebhookCallbackUrl,
            WebhookRegistrationStatus: configuration.WebhookRegistrationStatus,
            WebhookRegistrationError: configuration.WebhookRegistrationError,
            ChannelProviderActive: true,
            RequiresChannelProviderDeploymentChange: false);

    private static string GetCorrelationId(HttpContext httpContext)
    {
        if (httpContext.Request.Headers.TryGetValue("X-Correlation-ID", out var header) && !string.IsNullOrWhiteSpace(header))
        {
            return header.ToString();
        }

        return httpContext.TraceIdentifier;
    }
}

public sealed record WhatsAppSaveConfigurationRequest(
    string PhoneNumberId,
    string AccessToken,
    string AppSecret,
    string WabaId,
    string? BusinessDisplayName);

public sealed record WhatsAppConfigurationStatusResponse(
    bool Connected,
    string? PhoneNumberId,
    string? WabaId,
    string? BusinessDisplayName,
    string? ConfiguredByUserId,
    DateTimeOffset? ConfiguredAt,
    DateTimeOffset? LastValidatedAt,
    string? LastValidationStatus,
    string? LastValidationError,
    string? WebhookCallbackUrl,
    string? WebhookRegistrationStatus,
    string? WebhookRegistrationError,
    bool ChannelProviderActive,
    bool RequiresChannelProviderDeploymentChange)
{
    public static WhatsAppConfigurationStatusResponse NotConnected(string? callbackUrl) =>
        new(false, null, null, null, null, null, null, null, null, callbackUrl, null, null, true, false);
}
