using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Api.Common;

namespace InsuranceIntelligence.Api.Admin;

public sealed record UpdateTenantConfigurationRequest(
    string InsurerName,
    string? LogoUrl,
    string Country,
    string Region,
    IReadOnlyCollection<BusinessLineConfiguration> BusinessLines,
    IReadOnlyCollection<ChannelConfiguration> Channels,
    IReadOnlyCollection<ApprovalThresholdConfiguration> ApprovalThresholds,
    IReadOnlyCollection<ChecklistConfiguration> Checklists,
    int AuditRetentionDays);

public sealed record CreateUserRequest(
    string UserId,
    string Name,
    string Email,
    string? Phone,
    string Role,
    string? Department,
    string? Branch);

public sealed record UpdateUserRoleRequest(string Role);

public sealed record AdminRoleResponse(
    string Role,
    IReadOnlyCollection<string> Permissions,
    decimal? ApprovalLimit,
    string CaseVisibilityScope);

public sealed record AdminUserResponse(
    string UserId,
    string Name,
    string Email,
    string? Phone,
    string Role,
    string? Department,
    string? Branch,
    string Status,
    DateTimeOffset? LastLogin,
    IReadOnlyCollection<ActionStateResponse> AllowedActions,
    IReadOnlyCollection<ActionStateResponse> BlockedActions);

public sealed record ProviderHealthResponse(
    string OverallStatus,
    IReadOnlyCollection<ProviderHealthItem> Providers);

public sealed record ProviderHealthItem(
    string Name,
    string Mode,
    string Status,
    string Detail);
