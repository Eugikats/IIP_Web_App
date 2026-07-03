namespace InsuranceIntelligence.Domain.Customers;

public sealed record Customer(
    string CustomerId,
    string TenantId,
    string WorkspaceId,
    string FullName,
    string? Email,
    string? Phone);
