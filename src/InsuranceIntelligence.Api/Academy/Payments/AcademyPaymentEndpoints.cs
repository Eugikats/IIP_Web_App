using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Infrastructure.Configuration;

namespace InsuranceIntelligence.Api.Academy.Payments;

public sealed record CheckoutRequest(string? Method);

public sealed record ConfirmRequest(string? Reference, string? TransactionId);

public sealed record RefundRequest(bool RevokeAccess);

public sealed record PaymentSettingsRequest(decimal? UgxPerUsd, string? CardCurrency, decimal? TaxRatePercent);

public sealed record ZenithSettingsRequest(int? DailyLimit, long? MonthlyTokens);

public static class AcademyPaymentEndpoints
{
    private static Task<IResult> Go(HttpContext http, InfrastructureOptions o, ITenantContextAccessor t, AcademyOperator op, Func<AcademyLearner, Task<IResult>> action, bool operatorOnly = false) =>
        AcademyLearnerEndpoints.Run(http, o, t, op, action, operatorOnly);

    public static Task<IResult> Offer(string courseId, HttpContext http, InfrastructureOptions o, ITenantContextAccessor t, AcademyOperator op, AcademyPaymentService service, CancellationToken ct) =>
        Go(http, o, t, op, async learner => Results.Ok(await service.OfferAsync(learner.UserId, courseId, ct)));

    public static Task<IResult> Checkout(string courseId, CheckoutRequest request, HttpContext http, InfrastructureOptions o, ITenantContextAccessor t, AcademyOperator op, AcademyPaymentService service, CancellationToken ct) =>
        Go(http, o, t, op, async learner => Results.Ok(await service.CheckoutAsync(learner, courseId, request.Method, ct)));

    public static Task<IResult> Confirm(ConfirmRequest request, HttpContext http, InfrastructureOptions o, ITenantContextAccessor t, AcademyOperator op, AcademyPaymentService service, CancellationToken ct) =>
        Go(http, o, t, op, async learner => Results.Ok(await service.ConfirmAsync(learner, request.Reference, request.TransactionId, ct)));

    /// <summary>
    /// Called by the payment provider, so it carries no sign-in. It is protected by the provider's secret hash, and what it
    /// says is never believed: the provider is asked about the payment it names.
    /// </summary>
    public static async Task<IResult> Webhook(HttpContext http, AcademyPaymentService service, CancellationToken ct)
    {
        using var reader = new StreamReader(http.Request.Body);
        var body = await reader.ReadToEndAsync(ct);
        var outcome = await service.WebhookAsync(http.Request.Headers["verif-hash"].ToString(), body, ct);
        return outcome == "rejected" ? Results.Unauthorized() : Results.Ok(new { status = outcome });
    }

    // ---------------------------------------------------------------- the Academy team

    public static Task<IResult> Orders(HttpContext http, InfrastructureOptions o, ITenantContextAccessor t, AcademyOperator op, AcademyPaymentService service, CancellationToken ct) =>
        Go(http, o, t, op, async _ => Results.Ok(await service.OrdersAsync(ct)), operatorOnly: true);

    public static Task<IResult> Recheck(string reference, HttpContext http, InfrastructureOptions o, ITenantContextAccessor t, AcademyOperator op, AcademyPaymentService service, CancellationToken ct) =>
        Go(http, o, t, op, async _ => Results.Ok(await service.RecheckAsync(reference, ct)), operatorOnly: true);

    public static Task<IResult> Refunded(string reference, RefundRequest request, HttpContext http, InfrastructureOptions o, ITenantContextAccessor t, AcademyOperator op, AcademyPaymentService service, CancellationToken ct) =>
        Go(http, o, t, op, async _ => { await service.MarkRefundedAsync(reference, request.RevokeAccess, ct); return Results.Ok(new { refunded = true }); }, operatorOnly: true);

    public static Task<IResult> GetPaymentSettings(HttpContext http, InfrastructureOptions o, ITenantContextAccessor t, AcademyOperator op, AcademySettingsService settings, CancellationToken ct) =>
        Go(http, o, t, op, async _ => Results.Ok(await settings.PaymentsAsync(ct)), operatorOnly: true);

    public static Task<IResult> SavePaymentSettings(PaymentSettingsRequest request, HttpContext http, InfrastructureOptions o, ITenantContextAccessor t, AcademyOperator op, AcademySettingsService settings, CancellationToken ct) =>
        Go(http, o, t, op, async _ => Results.Ok(await settings.SavePaymentsAsync(request.UgxPerUsd, request.CardCurrency, request.TaxRatePercent, ct)), operatorOnly: true);

    public static Task<IResult> GetZenithSettings(HttpContext http, InfrastructureOptions o, ITenantContextAccessor t, AcademyOperator op, AcademySettingsService settings, AcademyZenithService zenith, CancellationToken ct) =>
        Go(http, o, t, op, async learner => Results.Ok(new { limits = await settings.ZenithAsync(ct), usage = await zenith.MonthUsageAsync(ct) }), operatorOnly: true);

    public static Task<IResult> SaveZenithSettings(ZenithSettingsRequest request, HttpContext http, InfrastructureOptions o, ITenantContextAccessor t, AcademyOperator op, AcademySettingsService settings, CancellationToken ct) =>
        Go(http, o, t, op, async _ => Results.Ok(await settings.SaveZenithAsync(request.DailyLimit, request.MonthlyTokens, ct)), operatorOnly: true);

    public static Task<IResult> FinalOpen(string courseId, HttpContext http, InfrastructureOptions o, ITenantContextAccessor t, AcademyOperator op, AcademyLearnerService learners, CancellationToken ct) =>
        Go(http, o, t, op, async learner => { await learners.FinalTaskOpenAsync(learner.UserId, courseId, ct); return Results.Ok(new { open = true }); });

    public static Task<IResult> FinalLeave(string courseId, HttpContext http, InfrastructureOptions o, ITenantContextAccessor t, AcademyOperator op, AcademyLearnerService learners, CancellationToken ct) =>
        Go(http, o, t, op, async learner => { await learners.EndSittingAsync(learner.UserId, "final-" + courseId.Trim(), ct); return Results.Ok(new { left = true }); });

    public static Task<IResult> AssessmentLeave(string courseId, string assessmentId, HttpContext http, InfrastructureOptions o, ITenantContextAccessor t, AcademyOperator op, AcademyLearnerService learners, CancellationToken ct) =>
        Go(http, o, t, op, async learner => { await learners.EndSittingAsync(learner.UserId, "assessment-" + assessmentId, ct); return Results.Ok(new { left = true }); });
}

/// <summary>Every few minutes, re-checks orders that are still pending, so a payment is never lost because a browser tab was closed.</summary>
public sealed class PaymentReconcileWorker(IServiceScopeFactory scopeFactory, ILogger<PaymentReconcileWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromMinutes(4), stoppingToken);
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await using var scope = scopeFactory.CreateAsyncScope();
                    var changed = await scope.ServiceProvider.GetRequiredService<AcademyPaymentService>().ReconcileAsync(stoppingToken);
                    if (changed > 0)
                    {
                        logger.LogInformation("Academy payment re-check settled {Count} pending orders.", changed);
                    }
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    // The table may not exist yet on a release that ships ahead of its migration, or the provider may be down.
                    logger.LogWarning("Academy payment re-check could not run: {Type}.", exception.GetType().Name);
                }

                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // shutting down
        }
    }
}
