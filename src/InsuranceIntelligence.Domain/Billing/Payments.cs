using System.Security.Cryptography;
using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Domain.Billing;

public static class PaymentMethods
{
    public const string MtnMomo = "mtn-momo";
    public const string AirtelMoney = "airtel-money";
    public const string Card = "card";

    public static readonly string[] All = [MtnMomo, AirtelMoney, Card];

    public static bool IsMobileMoney(string method) => method is MtnMomo or AirtelMoney;

    public static string Label(string method) => method switch
    {
        MtnMomo => "MTN Mobile Money",
        AirtelMoney => "Airtel Money",
        Card => "Card",
        _ => method,
    };
}

public static class PaymentPurposes
{
    /// <summary>A workspace paying for a plan. The reference is the invoice id.</summary>
    public const string PlanSubscription = "PlanSubscription";

    /// <summary>A learner paying for a course (item 33). The reference is the enrolment id.</summary>
    public const string AcademyEnrolment = "AcademyEnrolment";
}

public static class PaymentStatuses
{
    public const string Pending = "Pending";
    public const string Succeeded = "Succeeded";
    public const string Failed = "Failed";
    public const string Expired = "Expired";
}

public static class InvoiceStatuses
{
    public const string Open = "Open";
    public const string Paid = "Paid";
    public const string Void = "Void";
}

/// <summary>One attempt to collect money for something. A failed attempt stays on record; the next attempt is a new one.</summary>
public sealed record PaymentIntent
{
    public required Guid PaymentIntentId { get; init; }
    public required TenantScope Scope { get; init; }
    public required string Purpose { get; init; }
    public required string PurposeReference { get; init; }
    public required string Method { get; init; }
    public required decimal Amount { get; init; }
    public required string Currency { get; init; }
    public required string Description { get; init; }

    /// <summary>The payer's mobile money number or email. Kept only as long as the attempt needs it, then blanked on settlement.</summary>
    public string? PayerContact { get; init; }

    public string Status { get; init; } = PaymentStatuses.Pending;

    /// <summary>The provider's own id for the attempt; how we ask what became of it.</summary>
    public string? ProviderReference { get; init; }

    /// <summary>Where a card payer is sent to pay. Null for mobile money, which asks the payer on their phone.</summary>
    public string? RedirectUrl { get; init; }

    /// <summary>A plain instruction for the payer, such as "Approve the request on your phone".</summary>
    public string? PayerInstruction { get; init; }

    public string? FailureReason { get; init; }

    /// <summary>A fingerprint of the secret in this attempt's callback address. The secret itself is never stored.</summary>
    public required string CallbackSecretHash { get; init; }

    public required string RequestedBy { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required DateTimeOffset UpdatedAt { get; init; }
    public DateTimeOffset? SettledAt { get; init; }
    public DateTimeOffset? LastCheckedAt { get; init; }
}

/// <summary>What a workspace owes, and the receipt once it has paid.</summary>
public sealed record Invoice
{
    public required Guid InvoiceId { get; init; }
    public required TenantScope Scope { get; init; }
    public required string Number { get; init; }
    public required string PlanKey { get; init; }
    public required string PlanName { get; init; }
    public required decimal Amount { get; init; }
    public required string Currency { get; init; }

    /// <summary>"New" for the first purchase of a plan or a change of plan, "Renewal" for the next month of the same plan.</summary>
    public required string Kind { get; init; }

    public string Status { get; init; } = InvoiceStatuses.Open;
    public required DateTimeOffset IssuedAt { get; init; }
    public DateTimeOffset? PaidAt { get; init; }
    public string? ReceiptNumber { get; init; }
    public string? PaidWith { get; init; }
    public Guid? PaymentIntentId { get; init; }

    /// <summary>The first day the payment covers and the day it runs to.</summary>
    public DateTimeOffset? CoversFrom { get; init; }
    public DateTimeOffset? CoversTo { get; init; }
}

/// <summary>What a provider says became of an attempt. Only a status read from the provider counts; a call arriving at our webhook is never believed on its own.</summary>
public sealed record ProviderPaymentStatus(string Status, decimal? Amount, string? Currency, string? FailureReason);

public sealed record ProviderInitiation(string ProviderReference, string? RedirectUrl, string? PayerInstruction);

public sealed record ProviderPaymentRequest(
    Guid PaymentIntentId,
    string Method,
    decimal Amount,
    string Currency,
    string Description,
    string? PayerContact,
    string CallbackUrl,
    string ReturnUrl);

public sealed class PaymentProviderException(string message) : Exception(message);

/// <summary>A payment provider (a mobile money operator or a card gateway) behind one shape.</summary>
public interface IPaymentProvider
{
    /// <summary>The payment methods this provider collects, from <see cref="PaymentMethods"/>.</summary>
    IReadOnlyCollection<string> Methods { get; }

    /// <summary>Whether it has what it needs to run (credentials, endpoint). A provider that is not configured is not offered.</summary>
    bool IsConfigured { get; }

    Task<ProviderInitiation> InitiateAsync(ProviderPaymentRequest request, CancellationToken cancellationToken = default);

    Task<ProviderPaymentStatus> GetStatusAsync(string method, string providerReference, CancellationToken cancellationToken = default);
}

public interface IPaymentProviderSelector
{
    IPaymentProvider? For(string method);

    /// <summary>The methods that can be offered right now.</summary>
    IReadOnlyList<string> AvailableMethods { get; }
}

public interface IPaymentStore
{
    Task SaveIntentAsync(PaymentIntent intent, CancellationToken cancellationToken = default);

    Task<PaymentIntent?> GetIntentAsync(TenantScope scope, Guid paymentIntentId, CancellationToken cancellationToken = default);

    /// <summary>Finds an attempt from the id in a callback address, which does not carry a workspace. The caller must check the secret and then work only inside the attempt's own scope.</summary>
    Task<PaymentIntent?> FindIntentByIdAsync(Guid paymentIntentId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PaymentIntent>> ListIntentsAsync(TenantScope scope, int take, CancellationToken cancellationToken = default);

    /// <summary>Attempts still waiting for an answer that were made before the moment given, across all workspaces. Only the platform's reconciliation calls this.</summary>
    Task<IReadOnlyList<PaymentIntent>> ListPendingBeforeAsync(DateTimeOffset moment, int take, CancellationToken cancellationToken = default);

    Task SaveInvoiceAsync(Invoice invoice, CancellationToken cancellationToken = default);

    Task<Invoice?> GetInvoiceAsync(TenantScope scope, Guid invoiceId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Invoice>> ListInvoicesAsync(TenantScope scope, CancellationToken cancellationToken = default);
}

/// <summary>Reference numbers people can quote: a short year-month prefix and a random suffix that is hard to guess and easy to read out.</summary>
public static class PaymentReferences
{
    private const string Alphabet = "23456789ABCDEFGHJKLMNPQRSTUVWXYZ";

    public static string InvoiceNumber(DateTimeOffset now) => $"INV-{now:yyyyMM}-{Suffix(6)}";

    public static string ReceiptNumber(DateTimeOffset now) => $"RCT-{now:yyyyMM}-{Suffix(6)}";

    private static string Suffix(int length)
    {
        Span<byte> bytes = stackalloc byte[length];
        RandomNumberGenerator.Fill(bytes);
        return string.Create(length, bytes.ToArray(), (span, source) =>
        {
            for (var i = 0; i < span.Length; i++)
            {
                span[i] = Alphabet[source[i] % Alphabet.Length];
            }
        });
    }
}
