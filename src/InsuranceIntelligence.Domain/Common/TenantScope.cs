namespace InsuranceIntelligence.Domain.Common;

public sealed record TenantScope(string TenantId, string WorkspaceId)
{
    public static TenantScope Create(string tenantId, string workspaceId)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            throw new ArgumentException("Tenant ID is required.", nameof(tenantId));
        }

        if (string.IsNullOrWhiteSpace(workspaceId))
        {
            throw new ArgumentException("Workspace ID is required.", nameof(workspaceId));
        }

        return new TenantScope(tenantId.Trim(), workspaceId.Trim());
    }

    public bool Matches(TenantScope other) =>
        string.Equals(TenantId, other.TenantId, StringComparison.OrdinalIgnoreCase)
        && string.Equals(WorkspaceId, other.WorkspaceId, StringComparison.OrdinalIgnoreCase);
}
