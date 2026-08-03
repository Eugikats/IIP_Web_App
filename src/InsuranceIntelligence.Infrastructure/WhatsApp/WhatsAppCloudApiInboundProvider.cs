using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using InsuranceIntelligence.Domain.Integrations;
using InsuranceIntelligence.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;

namespace InsuranceIntelligence.Infrastructure.WhatsApp;

/// <summary>
/// Meta WhatsApp Cloud API inbound adapter. Config-gated: without PhoneNumberId + AccessToken +
/// AppSecret + VerifyToken the provider reports <c>IsConfigured = false</c> and never fabricates
/// messages or media. Media download is a two-step Graph call (metadata URL → binary).
/// </summary>
public sealed class WhatsAppCloudApiInboundProvider(
    InfrastructureOptions options,
    ILogger<WhatsAppCloudApiInboundProvider> logger,
    HttpClient? httpClient = null) : IWhatsAppInboundProvider
{
    private readonly HttpClient httpClient = httpClient ?? new HttpClient();

    public bool IsConfigured
    {
        get
        {
            var settings = options.WhatsApp;
            return !string.IsNullOrWhiteSpace(settings.PhoneNumberId)
                && !string.IsNullOrWhiteSpace(settings.AccessToken)
                && !string.IsNullOrWhiteSpace(settings.AppSecret)
                && !string.IsNullOrWhiteSpace(settings.VerifyToken);
        }
    }

    public bool TryVerifySubscription(string mode, string verifyToken, string challenge, out string? challengeResponse)
    {
        challengeResponse = null;
        if (!IsConfigured)
        {
            return false;
        }

        if (!string.Equals(mode, "subscribe", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.Equals(verifyToken, options.WhatsApp.VerifyToken, StringComparison.Ordinal))
        {
            return false;
        }

        challengeResponse = challenge;
        return !string.IsNullOrWhiteSpace(challenge);
    }

    public bool IsValidSignature(ReadOnlySpan<byte> payload, string? signatureHeader)
    {
        if (!IsConfigured || string.IsNullOrWhiteSpace(signatureHeader))
        {
            return false;
        }

        const string prefix = "sha256=";
        if (!signatureHeader.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var providedHex = signatureHeader[prefix.Length..].Trim();
        if (providedHex.Length != 64)
        {
            return false;
        }

        var key = Encoding.UTF8.GetBytes(options.WhatsApp.AppSecret!);
        var computed = HMACSHA256.HashData(key, payload);
        var computedHex = Convert.ToHexString(computed);

        return CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(computedHex.ToLowerInvariant()),
            Encoding.ASCII.GetBytes(providedHex.ToLowerInvariant()));
    }

    public IReadOnlyList<WhatsAppInboundMediaMessage> ParseInboundMediaMessages(string jsonPayload) =>
        WhatsAppWebhookParser.ParseMediaMessages(jsonPayload);

    public async Task<WhatsAppMediaContent> DownloadMediaAsync(
        string mediaId,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException("WhatsApp provider is not configured.");
        }

        if (string.IsNullOrWhiteSpace(mediaId))
        {
            throw new InvalidOperationException("WhatsApp media id is required.");
        }

        var settings = options.WhatsApp;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(settings.RequestTimeoutSeconds, 5, 60)));

        using var metaRequest = new HttpRequestMessage(
            HttpMethod.Get,
            $"{settings.GraphBaseUrl.TrimEnd('/')}/{Uri.EscapeDataString(mediaId)}");
        metaRequest.Headers.TryAddWithoutValidation("Authorization", $"Bearer {settings.AccessToken}");

        using var metaResponse = await httpClient.SendAsync(metaRequest, timeout.Token);
        metaResponse.EnsureSuccessStatusCode();
        var meta = await metaResponse.Content.ReadFromJsonAsync<MediaMetadata>(cancellationToken: timeout.Token)
            ?? throw new InvalidOperationException("WhatsApp media metadata response was empty.");

        if (string.IsNullOrWhiteSpace(meta.Url))
        {
            throw new InvalidOperationException("WhatsApp media metadata did not include a download URL.");
        }

        using var downloadRequest = new HttpRequestMessage(HttpMethod.Get, meta.Url);
        downloadRequest.Headers.TryAddWithoutValidation("Authorization", $"Bearer {settings.AccessToken}");
        using var downloadResponse = await httpClient.SendAsync(downloadRequest, timeout.Token);
        downloadResponse.EnsureSuccessStatusCode();
        var bytes = await downloadResponse.Content.ReadAsByteArrayAsync(timeout.Token);
        if (bytes.Length == 0)
        {
            throw new InvalidOperationException("WhatsApp media download returned empty content.");
        }

        var contentType = string.IsNullOrWhiteSpace(meta.MimeType)
            ? downloadResponse.Content.Headers.ContentType?.MediaType ?? "application/octet-stream"
            : meta.MimeType!;
        var fileName = BuildFileName(mediaId, contentType);

        logger.LogInformation(
            "WhatsApp media downloaded. MediaId={MediaId} Bytes={Bytes} ContentType={ContentType}",
            mediaId,
            bytes.Length,
            contentType);

        return new WhatsAppMediaContent(mediaId, contentType, fileName, bytes);
    }

    private static string BuildFileName(string mediaId, string contentType)
    {
        var extension = contentType.ToLowerInvariant() switch
        {
            "image/jpeg" => ".jpg",
            "image/png" => ".png",
            "image/webp" => ".webp",
            "application/pdf" => ".pdf",
            "image/tiff" => ".tiff",
            _ => ".bin",
        };
        var safeId = new string(mediaId.Where(char.IsLetterOrDigit).Take(24).ToArray());
        return string.IsNullOrWhiteSpace(safeId) ? $"whatsapp-media{extension}" : $"whatsapp-{safeId}{extension}";
    }

    private sealed record MediaMetadata(
        [property: System.Text.Json.Serialization.JsonPropertyName("url")] string? Url,
        [property: System.Text.Json.Serialization.JsonPropertyName("mime_type")] string? MimeType,
        [property: System.Text.Json.Serialization.JsonPropertyName("sha256")] string? Sha256,
        [property: System.Text.Json.Serialization.JsonPropertyName("file_size")] long? FileSize,
        [property: System.Text.Json.Serialization.JsonPropertyName("id")] string? Id);
}

