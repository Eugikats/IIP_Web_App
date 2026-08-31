using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using InsuranceIntelligence.Domain.Integrations;
using InsuranceIntelligence.Infrastructure.Configuration;

namespace InsuranceIntelligence.Infrastructure.WhatsApp;

public sealed class WhatsAppCloudApiOutboundProvider(
    InfrastructureOptions options,
    HttpClient httpClient) : IWhatsAppOutboundProvider
{
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(options.WhatsApp.PhoneNumberId)
        && !string.IsNullOrWhiteSpace(options.WhatsApp.AccessToken);

    public async Task<WhatsAppProviderSendResult> SendAsync(
        WhatsAppProviderSendRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured) return new(false, null, null, "WhatsApp outbound is not configured.");
        if (!string.Equals(request.BusinessPhoneNumberId, options.WhatsApp.PhoneNumberId, StringComparison.Ordinal))
            return new(false, null, null, "The WhatsApp sender is not configured for this tenant workspace.");

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(options.WhatsApp.RequestTimeoutSeconds, 5, 60)));
        using var message = new HttpRequestMessage(
            HttpMethod.Post,
            $"{options.WhatsApp.GraphBaseUrl.TrimEnd('/')}/{Uri.EscapeDataString(request.BusinessPhoneNumberId)}/messages");
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.WhatsApp.AccessToken);
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
