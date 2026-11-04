using System.Collections.Concurrent;
using InsuranceIntelligence.Domain.Billing;

namespace InsuranceIntelligence.Api.Local.Mock;

public sealed record MockPaymentEntry(string Reference, string Method, decimal Amount, string Currency, string CallbackUrl, string Status, string? FailureReason);

/// <summary>Remembers what the pretend operator was asked and what it says became of each request. Developer and demonstration runtimes only.</summary>
public sealed class MockPaymentLedger
{
    private readonly ConcurrentDictionary<string, MockPaymentEntry> entries = new(StringComparer.Ordinal);

    public void Add(MockPaymentEntry entry) => entries[entry.Reference] = entry;

    public MockPaymentEntry? Find(string reference) => entries.TryGetValue(reference, out var entry) ? entry : null;

    public IReadOnlyList<MockPaymentEntry> All() => entries.Values.ToArray();

    public bool Settle(string reference, string status, string? failureReason, decimal? amountOverride = null)
    {
        while (entries.TryGetValue(reference, out var current))
        {
            var next = current with { Status = status, FailureReason = failureReason, Amount = amountOverride ?? current.Amount };
            if (entries.TryUpdate(reference, next, current))
            {
                return true;
            }
        }

        return false;
    }
}

/// <summary>
/// A pretend operator for all three methods. A mobile money request waits until it is settled (through /api/v1/dev/mock-payments, the way
/// an operator's own app would answer), and a card payer is sent to a pretend gateway page in the portal. A number ending in 99 is refused
/// at once with "insufficient funds", so a failed payment can be shown without any extra step.
/// </summary>
public sealed class MockPaymentProvider(MockPaymentLedger ledger) : IPaymentProvider
{
    public IReadOnlyCollection<string> Methods { get; } = PaymentMethods.All;

    public bool IsConfigured => true;

    public Task<ProviderInitiation> InitiateAsync(ProviderPaymentRequest request, CancellationToken cancellationToken = default)
    {
        var reference = $"mock-{request.PaymentIntentId:N}";
        var refused = PaymentMethods.IsMobileMoney(request.Method) && (request.PayerContact ?? string.Empty).EndsWith("99", StringComparison.Ordinal);
        ledger.Add(new MockPaymentEntry(reference, request.Method, request.Amount, request.Currency, request.CallbackUrl,
            refused ? PaymentStatuses.Failed : PaymentStatuses.Pending, refused ? "Insufficient funds in the account." : null));

        if (request.Method == PaymentMethods.Card)
        {
            var origin = Uri.TryCreate(request.ReturnUrl, UriKind.Absolute, out var returnUri) ? returnUri.GetLeftPart(UriPartial.Authority) : "http://localhost:3000";
            return Task.FromResult(new ProviderInitiation(reference, $"{origin}/pay/mock?reference={Uri.EscapeDataString(reference)}", "You will be taken to a pretend card page. No card is charged."));
        }

        return Task.FromResult(new ProviderInitiation(reference, null, $"Approve the pretend {PaymentMethods.Label(request.Method)} request. In a real deployment this is a prompt on your phone."));
    }

    public Task<ProviderPaymentStatus> GetStatusAsync(string method, string providerReference, CancellationToken cancellationToken = default)
    {
        var entry = ledger.Find(providerReference);
        return Task.FromResult(entry is null
            ? new ProviderPaymentStatus(PaymentStatuses.Failed, null, null, "The pretend operator has no record of this payment.")
            : new ProviderPaymentStatus(entry.Status, entry.Status == PaymentStatuses.Succeeded ? entry.Amount : null, entry.Status == PaymentStatuses.Succeeded ? entry.Currency : null, entry.FailureReason));
    }
}

public static class MockPaymentEndpoints
{
    public sealed record SettleBody(string? Outcome, decimal? Amount);

    public static IResult List(MockPaymentLedger ledger) => Results.Ok(ledger.All());

    /// <summary>Answers a pretend request the way the operator would, then tells the platform's callback address (as a real operator does).</summary>
    public static async Task<IResult> Settle(string reference, SettleBody body, MockPaymentLedger ledger, IHttpClientFactory clients, CancellationToken cancellationToken)
    {
        var outcome = body.Outcome?.Trim().ToLowerInvariant();
        if (outcome is not ("succeeded" or "failed"))
        {
            return Results.BadRequest(new { message = "Outcome must be 'succeeded' or 'failed'." });
        }

        if (!ledger.Settle(reference, outcome == "succeeded" ? PaymentStatuses.Succeeded : PaymentStatuses.Failed, outcome == "failed" ? "The payer declined the request." : null, body.Amount))
        {
            return Results.NotFound(new { message = "No such pretend payment." });
        }

        var entry = ledger.Find(reference)!;
        var notified = false;
        if (Uri.TryCreate(entry.CallbackUrl, UriKind.Absolute, out var callback))
        {
            try
            {
                using var client = clients.CreateClient();
                using var response = await client.PostAsync(callback, null, cancellationToken);
                notified = response.IsSuccessStatusCode;
            }
            catch (HttpRequestException)
            {
                // The callback is a nudge; reconciliation settles the payment even if it never arrives.
            }
        }

        return Results.Ok(new { entry.Reference, entry.Status, notified });
    }
}
