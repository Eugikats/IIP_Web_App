namespace InsuranceIntelligence.Domain.Tenants;

public sealed record Workspace(
    string TenantId,
    string WorkspaceId,
    string DisplayName,
    string Region);
