using System.Text.Json;
using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Application.Platform;

public sealed record PlatformResult<T>(bool Ok, T? Value, string? Error, int Status = 200)
{
    public static PlatformResult<T> Success(T value) => new(true, value, null);

    public static PlatformResult<T> Fail(string error, int status = 400) => new(false, default, error, status);
}

/// <summary>Shared rules for the organisation platform: who may do what, the clock, JSON helpers.</summary>
public sealed class PlatformAccess(
    ITenantContextAccessor tenantContextAccessor,
    IPermissionService permissionService,
    TimeProvider? timeProvider = null)
{
    private bool? _admin;
    private bool? _approve;

    public TenantScope Scope => tenantContextAccessor.Current.Scope;

    public string UserId => tenantContextAccessor.Current.UserId;

    public DateTimeOffset Now => (timeProvider ?? TimeProvider.System).GetUtcNow();

    /// <summary>People who manage workspaces, structure and the access mode.</summary>
    public async Task<bool> IsOrgAdminAsync(CancellationToken cancellationToken) =>
        _admin ??= await permissionService.HasPermissionAsync(Permission.ManagePlatform, cancellationToken);

    /// <summary>People who may approve, reject, activate and resume automations.</summary>
    public async Task<bool> CanApproveAsync(CancellationToken cancellationToken) =>
        _approve ??= await permissionService.HasPermissionAsync(Permission.ApprovePlatformAutomations, cancellationToken);

    public Task RequireViewAsync(CancellationToken cancellationToken) =>
        permissionService.RequirePermissionAsync(Permission.ViewPlatform, cancellationToken);

    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    internal static string Write<T>(T value) => JsonSerializer.Serialize(value, Json);

    internal static T Read<T>(string? json, T fallback)
    {
        if (string.IsNullOrWhiteSpace(json)) return fallback;
        try { return JsonSerializer.Deserialize<T>(json, Json) ?? fallback; }
        catch (JsonException) { return fallback; }
    }

    internal static string Clip(string? value, int max) =>
        string.IsNullOrEmpty(value) ? "" : value.Length <= max ? value : value[..max];

    internal static JsonElement Element(string? json)
    {
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
            return doc.RootElement.Clone();
        }
        catch (JsonException)
        {
            using var empty = JsonDocument.Parse("{}");
            return empty.RootElement.Clone();
        }
    }
}

/// <summary>Writes the audit event for a platform change. Metadata only: ids, statuses and the action, never customer words.</summary>
public sealed class PlatformAudit(IAuditRepository auditRepository, ITenantContextAccessor tenantContextAccessor, TimeProvider? timeProvider = null)
{
    public Task LogAsync(string action, Guid entityId, string? before, string? after, CancellationToken cancellationToken, string? reason = null)
    {
        var context = tenantContextAccessor.Current;
        var system = context.UserId.StartsWith("system:", StringComparison.Ordinal);
        return auditRepository.WriteAsync(new AuditEvent(
            Guid.NewGuid(), context.Scope, null, entityId.ToString("N"), context.UserId,
            system ? ActorType.System : ActorType.User,
            action, (timeProvider ?? TimeProvider.System).GetUtcNow(), before, after,
            [entityId.ToString("N")], null, null, "recorded", null, reason, Guid.NewGuid().ToString("N")), cancellationToken);
    }
}
