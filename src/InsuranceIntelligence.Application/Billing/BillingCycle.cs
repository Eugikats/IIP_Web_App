using InsuranceIntelligence.Application.Identity;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Billing;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Application.Billing;

public sealed record BillingCycleSummary(int Examined, int Invoiced, int PastDue, int Lapsed, int Cancelled);

/// <summary>
/// The platform's daily billing pass. Mobile money cannot be charged on its own each month, so before a paid period ends the workspace is
/// sent an invoice and a reminder to pay it. A renewal that is not paid is tolerated for a grace period, then the workspace falls back to the
/// Free plan's limits. Nothing is ever deleted for non-payment, and paying again restores the plan.
/// </summary>
public sealed class SubscriptionLifecycleService(
    ISubscriptionStore subscriptions,
    IPaymentStore payments,
    PlanCatalogue catalogue,
    IIdentityMailer mailer,
    InsuranceIntelligence.Application.ClientPortal.IPortalAddress portalAddress,
    IAuditRepository auditRepository,
    TimeProvider? clock = null)
{
    public static readonly TimeSpan RenewalNotice = TimeSpan.FromDays(5);
    public static readonly TimeSpan Grace = TimeSpan.FromDays(7);
    private readonly TimeProvider time = clock ?? TimeProvider.System;

    /// <summary>Runs the pass for every workspace, or only for <paramref name="only"/> (used by tests and by a single-workspace check).</summary>
    public async Task<BillingCycleSummary> RunAsync(TenantScope? only = null, CancellationToken cancellationToken = default)
    {
        var now = time.GetUtcNow();
        var due = (await subscriptions.ListPaidEndingBeforeAsync(now + RenewalNotice, cancellationToken))
            .Where(subscription => only is null || subscription.Scope.Matches(only))
            .ToArray();
        int invoiced = 0, pastDue = 0, lapsed = 0, cancelled = 0;
        foreach (var subscription in due)
        {
            try
            {
                var result = await HandleAsync(subscription, now, cancellationToken);
                invoiced += result.Invoiced ? 1 : 0;
                pastDue += result.PastDue ? 1 : 0;
                lapsed += result.Lapsed ? 1 : 0;
                cancelled += result.Cancelled ? 1 : 0;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // One workspace's fault must not stop the pass for the others; it is picked up again tomorrow.
                await auditRepository.WriteAsync(Audit(subscription.Scope, "billing.cycle.failed", $"The billing pass could not finish for this workspace ({exception.GetType().Name})."), cancellationToken);
            }
        }

        return new BillingCycleSummary(due.Length, invoiced, pastDue, lapsed, cancelled);
    }

    private async Task<(bool Invoiced, bool PastDue, bool Lapsed, bool Cancelled)> HandleAsync(Subscription subscription, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (subscription.PeriodEnd is not { } end || subscription.Status == SubscriptionStatuses.Lapsed || catalogue.Find(subscription.PlanKey) is not { IsFree: false } plan)
        {
            return default;
        }

        // The administrator cancelled: the paid period runs out and the workspace moves to Free.
        if (subscription.CancelAtPeriodEnd)
        {
            if (end > now)
            {
                return default;
            }

            await subscriptions.SaveAsync(
                subscription with { PlanKey = PlanCatalogue.Free, Status = SubscriptionStatuses.Active, PeriodEnd = null, CancelAtPeriodEnd = false, GraceEndsAt = null, OpenRenewalInvoiceId = null, UpdatedAt = now },
                cancellationToken);
            await auditRepository.WriteAsync(Audit(subscription.Scope, "billing.plan.cancelled", $"The {plan.Name} plan ended as requested; the workspace is on the Free plan."), cancellationToken);
            await NotifyAsync(subscription, "Your IIP plan has ended", $"Your {plan.Name} plan ended as you asked, and the workspace is now on the Free plan. Nothing was deleted. You can choose a plan again from Admin, Billing.", cancellationToken);
            return (false, false, false, true);
        }

        var invoiced = false;
        var current = subscription;
        if (current.OpenRenewalInvoiceId is null)
        {
            var currency = plan.PriceIn(current.Currency) is > 0 ? current.Currency : plan.Prices.First(price => price.MonthlyAmount > 0).Currency;
            var amount = plan.PriceIn(currency)!.Value;
            var invoice = new Invoice
            {
                InvoiceId = Guid.NewGuid(),
                Scope = current.Scope,
                Number = PaymentReferences.InvoiceNumber(now),
                PlanKey = plan.Key,
                PlanName = plan.Name,
                Amount = amount,
                Currency = currency,
                Kind = "Renewal",
                IssuedAt = now,
            };
            await payments.SaveInvoiceAsync(invoice, cancellationToken);
            current = current with { OpenRenewalInvoiceId = invoice.InvoiceId, UpdatedAt = now };
            invoiced = true;
            await auditRepository.WriteAsync(Audit(current.Scope, "billing.invoice.issued", $"Renewal invoice {invoice.Number} for the {plan.Name} plan was issued."), cancellationToken);
            await NotifyAsync(
                current,
                $"Your IIP {plan.Name} plan renews on {end:d MMMM yyyy}",
                $"Your {plan.Name} plan runs until {end:d MMMM yyyy}. To keep it, pay invoice {invoice.Number} ({amount:N0} {currency}) from Admin, Billing{BillingLink(current.Scope)}.\n\nIf you do not, the workspace moves to the Free plan's limits after a {Grace.TotalDays:N0} day grace period. Nothing is deleted.",
                cancellationToken);
        }

        var isPastDue = false;
        var hasLapsed = false;
        if (end <= now)
        {
            if (current.Status == SubscriptionStatuses.Active)
            {
                current = current with { Status = SubscriptionStatuses.PastDue, GraceEndsAt = end + Grace, UpdatedAt = now };
                isPastDue = true;
                await auditRepository.WriteAsync(Audit(current.Scope, "billing.plan.past_due", $"The {plan.Name} plan renewal is unpaid; the grace period ends {current.GraceEndsAt:yyyy-MM-dd}."), cancellationToken);
                await NotifyAsync(current, "Your IIP plan renewal is overdue", $"We have not received payment for your {plan.Name} plan. It stays active until {current.GraceEndsAt:d MMMM yyyy}; after that the workspace moves to the Free plan's limits. Nothing is deleted, and paying restores the plan.", cancellationToken);
            }

            if (current.GraceEndsAt is { } graceEnd && graceEnd <= now)
            {
                current = current with { Status = SubscriptionStatuses.Lapsed, UpdatedAt = now };
                hasLapsed = true;
                await auditRepository.WriteAsync(Audit(current.Scope, "billing.plan.lapsed", $"The {plan.Name} plan lapsed unpaid; the workspace is held to the Free plan's limits."), cancellationToken);
                await NotifyAsync(current, "Your IIP plan has lapsed", $"The {plan.Name} plan lapsed because it was not renewed. The workspace is on the Free plan's limits. Nothing was deleted. Pay the open invoice from Admin, Billing to restore your plan.", cancellationToken);
            }
        }

        if (current != subscription)
        {
            await subscriptions.SaveAsync(current, cancellationToken);
        }

        return (invoiced, isPastDue, hasLapsed, false);
    }

    private string BillingLink(TenantScope scope) =>
        portalAddress.BaseUrlFor(scope) is { } baseUrl ? $" ({baseUrl.TrimEnd('/')}/app/admin/billing)" : string.Empty;

    private async Task NotifyAsync(Subscription subscription, string subject, string body, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(subscription.BillingEmail))
        {
            await mailer.SendAsync(subscription.Scope, subscription.BillingEmail, subject, body, "billing-cycle", cancellationToken);
        }
    }

    private AuditEvent Audit(TenantScope scope, string action, string reason) =>
        new(
            Guid.NewGuid(), scope, null, null, "system:billing", ActorType.User, action, time.GetUtcNow(),
            BeforeStatus: null, AfterStatus: null, EvidenceIds: [], ModelProvider: null, Confidence: null,
            ValidationResult: "accepted", ApprovalId: null, AuditReason: reason, CorrelationId: "billing-cycle");
}

