using InsuranceIntelligence.Api.Workflow;
using InsuranceIntelligence.Application.Billing;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Billing;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Infrastructure.Configuration;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace InsuranceIntelligence.Api.Admin.Billing;

public sealed record PlanPriceDto(string Currency, decimal MonthlyAmount);

public sealed record PlanAllowancesDto(int ClaimsPerMonth, int DocumentsPerMonth, long AiTokensPerMonth, int StorageMb, int Seats);

public sealed record PlanDto(string Key, string Name, string Summary, bool SelfServe, bool IsCurrent, IReadOnlyList<PlanPriceDto> Prices, PlanAllowancesDto Allowances);

public sealed record UpgradePromptDto(string Meter, string Level, string Message, string? UpgradeToPlanKey);

public sealed record CurrentPlanDto(string Key, string Name, string Status, DateTimeOffset? PeriodEnd, bool CancelAtPeriodEnd, string Currency, PlanAllowancesDto Allowances);

public sealed record BillingPlanResponse(
    CurrentPlanDto Current,
    IReadOnlyList<PlanDto> Plans,
    IReadOnlyList<string> PaymentMethods,
    IReadOnlyList<string> Currencies,
    IReadOnlyList<UpgradePromptDto> Prompts);

public sealed record CheckoutBody(string? PlanKey, string? Method, string? Currency, string? PayerPhone);

public sealed record InvoiceDto(
    Guid InvoiceId, string Number, string PlanName, decimal Amount, string Currency, string Kind, string Status,
    DateTimeOffset IssuedAt, DateTimeOffset? PaidAt, string? ReceiptNumber, string? PaidWith, DateTimeOffset? CoversFrom, DateTimeOffset? CoversTo);

/// <summary>
/// A workspace's plan, what it is allowed, and how it pays for a bigger one (build-loop item 32). Reading needs ViewBilling; buying, cancelling
/// and resuming need ManageTenantSettings. Every route works inside the caller's own workspace; the one public route is the payment provider's
/// callback, which carries an unguessable secret and is only ever a nudge to ask the provider what happened.
/// </summary>
public static class BillingPlanEndpoints
{
    public static async Task<IResult> GetPlan(PlanService plans, IPaymentProviderSelector providers, IBillingQueryService billing, HttpContext httpContext, CancellationToken cancellationToken)
    {
        if (!WorkflowEndpoints.TryResolveScope(httpContext, out var scope, out _, out var error))
        {
            return Validation("tenantScope", error);
        }

        var entitlements = await plans.GetEntitlementsAsync(scope, cancellationToken);
        var subscription = await plans.GetSubscriptionAsync(scope, cancellationToken);
        var summary = await billing.GetSummaryAsync(cancellationToken);
        var catalogue = plans.Catalogue;

        var visible = catalogue.Plans.Select(plan => new PlanDto(
            plan.Key, plan.Name, plan.Summary, plan.SelfServe, plan.Key == entitlements.Plan.Key,
            plan.Prices.Select(price => new PlanPriceDto(price.Currency, price.MonthlyAmount)).ToArray(),
            ToDto(plan.Allowances))).ToArray();

        var current = new CurrentPlanDto(
            entitlements.Plan.Key,
            entitlements.Plan.Name,
            entitlements.Status,
            entitlements.PeriodEnd,
            entitlements.CancelAtPeriodEnd,
            subscription?.Currency ?? "UGX",
            new PlanAllowancesDto(entitlements.ClaimsPerMonth, entitlements.DocumentsPerMonth, entitlements.AiTokensPerMonth, entitlements.StorageMb, entitlements.Seats));

        return Results.Ok(new BillingPlanResponse(current, visible, providers.AvailableMethods, PlanCatalogue.Currencies, Prompts(catalogue, entitlements, summary.Meters)));
    }

    private static PlanAllowancesDto ToDto(PlanAllowances allowances) =>
        new(allowances.ClaimsPerMonth, allowances.DocumentsPerMonth, allowances.AiTokensPerMonth, allowances.StorageMb, allowances.Seats);

