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
/// Computes this month's usage from records the platform actually stores: claims from the case store, model calls and
/// tokens from the action log's metering records, documents and storage from the document store. A dimension that
/// cannot be counted is returned Metered=false rather than as a made-up number (ADR-0003). Allowances come from the
/// workspace's plan (and its own operating settings, which can only lower them); 0 means no limit.
/// </summary>
public sealed class RecordBackedBillingUsageSource(
    IClaimsReadRepository claimsReadRepository,
    InsuranceIntelligence.Domain.Billing.IWorkspaceUsageReader usageReader,
    PlanService plans,
    TimeProvider? timeProvider = null) : IBillingUsageSource
{
    public async Task<IReadOnlyList<UsageMeterDto>> GetUsageAsync(
        string tenantId,
        string workspaceId,
        CancellationToken cancellationToken = default)
    {
        var scope = InsuranceIntelligence.Domain.Common.TenantScope.Create(tenantId, workspaceId);
        var entitlements = await plans.GetEntitlementsAsync(scope, cancellationToken);
        var period = InsuranceIntelligence.Domain.Billing.UsagePeriod.KeyFor((timeProvider ?? TimeProvider.System).GetUtcNow());
        var onPlan = entitlements.Plan.Key != InsuranceIntelligence.Domain.Billing.PlanCatalogue.Standard;

        // A workspace on a plan is measured by claims opened this month; one with no plan keeps the old running total against its own setting.
        UsageMeter claimsMeter;
        if (onPlan)
        {
            var opened = await usageReader.GetClaimsOpenedAsync(scope, period, cancellationToken);
            claimsMeter = new UsageMeter("claims", "Claims opened this month", opened, entitlements.ClaimsPerMonth, "claims", Metered: true);
        }
        else
        {
            var claims = await claimsReadRepository.ListAsync(new ClaimsQuery(Take: entitlements.ClaimsPerMonth + 1), cancellationToken);
            claimsMeter = new UsageMeter("claims", "Claims processed", claims.Count, entitlements.ClaimsPerMonth, "claims", Metered: true);
        }

        var ai = await usageReader.GetAiUsageAsync(scope, period, cancellationToken);
        var documents = await usageReader.GetDocumentUsageAsync(scope, period, cancellationToken);
        var costCents = ai.CostUsd is { } cost ? (int)Math.Round(cost * 100m, MidpointRounding.AwayFromZero) : 0;

        var meters = new[]
        {
            claimsMeter,
            new UsageMeter("documents", "Documents received this month", documents.DocumentsInPeriod, entitlements.DocumentsPerMonth, "documents", Metered: true),
            new UsageMeter("ai-calls", "AI calls this month", ai.Calls, 0, "calls", Metered: true),
            new UsageMeter("ai-tokens", "AI tokens this month", (int)Math.Min(int.MaxValue, ai.TotalTokens), (int)Math.Min(int.MaxValue, entitlements.AiTokensPerMonth), "tokens", Metered: true),
            // Cost needs a configured price for the model; without one it is shown as not configured, never guessed.
            new UsageMeter("ai-cost-usd", "Estimated AI cost this month (USD)", costCents, 0, "cents", Metered: ai.CostUsd is not null),
            new UsageMeter("storage", "Document storage", (int)Math.Min(int.MaxValue, documents.BytesHeld / (1024 * 1024)), entitlements.StorageMb, "MB", Metered: true),
        };

        return meters.Select(ToDto).ToArray();
    }

    private static UsageMeterDto ToDto(UsageMeter m) =>
        new(m.Key, m.Label, m.Used, m.Limit, m.Unit, m.Metered, m.OverLimit, m.PercentUsed);
}
