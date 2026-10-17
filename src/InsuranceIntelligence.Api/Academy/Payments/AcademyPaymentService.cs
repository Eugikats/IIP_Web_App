using System.Text.Json;
using InsuranceIntelligence.Domain.Academy;

namespace InsuranceIntelligence.Api.Academy.Payments;

/// <summary>One attempt to buy a course. Created by IIP before the learner is sent to pay, so a payment is matched to this and never to anything the browser says.</summary>
public sealed record PaymentOrder
{
    public required string Reference { get; init; }
    public required string UserId { get; init; }
    public required string CourseId { get; init; }
    public string CourseTitle { get; init; } = string.Empty;
    public string BuyerName { get; init; } = string.Empty;
    public string BuyerEmail { get; init; } = string.Empty;
    public string Method { get; init; } = "card";
    public decimal PriceUsd { get; init; }
    public string Currency { get; init; } = "USD";
    public decimal Amount { get; init; }
    public decimal TaxRatePercent { get; init; }
    public decimal TaxAmount { get; init; }

    /// <summary>pending, paid, failed, mismatch (paid, but not what was asked: needs a person) or refunded.</summary>
    public string Status { get; init; } = "pending";
    public string? TransactionId { get; init; }
    public string? Note { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? PaidAt { get; init; }
}

public sealed record PaymentOffer(string CourseId, string CourseTitle, decimal PriceUsd, bool Free, bool Configured, bool Enrolled, IReadOnlyList<PaymentChoice> Choices, decimal TaxRatePercent);

public sealed record PaymentChoice(string Method, string Currency, decimal Amount, string Label);

public sealed record CheckoutResult(string Url, string Reference);

public sealed record ConfirmResult(string Status, string? CourseId, string? CourseTitle, string Message);

/// <summary>What each course costs right now, in dollars, from the Academy's price list.</summary>
public interface IAcademyPriceSource
{
    Task<IReadOnlyDictionary<string, decimal>> GetAsync(CancellationToken cancellationToken);
}

public sealed class AcademyPriceSource(IAcademyPricingRepository pricing, AcademyOperator academyOperator) : IAcademyPriceSource
{
    public Task<IReadOnlyDictionary<string, decimal>> GetAsync(CancellationToken cancellationToken) =>
        AcademyPublicEndpoints.PricesAsync(pricing, academyOperator, cancellationToken);
}

/// <summary>
/// Selling courses. The price is worked out on the server; a payment is trusted only after the payment provider confirms it,
/// for the amount and currency that were asked for, against an order IIP made itself; and a course is opened once however
/// many of the redirect, the webhook and the background re-check arrive.
/// </summary>
public sealed class AcademyPaymentService(
    IAcademyRecordStore store,
    IAcademyContentProvider content,
    IAcademyPriceSource prices,
    AcademyLearnerService learners,
    AcademySettingsService settings,
    IPaymentGateway gateway,
    IConfiguration configuration,
    TimeProvider clock)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public const string ReferencePrefix = "IIPA-";
    private static readonly TimeSpan Expiry = TimeSpan.FromDays(3);

    public bool Configured => gateway.Configured;

    private static string Owner(string userId) => userId.Trim().ToLowerInvariant();

    private string PortalBase => (configuration["Academy:PortalBaseUrl"] ?? "https://app.insuranceintelligenceplatform.com").TrimEnd('/');

    // ---------------------------------------------------------------- what a course costs

    public static decimal ShillingsFor(decimal usd, decimal rate) => Math.Round(usd * rate / 1000m, MidpointRounding.AwayFromZero) * 1000m;

    private static decimal TaxIncluded(decimal gross, decimal ratePercent, string currency) =>
        ratePercent <= 0 ? 0m : Math.Round(gross - gross / (1 + ratePercent / 100m), currency == "UGX" ? 0 : 2, MidpointRounding.AwayFromZero);

    private AcademyCourse SellableCourse(string courseId)
    {
        var course = content.Course(courseId) ?? throw new AcademyRuleException(404, "That course was not found.");
        return course.IsReady ? course : throw new AcademyRuleException(409, "That course is coming soon.", "coming_soon");
    }

