using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using InsuranceIntelligence.Application.Channels;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Channels;
using InsuranceIntelligence.Domain.Compliance;
using InsuranceIntelligence.Domain.Integrations;

namespace InsuranceIntelligence.Api.Integrations;

/// <summary>Receives signed, unencrypted X Activity API Direct Message events.</summary>
public static class XActivityWebhookEndpoints
{
    private const int MaximumPayloadBytes = 65536;

    public static async Task<IResult> Challenge(string accountId, HttpRequest request,
        IIntegrationConnectionRepository connections, IConnectionSecretProtector protector,
        CancellationToken cancellationToken)
    {
        var token = request.Query["crc_token"].ToString();
        if (string.IsNullOrWhiteSpace(token) || token.Length > 512) return Results.BadRequest();
        var connection = await connections.FindByExternalAccountAsync(ConnectionType.X, accountId, cancellationToken);
        if (connection is null || !connection.IsActive) return Results.NotFound();
        var settings = ConnectionSettingsCodec.Decode(protector, connection);
        var secret = SigningSecret(settings);
        if (secret is null) return Results.NotFound();
        var digest = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(token));
        return Results.Json(new { response_token = "sha256=" + Convert.ToBase64String(digest) });
    }

    public static async Task<IResult> Receive(string accountId, HttpRequest request,
        IIntegrationConnectionRepository connections, IConnectionSecretProtector protector,
        IChannelMessageIngestionService ingestion, IConsentRepository consents,
        IAuditRepository audit, CancellationToken cancellationToken)
    {
        if (request.ContentLength is > MaximumPayloadBytes) return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
        var body = await ReadBoundedBodyAsync(request, cancellationToken);
        if (body is null) return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);

        var connection = await connections.FindByExternalAccountAsync(ConnectionType.X, accountId, cancellationToken);
        if (connection is null || !connection.IsActive) return Results.Ok(new { status = "Ignored" });
        var settings = ConnectionSettingsCodec.Decode(protector, connection);
        var secret = SigningSecret(settings);
        var supplied = request.Headers["X-Twitter-Webhooks-Signature-OAuth2"].ToString();
        if (string.IsNullOrWhiteSpace(supplied)) supplied = request.Headers["X-Twitter-Webhooks-Signature"].ToString();
        if (secret is null || !VerifySignature(secret, supplied, body)) return Results.Unauthorized();

        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            if (!TryProperty(root, "data", out var data)
                || !TryString(data, "event_type", out var eventType)
                || !string.Equals(eventType, "dm.received", StringComparison.Ordinal))
                return Results.Ok(new { status = "Ignored" });
            if (!TryProperty(data, "filter", out var filter)
                || !TryString(filter, "user_id", out var filteredAccount)
                || !string.Equals(filteredAccount, accountId, StringComparison.Ordinal)
                || !TryProperty(data, "payload", out var payload)
                || !TryProperty(payload, "direct_message_events", out var events)
                || events.ValueKind != JsonValueKind.Array)
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
            foreach (var messageEvent in events.EnumerateArray())
            {
                if (!TryProperty(messageEvent, "message_create", out var create)
                    || !TryProperty(create, "target", out var target)
                    || !TryString(target, "recipient_id", out var recipientId)
                    || !string.Equals(recipientId, accountId, StringComparison.Ordinal)
                    || !TryString(create, "sender_id", out var senderId)
                    || string.Equals(senderId, accountId, StringComparison.Ordinal)
                    || !TryString(messageEvent, "id", out var providerMessageId)) continue;

                var messageText = string.Empty;
                var hasText = TryProperty(create, "message_data", out var messageData)
                    && TryString(messageData, "text", out messageText);
                var text = hasText ? messageText! : "This X message type is not supported for display.";
                var timestamp = TryString(messageEvent, "created_timestamp", out var millisecondsText)
                    && long.TryParse(millisecondsText, System.Globalization.NumberStyles.None,
                        System.Globalization.CultureInfo.InvariantCulture, out var milliseconds)
                    ? DateTimeOffset.FromUnixTimeMilliseconds(milliseconds)
                    : DateTimeOffset.UtcNow;
                var correlation = request.HttpContext.TraceIdentifier;
                var threadId = BuildThreadId(accountId, senderId);
                var message = new ChannelMessage
                {
                    MessageId = Guid.NewGuid(), Scope = connection.Scope, Channel = ChannelValues.X,
                    AccountId = accountId, ThreadId = threadId, ExternalRecipientId = senderId,
                    Direction = ChannelMessageDirection.Inbound, ContentType = hasText ? "Text" : "Unsupported",
                    CustomerSafeBody = text.Length <= 4096 ? text : text[..4096],
                    ProviderMessageId = providerMessageId, DeliveryState = ChannelMessageDeliveryState.Received,
                    CreatedAt = timestamp, UpdatedAt = DateTimeOffset.UtcNow, CorrelationId = correlation,
                };
                var inserted = await ingestion.IngestInboundAsync(message, cancellationToken);
                if (inserted) accepted++;
                var currentConsent = await consents.GetCurrentAsync(connection.Scope, senderId, ConsentPurposes.X, cancellationToken);
                var stop = hasText && IsOptOut(text);
                if ((inserted || currentConsent is null) && (stop || hasText))
                {
                    var record = new ConsentRecord
                    {
                        RecordId = Guid.NewGuid(), Scope = connection.Scope, CustomerIdentifier = senderId,
                        Basis = stop ? ConsentBasis.Withdrawn : ConsentBasis.CustomerInitiatedConversation,
                        Purpose = ConsentPurposes.X, Channel = ChannelValues.X,
                        RecordedByUserId = $"integration:x:{accountId}", WithdrawnAt = stop ? DateTimeOffset.UtcNow : null,
                        RecordedAt = DateTimeOffset.UtcNow, CorrelationId = correlation, InteractionReference = threadId,
                    };
                    await consents.SaveAsync(record, cancellationToken);
                    await audit.WriteAsync(new AuditEvent(Guid.NewGuid(), connection.Scope, null, null,
                        record.RecordedByUserId, ActorType.Integration, stop ? "consent.withdrawn" : "consent.granted",
                        DateTimeOffset.UtcNow, null, record.Basis.ToString(), [], ChannelValues.X, null,
                        ConsentPurposes.X, null, stop ? "X messaging opt-out recorded; customer identifier omitted."
                            : "X customer initiated conversation; customer identifier omitted.", correlation), cancellationToken);
                }
            }
            return Results.Ok(new { status = "Accepted", messages = accepted });
        }
        catch (JsonException)
        {
            return Results.BadRequest(new { message = "X webhook payload was not valid JSON." });
        }
        catch (ArgumentOutOfRangeException)
        {
            return Results.BadRequest(new { message = "X webhook timestamp was invalid." });
        }
    }

    private static string? SigningSecret(IReadOnlyDictionary<string, string> settings)
    {
        if (settings.TryGetValue("clientSecret", out var clientSecret) && !string.IsNullOrWhiteSpace(clientSecret)) return clientSecret;
        return settings.TryGetValue("consumerSecret", out var consumerSecret) && !string.IsNullOrWhiteSpace(consumerSecret)
            ? consumerSecret : null;
    }

    private static bool VerifySignature(string secret, string supplied, byte[] body)
    {
        if (secret.Length > 512 || !supplied.StartsWith("sha256=", StringComparison.Ordinal)) return false;
        try
        {
            var expected = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), body);
            var actual = Convert.FromBase64String(supplied[7..]);
            return expected.Length == actual.Length && CryptographicOperations.FixedTimeEquals(expected, actual);
        }
        catch (FormatException) { return false; }
    }

    private static bool TryProperty(JsonElement element, string name, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out value)) return true;
        value = default;
        return false;
    }

    private static bool TryString(JsonElement element, string name, out string value)
    {
        value = string.Empty;
        if (!TryProperty(element, name, out var field) || field.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(field.GetString())) return false;
        value = field.GetString()!;
        return value.Length <= 4096;
    }

    private static bool IsOptOut(string text) => text.Trim().Equals("stop", StringComparison.OrdinalIgnoreCase)
        || text.Trim().Equals("unsubscribe", StringComparison.OrdinalIgnoreCase);

    private static string BuildThreadId(string accountId, string senderId) =>
        "x_" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"x:{accountId}:{senderId}"))).ToLowerInvariant();

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
}
