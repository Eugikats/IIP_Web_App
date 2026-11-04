namespace InsuranceIntelligence.Api.Admin.Billing;

/// <summary>
/// The platform's own billing timer, inside the API so it needs no second host. Every few minutes it asks each waiting payment's provider
/// what became of it (catching callbacks that never arrived); a few times a day it runs the renewal pass (invoices, reminders, overdue,
/// lapse). Both passes are safe to repeat, and a fault in one workspace never stops the rest. Switched off with
/// <c>INSURANCEINTELLIGENCE_BILLING_WORKER=Off</c> (tests do, and drive the passes by hand).
/// </summary>
public sealed class BillingWorker(IServiceScopeFactory scopes, IConfiguration configuration, ILogger<BillingWorker> logger) : BackgroundService
{
    private static readonly TimeSpan ReconcileEvery = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan CycleEvery = TimeSpan.FromHours(6);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.Equals(configuration["INSURANCEINTELLIGENCE_BILLING_WORKER"], "Off", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var nextCycle = DateTimeOffset.UtcNow;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var reconciliation = scope.ServiceProvider.GetRequiredService<InsuranceIntelligence.Application.Billing.PaymentReconciliationService>();
                var summary = await reconciliation.RunAsync(null, stoppingToken);
                if (summary.Checked > 0)
                {
                    logger.LogInformation("Payment reconciliation: {Checked} checked, {Succeeded} paid, {Failed} failed, {Expired} expired, {Pending} still waiting.", summary.Checked, summary.Succeeded, summary.Failed, summary.Expired, summary.StillPending);
                }

                if (DateTimeOffset.UtcNow >= nextCycle)
                {
                    var cycle = await scope.ServiceProvider.GetRequiredService<InsuranceIntelligence.Application.Billing.SubscriptionLifecycleService>().RunAsync(null, stoppingToken);
                    nextCycle = DateTimeOffset.UtcNow + CycleEvery;
                    if (cycle.Examined > 0)
                    {
                        logger.LogInformation("Billing pass: {Examined} plans examined, {Invoiced} invoiced, {PastDue} overdue, {Lapsed} lapsed, {Cancelled} ended.", cycle.Examined, cycle.Invoiced, cycle.PastDue, cycle.Lapsed, cycle.Cancelled);
                    }
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogWarning("The billing pass failed and will be tried again. ErrorType={ErrorType}", exception.GetType().Name);
            }

            try
            {
                await Task.Delay(ReconcileEvery, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }
}
