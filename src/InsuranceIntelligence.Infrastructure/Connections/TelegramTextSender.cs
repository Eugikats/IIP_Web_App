using System.Net.Http.Json;
using System.Text.Json;
using InsuranceIntelligence.Domain.Channels;

namespace InsuranceIntelligence.Infrastructure.Connections;

/// <summary>Calls Telegram with redirects disabled because bot tokens are part of the request URL.</summary>
public sealed class TelegramTextSender : ITelegramTextSender
{
    private static readonly HttpClient Client = new(new SocketsHttpHandler { AllowAutoRedirect = false });

    public async Task<ChannelProviderSendResult> SendAsync(string botToken, string chatId, string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(botToken) || botToken.Length > 256 || string.IsNullOrWhiteSpace(chatId)
            || chatId.Length > 40 || string.IsNullOrWhiteSpace(text) || text.Length > 4096)
            return new ChannelProviderSendResult(false, null, "Telegram send details are invalid.");

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(12));
        using var request = new HttpRequestMessage(HttpMethod.Post, $"https://api.telegram.org/bot{botToken}/sendMessage")
        {
            Content = JsonContent.Create(new { chat_id = chatId, text }),
        };
        try
        {
            using var response = await Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            if ((int)response.StatusCode >= 500)
                return new ChannelProviderSendResult(false, null, "Telegram did not confirm the send. Check the conversation before retrying.", true);
            if (response.Content.Headers.ContentLength is > 16384)
                return new ChannelProviderSendResult(false, null, "Telegram returned an unexpected response.");
            var body = await response.Content.ReadAsStringAsync(timeout.Token);
            if (body.Length > 16384)
                return new ChannelProviderSendResult(false, null, "Telegram returned an unexpected response.");
            using var json = JsonDocument.Parse(body);
            var root = json.RootElement;
            if (!response.IsSuccessStatusCode || root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("ok", out var ok) || ok.ValueKind != JsonValueKind.True)
                return new ChannelProviderSendResult(false, null, "Telegram did not accept the message.");
            var messageId = root.TryGetProperty("result", out var result) && result.ValueKind == JsonValueKind.Object
                && result.TryGetProperty("message_id", out var id) && id.TryGetInt64(out var value)
                ? $"message:{value.ToString(System.Globalization.CultureInfo.InvariantCulture)}" : null;
            if (messageId is null)
                return new ChannelProviderSendResult(false, null, "Telegram did not confirm the message ID. Check the conversation before retrying.", true);
            return new ChannelProviderSendResult(true, messageId, null);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new ChannelProviderSendResult(false, null, "Telegram did not confirm the send. Check the conversation before retrying.", true);
        }
        catch (HttpRequestException)
        {
            return new ChannelProviderSendResult(false, null, "Telegram did not confirm the send. Check the conversation before retrying.", true);
        }
        catch (JsonException)
        {
            return new ChannelProviderSendResult(false, null, "Telegram returned an unexpected response.", true);
        }
    }
}
