using InsuranceIntelligence.Domain.Integrations;
using InsuranceIntelligence.Domain.Webhooks;

namespace InsuranceIntelligence.Application.Webhooks;

public sealed record WebhookSendResult(int? StatusCode, string? Error);

/// <summary>Makes the one HTTP call. Kept behind an interface so the rules below can be tested, and
/// so the real implementation can refuse private addresses at the moment it connects.</summary>
public interface IWebhookHttpSender
{
    Task<WebhookSendResult> SendAsync(Uri url, IReadOnlyDictionary<string, string> headers, string body, CancellationToken cancellationToken = default);
}

/// <summary>
/// Delivers queued events, retrying with growing gaps and giving up after a day. A receiver that is
/// down for an hour loses nothing; one that is gone for good ends up with Dead deliveries the insurer
/// can see, not a queue that grows forever.
/// </summary>
public sealed class OutboundWebhookDispatcher(
    IOutboundWebhookRepository repository,
    IConnectionSecretProtector protector,
    IWebhookHttpSender sender)
{
    public static readonly IReadOnlyList<TimeSpan> RetryGaps =
    [
        TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(10), TimeSpan.FromHours(1),
        TimeSpan.FromHours(3), TimeSpan.FromHours(8), TimeSpan.FromHours(24),
    ];

    public static int MaxAttempts => RetryGaps.Count + 1;

    public async Task<int> DispatchDueAsync(DateTimeOffset now, int batch = 50, CancellationToken cancellationToken = default)
    {
        var due = await repository.ListDueAsync(now, batch, cancellationToken);
        foreach (var delivery in due)
        {
            await DeliverAsync(delivery, now, cancellationToken);
        }

        return due.Count;
    }

    private async Task DeliverAsync(OutboundDelivery delivery, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var subscription = await repository.GetSubscriptionAsync(delivery.Scope, delivery.SubscriptionId, cancellationToken);
        if (subscription is not { IsActive: true } || !WebhookUrlPolicy.TryValidate(subscription.Url, out var uri, out _))
        {
            await repository.SaveDeliveryAsync(delivery with { Status = OutboundDeliveryStatus.Dead, LastError = "The subscription is gone or switched off." }, cancellationToken);
            return;
        }

        var timestamp = now.ToUnixTimeSeconds();
        var headers = new Dictionary<string, string>
        {
            [WebhookSignature.TimestampHeader] = timestamp.ToString(),
            [WebhookSignature.SignatureHeader] = WebhookSignature.Compute(protector.Unprotect(subscription.EncryptedSecret), timestamp, delivery.Body),
            [WebhookSignature.EventHeader] = delivery.EventType,
            [WebhookSignature.DeliveryHeader] = delivery.DeliveryId.ToString(),
        };

        WebhookSendResult result;
        try
        {
            result = await sender.SendAsync(uri!, headers, delivery.Body, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            result = new WebhookSendResult(null, "Could not reach the address.");
        }

        var attempts = delivery.Attempts + 1;
        if (result.StatusCode is >= 200 and < 300)
        {
            await repository.SaveDeliveryAsync(delivery with
            {
                Status = OutboundDeliveryStatus.Delivered, Attempts = attempts, DeliveredAt = now, LastStatusCode = result.StatusCode, LastError = null,
            }, cancellationToken);
            await repository.SaveSubscriptionAsync(subscription with { LastDeliveredAt = now, ConsecutiveFailures = 0 }, cancellationToken);
            return;
        }

        var failed = delivery with
        {
            Attempts = attempts,
            LastStatusCode = result.StatusCode,
            LastError = result.Error ?? $"The receiver answered {result.StatusCode}.",
        };
        failed = attempts >= MaxAttempts
            ? failed with { Status = OutboundDeliveryStatus.Dead }
            : failed with { NextAttemptAt = now + RetryGaps[attempts - 1] };
        await repository.SaveDeliveryAsync(failed, cancellationToken);
        await repository.SaveSubscriptionAsync(subscription with { ConsecutiveFailures = subscription.ConsecutiveFailures + 1 }, cancellationToken);
    }
}
