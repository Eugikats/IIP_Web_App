using InsuranceIntelligence.Domain.Administration;

namespace InsuranceIntelligence.Domain.Users;

/// <summary>A staff member's directory identity. <see cref="PlatformRole"/> is the single role
/// vocabulary: the permission map, the membership store and this record all use it. (A second,
/// divergent <c>UserRole</c> enum used to exist here; it was never read and was removed.)</summary>
public sealed record InsuranceUser(
    string UserId,
    string TenantId,
    string WorkspaceId,
    string DisplayName,
    PlatformRole Role);
