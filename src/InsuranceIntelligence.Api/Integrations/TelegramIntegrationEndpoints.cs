using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using InsuranceIntelligence.Application.Channels;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Channels;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Compliance;
using InsuranceIntelligence.Domain.Integrations;

namespace InsuranceIntelligence.Api.Integrations;

public static class TelegramIntegrationEndpoints
{
    private const int MaximumPayloadBytes = 65536;
    private const string SecretHeader = "X-Telegram-Bot-Api-Secret-Token";

    public static async Task<IResult> ReceiveWebhook(
        string botId,
        HttpRequest request,
        IIntegrationConnectionRepository connections,
        IConnectionSecretProtector protector,
        IChannelMessageIngestionService ingestion,
        IConsentRepository consentRepository,
        IAuditRepository auditRepository,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(botId) || botId.Length > 40)
            return Results.NotFound();

        var connection = await connections.FindByExternalAccountAsync(ConnectionType.Telegram, botId, cancellationToken);
        if (connection is null)
            return Results.Ok(new { status = "Ignored" });

        var settings = ConnectionSettingsCodec.Decode(protector, connection);
        if (!settings.TryGetValue("webhookSecret", out var expectedSecret)
            || !FixedEquals(expectedSecret, request.Headers[SecretHeader].ToString()))
            return Results.Unauthorized();

