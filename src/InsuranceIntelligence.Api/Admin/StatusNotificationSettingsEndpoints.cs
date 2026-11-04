using InsuranceIntelligence.Api.Workflow;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Cases;

namespace InsuranceIntelligence.Api.Admin;

public sealed record StatusNotificationRow(
    string Status,
    string StaffLabel,
    bool SendsMessage,
    bool NeedsApproval,
    string? WhatsAppTemplateName,
    string? WhatsAppTemplateLanguage);

public sealed record StatusNotificationSettingsResponse(
    bool NotifyCustomerOnStatusChange,
    bool AutoSendStatusMessages,
    IReadOnlyList<StatusNotificationRow> Statuses);

public sealed record StatusNotificationTemplateInput(string? Name, string? Language);

/// <summary>Only what is supplied changes. Templates replace the whole set: a status left out has no template.</summary>
public sealed record SaveStatusNotificationSettingsRequest(
    bool? NotifyCustomerOnStatusChange,
    bool? AutoSendStatusMessages,
    IReadOnlyDictionary<string, StatusNotificationTemplateInput>? WhatsAppTemplates);

/// <summary>
/// Whether the customer is told when their claim's status changes, whether those messages may go without a
/// person approving them, and the WhatsApp templates Meta approved for each status (build-loop item 21).
/// Needs ManageTenantSettings; every change is audited by name.
/// </summary>
public static class StatusNotificationSettingsEndpoints
{
    public static async Task<IResult> Get(
        ITenantSettingsRepository repository,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!WorkflowEndpoints.TryResolveScope(httpContext, out var scope, out _, out var error))
        {
            return Validation("tenantScope", error);
        }

        return Results.Ok(ToResponse(await repository.GetAsync(scope, cancellationToken)));
    }

    public static async Task<IResult> Put(
        SaveStatusNotificationSettingsRequest request,
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
        var updated = current with
        {
            NotifyCustomerOnStatusChange = request?.NotifyCustomerOnStatusChange ?? current.NotifyCustomerOnStatusChange,
            AutoSendStatusMessages = request?.AutoSendStatusMessages ?? current.AutoSendStatusMessages,
            UpdatedByUserId = userId,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        if (request?.WhatsAppTemplates is { } templates)
        {
            var entered = new Dictionary<string, WhatsAppTemplateRef>(StringComparer.Ordinal);
            foreach (var (key, value) in templates)
            {
                if (string.IsNullOrWhiteSpace(value?.Name) && string.IsNullOrWhiteSpace(value?.Language))
                {
                    continue;
                }

                entered[key.Trim()] = new WhatsAppTemplateRef(value?.Name?.Trim() ?? string.Empty, string.IsNullOrWhiteSpace(value?.Language) ? "en" : value.Language.Trim());
            }

            updated = updated with { WhatsAppStatusTemplates = entered.Count == 0 ? null : entered };
        }

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

    private static StatusNotificationSettingsResponse ToResponse(TenantSettings settings) =>
        new(
            settings.NotifyCustomerOnStatusChange,
            settings.AutoSendStatusMessages,
            Enum.GetValues<CaseStatus>()
                .OrderBy(status => ClaimantTimeline.Rank(status))
                .ThenBy(status => (int)status)
                .Select(status =>
                {
                    WhatsAppTemplateRef? template = null;
                    settings.WhatsAppStatusTemplates?.TryGetValue(status.ToString(), out template);
                    return new StatusNotificationRow(
                        status.ToString(),
                        InsuranceIntelligence.Application.CommandCenter.ClaimStatusLabels.For(status),
                        ClaimStatusNotificationPolicy.SendsMessage(status),
                        !ClaimStatusNotificationPolicy.MaySendWithoutApproval(autoSendEnabled: true, status),
                        template?.Name,
                        template?.Language);
                })
                .ToArray());

    private static IResult Validation(string key, string message) =>
        Results.ValidationProblem(new Dictionary<string, string[]> { [key] = [message] });
}