    public async Task<PaymentOffer> OfferAsync(string userId, string courseId, CancellationToken ct)
    {
        var course = SellableCourse(courseId);
        var usd = AcademyPublicEndpoints.PriceOf(await prices.GetAsync(ct), course.Id);
        var pay = await settings.PaymentsAsync(ct);
        var enrolled = await learners.IsEnrolledAsync(userId, course.Id, ct);
        var choices = new List<PaymentChoice>
        {
            new("card", pay.CardCurrency, pay.CardCurrency == "USD" ? usd : ShillingsFor(usd, pay.UgxPerUsd), "Card"),
            new("mobile_money", "UGX", ShillingsFor(usd, pay.UgxPerUsd), "Mobile money"),
        };
        return new PaymentOffer(course.Id, course.Title, usd, usd <= 0, gateway.Configured, enrolled, usd <= 0 ? [] : choices, pay.TaxRatePercent);
    }

    /// <summary>True when this course must be paid for before enrolling: it has a price and a payment provider is set up.</summary>
    public async Task<bool> RequiresPaymentAsync(string courseId, CancellationToken ct)
    {
        if (!gateway.Configured)
        {
            return false;
        }

        var course = SellableCourse(courseId);
        return AcademyPublicEndpoints.PriceOf(await prices.GetAsync(ct), course.Id) > 0;
    }

    // ---------------------------------------------------------------- starting a payment

    public async Task<CheckoutResult> CheckoutAsync(AcademyLearner learner, string courseId, string? method, CancellationToken ct)
    {
        if (!gateway.Configured)
        {
            throw new AcademyRuleException(409, "Payments are not set up yet.", "payments_not_configured");
        }

        var offer = await OfferAsync(learner.UserId, courseId, ct);
        if (offer.Enrolled)
        {
            throw new AcademyRuleException(409, "You are already enrolled in this course.", "already_enrolled");
        }

        if (offer.Free)
        {
            throw new AcademyRuleException(409, "This course is free. Enrol without paying.", "free_course");
        }

        var choice = offer.Choices.FirstOrDefault(item => string.Equals(item.Method, method, StringComparison.OrdinalIgnoreCase))
            ?? throw new AcademyRuleException(400, "Choose card or mobile money.");
        var profile = await learners.ProfileAsync(learner, ct);
        var reference = ReferencePrefix + Guid.NewGuid().ToString("N")[..20].ToUpperInvariant();
        var order = new PaymentOrder
        {
            Reference = reference, UserId = Owner(learner.UserId), CourseId = offer.CourseId, CourseTitle = offer.CourseTitle,
            BuyerName = string.IsNullOrWhiteSpace(profile.Name) ? learner.Name : profile.Name, BuyerEmail = learner.Email ?? string.Empty,
            Method = choice.Method, PriceUsd = offer.PriceUsd, Currency = choice.Currency, Amount = choice.Amount,
            TaxRatePercent = offer.TaxRatePercent, TaxAmount = TaxIncluded(choice.Amount, offer.TaxRatePercent, choice.Currency),
            CreatedAt = clock.GetUtcNow(),
        };
        await SaveOrderAsync(order, ct);
        await store.SaveAsync(AcademyKinds.OrderRef, "-", reference, JsonSerializer.Serialize(new { userId = order.UserId }, Json), ct);

        try
        {
            var url = await gateway.CreateCheckoutAsync(
                new GatewayCheckout(reference, order.Amount, order.Currency, order.Method, order.BuyerName,
                    string.IsNullOrWhiteSpace(order.BuyerEmail) ? "learner@invalid.example" : order.BuyerEmail,
                    "IIP Academy", $"{order.CourseTitle}", $"{PortalBase}/academy/payment/callback"), ct);
            return new CheckoutResult(url, reference);
        }
        catch (Exception failure) when (failure is not OperationCanceledException)
        {
            await SaveOrderAsync(order with { Status = "failed", Note = "The payment page could not be opened." }, ct);
            throw;
        }
    }

    // ---------------------------------------------------------------- confirming one

    private async Task SaveOrderAsync(PaymentOrder order, CancellationToken ct) =>
        await store.SaveAsync(AcademyKinds.Order, order.UserId, order.Reference, JsonSerializer.Serialize(order, Json), ct);