        if (request.ContentLength is > MaximumPayloadBytes)
            return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
        var body = await ReadBoundedBodyAsync(request, cancellationToken);
        if (body is null) return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);

        try
        {
            using var json = JsonDocument.Parse(body);
            var root = json.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("update_id", out var updateId)
                || !updateId.TryGetInt64(out var updateNumber)
                || !root.TryGetProperty("message", out var message)
                || message.ValueKind != JsonValueKind.Object
                || !message.TryGetProperty("chat", out var chat)
                || chat.ValueKind != JsonValueKind.Object
                || !chat.TryGetProperty("id", out var chatIdValue)
                || chatIdValue.ValueKind != JsonValueKind.Number
                || !chatIdValue.TryGetInt64(out var chatNumber))
                return Results.Ok(new { status = "Ignored" });

            if (!chat.TryGetProperty("type", out var chatType) || chatType.ValueKind != JsonValueKind.String
                || !string.Equals(chatType.GetString(), "private", StringComparison.Ordinal))
                return Results.Ok(new { status = "Ignored", reason = "Private conversations only." });
            if (message.TryGetProperty("from", out var sender)
                && sender.ValueKind == JsonValueKind.Object
                && sender.TryGetProperty("is_bot", out var isBot)
                && isBot.ValueKind == JsonValueKind.True)
                return Results.Ok(new { status = "Ignored" });

            var chatId = chatNumber.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var accountId = botId.Trim();
            var threadId = BuildThreadId(accountId, chatId);
            var hasText = message.TryGetProperty("text", out var textElement) && textElement.ValueKind == JsonValueKind.String;
            var text = hasText ? textElement.GetString() ?? string.Empty : "This Telegram message type is not yet supported.";
            var createdAt = message.TryGetProperty("date", out var dateValue) && dateValue.TryGetInt64(out var unixSeconds)
                ? DateTimeOffset.FromUnixTimeSeconds(unixSeconds)
                : DateTimeOffset.UtcNow;
            var messageRecord = new ChannelMessage
            {
                MessageId = Guid.NewGuid(),
                Scope = connection.Scope,
                Channel = ChannelValues.Telegram,
                AccountId = accountId,
                ThreadId = threadId,
                ExternalRecipientId = chatId,
                Direction = ChannelMessageDirection.Inbound,
                ContentType = hasText ? "Text" : "Unsupported",
                CustomerSafeBody = text.Length <= 4096 ? text : text[..4096],
                ProviderMessageId = $"update:{updateNumber.ToString(System.Globalization.CultureInfo.InvariantCulture)}",
                DeliveryState = ChannelMessageDeliveryState.Received,
                CreatedAt = createdAt,
                UpdatedAt = DateTimeOffset.UtcNow,
                CorrelationId = request.HttpContext.TraceIdentifier,
            };
            var inserted = await ingestion.IngestInboundAsync(messageRecord, cancellationToken);
            var currentConsent = await consentRepository.GetCurrentAsync(connection.Scope, chatId, ConsentPurposes.Telegram, cancellationToken);
            var normalizedText = text.Trim();
            var withdrawConsent = hasText && (normalizedText.Equals("/stop", StringComparison.OrdinalIgnoreCase)
                || normalizedText.Equals("stop", StringComparison.OrdinalIgnoreCase)
                || normalizedText.Equals("unsubscribe", StringComparison.OrdinalIgnoreCase));
            if (withdrawConsent && inserted)
            {
                var consentRecord = new ConsentRecord
                {
                    RecordId = Guid.NewGuid(), Scope = connection.Scope, CustomerIdentifier = chatId,
                    Basis = ConsentBasis.Withdrawn, Purpose = ConsentPurposes.Telegram,
                    Channel = ChannelValues.Telegram, RecordedByUserId = $"integration:telegram:{accountId}",
                    WithdrawnAt = DateTimeOffset.UtcNow, RecordedAt = DateTimeOffset.UtcNow,
                    CorrelationId = messageRecord.CorrelationId, InteractionReference = threadId,
                };
                await consentRepository.SaveAsync(consentRecord, cancellationToken);
                await auditRepository.WriteAsync(new AuditEvent(Guid.NewGuid(), connection.Scope, null, null,
                    consentRecord.RecordedByUserId, ActorType.Integration, "consent.withdrawn", DateTimeOffset.UtcNow,
                    null, consentRecord.Basis.ToString(), [], ChannelValues.Telegram, null, ConsentPurposes.Telegram, null,
                    "Telegram opt-out recorded; customer identifier omitted.", consentRecord.CorrelationId), cancellationToken);
            }
            else if (!withdrawConsent && (inserted || currentConsent is null))
            {
                var consentRecord = new ConsentRecord
                {
                    RecordId = Guid.NewGuid(), Scope = connection.Scope, CustomerIdentifier = chatId,
                    Basis = ConsentBasis.CustomerInitiatedConversation, Purpose = ConsentPurposes.Telegram,
                    Channel = ChannelValues.Telegram, RecordedByUserId = $"integration:telegram:{accountId}",
                    RecordedAt = DateTimeOffset.UtcNow, CorrelationId = messageRecord.CorrelationId, InteractionReference = threadId,
                };
                await consentRepository.SaveAsync(consentRecord, cancellationToken);
                await auditRepository.WriteAsync(new AuditEvent(Guid.NewGuid(), connection.Scope, null, null,
                    consentRecord.RecordedByUserId, ActorType.Integration, "consent.granted", DateTimeOffset.UtcNow,
                    null, consentRecord.Basis.ToString(), [], ChannelValues.Telegram, null, ConsentPurposes.Telegram, null,
                    "Telegram customer initiated conversation; identifier omitted.", consentRecord.CorrelationId), cancellationToken);
            }
            return Results.Ok(new { status = inserted ? "Accepted" : "Duplicate" });
        }
        catch (JsonException)
        {
            return Results.BadRequest(new { message = "Telegram webhook payload was not valid JSON." });
        }
        catch (ArgumentOutOfRangeException)
        {
            return Results.BadRequest(new { message = "Telegram webhook timestamp was invalid." });
        }
    }

    private static bool FixedEquals(string expected, string supplied)
    {
        if (string.IsNullOrEmpty(expected) || string.IsNullOrEmpty(supplied) || expected.Length > 256 || supplied.Length > 256)
            return false;
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var suppliedBytes = Encoding.UTF8.GetBytes(supplied);
        return expectedBytes.Length == suppliedBytes.Length && CryptographicOperations.FixedTimeEquals(expectedBytes, suppliedBytes);
    }

    private static async Task<byte[]?> ReadBoundedBodyAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        using var memory = new MemoryStream();
        var buffer = new byte[8192];
        while (true)
        {
            var read = await request.Body.ReadAsync(buffer, cancellationToken);
            if (read == 0) break;
            if (memory.Length + read > MaximumPayloadBytes) return null;
            await memory.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }
        return memory.ToArray();
    }

    private static string BuildThreadId(string accountId, string chatId)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"telegram:{accountId}:{chatId}"));
        return $"tg_{Convert.ToHexString(bytes).ToLowerInvariant()}";
    }
}
