using System.Text.Json;
using InsuranceIntelligence.Api.Auth;
using InsuranceIntelligence.Api.Workflow;
using InsuranceIntelligence.Application.Workspaces;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Infrastructure.Configuration;

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
        return Results.Ok(new { status = request?.Status ?? "NotRequested", request, metaVerification = "Not configured" });
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
        if (existing is not null && Deserialize(existing.PayloadJson)?.Status is not ("Cancelled" or "Active"))
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
        if (current.Status is "Active" or "Cancelled") return Results.Conflict(new { message = "This request can no longer be cancelled." });
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
        return Results.Ok(new { items, metaVerification = "Not configured" });
    }

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
    string Status, DateTimeOffset CreatedAt, string RequestedByUserId, bool Consent, DateTimeOffset ConsentAt);
