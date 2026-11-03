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

/// <summary>Receives only signed customer initiated Messenger and Instagram messages.</summary>
public static class MetaMessagingWebhookEndpoints
{
    private const int MaximumPayloadBytes = 65536;
    private const string SignatureHeader = "X-Hub-Signature-256";

    public static async Task<IResult> Verify(string channelName, string accountId, HttpRequest request,
        IIntegrationConnectionRepository connections, IConnectionSecretProtector protector, CancellationToken cancellationToken)
    {
        var mode = request.Query["hub.mode"].ToString();
        var token = request.Query["hub.verify_token"].ToString();
        var challenge = request.Query["hub.challenge"].ToString();
        if (!string.Equals(mode, "subscribe", StringComparison.Ordinal) || challenge.Length > 256)
            return Results.BadRequest();

        var type = ParseConnectionType(channelName);
        if (type is null) return Results.NotFound();
        var connection = await connections.FindByExternalAccountAsync(type.Value, accountId, cancellationToken);
        if (connection is null || !connection.IsActive) return Results.NotFound();
        var settings = ConnectionSettingsCodec.Decode(protector, connection);
        if (settings.TryGetValue("webhookVerifyToken", out var expected) && FixedEquals(expected, token))
            return Results.Text(challenge, "text/plain");
        return Results.Unauthorized();
    }