    /// <summary>One plain prompt for each allowance that is nearly or fully used. Nothing for a workspace on no plan, which has nothing to upgrade to.</summary>
    internal static IReadOnlyList<UpgradePromptDto> Prompts(PlanCatalogue catalogue, Entitlements entitlements, IReadOnlyList<UsageMeterDto> meters)
    {
        if (entitlements.Plan.Key == PlanCatalogue.Standard)
        {
            return [];
        }

        var next = EntitlementRules.NextUp(catalogue, entitlements.Plan);
        var suggestion = next is null ? "Contact us about Enterprise to raise it." : $"Upgrade to {next.Name} to raise it.";
        var prompts = new List<UpgradePromptDto>();
        foreach (var meter in meters.Where(item => item.Metered && item.Limit > 0 && item.Key is "claims" or "documents" or "ai-tokens" or "storage"))
        {
            var what = meter.Key switch
            {
                "claims" => "claims this month",
                "documents" => "documents this month",
                "ai-tokens" => "AI use this month",
                _ => "document storage",
            };
            if (meter.Used >= meter.Limit)
            {
                prompts.Add(new UpgradePromptDto(meter.Key, "limit", $"You have used all of your {what} on the {entitlements.Plan.Name} plan. {suggestion}", next?.Key));
            }
            else if (meter.PercentUsed >= 80)
            {
                prompts.Add(new UpgradePromptDto(meter.Key, "warning", $"You have used {meter.PercentUsed}% of your {what} on the {entitlements.Plan.Name} plan. {suggestion}", next?.Key));
            }
        }

        if (entitlements.Status == SubscriptionStatuses.PastDue)
        {
            prompts.Insert(0, new UpgradePromptDto("plan", "limit", $"Your {entitlements.Plan.Name} plan renewal is overdue. Pay the open invoice to keep it; after the grace period the workspace moves to the Free plan's limits.", entitlements.Plan.Key));
        }
        else if (entitlements.Status == SubscriptionStatuses.Lapsed)
        {
            prompts.Insert(0, new UpgradePromptDto("plan", "limit", "Your paid plan lapsed unpaid, so the workspace is on the Free plan's limits. Nothing was deleted. Choose a plan to restore it.", next?.Key));
        }

        return prompts;
    }

    public static async Task<IResult> Checkout(
        CheckoutBody body,
        PlanService plans,
        PaymentService payments,
        IUserAccountRepository userAccounts,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!WorkflowEndpoints.TryResolveScope(httpContext, out var scope, out var userId, out var error))
        {
            return Validation("tenantScope", error);
        }

        var account = await userAccounts.GetAsync(userId, scope, cancellationToken);
        var subscription = await plans.GetSubscriptionAsync(scope, cancellationToken);
        var (payment, problem) = await payments.CheckoutPlanAsync(
            scope, userId, account?.Email, plans.Catalogue, subscription,
            new CheckoutRequest(body.PlanKey ?? string.Empty, body.Method ?? string.Empty, body.Currency ?? string.Empty, body.PayerPhone),
            httpContext.TraceIdentifier, cancellationToken);
        if (payment is null)
        {
            return Validation("checkout", problem ?? "The payment could not be started.");
        }

        if (!string.IsNullOrWhiteSpace(account?.Email))
        {
            await plans.SetBillingEmailAsync(scope, account.Email, cancellationToken);
        }

        return Results.Ok(payment);
    }

    public static async Task<IResult> GetPayment(Guid paymentIntentId, PaymentService payments, HttpContext httpContext, CancellationToken cancellationToken)
    {
        if (!WorkflowEndpoints.TryResolveScope(httpContext, out var scope, out _, out var error))
        {
            return Validation("tenantScope", error);
        }

        var payment = await payments.RefreshAsync(scope, paymentIntentId, httpContext.TraceIdentifier, cancellationToken);
        return payment is null ? Results.NotFound(new { message = "That payment was not found." }) : Results.Ok(payment);
    }

    public static async Task<IResult> ListPayments(PaymentService payments, HttpContext httpContext, CancellationToken cancellationToken)
    {
        if (!WorkflowEndpoints.TryResolveScope(httpContext, out var scope, out _, out var error))
        {
            return Validation("tenantScope", error);
        }

        return Results.Ok(await payments.ListAsync(scope, 50, cancellationToken));
    }

    public static async Task<IResult> ListInvoices(IPaymentStore store, HttpContext httpContext, CancellationToken cancellationToken)
    {
        if (!WorkflowEndpoints.TryResolveScope(httpContext, out var scope, out _, out var error))
        {
            return Validation("tenantScope", error);
        }

        return Results.Ok((await store.ListInvoicesAsync(scope, cancellationToken)).Select(ToDto).ToArray());
    }

    public static async Task<IResult> GetReceipt(Guid invoiceId, IPaymentStore store, ITenantSettingsRepository settings, HttpContext httpContext, CancellationToken cancellationToken)
    {
        if (!WorkflowEndpoints.TryResolveScope(httpContext, out var scope, out _, out var error))
        {
            return Validation("tenantScope", error);
        }

        var invoice = await store.GetInvoiceAsync(scope, invoiceId, cancellationToken);
        if (invoice is null || invoice.Status != InvoiceStatuses.Paid)
        {
            return Results.NotFound(new { message = "There is no receipt for that invoice." });
        }

        var tenant = await settings.GetAsync(scope, cancellationToken);
        return Results.Ok(new
        {
            customer = string.IsNullOrWhiteSpace(tenant.AssistantOrganisation) ? scope.TenantId : tenant.AssistantOrganisation,
            invoice = ToDto(invoice),
        });
    }

    public static async Task<IResult> Cancel(PlanService plans, IAuditRepository audit, HttpContext httpContext, CancellationToken cancellationToken) =>
        await SetCancel(true, plans, audit, httpContext, cancellationToken);

    public static async Task<IResult> Resume(PlanService plans, IAuditRepository audit, HttpContext httpContext, CancellationToken cancellationToken) =>
        await SetCancel(false, plans, audit, httpContext, cancellationToken);

    private static async Task<IResult> SetCancel(bool cancel, PlanService plans, IAuditRepository audit, HttpContext httpContext, CancellationToken cancellationToken)
    {
        if (!WorkflowEndpoints.TryResolveScope(httpContext, out var scope, out var userId, out var error))
        {
            return Validation("tenantScope", error);
        }

        if (!await plans.SetCancelAtPeriodEndAsync(scope, cancel, cancellationToken))
        {
            return Validation("plan", "There is no paid plan running to cancel.");
        }

        await audit.WriteAsync(
            new AuditEvent(
                Guid.NewGuid(), scope, null, null, userId, ActorType.User, cancel ? "billing.plan.cancel_requested" : "billing.plan.cancel_withdrawn", DateTimeOffset.UtcNow,
                null, null, [], null, null, "accepted", null,
                cancel ? "The paid plan will end at the close of the period already paid for." : "The request to end the paid plan was withdrawn.", httpContext.TraceIdentifier),
            cancellationToken);
        return Results.NoContent();
    }

    /// <summary>A provider calls this when a payment changes. The secret in the address is the only credential; the call body is ignored.</summary>
    public static async Task<IResult> Callback(Guid paymentIntentId, string secret, PaymentService payments, HttpContext httpContext, CancellationToken cancellationToken)
    {
        var known = await payments.HandleCallbackAsync(paymentIntentId, secret, httpContext.TraceIdentifier, cancellationToken);
        return known ? Results.Ok() : Results.NotFound();
    }

    private static InvoiceDto ToDto(Invoice invoice) => new(
        invoice.InvoiceId, invoice.Number, invoice.PlanName, invoice.Amount, invoice.Currency, invoice.Kind, invoice.Status,
        invoice.IssuedAt, invoice.PaidAt, invoice.ReceiptNumber, invoice.PaidWith, invoice.CoversFrom, invoice.CoversTo);

    private static IResult Validation(string key, string message) =>
        Results.ValidationProblem(new Dictionary<string, string[]> { [key] = [message] });
}

