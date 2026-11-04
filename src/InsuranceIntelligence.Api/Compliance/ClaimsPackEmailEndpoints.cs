using InsuranceIntelligence.Api.Workflow;
using InsuranceIntelligence.Application.Compliance;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.ClientPortal;
using InsuranceIntelligence.Domain.Compliance;
using InsuranceIntelligence.Domain.Storage;

namespace InsuranceIntelligence.Api.Compliance;

public sealed record ClaimsPackDeliveryResponse(IReadOnlyList<string> Recipients, string Trigger, string? TriggerStatus, int MaxRecipients, long MaxAttachmentBytes, int LinkDays);

public sealed record SaveClaimsPackDeliveryRequest(IReadOnlyList<string>? Recipients, string? Trigger, string? TriggerStatus);

/// <summary>
/// Emailing a claims pack (build-loop item 25): the workspace's recipients and trigger (Admin), "Send pack now" and the
/// history on the claim (staff who may export evidence), and the public download page for a large pack's secure link.
/// </summary>
public static class ClaimsPackEmailEndpoints
{
    public static async Task<IResult> GetSettings(ITenantSettingsRepository repository, HttpContext httpContext, CancellationToken cancellationToken)
    {
        if (!WorkflowEndpoints.TryResolveScope(httpContext, out var scope, out _, out var error))
        {
            return Validation("tenantScope", error);
        }

        return Results.Ok(ToResponse(await repository.GetAsync(scope, cancellationToken)));
    }

    public static async Task<IResult> PutSettings(
        SaveClaimsPackDeliveryRequest request,
        ITenantSettingsRepository repository,
        IAuditRepository auditRepository,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!WorkflowEndpoints.TryResolveScope(httpContext, out var scope, out var userId, out var error))
        {
            return Validation("tenantScope", error);
        }

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Validation("userId", "Authenticated user context is required to change settings.");
        }

        var current = await repository.GetAsync(scope, cancellationToken);
        var trigger = string.IsNullOrWhiteSpace(request?.Trigger) ? current.ClaimsPackTrigger : request!.Trigger!.Trim();
        var updated = current with
        {
            ClaimsPackRecipients = request?.Recipients is null ? current.ClaimsPackRecipients : ClaimsPackDeliveryRules.Normalise(request.Recipients),
            ClaimsPackTrigger = ClaimsPackDeliveryRules.TryParseTrigger(trigger, out var parsed) ? parsed.ToString() : trigger,
            ClaimsPackTriggerStatus = request?.Trigger is null && request?.TriggerStatus is null ? current.ClaimsPackTriggerStatus
                : string.IsNullOrWhiteSpace(request?.TriggerStatus) ? null : request!.TriggerStatus!.Trim(),
            UpdatedByUserId = userId,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        var problems = updated.Validate();
        if (problems.Count > 0)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["settings"] = problems.ToArray() });
        }

        var changed = updated.ChangedFrom(current);
        if (changed.Count == 0)
        {
            return Results.Ok(ToResponse(current));
        }

        await repository.SaveAsync(updated, cancellationToken);
        await auditRepository.WriteAsync(AuditEvent.TenantSettingsUpdated(scope, userId, changed, httpContext.TraceIdentifier), cancellationToken);
        return Results.Ok(ToResponse(updated));
    }

    public static async Task<IResult> SendNow(
        Guid caseId,
        ICaseRepository caseRepository,
        IClaimsPackEmailService service,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!WorkflowEndpoints.TryResolveScope(httpContext, out var scope, out var userId, out var error))
        {
            return Validation("tenantScope", error);
        }

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Validation("userId", "Authenticated user context is required to send a claims pack.");
        }

        var claim = await caseRepository.GetAsync(caseId, cancellationToken);
        if (claim is null || claim.Scope != scope)
        {
            return Results.NotFound(new { message = "Claim was not found." });
        }

        var result = await service.SendAsync(scope, caseId, $"manual:{Guid.NewGuid():N}", $"sent by {userId}", userId, httpContext.TraceIdentifier, cancellationToken);
        return result.Outcome switch
        {
            "Sent" or "PartlySent" => Results.Ok(result),
            "NotFound" => Results.NotFound(new { message = result.Message }),
            _ => Results.Conflict(new { outcome = result.Outcome, message = result.Message ?? "The pack could not be sent." }),
        };
    }

    public static async Task<IResult> History(
        Guid caseId,
        ICaseRepository caseRepository,
        IClaimsPackEmailService service,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!WorkflowEndpoints.TryResolveScope(httpContext, out var scope, out _, out var error))
        {
            return Validation("tenantScope", error);
        }

        var claim = await caseRepository.GetAsync(caseId, cancellationToken);
        if (claim is null || claim.Scope != scope)
        {
            return Results.NotFound(new { message = "Claim was not found." });
        }

        var history = await service.HistoryAsync(scope, caseId, cancellationToken);
        return Results.Ok(history.Select(item => new
        {
            at = item.At,
            reason = item.Reason,
            sentBy = item.SentBy,
            recipients = item.RecipientsMasked,
            mode = item.Mode,
            outcome = item.Outcome,
            files = item.FileCount,
            bytes = item.PackBytes,
            linkExpiresAt = item.LinkExpiresAt,
        }));
    }

    /// <summary>The public download for a large pack. Anyone holding the link can download until it expires or runs out of
    /// downloads; there is no sign in, so the link is long, random, kept only as a fingerprint, and every open is audited.</summary>
    public static async Task<IResult> DownloadShared(
        string token,
        IClaimsPackShareLinkRepository links,
        IEvidencePackStorageProvider storage,
        IAuditRepository auditRepository,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var gone = Results.NotFound(new { message = "This link has expired or is no longer valid. Ask the sender for a new one." });
        if (string.IsNullOrWhiteSpace(token) || token.Length > 200)
        {
            return gone;
        }

        var link = await links.FindByTokenHashAsync(ClaimantTokenGenerator.Hash(token), cancellationToken);
        var now = DateTimeOffset.UtcNow;
        if (link is null || !link.IsUsableAt(now))
        {
            return gone;
        }

        Stream content;
        try
        {
            content = await storage.ReadAsync(link.Scope, link.ObjectKey, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return gone;
        }

        await links.SaveAsync(link with { DownloadCount = link.DownloadCount + 1 }, cancellationToken);
        await auditRepository.WriteAsync(AuditEvent.ClaimsPackLinkOpened(link.Scope, link.CaseId, link.DownloadCount + 1, httpContext.TraceIdentifier), cancellationToken);
        return Results.File(content, "application/zip", link.FileName);
    }

    private static ClaimsPackDeliveryResponse ToResponse(TenantSettings settings) =>
        new(
            ClaimsPackDeliveryRules.Normalise(settings.ClaimsPackRecipients),
            ClaimsPackDeliveryRules.TryParseTrigger(settings.ClaimsPackTrigger, out var trigger) ? trigger.ToString() : "Manual",
            settings.ClaimsPackTriggerStatus,
            ClaimsPackDeliveryRules.MaxRecipients,
            ClaimsPackDeliveryRules.MaxAttachmentBytes,
            (int)ClaimsPackDeliveryRules.LinkLifetime.TotalDays);

    private static IResult Validation(string key, string message) =>
        Results.ValidationProblem(new Dictionary<string, string[]> { [key] = [message] });
}
