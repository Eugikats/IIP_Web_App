using InsuranceIntelligence.Application.CustomerAssistant;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.CustomerAssistant;
using InsuranceIntelligence.Domain.Integrations;

namespace InsuranceIntelligence.Application.WhatsApp;

/// <summary>
/// The safety net under the in-memory assistant queue. The webhook saves a customer's message and
/// answers Meta before the assistant runs, so a restart or crash in between used to leave the
/// message saved and never answered. This finds those: text messages that arrived long enough ago
/// that the assistant's own time limit has passed, that the assistant never marked as finished, and
/// that no reply in the conversation follows, and gives them to the assistant again.
///
/// A message is answered again only when nothing shows it was answered, so the worst case of a
/// missed mark is one extra look, not a second reply.
/// </summary>
public sealed class WhatsAppAssistantRecoveryService(
    IWhatsAppMessageRepository messages,
    ICustomerConversationRepository conversations,
    ICustomerAssistantService assistant)
{
    /// <summary>Longer than the assistant's own 3 minute limit, so a message still being worked on is never taken.</summary>
    public static readonly TimeSpan MinimumAge = TimeSpan.FromMinutes(3.5);

    /// <summary>Further back than this a reply would arrive too late to help; a person has it in the Inbox.</summary>
    public static readonly TimeSpan MaximumAge = TimeSpan.FromHours(24);

    public async Task<int> RecoverAsync(TenantScope scope, bool autoReplyEnabled, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var candidates = (await messages.ListSinceAsync(scope, now - MaximumAge, cancellationToken))
            .Where(m => m.Direction == WhatsAppMessageDirection.Inbound
                && m.ContentType == WhatsAppContentType.Text
                && m.AssistantHandledAt is null
                && !string.IsNullOrWhiteSpace(m.ProviderMessageId)
                && !string.IsNullOrWhiteSpace(m.CustomerSafeBody)
                && now - m.CreatedAt >= MinimumAge)
            .ToArray();
        if (candidates.Length == 0)
        {
            return 0;
        }

        var lost = new List<WhatsAppInboundMessage>();
        foreach (var thread in candidates.GroupBy(m => m.ThreadId))
        {
            var turns = await conversations.ListForThreadAsync(scope, thread.Key, cancellationToken);
            foreach (var message in thread.OrderBy(m => m.CreatedAt))
            {
                if (Answered(message, turns))
                {
                    continue;
                }

                lost.Add(new WhatsAppInboundMessage(
                    message.ProviderMessageId!, message.BusinessPhoneNumberId, message.ContactWaId, message.ContentType,
                    message.CustomerSafeBody, message.ReplyToProviderMessageId, message.CreatedAt));
            }
        }

        if (lost.Count == 0)
        {
            return 0;
        }

        await assistant.HandleInboundMessagesAsync(scope, lost, $"recovery-{now:yyyyMMddHHmmss}", autoReplyEnabled, cancellationToken);
        return lost.Count;
    }

    /// <summary>A conversation turn after the message, carrying its words, shows it was dealt with.
    /// Works for messages from before the assistant marked them, so nothing old is answered twice.</summary>
    private static bool Answered(WhatsAppMessage message, IReadOnlyCollection<CustomerConversationTurn> turns)
    {
        var words = message.CustomerSafeBody.Trim();
        return turns.Any(turn =>
            turn.OccurredAt >= message.CreatedAt
            && (string.Equals(turn.InboundMessageText?.Trim(), words, StringComparison.Ordinal)
                || string.Equals(turn.OriginalInboundText?.Trim(), words, StringComparison.Ordinal)));
    }
}
