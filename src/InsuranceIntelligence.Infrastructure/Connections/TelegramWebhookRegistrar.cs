using System.Net.Http.Json;
using System.Text.Json;
using InsuranceIntelligence.Domain.Integrations;

namespace InsuranceIntelligence.Infrastructure.Connections;

/// <summary>Registers a Telegram webhook without logging the bot token embedded in Telegram's URL.</summary>
public sealed class TelegramWebhookRegistrar : ITelegramWebhookRegistrar
{
    private static readonly HttpClient Client = new(new SocketsHttpHandler { AllowAutoRedirect = false });

    public async Task<TelegramWebhookRegistrationResult> RegisterAsync(
        string botToken,
        string callbackUrl,
        string webhookSecret,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(botToken) || string.IsNullOrWhiteSpace(callbackUrl)
            || string.IsNullOrWhiteSpace(webhookSecret))
            return new TelegramWebhookRegistrationResult(false, "Telegram webhook registration is missing required configuration.");

        var endpoint = $"https://api.telegram.org/bot{botToken}/setWebhook";
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(12));
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = JsonContent.Create(new
            {
                url = callbackUrl,
                secret_token = webhookSecret,
                allowed_updates = new[] { "message" },
                drop_pending_updates = false,
            }),
        };

        try
        {
            using var response = await Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            if (!response.IsSuccessStatusCode || response.Content.Headers.ContentLength is > 16384)
                return new TelegramWebhookRegistrationResult(false, "Telegram could not register the webhook. Check the public API URL and bot permissions.");
            var payload = await response.Content.ReadAsStringAsync(timeout.Token);
            if (payload.Length > 16384)
                return new TelegramWebhookRegistrationResult(false, "Telegram returned an unexpected webhook response.");
            using var json = JsonDocument.Parse(payload);
            var root = json.RootElement;
            return root.ValueKind == JsonValueKind.Object
                && root.TryGetProperty("ok", out var ok)
                && ok.ValueKind == JsonValueKind.True
                    ? new TelegramWebhookRegistrationResult(true, null)
                    : new TelegramWebhookRegistrationResult(false, "Telegram rejected webhook registration. Check the public API URL and bot permissions.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new TelegramWebhookRegistrationResult(false, "Telegram webhook registration timed out.");
        }
        catch (HttpRequestException)
        {
            return new TelegramWebhookRegistrationResult(false, "Telegram could not be reached to register the webhook.");
        }
        catch (JsonException)
        {
            return new TelegramWebhookRegistrationResult(false, "Telegram returned an unexpected webhook response.");
        }
    }
}
