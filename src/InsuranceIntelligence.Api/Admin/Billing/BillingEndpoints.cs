using InsuranceIntelligence.Application.Billing;
using InsuranceIntelligence.Application.Claims;
using InsuranceIntelligence.Domain.Billing;
using InsuranceIntelligence.Domain.Knowledge;

namespace InsuranceIntelligence.Api.Admin.Billing;

public static class BillingEndpoints
{
    public static async Task<IResult> GetCredits(
        IAiCreditsSummaryService service,
        CancellationToken cancellationToken) =>
        Results.Ok(await service.GetAsync(cancellationToken));

    public static async Task<IResult> GetSummary(
        IBillingQueryService service,
        CancellationToken cancellationToken)
    {
        var summary = await service.GetSummaryAsync(cancellationToken);
        return Results.Ok(summary);
    }
}

/// <summary>
/// Computes usage from records the platform actually stores. Claims and AI token
/// counts are aggregated from real AgentActionLog records; dimensions without a
/// counter yet (storage) are returned Metered=false (ADR-0003).
/// </summary>
public sealed class RecordBackedBillingUsageSource(
    IClaimsReadRepository claimsReadRepository,
    IAgentActionLogRepository actionLogRepository,
    InsuranceIntelligence.Domain.Administration.ITenantSettingsRepository? settingsRepository = null) : IBillingUsageSource
{
    public async Task<IReadOnlyList<UsageMeterDto>> GetUsageAsync(
        string tenantId,
        string workspaceId,
        CancellationToken cancellationToken = default)
    {
        var scope = InsuranceIntelligence.Domain.Common.TenantScope.Create(tenantId, workspaceId);
        var claimsLimit = settingsRepository is null
            ? InsuranceIntelligence.Domain.Administration.TenantSettings.DefaultClaimsMeterLimit
            : (await settingsRepository.GetAsync(scope, cancellationToken)).ClaimsMeterLimit;
        var claims = await claimsReadRepository.ListAsync(new ClaimsQuery(Take: claimsLimit + 1), cancellationToken);
        var claimCount = claims.Count;

        var actionLogs = await actionLogRepository.ListAsync(scope, cancellationToken);
        var promptTokens = actionLogs.Sum(log => log.PromptTokens ?? 0);
        var completionTokens = actionLogs.Sum(log => log.CompletionTokens ?? 0);
        var totalTokens = promptTokens + completionTokens;
        var estimatedCost = actionLogs.Sum(log => log.EstimatedCostUsd ?? 0m);
        var aiCallsMetered = actionLogs.Any(log => log.PromptTokens.HasValue);

        var meters = new[]
        {
            new UsageMeter("claims", "Claims processed", claimCount, claimsLimit, "claims", Metered: true),
            new UsageMeter("documents", "Documents processed", 0, 0, "documents", Metered: false),
            new UsageMeter("ai-tokens", "AI tokens consumed", totalTokens, 0, "tokens", Metered: aiCallsMetered),
            new UsageMeter("ai-cost-usd", "Estimated AI cost (USD)", (int)(estimatedCost * 100), 0, "cents", Metered: aiCallsMetered),
            new UsageMeter("storage", "Document storage", 0, 0, "GB", Metered: false),
        };

        return meters.Select(ToDto).ToArray();
    }

    private static UsageMeterDto ToDto(UsageMeter m) =>
        new(m.Key, m.Label, m.Used, m.Limit, m.Unit, m.Metered, m.OverLimit, m.PercentUsed);
}
