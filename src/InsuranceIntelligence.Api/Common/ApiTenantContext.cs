using InsuranceIntelligence.Domain.Administration;

namespace InsuranceIntelligence.Api.Common;

/// <summary>
/// Provides the calling user's tenant/workspace/role to endpoints and services
/// without coupling them to HttpContext. Registered as scoped per request.
/// </summary>
/// <remarks>
/// Extracted from the (removed) agent framework during the v2 migration so the
/// standard endpoints no longer depend on Api.Agents. The header-based resolution
/// in <see cref="HttpContextTenantContext"/> is Development-mode only and MUST be
/// replaced by JWT-claim (Entra multitenant) resolution during the auth re-derive.
/// </remarks>
public interface ITenantContext
{
    Guid TenantId { get; }
    string WorkspaceId { get; }
    PlatformRole UserRole { get; }
    string UserId { get; }
}

/// <summary>
/// Default implementation hydrated from request headers (Development mode)
/// or JWT claims (Entra mode) by the DI pipeline.
/// </summary>
public sealed class HttpContextTenantContext(
    IHttpContextAccessor httpContextAccessor,
    InsuranceIntelligence.Infrastructure.Configuration.InfrastructureOptions options)
    : ITenantContext
{
    private readonly Lazy<(Guid TenantId, string WorkspaceId, PlatformRole Role, string UserId)> _resolved =
        new(() => Resolve(httpContextAccessor, options));

    public Guid TenantId => _resolved.Value.TenantId;
    public string WorkspaceId => _resolved.Value.WorkspaceId;
    public PlatformRole UserRole => _resolved.Value.Role;
    public string UserId => _resolved.Value.UserId;

    private static (Guid TenantId, string WorkspaceId, PlatformRole Role, string UserId) Resolve(
        IHttpContextAccessor accessor,
        InsuranceIntelligence.Infrastructure.Configuration.InfrastructureOptions options)
    {
        var ctx = accessor.HttpContext;
        if (ctx is null)
            return (Guid.Empty, "default", PlatformRole.ClaimsOfficer, "system");

        var tenantStr = ctx.Request.Headers.TryGetValue("X-Tenant-Id", out var th) ? th.ToString() : options.TenantId;
        var workspaceStr = ctx.Request.Headers.TryGetValue("X-Workspace-Id", out var wh) ? wh.ToString() : options.WorkspaceId;
        var tenantId = Guid.TryParse(tenantStr, out var tid) ? tid : Guid.Empty;
        var workspaceId = string.IsNullOrWhiteSpace(workspaceStr) ? "default" : workspaceStr!;
        var role = Auth.DemoAuthorization.GetEffectiveRole(ctx, options);
        var userId = Auth.DemoAuthorization.GetUserId(ctx, "system");

        return (tenantId, workspaceId, role, userId);
    }
}