/// <summary>Where payment providers call back and where a card payer returns to.</summary>
public sealed class PaymentLinks(InfrastructureOptions options, IHttpContextAccessor httpContextAccessor, InsuranceIntelligence.Application.ClientPortal.IPortalAddress portalAddress, IHostEnvironment environment) : IPaymentLinks
{
    public string? CallbackUrl(Guid paymentIntentId, string secret)
    {
        var baseUrl = options.ApiPublicBaseUrl;
        if (string.IsNullOrWhiteSpace(baseUrl) && environment.IsDevelopment() && httpContextAccessor.HttpContext is { } context)
        {
            // A developer's machine has no public address; the address the request came to stands in so the pretend operator can call back.
            baseUrl = $"{context.Request.Scheme}://{context.Request.Host}";
        }

        return string.IsNullOrWhiteSpace(baseUrl) ? null : $"{baseUrl.TrimEnd('/')}/api/v1/payments/callback/{paymentIntentId}/{secret}";
    }

    public string? ReturnUrl(TenantScope scope)
    {
        // On a developer's machine the portal runs wherever the developer started it, so the page that asked is where the payer returns to.
        // Anywhere else the address comes from the workspace's own settings, never from the request.
        if (environment.IsDevelopment()
            && httpContextAccessor.HttpContext?.Request.Headers.Origin.ToString() is { Length: > 0 } origin
            && Uri.TryCreate(origin, UriKind.Absolute, out var originUri)
            && originUri.Scheme is "http" or "https")
        {
            return $"{originUri.GetLeftPart(UriPartial.Authority)}/app/admin/billing";
        }

        return portalAddress.BaseUrlFor(scope) is { } baseUrl ? $"{baseUrl.TrimEnd('/')}/app/admin/billing" : null;
    }
}

/// <summary>Turns a used-up allowance into a clear "payment required" answer instead of a server error.</summary>
public sealed class PlanLimitExceptionHandler(ILogger<PlanLimitExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not PlanLimitExceededException limit)
        {
            return false;
        }

        logger.LogInformation("Request to {Path} stopped by the {Meter} allowance of the {Plan} plan.", httpContext.Request.Path, limit.Meter, limit.PlanName);
        httpContext.Response.StatusCode = StatusCodes.Status402PaymentRequired;
        await httpContext.Response.WriteAsJsonAsync(
            new ProblemDetails
            {
                Type = "https://iip.internal/errors/plan-limit",
                Title = "Plan allowance used",
                Status = StatusCodes.Status402PaymentRequired,
                Detail = limit.Message,
                Extensions =
                {
                    ["code"] = "plan_limit",
                    ["meter"] = limit.Meter,
                    ["limit"] = limit.Limit,
                    ["used"] = limit.Used,
                    ["upgradeToPlanKey"] = limit.UpgradeToPlanKey,
                },
            },
            cancellationToken);
        return true;
    }
}