    private async Task<PaymentOrder?> FindOrderAsync(string reference, CancellationToken ct)
    {
        var pointer = await store.GetAsync(AcademyKinds.OrderRef, "-", reference, ct);
        if (pointer is null)
        {
            return null;
        }

        var userId = JsonDocument.Parse(pointer.Json).RootElement.GetProperty("userId").GetString() ?? string.Empty;
        var record = await store.GetAsync(AcademyKinds.Order, userId, reference, ct);
        return record is null ? null : JsonSerializer.Deserialize<PaymentOrder>(record.Json, Json);
    }

    /// <summary>The learner came back from the payment page. Only the owner of the order may ask, and the answer comes from the provider.</summary>
    public async Task<ConfirmResult> ConfirmAsync(AcademyLearner learner, string? reference, string? transactionId, CancellationToken ct)
    {
        var order = string.IsNullOrWhiteSpace(reference) ? null : await FindOrderAsync(reference.Trim(), ct);
        if (order is null || order.UserId != Owner(learner.UserId))
        {
            throw new AcademyRuleException(404, "That payment was not found.");
        }

        order = await CheckWithProviderAsync(order, transactionId, ct);
        return ResultOf(order);
    }

    private ConfirmResult ResultOf(PaymentOrder order) => order.Status switch
    {
        "paid" => new ConfirmResult("paid", order.CourseId, order.CourseTitle, $"Payment received. You are enrolled in {order.CourseTitle}."),
        "mismatch" => new ConfirmResult("mismatch", null, order.CourseTitle, "Your payment arrived but did not match the price. The Academy team has been told and will sort it out. You have not been charged twice."),
        "refunded" => new ConfirmResult("refunded", null, order.CourseTitle, "This payment was refunded."),
        "failed" => new ConfirmResult("failed", null, order.CourseTitle, "The payment did not go through. You have not been enrolled."),
        _ => new ConfirmResult("pending", null, order.CourseTitle, "Waiting for the payment provider to confirm. This can take a minute."),
    };

    private async Task<PaymentOrder> CheckWithProviderAsync(PaymentOrder order, string? transactionId, CancellationToken ct)
    {
        if (order.Status is "paid" or "refunded")
        {
            return order;
        }

        GatewayPayment? payment;
        try
        {
            payment = await gateway.VerifyAsync(transactionId, order.Reference, ct);
        }
        catch (Exception failure) when (failure is not OperationCanceledException)
        {
            return order;
        }

        return await ApplyAsync(order, payment, ct);
    }

    private async Task<PaymentOrder> ApplyAsync(PaymentOrder order, GatewayPayment? payment, CancellationToken ct)
    {
        if (order.Status is "paid" or "refunded" || payment is null)
        {
            return order;
        }

        if (!payment.Successful)
        {
            if (payment.Status is "failed" or "cancelled")
            {
                order = order with { Status = "failed", Note = $"The provider reported the payment as {payment.Status}." };
                await SaveOrderAsync(order, ct);
            }

            return order;
        }

        var asked = order.Amount;
        if (!string.Equals(payment.Reference, order.Reference, StringComparison.Ordinal)
            || !string.Equals(payment.Currency, order.Currency, StringComparison.OrdinalIgnoreCase)
            || payment.Amount < asked)
        {
            order = order with
            {
                Status = "mismatch", TransactionId = payment.TransactionId,
                Note = $"Paid {payment.Amount:0.##} {payment.Currency} against {asked:0.##} {order.Currency} asked for. Not enrolled.",
            };
            await SaveOrderAsync(order, ct);
            return order;
        }

        order = order with { Status = "paid", TransactionId = payment.TransactionId, PaidAt = clock.GetUtcNow(), Note = null };
        await SaveOrderAsync(order, ct);
        await learners.EnrolAsync(order.UserId, order.CourseId, "payment:" + order.Reference, null, ct);
        await learners.NotifyPaymentAsync(order.UserId, $"Payment received: {order.Amount:0.##} {order.Currency} for {order.CourseTitle}. Reference {order.Reference}.", $"/academy/learn/{order.CourseId}", ct);
        return order;
    }

    // ---------------------------------------------------------------- the provider's webhook and the background re-check

