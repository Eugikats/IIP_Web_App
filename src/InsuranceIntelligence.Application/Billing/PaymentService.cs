using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using InsuranceIntelligence.Application.Identity;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Billing;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Application.Billing;

/// <summary>Where a provider calls back and where a card payer is sent afterwards. Null when the deployment has no public address to give.</summary>
public interface IPaymentLinks
{
    string? CallbackUrl(Guid paymentIntentId, string secret);

    string? ReturnUrl(TenantScope scope);
}

/// <summary>What should happen once an attempt is settled, for one kind of purchase.</summary>
public interface IPaymentOutcomeHandler
{
    string Purpose { get; }

    Task OnSucceededAsync(PaymentIntent intent, CancellationToken cancellationToken = default);

    Task OnFailedAsync(PaymentIntent intent, CancellationToken cancellationToken = default);
}

public sealed record CheckoutRequest(string PlanKey, string Method, string Currency, string? PayerPhone);

public sealed record PaymentView(
    Guid PaymentIntentId,
    string Status,
    string Method,
    decimal Amount,
    string Currency,
    string Description,
    string? RedirectUrl,
    string? PayerInstruction,
    string? FailureReason,
    Guid? InvoiceId,
    DateTimeOffset CreatedAt);

/// <summary>
/// Collects money through a mobile money operator or a card gateway and settles the result. The rule that shapes everything here: a call
/// arriving at our callback address is only a nudge. The payment's truth is always read back from the provider, and the amount and currency
/// it reports must equal what was asked for, before anything is marked paid.
/// </summary>
public sealed class PaymentService(
    IPaymentStore store,
    IPaymentProviderSelector providers,
    IPaymentLinks links,
    IEnumerable<IPaymentOutcomeHandler> handlers,
    IAuditRepository auditRepository,
    TimeProvider? clock = null)
{
    private static readonly Regex PhonePattern = new(@"^\d{9,15}$", RegexOptions.Compiled);
    private static readonly TimeSpan DuplicateWindow = TimeSpan.FromMinutes(15);
    private readonly TimeProvider time = clock ?? TimeProvider.System;

    public IReadOnlyList<string> AvailableMethods => providers.AvailableMethods;

    /// <summary>Starts paying for a plan. The result's second item is a plain sentence when the request could not be started.</summary>
    public async Task<(PaymentView? Payment, string? Problem)> CheckoutPlanAsync(
        TenantScope scope,
        string actor,
        string? payerEmail,
        PlanCatalogue catalogue,
        Subscription? current,
        CheckoutRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var plan = catalogue.Find(request.PlanKey);
        if (plan is null || !plan.SelfServe || plan.IsFree)
        {
            return (null, "That plan cannot be bought here. Choose one of the paid plans, or contact us about Enterprise.");
        }

        var currency = (request.Currency ?? string.Empty).Trim().ToUpperInvariant();
        if (plan.PriceIn(currency) is not { } amount || amount <= 0)
        {
            return (null, $"The {plan.Name} plan is not priced in {currency}.");
        }

        var method = (request.Method ?? string.Empty).Trim().ToLowerInvariant();
        var early = ValidateMethod(method);
        if (early is not null)
        {
            return (null, early);
        }

        var now = time.GetUtcNow();
        var invoices = await store.ListInvoicesAsync(scope, cancellationToken);
        var invoice = invoices.FirstOrDefault(item => item.Status == InvoiceStatuses.Open && item.PlanKey == plan.Key
                && string.Equals(item.Currency, currency, StringComparison.OrdinalIgnoreCase) && item.Amount == amount);
        invoice ??= new Invoice
        {
            InvoiceId = Guid.NewGuid(),
            Scope = scope,
            Number = PaymentReferences.InvoiceNumber(now),
            PlanKey = plan.Key,
            PlanName = plan.Name,
            Amount = amount,
            Currency = currency,
            Kind = current is { Status: not SubscriptionStatuses.Lapsed } && string.Equals(current.PlanKey, plan.Key, StringComparison.OrdinalIgnoreCase) ? "Renewal" : "New",
            IssuedAt = now,
        };

        var (intent, problem) = await StartAttemptAsync(
            scope, actor, PaymentPurposes.PlanSubscription, invoice.InvoiceId.ToString(),
            $"IIP {plan.Name} plan, one month ({invoice.Number})", amount, currency, method, request.PayerPhone, payerEmail,
            links.ReturnUrl(scope), "billing.payment", correlationId, cancellationToken);
        if (intent is null)
        {
            return (null, problem);
        }

        // The invoice exists only once an attempt has actually been started for it.
        await store.SaveInvoiceAsync(invoice with { PaymentIntentId = intent.PaymentIntentId }, cancellationToken);
        return (ToView(intent, invoice.InvoiceId), null);
    }

    private string? ValidateMethod(string method)
    {
        if (providers.For(method) is not null)
        {
            return null;
        }

        return providers.AvailableMethods.Count == 0
            ? "Payments are not set up on this deployment yet, so this cannot be paid for here."
            : $"{PaymentMethods.Label(method)} is not available. Choose {string.Join(" or ", providers.AvailableMethods.Select(PaymentMethods.Label))}.";
    }

    /// <summary>
    /// Starts one attempt to collect money for anything (a plan, a course): checks the method and the payer's number or email, asks the
    /// provider, and records the attempt. A second press within 15 minutes returns the attempt already waiting rather than asking the phone
    /// again. The result's second item is a plain sentence when the attempt could not be started.
    /// </summary>
    public async Task<(PaymentIntent? Intent, string? Problem)> StartAttemptAsync(
        TenantScope scope,
        string actor,
        string purpose,
        string purposeReference,
        string description,
        decimal amount,
        string currency,
        string method,
        string? payerPhone,
        string? payerEmail,
        string? returnUrl,
        string auditPrefix,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        method = (method ?? string.Empty).Trim().ToLowerInvariant();
        var provider = providers.For(method);
        if (provider is null)
        {
            return (null, ValidateMethod(method));
        }

        string? contact;
        if (PaymentMethods.IsMobileMoney(method))
        {
            contact = new string((payerPhone ?? string.Empty).Where(char.IsDigit).ToArray());
            if (!PhonePattern.IsMatch(contact))
            {
                return (null, "Enter the mobile money number with its country code, for example 256700123456.");
            }
        }
        else
        {
            contact = payerEmail;
            if (string.IsNullOrWhiteSpace(contact))
            {
                return (null, "A card payment needs an email address for the receipt.");
            }
        }

        var now = time.GetUtcNow();

        // Pressing the button twice must not ask the phone twice.
        var recent = (await store.ListIntentsAsync(scope, 20, cancellationToken))
            .FirstOrDefault(item => item.PurposeReference == purposeReference && item.Purpose == purpose && item.Status == PaymentStatuses.Pending
                && item.Method == method && now - item.CreatedAt < DuplicateWindow);
        if (recent is not null)
        {
            return (recent, null);
        }

        var intentId = Guid.NewGuid();
        var secret = Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
        var callbackUrl = links.CallbackUrl(intentId, secret) ?? string.Empty;

        ProviderInitiation initiation;
        try
        {
            initiation = await provider.InitiateAsync(
                new ProviderPaymentRequest(intentId, method, amount, currency, description, contact, callbackUrl, returnUrl ?? string.Empty),
                cancellationToken);
        }
        catch (PaymentProviderException exception)
        {
            await auditRepository.WriteAsync(Audit(scope, actor, $"{auditPrefix}.start_failed", $"A {PaymentMethods.Label(method)} payment could not be started.", correlationId), cancellationToken);
            return (null, exception.Message);
        }

        var intent = new PaymentIntent
        {
            PaymentIntentId = intentId,
            Scope = scope,
            Purpose = purpose,
            PurposeReference = purposeReference,
            Method = method,
            Amount = amount,
            Currency = currency,
            Description = description,
            PayerContact = contact,
            ProviderReference = initiation.ProviderReference,
            RedirectUrl = initiation.RedirectUrl,
            PayerInstruction = initiation.PayerInstruction,
            CallbackSecretHash = Hash(secret),
            RequestedBy = actor,
            CreatedAt = now,
            UpdatedAt = now,
        };
        await store.SaveIntentAsync(intent, cancellationToken);
        await auditRepository.WriteAsync(Audit(scope, actor, $"{auditPrefix}.started", $"A {PaymentMethods.Label(method)} payment of {amount:N0} {currency} was started ({description}).", correlationId), cancellationToken);
        return (intent, null);
    }

    /// <summary>Asks the provider what became of an attempt and settles it. Safe to call as often as wanted.</summary>
    public async Task<PaymentView?> RefreshAsync(TenantScope scope, Guid paymentIntentId, string correlationId, CancellationToken cancellationToken = default)
    {
        var intent = await store.GetIntentAsync(scope, paymentIntentId, cancellationToken);
        if (intent is null)
        {
            return null;
        }

        intent = await CheckAsync(intent, correlationId, cancellationToken);
        return ToView(intent, Guid.TryParse(intent.PurposeReference, out var invoiceId) ? invoiceId : null);
    }

    /// <summary>
    /// A provider called the callback address. The secret in the address is checked first; the body is never read. The attempt is then
    /// checked against the provider itself, inside the attempt's own workspace. Anything wrong is answered as "not found".
    /// </summary>
    public async Task<bool> HandleCallbackAsync(Guid paymentIntentId, string? secret, string correlationId, CancellationToken cancellationToken = default)
    {
        var intent = await store.FindIntentByIdAsync(paymentIntentId, cancellationToken);
        if (intent is null || string.IsNullOrEmpty(secret) || !FixedTimeEquals(Hash(secret), intent.CallbackSecretHash))
        {
            return false;
        }

        await CheckAsync(intent, correlationId, cancellationToken);
        return true;
    }

    /// <summary>Checks one attempt against its provider and settles it if the provider has an answer.</summary>
    public async Task<PaymentIntent> CheckAsync(PaymentIntent intent, string correlationId, CancellationToken cancellationToken = default)
    {
        if (intent.Status != PaymentStatuses.Pending || string.IsNullOrEmpty(intent.ProviderReference))
        {
            return intent;
        }

        var provider = providers.For(intent.Method);
        if (provider is null)
        {
            return intent;
        }

        ProviderPaymentStatus status;
        try
        {
            status = await provider.GetStatusAsync(intent.Method, intent.ProviderReference, cancellationToken);
        }
        catch (PaymentProviderException)
        {
            await store.SaveIntentAsync(intent with { LastCheckedAt = time.GetUtcNow() }, cancellationToken);
            return intent;
        }

        return await SettleAsync(intent, status, correlationId, cancellationToken);
    }

    /// <summary>Marks an attempt settled from what its provider reported. Idempotent: a settled attempt is never settled twice.</summary>
    public async Task<PaymentIntent> SettleAsync(PaymentIntent intent, ProviderPaymentStatus status, string correlationId, CancellationToken cancellationToken = default)
    {
        if (intent.Status != PaymentStatuses.Pending)
        {
            return intent;
        }

        var now = time.GetUtcNow();
        if (status.Status == PaymentStatuses.Pending)
        {
            var waiting = intent with { LastCheckedAt = now };
            await store.SaveIntentAsync(waiting, cancellationToken);
            return waiting;
        }

        var outcome = status.Status;
        var reason = status.FailureReason;
        if (outcome == PaymentStatuses.Succeeded
            && ((status.Amount is { } paid && paid != intent.Amount)
                || (status.Currency is { } paidCurrency && !string.Equals(paidCurrency, intent.Currency, StringComparison.OrdinalIgnoreCase))))
        {
            // Money moved but not the sum we asked for. Do not grant anything; a person has to look.
            outcome = PaymentStatuses.Failed;
            reason = "The amount received did not match the amount asked for, so the plan was not changed. Contact support with the invoice number.";
            await auditRepository.WriteAsync(Audit(intent.Scope, "system:payments", "billing.payment.amount_mismatch", $"Payment {intent.PaymentIntentId} reported {status.Amount} {status.Currency} against {intent.Amount} {intent.Currency}.", correlationId), cancellationToken);
        }

        var settled = intent with
        {
            Status = outcome == PaymentStatuses.Succeeded ? PaymentStatuses.Succeeded : outcome == PaymentStatuses.Expired ? PaymentStatuses.Expired : PaymentStatuses.Failed,
            FailureReason = outcome == PaymentStatuses.Succeeded ? null : PlainReason(reason),
            PayerContact = null,
            SettledAt = now,
            LastCheckedAt = now,
            UpdatedAt = now,
        };
        await store.SaveIntentAsync(settled, cancellationToken);

        var handler = handlers.FirstOrDefault(item => item.Purpose == intent.Purpose);
        if (handler is not null)
        {
            if (settled.Status == PaymentStatuses.Succeeded)
            {
                await handler.OnSucceededAsync(settled, cancellationToken);
            }
            else
            {
                await handler.OnFailedAsync(settled, cancellationToken);
            }
        }

        await auditRepository.WriteAsync(
            Audit(intent.Scope, intent.RequestedBy, settled.Status == PaymentStatuses.Succeeded ? "billing.payment.succeeded" : "billing.payment.failed",
                $"A {PaymentMethods.Label(intent.Method)} payment of {intent.Amount:N0} {intent.Currency} {(settled.Status == PaymentStatuses.Succeeded ? "was received" : "did not complete")}.", correlationId),
            cancellationToken);
        return settled;
    }

    public async Task<IReadOnlyList<PaymentView>> ListAsync(TenantScope scope, int take, CancellationToken cancellationToken = default) =>
        (await store.ListIntentsAsync(scope, take, cancellationToken))
        .Select(intent => ToView(intent, Guid.TryParse(intent.PurposeReference, out var invoiceId) ? invoiceId : null))
        .ToArray();

    internal static PaymentView ToView(PaymentIntent intent, Guid? invoiceId) =>
        new(intent.PaymentIntentId, intent.Status, intent.Method, intent.Amount, intent.Currency, intent.Description, intent.RedirectUrl, intent.PayerInstruction, intent.FailureReason, invoiceId, intent.CreatedAt);

    /// <summary>Provider text is never shown as it came: only a short plain sentence, and a generic one when the text is long or unfamiliar.</summary>
    internal static string PlainReason(string? providerReason)
    {
        var text = (providerReason ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            return "The payment was not completed.";
        }

        var lower = text.ToLowerInvariant();
        if (lower.Contains("insufficient", StringComparison.Ordinal) || lower.Contains("balance", StringComparison.Ordinal))
        {
            return "There was not enough money in the account.";
        }

        if (lower.Contains("reject", StringComparison.Ordinal) || lower.Contains("declin", StringComparison.Ordinal) || lower.Contains("cancel", StringComparison.Ordinal))
        {
            return "The payment was declined or cancelled.";
        }

        if (lower.Contains("timeout", StringComparison.Ordinal) || lower.Contains("expire", StringComparison.Ordinal))
        {
            return "The request timed out before it was approved.";
        }

        if (lower.Contains("amount", StringComparison.Ordinal) && lower.Contains("match", StringComparison.Ordinal))
        {
            return text;
        }

        return "The payment was not completed.";
    }

    public static string Hash(string secret) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(secret)));

    private static bool FixedTimeEquals(string left, string right) =>
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(left), Encoding.UTF8.GetBytes(right));

    private AuditEvent Audit(TenantScope scope, string actor, string action, string reason, string correlationId) =>
        new(
            Guid.NewGuid(), scope, null, null, actor, ActorType.User, action, time.GetUtcNow(),
            BeforeStatus: null, AfterStatus: null, EvidenceIds: [], ModelProvider: null, Confidence: null,
            ValidationResult: "accepted", ApprovalId: null, AuditReason: reason, CorrelationId: correlationId);
}

