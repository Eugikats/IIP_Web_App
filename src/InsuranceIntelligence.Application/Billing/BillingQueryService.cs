using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Billing;

namespace InsuranceIntelligence.Application.Billing;

/// <summary>
/// Returns the tenant's entitlement package and usage meters. Usage is computed
/// from real counters supplied by <see cref="IBillingUsageSource"/>; unmetered
/// dimensions surface as Metered=false (ADR-0003, no fabricated usage). No
/// external billing/payment integration in v1.
/// </summary>
public sealed class BillingQueryService(
    ITenantContextAccessor tenantContextAccessor,
    IPermissionService permissionService,
    IBillingUsageSource usageSource,
    ITenantSettingsRepository? settingsRepository = null,
    PlanService? plans = null) : IBillingQueryService
{
    public async Task<BillingSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        await permissionService.RequirePermissionAsync(Permission.ViewBilling, cancellationToken);

        var context = tenantContextAccessor.Current;
        var meters = await usageSource.GetUsageAsync(context.TenantId, context.WorkspaceId, cancellationToken);

        var package = DefaultEntitlements.Package;
        if (plans is not null)
        {
            var entitlements = await plans.GetEntitlementsAsync(context.Scope, cancellationToken);
            var onPlan = entitlements.Plan.Key != PlanCatalogue.Standard;
            return new BillingSummaryDto(
                onPlan ? $"IIP Operations, {entitlements.Plan.Name}" : package.Name,
                package.Modules,
                entitlements.Seats,
                meters,
                entitlements.Plan.Key,
                entitlements.Status);
        }

        // The seat limit is a workspace setting; the package's own number is only the default.
        var seatLimit = settingsRepository is null
            ? package.SeatLimit
            : (await settingsRepository.GetAsync(context.Scope, cancellationToken)).SeatLimit;
        return new BillingSummaryDto(package.Name, package.Modules, seatLimit, meters);
    }
}