/// <summary>Honest not-configured fallback — never fabricates inbound WhatsApp traffic.</summary>
public sealed class DisabledWhatsAppInboundProvider : IWhatsAppInboundProvider
{
    public bool IsConfigured => false;

    public bool TryVerifySubscription(string mode, string verifyToken, string challenge, out string? challengeResponse)
    {
        challengeResponse = null;
        return false;
    }

    public bool IsValidSignature(ReadOnlySpan<byte> payload, string? signatureHeader) => false;

    public IReadOnlyList<WhatsAppInboundMediaMessage> ParseInboundMediaMessages(string jsonPayload) =>
        Array.Empty<WhatsAppInboundMediaMessage>();

    public Task<WhatsAppMediaContent> DownloadMediaAsync(string mediaId, CancellationToken cancellationToken = default) =>
        Task.FromException<WhatsAppMediaContent>(
            new InvalidOperationException("WhatsApp provider is not configured."));
}

/// <summary>Parses Meta Cloud API webhook JSON into provider-neutral media messages.</summary>
public static class WhatsAppWebhookParser
{
    public static IReadOnlyList<WhatsAppInboundMediaMessage> ParseMediaMessages(string jsonPayload)
    {
        if (string.IsNullOrWhiteSpace(jsonPayload))
        {
            return Array.Empty<WhatsAppInboundMediaMessage>();
        }

        using var document = JsonDocument.Parse(jsonPayload);
        if (!document.RootElement.TryGetProperty("entry", out var entries)
            || entries.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<WhatsAppInboundMediaMessage>();
        }

        var results = new List<WhatsAppInboundMediaMessage>();
        foreach (var entry in entries.EnumerateArray())
        {
            if (!entry.TryGetProperty("changes", out var changes) || changes.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var change in changes.EnumerateArray())
            {
                if (!change.TryGetProperty("value", out var value))
                {
                    continue;
                }

                var phoneNumberId = value.TryGetProperty("metadata", out var metadata)
                    && metadata.TryGetProperty("phone_number_id", out var phoneIdElement)
                    ? phoneIdElement.GetString()
                    : null;
                if (string.IsNullOrWhiteSpace(phoneNumberId)
                    || !value.TryGetProperty("messages", out var messages)
                    || messages.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var message in messages.EnumerateArray())
                {
                    var media = TryReadMedia(message);
                    if (media is null)
                    {
                        continue;
                    }

                    var messageId = message.TryGetProperty("id", out var idElement) ? idElement.GetString() : null;
                    var from = message.TryGetProperty("from", out var fromElement) ? fromElement.GetString() : null;
                    var timestamp = message.TryGetProperty("timestamp", out var tsElement)
                        && long.TryParse(tsElement.GetString(), out var unix)
                            ? DateTimeOffset.FromUnixTimeSeconds(unix)
                            : DateTimeOffset.UtcNow;

                    if (string.IsNullOrWhiteSpace(messageId) || string.IsNullOrWhiteSpace(from))
                    {
                        continue;
                    }

                    results.Add(new WhatsAppInboundMediaMessage(
                        messageId!,
                        phoneNumberId!,
                        from!,
                        media.Value.MediaId,
                        media.Value.MediaType,
                        media.Value.MimeType,
                        media.Value.FileName,
                        media.Value.Caption,
                        timestamp));
                }
            }
        }

        return results;
    }

    private static (string MediaId, string MediaType, string? MimeType, string? FileName, string? Caption)? TryReadMedia(
        JsonElement message)
    {
        if (!message.TryGetProperty("type", out var typeElement))
        {
            return null;
        }

        var type = typeElement.GetString();
        if (string.IsNullOrWhiteSpace(type))
        {
            return null;
        }

        if (!message.TryGetProperty(type, out var mediaElement) || mediaElement.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (type is not ("image" or "document"))
        {
            return null;
        }

        var mediaId = mediaElement.TryGetProperty("id", out var idElement) ? idElement.GetString() : null;
        if (string.IsNullOrWhiteSpace(mediaId))
        {
            return null;
        }

        var mimeType = mediaElement.TryGetProperty("mime_type", out var mimeElement) ? mimeElement.GetString() : null;
        var caption = mediaElement.TryGetProperty("caption", out var captionElement) ? captionElement.GetString() : null;
        var fileName = mediaElement.TryGetProperty("filename", out var fileNameElement) ? fileNameElement.GetString() : null;
        return (mediaId!, type, mimeType, fileName, caption);
    }
}