    /// <summary>
    /// The provider's webhook. The body is never believed: it only names a reference, and the provider is asked about that
    /// reference. A call without the right secret, or about a payment that is not an Academy order, changes nothing.
    /// </summary>
    public async Task<string> WebhookAsync(string? signature, string body, CancellationToken ct)
    {
        if (!gateway.WebhookAuthentic(signature))
        {
            return "rejected";
        }

        string? reference = null;
        string? transactionId = null;
        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.TryGetProperty("data", out var data))
            {
                reference = data.TryGetProperty("tx_ref", out var tx) ? tx.GetString() : null;
                transactionId = data.TryGetProperty("id", out var id) ? id.ToString() : null;
            }
        }
        catch (JsonException)
        {
            return "ignored";
        }

        if (reference is null || !reference.StartsWith(ReferencePrefix, StringComparison.Ordinal))
        {
            return "ignored";
        }

        var order = await FindOrderAsync(reference, ct);
        if (order is null)
        {
            return "ignored";
        }

        await CheckWithProviderAsync(order, transactionId, ct);
        return "ok";
    }

    /// <summary>Re-checks orders that stayed pending, so a person who paid and closed the page is enrolled anyway. Old pending orders are closed.</summary>
    public async Task<int> ReconcileAsync(CancellationToken ct)
    {
        if (!gateway.Configured)
        {
            return 0;
        }

        var changed = 0;
        var now = clock.GetUtcNow();
        foreach (var record in await store.ListByKindAsync(AcademyKinds.Order, 2000, ct))
        {
            var order = JsonSerializer.Deserialize<PaymentOrder>(record.Json, Json);
            if (order is null || order.Status != "pending" || now - order.CreatedAt < TimeSpan.FromMinutes(5))
            {
                continue;
            }

            var after = await CheckWithProviderAsync(order, null, ct);
            if (after.Status == "pending" && now - order.CreatedAt > Expiry)
            {
                after = after with { Status = "failed", Note = "No payment arrived within three days." };
                await SaveOrderAsync(after, ct);
            }

            if (after.Status != order.Status)
            {
                changed++;
            }
        }

        return changed;
    }

    // ---------------------------------------------------------------- the Academy team

    public async Task<object> OrdersAsync(CancellationToken ct)
    {
        var orders = (await store.ListByKindAsync(AcademyKinds.Order, 500, ct))
            .Select(record => JsonSerializer.Deserialize<PaymentOrder>(record.Json, Json)!)
            .OrderByDescending(order => order.CreatedAt)
            .ToList();
        return new
        {
            configured = gateway.Configured,
            provider = gateway.Name,
            orders = orders.Select(order => new
            {
                reference = order.Reference, name = order.BuyerName, email = order.BuyerEmail, courseId = order.CourseId, courseTitle = order.CourseTitle,
                method = order.Method, currency = order.Currency, amount = order.Amount, taxAmount = order.TaxAmount, status = order.Status,
                note = order.Note, createdAt = order.CreatedAt, paidAt = order.PaidAt, transactionId = order.TransactionId,
            }),
        };
    }

    public async Task<object> RecheckAsync(string reference, CancellationToken ct)
    {
        var order = await FindOrderAsync(reference, ct) ?? throw new AcademyRuleException(404, "That order was not found.");
        order = await CheckWithProviderAsync(order, order.TransactionId, ct);
        return new { status = order.Status, note = order.Note };
    }

    /// <summary>Records that a payment was refunded (the refund itself is made in the provider's dashboard) and, if asked, takes the course away.</summary>
    public async Task MarkRefundedAsync(string reference, bool revokeAccess, CancellationToken ct)
    {
        var order = await FindOrderAsync(reference, ct) ?? throw new AcademyRuleException(404, "That order was not found.");
        if (order.Status is not ("paid" or "mismatch"))
        {
            throw new AcademyRuleException(409, "Only a payment that was received can be marked as refunded.");
        }

        await SaveOrderAsync(order with { Status = "refunded", Note = revokeAccess ? "Refunded. Access removed." : "Refunded." }, ct);
        if (revokeAccess)
        {
            await learners.UnenrolAsync(order.UserId, order.CourseId, ct);
        }
    }
}
