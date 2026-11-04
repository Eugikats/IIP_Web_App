using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Billing;
using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Application.Billing;

/// <summary>
/// What plan a workspace is on and what that lets it do. A workspace that never chose a plan is on Standard and keeps the limits in its
/// own operating settings; only workspaces that sign up for themselves, or buy a plan, are held to a plan's allowances.
/// </summary>
public sealed class PlanService(
    ISubscriptionStore subscriptions,
    ITenantSettingsRepository settingsRepository,
    IWorkspaceUsageReader usage,
    PlanCatalogue catalogue,
    TimeProvider? clock = null) : IPlanAllowance
{
    private readonly TimeProvider time = clock ?? TimeProvider.System;

    public PlanCatalogue Catalogue => catalogue;

    public async Task<Subscription?> GetSubscriptionAsync(TenantScope scope, CancellationToken cancellationToken = default) =>
        await subscriptions.GetAsync(scope, cancellationToken);

    public async Task<Entitlements> GetEntitlementsAsync(TenantScope scope, CancellationToken cancellationToken = default)
    {
        var subscription = await subscriptions.GetAsync(scope, cancellationToken);
        var settings = await settingsRepository.GetAsync(scope, cancellationToken);
        return EntitlementRules.For(subscription, catalogue, settings.SeatLimit, settings.ClaimsMeterLimit);
    }

    /// <summary>Puts a workspace on the Free plan. Does nothing if it already chose one.</summary>
    public async Task StartFreeAsync(TenantScope scope, string? billingEmail, CancellationToken cancellationToken = default)
    {
        if (await subscriptions.GetAsync(scope, cancellationToken) is null)
        {
            await subscriptions.SaveAsync(Subscription.Free(scope, time.GetUtcNow()) with { BillingEmail = billingEmail }, cancellationToken);
        }
    }

    /// <summary>Remembers who receipts and renewal notices go to.</summary>
    public async Task SetBillingEmailAsync(TenantScope scope, string email, CancellationToken cancellationToken = default)
    {
        var current = await subscriptions.GetAsync(scope, cancellationToken) ?? Subscription.Free(scope, time.GetUtcNow());
        if (!string.Equals(current.BillingEmail, email, StringComparison.OrdinalIgnoreCase))
        {
            await subscriptions.SaveAsync(current with { BillingEmail = email, UpdatedAt = time.GetUtcNow() }, cancellationToken);
        }
    }

    /// <summary>Stops a paid plan renewing: it runs to the end of the period already paid for, then the workspace moves to Free.</summary>
    public async Task<bool> SetCancelAtPeriodEndAsync(TenantScope scope, bool cancel, CancellationToken cancellationToken = default)
    {
        var current = await subscriptions.GetAsync(scope, cancellationToken);
        if (current is null || current.PeriodEnd is null || current.Status == SubscriptionStatuses.Lapsed || catalogue.Find(current.PlanKey) is not { IsFree: false })
        {
            return false;
        }

        await subscriptions.SaveAsync(current with { CancelAtPeriodEnd = cancel, UpdatedAt = time.GetUtcNow() }, cancellationToken);
        return true;
    }

    public async Task EnsureCanOpenClaimAsync(TenantScope scope, CancellationToken cancellationToken = default)
    {
        var entitlements = await GetEntitlementsAsync(scope, cancellationToken);
        if (entitlements.Plan.Key == PlanCatalogue.Standard || entitlements.ClaimsPerMonth <= 0)
        {
            return;
        }

        var opened = await usage.GetClaimsOpenedAsync(scope, UsagePeriod.KeyFor(time.GetUtcNow()), cancellationToken);
        if (opened >= entitlements.ClaimsPerMonth)
        {
            throw new PlanLimitExceededException("claims", entitlements.Plan.Name, entitlements.ClaimsPerMonth, opened, EntitlementRules.NextUp(catalogue, entitlements.Plan)?.Key);
        }
    }

    public async Task EnsureCanUseAiAsync(TenantScope scope, CancellationToken cancellationToken = default)
    {
        var entitlements = await GetEntitlementsAsync(scope, cancellationToken);
        if (entitlements.Plan.Key == PlanCatalogue.Standard || entitlements.AiTokensPerMonth <= 0)
        {
            return;
        }

        var ai = await usage.GetAiUsageAsync(scope, UsagePeriod.KeyFor(time.GetUtcNow()), cancellationToken);
        if (ai.TotalTokens >= entitlements.AiTokensPerMonth)
        {
            throw new PlanLimitExceededException("ai-tokens", entitlements.Plan.Name, entitlements.AiTokensPerMonth, ai.TotalTokens, EntitlementRules.NextUp(catalogue, entitlements.Plan)?.Key);
        }
    }
}
