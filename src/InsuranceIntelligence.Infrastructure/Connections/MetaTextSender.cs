using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using InsuranceIntelligence.Domain.Channels;

namespace InsuranceIntelligence.Infrastructure.Connections;

/// <summary>Uses Meta's official Graph Send API for Messenger and Instagram conversations.</summary>
public sealed class MetaTextSender(IHttpClientFactory clients) : IMetaTextSender
{
    private const string GraphVersion = "v26.0";

    public async Task<ChannelProviderSendResult> SendAsync(
        string accountId,
        string pageAccessToken,
        string recipientId,
        string text,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(accountId) || accountId.Length > 256
            || string.IsNullOrWhiteSpace(pageAccessToken) || pageAccessToken.Length > 8192
            || string.IsNullOrWhiteSpace(recipientId) || recipientId.Length > 256
            || string.IsNullOrWhiteSpace(text) || text.Length > 1000)
            return new ChannelProviderSendResult(false, null, "Message details are invalid for this Meta channel.");

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(12));
        using var request = new HttpRequestMessage(HttpMethod.Post,
            $"https://graph.facebook.com/{GraphVersion}/{Uri.EscapeDataString(accountId)}/messages")
        {
            Content = JsonContent.Create(new { recipient = new { id = recipientId }, message = new { text } }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", pageAccessToken);
        try
        {
            using var response = await clients.CreateClient().SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            if ((int)response.StatusCode >= 500)
                return new ChannelProviderSendResult(false, null, "Meta did not confirm the send. Check the conversation before retrying.", true);
            if (response.Content.Headers.ContentLength is > 16384)
                return new ChannelProviderSendResult(false, null, "Meta returned an unexpected response.");
            var body = await response.Content.ReadAsStringAsync(timeout.Token);
            if (body.Length > 16384) return new ChannelProviderSendResult(false, null, "Meta returned an unexpected response.");
            using var json = JsonDocument.Parse(body);
            var root = json.RootElement;
            if (!response.IsSuccessStatusCode || root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("message_id", out var id) || id.ValueKind != JsonValueKind.String
                || string.IsNullOrWhiteSpace(id.GetString()))
                return new ChannelProviderSendResult(false, null, "Meta did not accept the message. Check account permissions and messaging eligibility.");
            return new ChannelProviderSendResult(true, id.GetString(), null);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new ChannelProviderSendResult(false, null, "Meta did not confirm the send. Check the conversation before retrying.", true);
        }
        catch (HttpRequestException)
        {
            return new ChannelProviderSendResult(false, null, "Meta did not confirm the send. Check the conversation before retrying.", true);
        }
        catch (JsonException)
        {
            return new ChannelProviderSendResult(false, null, "Meta returned an unexpected response.", true);
        }
    }
}
