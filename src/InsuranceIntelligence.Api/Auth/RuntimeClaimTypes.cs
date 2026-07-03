using System.Security.Claims;

namespace InsuranceIntelligence.Api.Auth;

public static class RuntimeClaimTypes
{
    public static readonly IReadOnlyCollection<string> TenantScope =
    [
        "tenant_id",
        "tenantId",
        "iip_tenant_id",
        "tid"
    ];

    public static readonly IReadOnlyCollection<string> WorkspaceScope =
    [
        "workspace_id",
        "workspaceId",
        "iip_workspace_id"
    ];

    public static readonly IReadOnlyCollection<string> Roles =
    [
        ClaimTypes.Role,
        "roles",
        "role",
        "appRole"
    ];

    public static readonly IReadOnlyCollection<string> UserIds =
    [
        ClaimTypes.NameIdentifier,
        "oid",
        "sub",
        "preferred_username",
        "upn"
    ];

    public static readonly IReadOnlyCollection<string> DisplayNames =
    [
        ClaimTypes.Name,
        "name",
        "preferred_username",
        "upn"
    ];
}
