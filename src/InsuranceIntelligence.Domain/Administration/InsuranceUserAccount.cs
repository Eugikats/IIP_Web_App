using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Domain.Administration;

public sealed record InsuranceUserAccount
{
    public required string UserId { get; init; }
    public required TenantScope Scope { get; init; }
    public required string Name { get; init; }
    public required string Email { get; init; }
    public string? Phone { get; init; }
    public required PlatformRole Role { get; init; }
    public string? Department { get; init; }
    public string? Branch { get; init; }
    public string Status { get; init; } = "Active";
    public string? EntraObjectId { get; init; }
    public DateTimeOffset? LastLogin { get; init; }
}