    public static async Task<IResult> Receive(string channelName, string accountId, HttpRequest request, IIntegrationConnectionRepository connections,
        IConnectionSecretProtector protector, IChannelMessageIngestionService ingestion,
        IConsentRepository consents, IAuditRepository audit, CancellationToken cancellationToken)
    {
        if (request.ContentLength is > MaximumPayloadBytes) return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
        var body = await ReadBoundedBodyAsync(request, cancellationToken);
        if (body is null) return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
        var signature = request.Headers[SignatureHeader].ToString();
        if (!TryReadObject(body, out var root)) return Results.BadRequest(new { message = "Meta webhook payload was not valid JSON." });
        using (root)
        {
            var type = ParseConnectionType(channelName);
            if (type is null) return Results.NotFound();
            var connection = await connections.FindByExternalAccountAsync(type.Value, accountId, cancellationToken);
            if (connection is null || !connection.IsActive) return Results.Ok(new { status = "Ignored" });
            var settings = ConnectionSettingsCodec.Decode(protector, connection);
            if (!settings.TryGetValue("appSecret", out var appSecret) || !VerifySignature(appSecret, signature, body))
                return Results.Unauthorized();

            if (!root.RootElement.TryGetProperty("object", out var objectValue) || objectValue.ValueKind != JsonValueKind.String)
                return Results.Ok(new { status = "Ignored" });
            var objectName = objectValue.GetString();
            var payloadType = objectName switch
            {
                "instagram" => ConnectionType.Instagram,
                "page" => ConnectionType.Messenger,
                _ => (ConnectionType?)null,
            };
            if (payloadType != type || !root.RootElement.TryGetProperty("entry", out var entries) || entries.ValueKind != JsonValueKind.Array)
                return Results.Ok(new { status = "Ignored" });
            var discovered = connection.LastValidationDiscoveredJson is null
                ? new Dictionary<string, string>(StringComparer.Ordinal)
                : JsonSerializer.Deserialize<Dictionary<string, string>>(connection.LastValidationDiscoveredJson)
                    ?? new Dictionary<string, string>(StringComparer.Ordinal);
            discovered["messagingStatus"] = "Webhook active";
            connection = connection with
            {
                LastValidatedAt = DateTimeOffset.UtcNow,
                LastValidationDiscoveredJson = ConnectionSettingsCodec.EncodeDiscovered(discovered),
            };
            await connections.SaveAsync(connection, cancellationToken);

            var accepted = 0;
            foreach (var entry in entries.EnumerateArray())
            {
                if (entry.ValueKind != JsonValueKind.Object || !entry.TryGetProperty("id", out var idValue)
                    || idValue.ValueKind != JsonValueKind.String || !string.Equals(idValue.GetString(), accountId, StringComparison.Ordinal)) continue;
                var channel = type == ConnectionType.Instagram ? ChannelValues.Instagram : ChannelValues.Messenger;
                var purpose = type == ConnectionType.Instagram ? ConsentPurposes.Instagram : ConsentPurposes.Messenger;
                if (!entry.TryGetProperty("messaging", out var events) || events.ValueKind != JsonValueKind.Array) continue;
                foreach (var item in events.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.Object
                        || !item.TryGetProperty("sender", out var sender) || sender.ValueKind != JsonValueKind.Object
                        || !TryString(sender, "id", out var senderId)
                        || !item.TryGetProperty("recipient", out var recipient) || recipient.ValueKind != JsonValueKind.Object
                        || !TryString(recipient, "id", out var recipientId)
                        || string.Equals(recipientId, accountId, StringComparison.Ordinal) == false
                        || !item.TryGetProperty("message", out var message) || message.ValueKind != JsonValueKind.Object
                        || (message.TryGetProperty("is_echo", out var echo) && echo.ValueKind == JsonValueKind.True)) continue;

                    var hasText = TryString(message, "text", out var text);
                    var providerId = TryString(message, "mid", out var mid) ? mid : null;
                    if (providerId is null) continue;
                    var timestamp = item.TryGetProperty("timestamp", out var timestampValue) && timestampValue.TryGetInt64(out var milliseconds)
                        ? DateTimeOffset.FromUnixTimeMilliseconds(milliseconds) : DateTimeOffset.UtcNow;
                    var correlation = request.HttpContext.TraceIdentifier;
                    var threadId = BuildThreadId(channel, accountId, senderId);
                    var normalized = new ChannelMessage
                    {
                        MessageId = Guid.NewGuid(), Scope = connection.Scope, Channel = channel,
                        AccountId = accountId, ThreadId = threadId, ExternalRecipientId = senderId,
                        Direction = ChannelMessageDirection.Inbound, ContentType = hasText ? "Text" : "Unsupported",
                        CustomerSafeBody = hasText ? text[..Math.Min(text.Length, 4096)] : "This message type is not supported for display.",
                        ProviderMessageId = providerId, DeliveryState = ChannelMessageDeliveryState.Received,
                        CreatedAt = timestamp, UpdatedAt = DateTimeOffset.UtcNow, CorrelationId = correlation,
                    };
                    var inserted = await ingestion.IngestInboundAsync(normalized, cancellationToken);
                    if (inserted) accepted++;
                    var current = await consents.GetCurrentAsync(connection.Scope, senderId, purpose, cancellationToken);
                    var stop = hasText && IsOptOut(text);
                    if ((inserted || current is null) && (stop || hasText))
                    {
                        var record = new ConsentRecord
                        {
                            RecordId = Guid.NewGuid(), Scope = connection.Scope, CustomerIdentifier = senderId,
                            Basis = stop ? ConsentBasis.Withdrawn : ConsentBasis.CustomerInitiatedConversation,
                            Purpose = purpose, Channel = channel,
                            RecordedByUserId = $"integration:meta:{accountId}",
                            WithdrawnAt = stop ? DateTimeOffset.UtcNow : null, RecordedAt = DateTimeOffset.UtcNow,
                            CorrelationId = correlation, InteractionReference = threadId,
                        };
                        await consents.SaveAsync(record, cancellationToken);
                        await audit.WriteAsync(new AuditEvent(Guid.NewGuid(), connection.Scope, null, null,
                            record.RecordedByUserId, ActorType.Integration, stop ? "consent.withdrawn" : "consent.granted",
                            DateTimeOffset.UtcNow, null, record.Basis.ToString(), [], channel, null, purpose, null,
                            stop ? "Meta messaging opt-out recorded; customer identifier omitted." : "Meta customer initiated conversation; identifier omitted.", correlation), cancellationToken);
                    }
                }
            }
            return Results.Ok(new { status = "Accepted", messages = accepted });
        }
    }

    private static ConnectionType? ParseConnectionType(string channelName) => channelName.ToLowerInvariant() switch
    {
        ChannelValues.Instagram => ConnectionType.Instagram,
        ChannelValues.Messenger => ConnectionType.Messenger,
        _ => null,
    };

    private static bool TryReadObject(byte[] body, out JsonDocument document)
    {
        try { document = JsonDocument.Parse(body); return document.RootElement.ValueKind == JsonValueKind.Object; }
        catch (JsonException) { document = null!; return false; }
    }

    private static bool TryString(JsonElement element, string property, out string value)
    {
        value = string.Empty;
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(property, out var field)
            || field.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(field.GetString())) return false;
        value = field.GetString()!;
        return value.Length <= 320;
    }

    private static bool IsOptOut(string text) => text.Trim().Equals("stop", StringComparison.OrdinalIgnoreCase)
        || text.Trim().Equals("unsubscribe", StringComparison.OrdinalIgnoreCase);

    private static bool VerifySignature(string secret, string supplied, byte[] body)
    {
        if (string.IsNullOrWhiteSpace(secret) || secret.Length > 512 || !supplied.StartsWith("sha256=", StringComparison.Ordinal)) return false;
        try
        {
            var expected = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), body);
            var actual = Convert.FromHexString(supplied[7..]);
            return expected.Length == actual.Length && CryptographicOperations.FixedTimeEquals(expected, actual);
        }
        catch (FormatException) { return false; }
    }

    private static bool FixedEquals(string expected, string supplied)
    {
        if (string.IsNullOrEmpty(expected) || string.IsNullOrEmpty(supplied) || expected.Length > 256 || supplied.Length > 256) return false;
        var left = Encoding.UTF8.GetBytes(expected);
        var right = Encoding.UTF8.GetBytes(supplied);
        return left.Length == right.Length && CryptographicOperations.FixedTimeEquals(left, right);
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

    private static string BuildThreadId(string channel, string accountId, string senderId)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{channel}:{accountId}:{senderId}"));
        return $"meta_{Convert.ToHexString(hash).ToLowerInvariant()}";
    }
}
