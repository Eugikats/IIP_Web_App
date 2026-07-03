namespace InsuranceIntelligence.Domain.Users;

public sealed record InsuranceUser(
    string UserId,
    string TenantId,
    string WorkspaceId,
    string DisplayName,
    UserRole Role);
