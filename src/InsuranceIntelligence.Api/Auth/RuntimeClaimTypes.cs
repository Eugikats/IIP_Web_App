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
        // ADR-0005: the Entra object id (oid) is the stable, immutable per-user key IIP membership is
        // keyed on. It MUST take precedence over `sub` (pairwise, per-app) and the mapped
        // NameIdentifier so the same user resolves consistently across tokens and apps.
        "oid",
        "http://schemas.microsoft.com/identity/claims/objectidentifier",
        ClaimTypes.NameIdentifier,
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
