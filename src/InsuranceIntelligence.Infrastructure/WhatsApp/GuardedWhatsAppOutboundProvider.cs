using InsuranceIntelligence.Domain.Integrations;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace InsuranceIntelligence.Infrastructure.WhatsApp;

/// <summary>
/// How many messages one number may be sent in an hour and in a day. A runaway loop (a retry that never stops, two
/// automated senders answering each other) is the failure this stops: the limits are far above what a real
/// conversation needs. Ported from CIP's relay <c>RateLimiter</c>, whose limits (5 an hour) suit a credit memo
/// and would break a chat, so IIP's defaults are higher and configurable.
/// </summary>
public sealed class OutboundRateLimiter(IMemoryCache cache, IConfiguration configuration, TimeProvider clock)
{
    private readonly int maxPerHour = int.TryParse(configuration["WhatsApp:Outbound:MaxPerRecipientPerHour"], out var h) && h > 0 ? h : 60;
    private readonly int maxPerDay = int.TryParse(configuration["WhatsApp:Outbound:MaxPerRecipientPerDay"], out var d) && d > 0 ? d : 300;

    public bool TryAcquire(string businessNumberId, string recipientWaId, out string? reason)
    {
        var now = clock.GetUtcNow();
        var who = $"{businessNumberId}:{new string(recipientWaId.Where(char.IsDigit).ToArray())}";
        var hourly = Increment($"wa-out:{who}:h:{now:yyyyMMddHH}", TimeSpan.FromMinutes(65));
        var daily = Increment($"wa-out:{who}:d:{now:yyyyMMdd}", TimeSpan.FromHours(25));
        if (hourly > maxPerHour)
        {
            reason = "Too many messages were sent to this number in the last hour, so this one was held back.";
            return false;
        }

        if (daily > maxPerDay)
        {
            reason = "Too many messages were sent to this number today, so this one was held back.";
            return false;
        }

        reason = null;
        return true;
    }

    private int Increment(string key, TimeSpan lifetime)
    {
        // A lock keeps the count right when two replies go to one number at the same moment.
        lock (cache)
        {
            var count = (cache.TryGetValue(key, out int existing) ? existing : 0) + 1;
            cache.Set(key, count, lifetime);
            return count;
        }
    }
}

/// <summary>
/// Wraps the live WhatsApp sender with the checks that apply to everything leaving for a customer: the content guard
/// (nothing that exposes a secret or asks for a credential) and the per-number rate limit. A refused message comes
/// back as a failed send with a plain reason, so the caller stores it as failed and a person sees it, instead of
/// the message silently vanishing. No message text is written to logs.
/// </summary>
public sealed class GuardedWhatsAppOutboundProvider(
    IWhatsAppOutboundProvider inner,
    OutboundRateLimiter limiter,
    ILogger<GuardedWhatsAppOutboundProvider> logger) : IWhatsAppOutboundProvider
{
    public bool IsConfigured => inner.IsConfigured;

    public Task<bool> ShowTypingAsync(string businessPhoneNumberId, string inboundProviderMessageId, CancellationToken cancellationToken = default) =>
        inner.ShowTypingAsync(businessPhoneNumberId, inboundProviderMessageId, cancellationToken);

    public async Task<WhatsAppProviderSendResult> SendAsync(WhatsAppProviderSendRequest request, CancellationToken cancellationToken = default)
    {
        var verdict = OutboundMessageGuard.Check(request.CustomerSafeBody);
        if (!verdict.Allowed)
        {
            logger.LogWarning("Outbound WhatsApp message refused by the content guard: {Code}.", verdict.Code);
            return new WhatsAppProviderSendResult(false, null, $"egress_{verdict.Code}", verdict.SafeReason);
        }

        if (!limiter.TryAcquire(request.BusinessPhoneNumberId, request.RecipientWaId, out var reason))
        {
            logger.LogWarning("Outbound WhatsApp message held back by the per-number rate limit.");
            return new WhatsAppProviderSendResult(false, null, "egress_rate_limited", reason);
        }

        return await inner.SendAsync(request, cancellationToken);
    }

    public Task<WhatsAppProviderSendResult> SendVoiceNoteAsync(
        string businessPhoneNumberId, string recipientWaId, byte[] oggOpusAudio, string? replyToProviderMessageId, CancellationToken cancellationToken = default)
    {
        if (!limiter.TryAcquire(businessPhoneNumberId, recipientWaId, out var reason))
        {
            logger.LogWarning("Outbound WhatsApp voice note held back by the per-number rate limit.");
            return Task.FromResult(new WhatsAppProviderSendResult(false, null, "egress_rate_limited", reason));
        }

        return inner.SendVoiceNoteAsync(businessPhoneNumberId, recipientWaId, oggOpusAudio, replyToProviderMessageId, cancellationToken);
    }
}