public sealed record ReconciliationSummary(int Checked, int Succeeded, int Failed, int Expired, int StillPending);

/// <summary>
/// Catches payments whose callback never arrived or was lost: every attempt still waiting after a few minutes is asked about at its provider and
/// settled, and one that has waited a full day with no answer is closed as expired so it cannot be paid late by accident.
/// </summary>
public sealed class PaymentReconciliationService(IPaymentStore store, PaymentService payments, TimeProvider? clock = null)
{
    public static readonly TimeSpan SettleAfter = TimeSpan.FromMinutes(3);
    public static readonly TimeSpan GiveUpAfter = TimeSpan.FromHours(24);
    private readonly TimeProvider time = clock ?? TimeProvider.System;

    public async Task<ReconciliationSummary> RunAsync(TenantScope? only = null, CancellationToken cancellationToken = default)
    {
        var now = time.GetUtcNow();
        var pending = (await store.ListPendingBeforeAsync(now - SettleAfter, 500, cancellationToken))
            .Where(intent => only is null || intent.Scope.Matches(only))
            .ToArray();
        int succeeded = 0, failed = 0, expired = 0, still = 0;
        foreach (var intent in pending)
        {
            var settled = await payments.CheckAsync(intent, "reconciliation", cancellationToken);
            if (settled.Status == PaymentStatuses.Pending && now - intent.CreatedAt >= GiveUpAfter)
            {
                settled = await payments.SettleAsync(settled, new ProviderPaymentStatus(PaymentStatuses.Expired, null, null, "The request timed out before it was approved."), "reconciliation", cancellationToken);
            }

            switch (settled.Status)
            {
                case PaymentStatuses.Succeeded: succeeded++; break;
                case PaymentStatuses.Failed: failed++; break;
                case PaymentStatuses.Expired: expired++; break;
                default: still++; break;
            }
        }

        return new ReconciliationSummary(pending.Length, succeeded, failed, expired, still);
    }
}
