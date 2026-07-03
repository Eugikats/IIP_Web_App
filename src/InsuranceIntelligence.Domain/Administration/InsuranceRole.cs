using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Domain.Administration;

public sealed record InsuranceRole(
    Guid RoleId,
    TenantScope Scope,
    PlatformRole Role,
    IReadOnlyCollection<Permission> Permissions,
    decimal? ApprovalLimit,
    string CaseVisibilityScope);
