using System.Security.Claims;

namespace InsuranceIntelligence.Api.Auth;

public static class RuntimeClaimTypes
{
    /// <summary>
    /// A client-supplied workspace-switch hint, honored in every auth mode. It is NEVER trusted as
    /// authority, IPermissionReadRepository.GetEffectiveMembershipAsync always re-validates it
    /// against the user's real active membership and fails closed (no access) if the user does not
    /// belong to the requested workspace. This is what lets the portal's workspace switcher
    /// re-resolve server-side scope instead of substituting it client-side (I5).
    /// </summary>
    public const string RequestedWorkspaceHeaderName = "X-Requested-Workspace-Id";

    /// <summary>
    /// The organisation (insurer or broker) the person has chosen to act in. Like the workspace
    /// header it is a request, never authority: the membership store confirms they belong to it, or
    /// that their broker organisation has an active link to it, and refuses otherwise. In multi-tenant
    /// mode this is the only organisation hint; the token's own tenant claim names a sign-in
    /// directory, not an insurer.
    /// </summary>
    public const string RequestedOrganisationHeaderName = "X-Organisation-Id";

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
