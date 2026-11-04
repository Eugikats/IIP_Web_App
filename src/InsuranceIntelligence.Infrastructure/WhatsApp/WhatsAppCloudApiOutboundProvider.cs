using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using InsuranceIntelligence.Domain.Integrations;

namespace InsuranceIntelligence.Infrastructure.WhatsApp;

public sealed class WhatsAppCloudApiOutboundProvider(
    IWhatsAppRuntimeSettingsProvider settingsProvider,
    HttpClient httpClient) : IWhatsAppOutboundProvider
{
    public bool IsConfigured => settingsProvider.HasAny;

    public async Task<WhatsAppProviderSendResult> SendAsync(
        WhatsAppProviderSendRequest request,
        CancellationToken cancellationToken = default)
    {
        // The credentials are those of whichever workspace owns the sending number, so one workspace's
        // token can never carry another workspace's message (build-loop item 27).
        if (!settingsProvider.HasAny) return new(false, null, null, "WhatsApp outbound is not configured.");
        var settings = settingsProvider.ForPhoneNumber(request.BusinessPhoneNumberId);
        if (settings is null)
            return new(false, null, null, "The WhatsApp sender is not configured for this tenant workspace.");

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(settings.RequestTimeoutSeconds, 5, 60)));
        using var message = new HttpRequestMessage(
            HttpMethod.Post,
            $"{settings.GraphBaseUrl.TrimEnd('/')}/{Uri.EscapeDataString(request.BusinessPhoneNumberId)}/messages");
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.AccessToken);
        message.Headers.TryAddWithoutValidation("Idempotency-Key", request.IdempotencyKey);
        message.Content = JsonContent.Create(BuildPayload(request));
        using var response = await httpClient.SendAsync(message, timeout.Token);
        var responseJson = await response.Content.ReadAsStringAsync(timeout.Token);
        if (!response.IsSuccessStatusCode)
            return new(false, null, ReadProviderErrorCode(responseJson), "WhatsApp did not accept the message. Review the provider status before retrying.");

        using var document = JsonDocument.Parse(responseJson);
        var providerMessageId = document.RootElement.TryGetProperty("messages", out var messages)
            && messages.ValueKind == JsonValueKind.Array
            && messages.GetArrayLength() > 0
            && messages[0].TryGetProperty("id", out var id)
                ? id.GetString()
                : null;
        return string.IsNullOrWhiteSpace(providerMessageId)
            ? new(false, null, null, "WhatsApp returned no message reference. Reconcile before retrying.")
            : new(true, providerMessageId, null, null);
    }

    public async Task<bool> ShowTypingAsync(
        string businessPhoneNumberId,
        string inboundProviderMessageId,
        CancellationToken cancellationToken = default)
    {
        var settings = settingsProvider.ForPhoneNumber(businessPhoneNumberId);
        if (settings is null || string.IsNullOrWhiteSpace(inboundProviderMessageId))
        {
            return false;
        }

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(10));
            using var message = new HttpRequestMessage(
                HttpMethod.Post,
                $"{settings.GraphBaseUrl.TrimEnd('/')}/{Uri.EscapeDataString(businessPhoneNumberId)}/messages");
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.AccessToken);
            message.Content = JsonContent.Create(BuildTypingPayload(inboundProviderMessageId));
            using var response = await httpClient.SendAsync(message, timeout.Token);
            return response.IsSuccessStatusCode;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            return false;
        }
    }

    /// <summary>Uploads the audio to Meta's media store, then sends it as an audio message. OGG with
    /// the Opus codec is what WhatsApp shows as a voice note.</summary>
    public async Task<WhatsAppProviderSendResult> SendVoiceNoteAsync(
        string businessPhoneNumberId,
        string recipientWaId,
        byte[] oggOpusAudio,
        string? replyToProviderMessageId,
        CancellationToken cancellationToken = default)
    {
        if (!settingsProvider.HasAny) return new(false, null, null, "WhatsApp outbound is not configured.");
        var settings = settingsProvider.ForPhoneNumber(businessPhoneNumberId);
        if (settings is null)
            return new(false, null, null, "The WhatsApp sender is not configured for this tenant workspace.");

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(settings.RequestTimeoutSeconds * 2, 20, 90)));
        var baseUrl = $"{settings.GraphBaseUrl.TrimEnd('/')}/{Uri.EscapeDataString(businessPhoneNumberId)}";

        using var form = new MultipartFormDataContent();
        form.Add(new StringContent("whatsapp"), "messaging_product");
        form.Add(new StringContent("audio/ogg"), "type");
        var file = new ByteArrayContent(oggOpusAudio);
        file.Headers.ContentType = new MediaTypeHeaderValue("audio/ogg");
        form.Add(file, "file", "reply.ogg");
        using var upload = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/media") { Content = form };
        upload.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.AccessToken);
        using var uploadResponse = await httpClient.SendAsync(upload, timeout.Token);
        var uploadJson = await uploadResponse.Content.ReadAsStringAsync(timeout.Token);
        if (!uploadResponse.IsSuccessStatusCode)
            return new(false, null, ReadProviderErrorCode(uploadJson), "WhatsApp did not accept the voice note.");

        string? mediaId;
        using (var uploaded = JsonDocument.Parse(uploadJson))
        {
            mediaId = uploaded.RootElement.TryGetProperty("id", out var idElement) ? idElement.GetString() : null;
        }

        if (string.IsNullOrWhiteSpace(mediaId))
            return new(false, null, null, "WhatsApp returned no media reference for the voice note.");

        using var message = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/messages");
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.AccessToken);
        message.Content = JsonContent.Create(BuildVoiceNotePayload(recipientWaId, mediaId, replyToProviderMessageId));
        using var response = await httpClient.SendAsync(message, timeout.Token);
        var responseJson = await response.Content.ReadAsStringAsync(timeout.Token);
        if (!response.IsSuccessStatusCode)
            return new(false, null, ReadProviderErrorCode(responseJson), "WhatsApp did not accept the voice note.");

        using var document = JsonDocument.Parse(responseJson);
        var providerMessageId = document.RootElement.TryGetProperty("messages", out var messages)
            && messages.ValueKind == JsonValueKind.Array
            && messages.GetArrayLength() > 0
            && messages[0].TryGetProperty("id", out var id)
                ? id.GetString()
                : null;
        return string.IsNullOrWhiteSpace(providerMessageId)
            ? new(false, null, null, "WhatsApp returned no message reference. Reconcile before retrying.")
            : new(true, providerMessageId, null, null);
    }

    public static object BuildVoiceNotePayload(string recipientWaId, string mediaId, string? replyToProviderMessageId) => new
    {
        messaging_product = "whatsapp",
        to = recipientWaId,
        type = "audio",
        context = string.IsNullOrWhiteSpace(replyToProviderMessageId) ? null : new { message_id = replyToProviderMessageId },
        audio = new { id = mediaId },
    };

    /// <summary>Meta's read receipt with a typing indicator attached: one call marks the customer's
    /// message read and shows "typing..." until the reply is delivered or 25 seconds pass.</summary>
    public static object BuildTypingPayload(string inboundProviderMessageId) => new
    {
        messaging_product = "whatsapp",
        status = "read",
        message_id = inboundProviderMessageId,
        typing_indicator = new { type = "text" },
    };

    private static object BuildPayload(WhatsAppProviderSendRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.TemplateName))
        {
            var parameters = request.TemplateParameters?.Select(pair => new
            {
                type = "text",
                parameter_name = pair.Key,
                text = pair.Value
            }).ToArray() ?? [];
            return new
            {
                messaging_product = "whatsapp",
                to = request.RecipientWaId,
                type = "template",
                template = new
                {
                    name = request.TemplateName,
                    language = new { code = request.Language },
                    components = parameters.Length == 0 ? [] : new[] { new { type = "body", parameters } }
                }
            };
        }

        return new
        {
            messaging_product = "whatsapp",
            to = request.RecipientWaId,
            type = "text",
            context = string.IsNullOrWhiteSpace(request.ReplyToProviderMessageId) ? null : new { message_id = request.ReplyToProviderMessageId },
            text = new { body = request.CustomerSafeBody, preview_url = false }
        };
    }

    private static string? ReadProviderErrorCode(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.TryGetProperty("error", out var error)
                && error.TryGetProperty("code", out var code) ? code.ToString() : null;
        }
        catch (JsonException) { return null; }
    }
}

public sealed class DisabledWhatsAppOutboundProvider : IWhatsAppOutboundProvider
{
    public bool IsConfigured => false;
    public Task<WhatsAppProviderSendResult> SendAsync(WhatsAppProviderSendRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(new WhatsAppProviderSendResult(false, null, null, "WhatsApp outbound is not configured."));
}
