namespace InsuranceIntelligence.Application.Billing;

public interface IBillingQueryService
{
    Task<BillingSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Port that supplies real usage counts for the tenant/workspace. Implementations
/// count actual records the platform already stores; dimensions without a counter
/// are returned with Metered=false (never fabricated).
/// </summary>
public interface IBillingUsageSource
{
    Task<IReadOnlyList<UsageMeterDto>> GetUsageAsync(
        string tenantId,
        string workspaceId,
        CancellationToken cancellationToken = default);
}

public sealed record BillingSummaryDto(
    string PackageName,
    IReadOnlyList<string> Modules,
    int SeatLimit,
    IReadOnlyList<UsageMeterDto> Meters,
    string? PlanKey = null,
    string? PlanStatus = null);

public sealed record UsageMeterDto(
    string Key,
    string Label,
    int Used,
    int Limit,
    string Unit,
    bool Metered,
    bool OverLimit,
    int PercentUsed);
