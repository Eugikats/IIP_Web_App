using System.Text.Json;
using InsuranceIntelligence.Api.Auth;
using InsuranceIntelligence.Api.Workflow;
using InsuranceIntelligence.Application.Workspaces;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.ClientPortal;
using InsuranceIntelligence.Domain.Integrations;
using InsuranceIntelligence.Infrastructure.Configuration;
using InsuranceIntelligence.Infrastructure.WhatsApp;

namespace InsuranceIntelligence.Api.Integrations;

/// <summary>Tenant request intake and the explicitly cross-tenant Publisher Support queue.
/// Verification codes are deliberately not part of the persisted request contract.</summary>
public static class WhatsAppOnboardingEndpoints
{
    private const string Kind = "whatsapp-onboarding";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static async Task<IResult> GetMine(
        IWorkspaceRecordStore records, InfrastructureOptions options, HttpContext context, CancellationToken cancellationToken)
    {
        if (DemoAuthorization.Require(context, options, Permission.ConfigureIntegrations) is { } forbidden) return forbidden;
        if (!WorkflowEndpoints.TryResolveScope(context, out var scope, out _, out var error))
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["tenantScope"] = [error] });
        var row = await records.GetAsync(scope, Kind, "current", cancellationToken);
        var request = row is null ? null : Deserialize(row.PayloadJson);
        return Results.Ok(new { status = request?.Status ?? "NotRequested", request, metaVerification = ManagedReady(options) ? "Available" : "Not configured" });
    }

    public static async Task<IResult> Submit(
        SubmitOnboardingRequest request, IWorkspaceRecordStore records, IAuditRepository audit,
        InfrastructureOptions options, HttpContext context, CancellationToken cancellationToken)
    {
        if (DemoAuthorization.Require(context, options, Permission.ConfigureIntegrations) is { } forbidden) return forbidden;
        if (!WorkflowEndpoints.TryResolveScope(context, out var scope, out var userId, out var error))
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["tenantScope"] = [error] });
        var phone = NormalizeE164(request.PhoneNumber);
        if (phone is null || string.IsNullOrWhiteSpace(request.DisplayName) || string.IsNullOrWhiteSpace(request.ContactEmail) || !request.Consent)
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["request"] = ["Enter a valid international phone number, a display name and contact email, and confirm that support may contact you about setup."]
            });
        var existing = await records.GetAsync(scope, Kind, "current", cancellationToken);
        if (existing is not null && Deserialize(existing.PayloadJson)?.Status != "Cancelled")
            return Results.Conflict(new { message = "A WhatsApp number request is already open for this workspace." });

        var saved = new OnboardingRequest(Guid.NewGuid(), phone, request.DisplayName.Trim(), request.ContactEmail.Trim(), "Requested", DateTimeOffset.UtcNow,
            DemoAuthorization.GetUserId(context, string.Empty), request.Consent, DateTimeOffset.UtcNow);
        await records.SaveAsync(scope, Kind, "current", JsonSerializer.Serialize(saved, JsonOptions), cancellationToken);
        await WriteAudit(audit, scope, userId, "whatsapp.onboarding.requested", null, saved.Status, context, cancellationToken);
        return Results.Created("/api/v1/integrations/whatsapp/onboarding", saved);
    }

    public static async Task<IResult> CancelMine(
        IWorkspaceRecordStore records, IAuditRepository audit, InfrastructureOptions options, HttpContext context, CancellationToken cancellationToken)
    {
        if (DemoAuthorization.Require(context, options, Permission.ConfigureIntegrations) is { } forbidden) return forbidden;
        if (!WorkflowEndpoints.TryResolveScope(context, out var scope, out var userId, out var error))
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["tenantScope"] = [error] });
        var row = await records.GetAsync(scope, Kind, "current", cancellationToken);
        var current = row is null ? null : Deserialize(row.PayloadJson);
        if (current is null) return Results.NotFound();
        if (current.Status is "Active" or "Cancelled" or "Registering" || current.RegistrationSucceeded)
            return Results.Conflict(new { message = "This number has reached registration. Ask Publisher Support to disconnect it." });
        var updated = current with { Status = "Cancelled" };
        await records.SaveAsync(scope, Kind, "current", JsonSerializer.Serialize(updated, JsonOptions), cancellationToken);
        await WriteAudit(audit, scope, userId, "whatsapp.onboarding.cancelled", current.Status, updated.Status, context, cancellationToken);
        return Results.Ok(updated);
    }

    public static async Task<IResult> SupportQueue(
        IWorkspaceRecordStore records, InfrastructureOptions options, HttpContext context, CancellationToken cancellationToken)
    {
        if (DemoAuthorization.Require(context, options, Permission.ManageTenantOnboarding) is { } forbidden) return forbidden;
        var rows = await records.ListPlatformAsync(Kind, 250, cancellationToken);
        var items = rows.Select(row => new
        {
            tenantId = row.Scope.TenantId,
            workspaceId = row.Scope.WorkspaceId,
            request = Deserialize(row.Record.PayloadJson)
        }).Where(item => item.request is not null && item.request.Status is not ("Cancelled" or "Active")).ToArray();
        return Results.Ok(new { items, metaVerification = ManagedReady(options) ? "Available" : "Not configured" });
    }

    public static async Task<IResult> AssignNumber(
        string tenantId, string workspaceId, AssignManagedNumber request, IWorkspaceRecordStore records,
        IWhatsAppGraphValidationClient validation, IWhatsAppChannelConfigurationRepository configurations,
        IAuditRepository audit, InfrastructureOptions options,
        HttpContext context, CancellationToken cancellationToken)
    {
        if (DemoAuthorization.Require(context, options, Permission.ManageTenantOnboarding) is { } forbidden) return forbidden;
        if (!ManagedReady(options)) return Results.Problem("Managed WhatsApp setup is Not configured.", statusCode: 503);
        if (string.IsNullOrWhiteSpace(tenantId) || string.IsNullOrWhiteSpace(workspaceId)
            || string.IsNullOrWhiteSpace(request.WabaId) || string.IsNullOrWhiteSpace(request.PhoneNumberId))
            return Results.BadRequest(new { message = "A tenant, workspace, WABA id and phone number id are required." });
        var scope = TenantScope.Create(tenantId, workspaceId);
        var row = await records.GetAsync(scope, Kind, "current", cancellationToken);
        var current = row is null ? null : Deserialize(row.PayloadJson);
        if (current is null) return Results.NotFound();
        if (current.Status is not ("Requested" or "NeedsAttention"))
            return Results.Conflict(new { message = "This request cannot be assigned now." });
        if (current.RegistrationSucceeded && current.PhoneNumberId != request.PhoneNumberId.Trim())
            return Results.Conflict(new { message = "A number already registered at Meta cannot be replaced here." });
        var holders = await records.ListPlatformAsync(Kind, 250, cancellationToken);
        if (holders.Any(item => (item.Scope.TenantId != tenantId || item.Scope.WorkspaceId != workspaceId)
            && Deserialize(item.Record.PayloadJson) is { Status: not "Cancelled", PhoneNumberId: not null } other
            && other.PhoneNumberId == request.PhoneNumberId.Trim()))
            return Results.Conflict(new { message = "That Meta phone number id is already assigned to another organisation." });
        var active = await configurations.ListActiveAsync(cancellationToken);
        if (active.Any(item => item.PhoneNumberId == request.PhoneNumberId.Trim()
            && (item.Scope.TenantId != tenantId || item.Scope.WorkspaceId != workspaceId)))
            return Results.Conflict(new { message = "That Meta phone number id is already connected to another organisation." });
        var checkedNumber = await validation.ValidateAsync(options.WhatsApp.GraphBaseUrl, request.PhoneNumberId.Trim(),
            options.WhatsApp.ManagedAccessToken!, options.WhatsApp.RequestTimeoutSeconds, cancellationToken);
        if (!checkedNumber.Success) return Results.BadRequest(new { message = checkedNumber.SafeError ?? "Meta could not verify the phone number id." });
        if (Digits(checkedNumber.DisplayPhoneNumber) != Digits(current.PhoneNumber))
            return Results.BadRequest(new { message = "Meta's phone number does not match the organisation's request." });
        var updated = current with { Status = current.RegistrationSucceeded ? "NeedsAttention" : "AwaitingCode",
            WabaId = request.WabaId.Trim(), PhoneNumberId = request.PhoneNumberId.Trim() };
        await Save(records, scope, updated, cancellationToken);
        await WriteAudit(audit, scope, DemoAuthorization.GetUserId(context, ""), "whatsapp.onboarding.assigned", current.Status, updated.Status, context, cancellationToken);
        return Results.Ok(updated);
    }

    public static async Task<IResult> RequestCode(
        RequestManagedCode request, IWorkspaceRecordStore records, IWhatsAppPhoneNumberLifecycleClient meta,
        IAuditRepository audit, InfrastructureOptions options, HttpContext context, CancellationToken cancellationToken)
    {
        if (DemoAuthorization.Require(context, options, Permission.ConfigureIntegrations) is { } forbidden) return forbidden;
        if (!WorkflowEndpoints.TryResolveScope(context, out var scope, out var userId, out var error)) return ScopeError(error);
        if (!ManagedReady(options)) return Results.Problem("Managed WhatsApp setup is Not configured.", statusCode: 503);
        var current = await Mine(records, scope, cancellationToken);
        if (current?.Status is not ("AwaitingCode" or "CodeSent") || current.PhoneNumberId is null)
            return Results.Conflict(new { message = "This number is not ready for a code request." });
        if (current.CodeRequestedAt is { } sent && DateTimeOffset.UtcNow - sent < TimeSpan.FromMinutes(1))
            return Results.Problem("Wait a minute before requesting another code.", statusCode: 429);
        var method = request.Method?.Trim().ToUpperInvariant();
        if (method is not ("SMS" or "VOICE")) return Results.BadRequest(new { message = "Choose SMS or voice." });
        var result = await meta.RequestCodeAsync(current.PhoneNumberId, method, "en_US", options.WhatsApp.ManagedAccessToken!, cancellationToken);
        if (!result.Success)
        {
            await WriteAudit(audit, scope, userId, "whatsapp.onboarding.code_request_failed", current.Status, current.Status, context, cancellationToken);
            return MetaFailure(result);
        }
        var updated = current with { Status = "CodeSent", CodeMethod = method, CodeRequestedAt = DateTimeOffset.UtcNow, CodeAttempts = 0 };
        await Save(records, scope, updated, cancellationToken);
        await WriteAudit(audit, scope, userId, "whatsapp.onboarding.code_requested", current.Status, updated.Status, context, cancellationToken);
        return Results.Ok(updated);
    }

    public static async Task<IResult> VerifyCode(
        VerifyManagedCode request, IWorkspaceRecordStore records, IWhatsAppPhoneNumberLifecycleClient meta,
        IAuditRepository audit, InfrastructureOptions options, HttpContext context, CancellationToken cancellationToken)
    {
        if (DemoAuthorization.Require(context, options, Permission.ConfigureIntegrations) is { } forbidden) return forbidden;
        if (!WorkflowEndpoints.TryResolveScope(context, out var scope, out var userId, out var error)) return ScopeError(error);
        if (!ManagedReady(options)) return Results.Problem("Managed WhatsApp setup is Not configured.", statusCode: 503);
        var row = await records.GetAsync(scope, Kind, "current", cancellationToken);
        var current = row is null ? null : Deserialize(row.PayloadJson);
        if (current?.Status != "CodeSent" || current.PhoneNumberId is null || current.CodeRequestedAt is null)
            return Results.Conflict(new { message = "Request a code before verifying this number." });
        if (DateTimeOffset.UtcNow - current.CodeRequestedAt > TimeSpan.FromMinutes(10))
            return Results.Conflict(new { message = "This code has expired. Request a new one." });
        if (current.CodeAttempts >= 5) return Results.Problem("Too many attempts. Request a new code.", statusCode: 429);
        if (request.Code is null || request.Code.Length is < 4 or > 10 || !request.Code.All(char.IsDigit))
            return Results.BadRequest(new { message = "Enter the numeric code Meta sent to the number." });
        var attempts = current with { CodeAttempts = current.CodeAttempts + 1 };
        if (!await records.TryReplaceAsync(scope, Kind, "current", row!.PayloadJson,
                JsonSerializer.Serialize(attempts, JsonOptions), cancellationToken))
            return Results.Conflict(new { message = "The verification state changed. Check the current request and try again." });
        var result = await meta.VerifyCodeAsync(current.PhoneNumberId, request.Code, options.WhatsApp.ManagedAccessToken!, cancellationToken);
        if (!result.Success)
        {
            await WriteAudit(audit, scope, userId, "whatsapp.onboarding.code_rejected", current.Status, current.Status, context, cancellationToken);
            return MetaFailure(result);
        }
        var updated = attempts with { Status = "Verified", VerifiedAt = DateTimeOffset.UtcNow };
        await Save(records, scope, updated, cancellationToken);
        await WriteAudit(audit, scope, userId, "whatsapp.onboarding.code_verified", current.Status, updated.Status, context, cancellationToken);
        return Results.Ok(updated);
    }

    public static async Task<IResult> RegisterNumber(
        RegisterManagedNumber request, IWorkspaceRecordStore records, IWhatsAppPhoneNumberLifecycleClient meta,
        IWhatsAppGraphValidationClient validation, IWhatsAppChannelConfigurationRepository configurations,
        IWhatsAppCredentialProtector protector, IWhatsAppRuntimeSettingsProvider runtime, IAuditRepository audit,
        InfrastructureOptions options, HttpContext context, CancellationToken cancellationToken)
    {
        if (DemoAuthorization.Require(context, options, Permission.ConfigureIntegrations) is { } forbidden) return forbidden;
        if (!WorkflowEndpoints.TryResolveScope(context, out var scope, out var userId, out var error)) return ScopeError(error);
        if (!ManagedReady(options)) return Results.Problem("Managed WhatsApp setup is Not configured.", statusCode: 503);
        var row = await records.GetAsync(scope, Kind, "current", cancellationToken);
        var current = row is null ? null : Deserialize(row.PayloadJson);
        if ((current?.Status != "Verified" && !(current?.Status == "NeedsAttention" && current.RegistrationSucceeded))
            || current.PhoneNumberId is null || current.WabaId is null)
            return Results.Conflict(new { message = "Verify the number before registering it." });
        if (!current.RegistrationSucceeded && (request.Pin is null || request.Pin.Length != 6 || !request.Pin.All(char.IsAsciiDigit)))
            return Results.BadRequest(new { message = "Enter a six-digit Meta registration PIN." });
        var callbackUrl = string.IsNullOrWhiteSpace(options.ApiPublicBaseUrl) ? null
            : $"{options.ApiPublicBaseUrl.TrimEnd('/')}/api/v1/integrations/whatsapp/webhook";
        if (callbackUrl is null) return Results.Problem("The public API callback is Not configured.", statusCode: 503);
        var existing = await configurations.ListActiveAsync(cancellationToken);
        if (existing.Any(item => item.PhoneNumberId == current.PhoneNumberId
            && (item.Scope.TenantId != scope.TenantId || item.Scope.WorkspaceId != scope.WorkspaceId)))
            return Results.Conflict(new { message = "That Meta phone number is already connected to another organisation." });
        var token = options.WhatsApp.ManagedAccessToken!;
        var inProgress = current with { Status = "Registering" };
        if (!await records.TryReplaceAsync(scope, Kind, "current", row!.PayloadJson,
                JsonSerializer.Serialize(inProgress, JsonOptions), cancellationToken))
            return Results.Conflict(new { message = "The registration state changed. Check the current request and try again." });
        await WriteAudit(audit, scope, userId, "whatsapp.onboarding.registration_started", current.Status, inProgress.Status, context, cancellationToken);
        if (!current.RegistrationSucceeded)
        {
            var registered = await meta.RegisterAsync(current.PhoneNumberId, request.Pin!, token, cancellationToken);
            if (!registered.Success)
            {
                await Save(records, scope, current, cancellationToken);
                await WriteAudit(audit, scope, userId, "whatsapp.onboarding.registration_failed", inProgress.Status, current.Status, context, cancellationToken);
                return MetaFailure(registered);
            }
        }
        inProgress = inProgress with { RegistrationSucceeded = true };
        await Save(records, scope, inProgress, cancellationToken);
        var subscribed = await meta.SubscribeAppAsync(current.WabaId, token, cancellationToken);
        if (!subscribed.Success)
        {
            await MarkNeedsAttention(records, audit, scope, inProgress, userId, context, cancellationToken);
            return MetaFailure(subscribed);
        }
        var now = DateTimeOffset.UtcNow;
        var verifyToken = ClaimantTokenGenerator.GenerateRawToken();
        var configuration = new WhatsAppChannelConfiguration
        {
            Scope = scope,
            PhoneNumberId = current.PhoneNumberId,
            WabaId = current.WabaId,
            BusinessDisplayName = current.DisplayName,
            EncryptedAccessToken = protector.Protect(token),
            EncryptedAppSecret = protector.Protect(options.WhatsApp.ManagedAppSecret!),
            EncryptedVerifyToken = protector.Protect(verifyToken),
            IsActive = true,
            ConfiguredByUserId = userId,
            ConfiguredAt = now,
            LastValidatedAt = now,
            LastValidationStatus = "Success",
            WebhookCallbackUrl = callbackUrl,
            WebhookRegistrationStatus = "Pending"
        };
        await configurations.SaveAsync(configuration, cancellationToken);
        await runtime.RefreshAsync(cancellationToken);
        var webhook = await validation.RegisterWebhookAsync(options.WhatsApp.GraphBaseUrl, current.WabaId,
            token, callbackUrl, verifyToken, options.WhatsApp.RequestTimeoutSeconds, cancellationToken);
        if (!webhook.Success)
        {
            await configurations.SaveAsync(configuration with
            {
                IsActive = false,
                WebhookRegistrationStatus = "RegistrationFailed",
                WebhookRegistrationError = webhook.SafeError
            }, cancellationToken);
            await runtime.RefreshAsync(cancellationToken);
            await MarkNeedsAttention(records, audit, scope, inProgress, userId, context, cancellationToken);
            return Results.Problem(webhook.SafeError ?? "Meta did not confirm the webhook registration.", statusCode: 409);
        }
        await configurations.SaveAsync(configuration with { WebhookRegistrationStatus = "Registered" }, cancellationToken);
        var updated = inProgress with { Status = "Active" };
        await Save(records, scope, updated, cancellationToken);
        await WriteAudit(audit, scope, userId, "whatsapp.onboarding.activated", current.Status, updated.Status, context, cancellationToken);
        return Results.Ok(updated);
    }

    private static async Task MarkNeedsAttention(IWorkspaceRecordStore records, IAuditRepository audit,
        TenantScope scope, OnboardingRequest current, string userId, HttpContext context, CancellationToken cancellationToken)
    {
        var updated = current with { Status = "NeedsAttention" };
        await Save(records, scope, updated, cancellationToken);
        await WriteAudit(audit, scope, userId, "whatsapp.onboarding.needs_attention", current.Status, updated.Status, context, cancellationToken);
    }

    private static bool ManagedReady(InfrastructureOptions options) =>
        !string.IsNullOrWhiteSpace(options.WhatsApp.ManagedAccessToken)
        && !string.IsNullOrWhiteSpace(options.WhatsApp.ManagedAppSecret)
        && Uri.TryCreate(options.ApiPublicBaseUrl, UriKind.Absolute, out var callback)
        && callback.Scheme == Uri.UriSchemeHttps;
    private static string Digits(string? value) => new((value ?? "").Where(char.IsDigit).ToArray());
    private static IResult ScopeError(string error) => Results.ValidationProblem(new Dictionary<string, string[]> { ["tenantScope"] = [error] });
    private static IResult MetaFailure(WhatsAppPhoneNumberResult result) => Results.Problem(result.SafeMessage ?? "Meta could not complete this step.", statusCode: result.Failure == WhatsAppPhoneNumberFailure.RateLimited ? 429 : 409);
    private static async Task<OnboardingRequest?> Mine(IWorkspaceRecordStore records, TenantScope scope, CancellationToken cancellationToken) =>
        (await records.GetAsync(scope, Kind, "current", cancellationToken)) is { } row ? Deserialize(row.PayloadJson) : null;
    private static Task Save(IWorkspaceRecordStore records, TenantScope scope, OnboardingRequest request, CancellationToken cancellationToken) =>
        records.SaveAsync(scope, Kind, "current", JsonSerializer.Serialize(request, JsonOptions), cancellationToken);

    private static string? NormalizeE164(string? value)
    {
        var digits = new string((value ?? string.Empty).Where(char.IsDigit).ToArray());
        if (digits.Length is < 8 or > 15 || !(value ?? string.Empty).TrimStart().StartsWith('+')) return null;
        return $"+{digits}";
    }

    private static OnboardingRequest? Deserialize(string json) => JsonSerializer.Deserialize<OnboardingRequest>(json, JsonOptions);

    private static Task WriteAudit(IAuditRepository audit, TenantScope scope, string userId, string action, string? before, string after, HttpContext context, CancellationToken cancellationToken) =>
        audit.WriteAsync(new AuditEvent(Guid.NewGuid(), scope, null, null, userId, ActorType.User, action, DateTimeOffset.UtcNow,
            before, after, Array.Empty<string>(), null, null, "accepted", null, null,
            context.Request.Headers.TryGetValue("X-Correlation-ID", out var id) && !string.IsNullOrWhiteSpace(id) ? id.ToString() : context.TraceIdentifier), cancellationToken);
}

public sealed record SubmitOnboardingRequest(string PhoneNumber, string DisplayName, string ContactEmail, bool Consent);
public sealed record OnboardingRequest(Guid Id, string PhoneNumber, string DisplayName, string ContactEmail,
    string Status, DateTimeOffset CreatedAt, string RequestedByUserId, bool Consent, DateTimeOffset ConsentAt,
    string? WabaId = null, string? PhoneNumberId = null, string? CodeMethod = null,
    DateTimeOffset? CodeRequestedAt = null, int CodeAttempts = 0, DateTimeOffset? VerifiedAt = null,
    bool RegistrationSucceeded = false);
public sealed record AssignManagedNumber(string WabaId, string PhoneNumberId);
public sealed record RequestManagedCode(string Method);
public sealed record VerifyManagedCode(string Code);
public sealed record RegisterManagedNumber(string? Pin);
