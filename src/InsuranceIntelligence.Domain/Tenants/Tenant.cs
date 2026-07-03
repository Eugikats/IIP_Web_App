namespace InsuranceIntelligence.Domain.Tenants;

public sealed record Tenant(
    string TenantId,
    string DisplayName,
    IReadOnlyCollection<string> EnabledBusinessLines);