/// <summary>When a plan payment settles: the invoice is paid and receipted, the plan starts or extends, and a receipt is emailed.</summary>
public sealed class PlanPaymentOutcomeHandler(
    IPaymentStore store,
    ISubscriptionStore subscriptions,
    IIdentityMailer mailer,
    IAuditRepository auditRepository,
    TimeProvider? clock = null) : IPaymentOutcomeHandler
{
    private readonly TimeProvider time = clock ?? TimeProvider.System;

    public string Purpose => PaymentPurposes.PlanSubscription;

    public async Task OnSucceededAsync(PaymentIntent intent, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(intent.PurposeReference, out var invoiceId) || await store.GetInvoiceAsync(intent.Scope, invoiceId, cancellationToken) is not { } invoice || invoice.Status == InvoiceStatuses.Paid)
        {
            return;
        }

        var now = time.GetUtcNow();
        var current = await subscriptions.GetAsync(intent.Scope, cancellationToken);

        // A renewal that arrives before the paid period ends continues from its end; anything else starts now.
        var from = current is { PeriodEnd: { } end, Status: not SubscriptionStatuses.Lapsed } && end > now && string.Equals(current.PlanKey, invoice.PlanKey, StringComparison.OrdinalIgnoreCase)
            ? end
            : now;
        var to = from.AddMonths(1);
        var receipt = PaymentReferences.ReceiptNumber(now);
        await store.SaveInvoiceAsync(
            invoice with { Status = InvoiceStatuses.Paid, PaidAt = now, ReceiptNumber = receipt, PaidWith = PaymentMethods.Label(intent.Method), PaymentIntentId = intent.PaymentIntentId, CoversFrom = from, CoversTo = to },
            cancellationToken);

        var email = current?.BillingEmail;
        await subscriptions.SaveAsync(
            (current ?? Subscription.Free(intent.Scope, now)) with
            {
                PlanKey = invoice.PlanKey,
                Status = SubscriptionStatuses.Active,
                StartedAt = current is not null && string.Equals(current.PlanKey, invoice.PlanKey, StringComparison.OrdinalIgnoreCase) ? current.StartedAt : now,
                PeriodEnd = to,
                Currency = invoice.Currency,
                CancelAtPeriodEnd = false,
                GraceEndsAt = null,
                OpenRenewalInvoiceId = null,
                BillingEmail = email,
                UpdatedAt = now,
            },
            cancellationToken);
        await auditRepository.WriteAsync(
            new AuditEvent(
                Guid.NewGuid(), intent.Scope, null, null, "system:payments", ActorType.User, "billing.plan.activated", now,
                BeforeStatus: current?.PlanKey, AfterStatus: invoice.PlanKey, EvidenceIds: [], ModelProvider: null, Confidence: null,
                ValidationResult: "accepted", ApprovalId: null, AuditReason: $"The {invoice.PlanName} plan is paid until {to:yyyy-MM-dd} (invoice {invoice.Number}, receipt {receipt}).", CorrelationId: intent.PaymentIntentId.ToString()),
            cancellationToken);

        if (!string.IsNullOrWhiteSpace(email))
        {
            await mailer.SendAsync(
                intent.Scope,
                email,
                $"Receipt {receipt}: IIP {invoice.PlanName} plan",
                $"Thank you. We received {invoice.Amount:N0} {invoice.Currency} by {PaymentMethods.Label(intent.Method)}.\n\nReceipt: {receipt}\nInvoice: {invoice.Number}\nPlan: {invoice.PlanName}\nCovers: {from:d MMMM yyyy} to {to:d MMMM yyyy}\n\nKeep this email as your receipt.",
                intent.PaymentIntentId.ToString(),
                cancellationToken);
        }
    }

    public async Task OnFailedAsync(PaymentIntent intent, CancellationToken cancellationToken = default)
    {
        var current = await subscriptions.GetAsync(intent.Scope, cancellationToken);
        if (!string.IsNullOrWhiteSpace(current?.BillingEmail))
        {
            await mailer.SendAsync(
                intent.Scope,
                current!.BillingEmail!,
                "Your IIP payment did not go through",
                $"The {PaymentMethods.Label(intent.Method)} payment of {intent.Amount:N0} {intent.Currency} did not complete. {intent.FailureReason}\n\nNothing was charged for it and your plan is unchanged. You can try again from Admin, Billing.",
                intent.PaymentIntentId.ToString(),
                cancellationToken);
        }
    }
}
