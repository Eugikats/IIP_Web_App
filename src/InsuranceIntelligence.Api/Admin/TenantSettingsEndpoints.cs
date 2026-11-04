using InsuranceIntelligence.Api.Workflow;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Workflow;

namespace InsuranceIntelligence.Api.Admin;

public sealed record TenantSettingsResponse(
    bool RequireIraApprovalForPublish,
    int MissingDocumentReminderDays,
    int SlaWarningLeadHours,
    int SeatLimit,
    int ClaimsMeterLimit,
    string? UpdatedByUserId,
    DateTimeOffset? UpdatedAt,
    TenantSettingsDefaults Defaults,
    string? PrivacyNoticeText = null,
    int PrivacyNoticeVersion = 1,
    string DefaultPrivacyNoticeText = "",
    bool CustomerMemoryEnabled = true,
    IReadOnlyList<string>? CustomerMemoryFieldKeys = null,
    IReadOnlyList<string>? CustomerMemoryDocumentKeys = null,
    IReadOnlyList<string>? DefaultCustomerMemoryFieldKeys = null,
    IReadOnlyList<string>? DefaultCustomerMemoryDocumentKeys = null);

/// <summary>What each setting is when nobody has changed it, so the screen can offer "reset".</summary>
public sealed record TenantSettingsDefaults(
    bool RequireIraApprovalForPublish,
    int MissingDocumentReminderDays,
    int SlaWarningLeadHours,
    int SeatLimit,
    int ClaimsMeterLimit);

/// <summary>Only the settings supplied are changed; the rest keep their current value.</summary>
public sealed record SaveTenantSettingsRequest(
    bool? RequireIraApprovalForPublish,
    int? MissingDocumentReminderDays,
    int? SlaWarningLeadHours,
    int? SeatLimit,
    int? ClaimsMeterLimit,
    string? PrivacyNoticeText = null,
    bool? CustomerMemoryEnabled = null,
    IReadOnlyList<string>? CustomerMemoryFieldKeys = null,
    IReadOnlyList<string>? CustomerMemoryDocumentKeys = null);

/// <summary>
/// The operating settings a workspace administrator can change (build-loop item 6): the publishing
/// rule, reminder and SLA warning timings, the seat limit and the claims allowance. Reads and
/// writes are scoped to the caller's own workspace and need ManageTenantSettings; every change is
/// audited by name (never a customer's data, there is none here).
/// </summary>
public static class TenantSettingsEndpoints
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
        SaveTenantSettingsRequest request,
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
            RequireIraApprovalForPublish = request.RequireIraApprovalForPublish ?? current.RequireIraApprovalForPublish,
            MissingDocumentReminderDays = request.MissingDocumentReminderDays ?? current.MissingDocumentReminderDays,
            SlaWarningLeadHours = request.SlaWarningLeadHours ?? current.SlaWarningLeadHours,
            SeatLimit = request.SeatLimit ?? current.SeatLimit,
            ClaimsMeterLimit = request.ClaimsMeterLimit ?? current.ClaimsMeterLimit,
            UpdatedByUserId = userId,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        if (request.PrivacyNoticeText is not null)
        {
            // An empty text goes back to the default. Any change of wording is a new version, so a
            // consent can name the exact words it agreed to.
            var text = string.IsNullOrWhiteSpace(request.PrivacyNoticeText) ? null : request.PrivacyNoticeText.Trim();
            if (text != current.PrivacyNoticeText)
            {
                updated = updated with { PrivacyNoticeText = text, PrivacyNoticeVersion = current.PrivacyNoticeVersion + 1 };
            }
        }

        updated = updated with
        {
            CustomerMemoryEnabled = request.CustomerMemoryEnabled ?? current.CustomerMemoryEnabled,
            CustomerMemoryFieldKeys = request.CustomerMemoryFieldKeys is null ? current.CustomerMemoryFieldKeys : CleanKeys(request.CustomerMemoryFieldKeys),
            CustomerMemoryDocumentKeys = request.CustomerMemoryDocumentKeys is null ? current.CustomerMemoryDocumentKeys : CleanKeys(request.CustomerMemoryDocumentKeys),
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
        await auditRepository.WriteAsync(
            AuditEvent.TenantSettingsUpdated(scope, userId, changed, httpContext.TraceIdentifier),
            cancellationToken);
        return Results.Ok(ToResponse(updated));
    }

    private static TenantSettingsResponse ToResponse(TenantSettings settings) =>
        new(
            settings.RequireIraApprovalForPublish,
            settings.MissingDocumentReminderDays,
            settings.SlaWarningLeadHours,
            settings.SeatLimit,
            settings.ClaimsMeterLimit,
            settings.UpdatedByUserId,
            settings.UpdatedAt,
            new TenantSettingsDefaults(
                true,
                TenantSettings.DefaultMissingDocumentReminderDays,
                TenantSettings.DefaultSlaWarningLeadHours,
                TenantSettings.DefaultSeatLimit,
                TenantSettings.DefaultClaimsMeterLimit),
            settings.PrivacyNoticeText,
            settings.PrivacyNoticeVersion,
            InsuranceIntelligence.Domain.Compliance.PrivacyNotices.DefaultText,
            settings.CustomerMemoryEnabled,
            settings.CustomerMemoryFieldKeys,
            settings.CustomerMemoryDocumentKeys,
            InsuranceIntelligence.Domain.Customers.CustomerMemoryPolicy.DefaultFieldKeys,
            InsuranceIntelligence.Domain.Customers.CustomerMemoryPolicy.DefaultDocumentKeys);

    private static IReadOnlyList<string>? CleanKeys(IReadOnlyList<string> keys)
    {
        var cleaned = keys.Select(key => key?.Trim() ?? string.Empty).Where(key => key.Length > 0).Distinct(StringComparer.Ordinal).ToArray();
        return cleaned.Length == 0 ? null : cleaned;
    }

    private static IResult Validation(string key, string message) =>
        Results.ValidationProblem(new Dictionary<string, string[]> { [key] = [message] });
}
