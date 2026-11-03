using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using InsuranceIntelligence.Domain.Channels;

namespace InsuranceIntelligence.Infrastructure.Connections;

/// <summary>Text replies for approved TikTok Business Messaging accounts.</summary>
public sealed class TikTokTextSender(IHttpClientFactory clients) : ITikTokTextSender
{
    public async Task<ChannelProviderSendResult> SendAsync(string businessId, string accessToken, string conversationId, string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(businessId) || string.IsNullOrWhiteSpace(accessToken)
            || string.IsNullOrWhiteSpace(conversationId) || string.IsNullOrWhiteSpace(text)
            || businessId.Length > 256 || accessToken.Length > 8192 || conversationId.Length > 512 || text.Length > 1000)
            return new(false, null, "Message details are invalid for TikTok.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(12));
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://business-api.tiktok.com/open_api/v1.3/business/message/send/");
        request.Headers.TryAddWithoutValidation("Access-Token", accessToken);
        request.Content = System.Net.Http.Json.JsonContent.Create(new
        {
            business_id = businessId,
            recipient_type = "CONVERSATION",
            recipient = conversationId,
            message_type = "TEXT",
            text = new { body = text }
        });
        try
        {
            using var response = await clients.CreateClient().SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            if ((int)response.StatusCode >= 500) return new(false, null, "TikTok did not confirm the send. Check the conversation before retrying.", true);
            var body = await response.Content.ReadAsStringAsync(timeout.Token);
            if (body.Length > 16384) return new(false, null, "TikTok returned an unexpected response.");
            using var json = JsonDocument.Parse(body);
            var root = json.RootElement;
            if (!response.IsSuccessStatusCode || !root.TryGetProperty("code", out var code) || code.GetInt32() != 0
                || !root.TryGetProperty("data", out var data) || !data.TryGetProperty("message", out var message)
                || !message.TryGetProperty("message_id", out var messageId) || messageId.ValueKind != JsonValueKind.String
                || string.IsNullOrWhiteSpace(messageId.GetString()))
                return new(false, null, "TikTok did not accept the message. Check Business Messaging access and conversation eligibility.");
            return new(true, messageId.ToString(), null);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { return new(false, null, "TikTok did not confirm the send. Check the conversation before retrying.", true); }
        catch (HttpRequestException) { return new(false, null, "TikTok did not confirm the send. Check the conversation before retrying.", true); }
        catch (JsonException) { return new(false, null, "TikTok returned an unexpected response.", true); }
    }
}

/// <summary>OAuth 2.0 user-context sender for X Direct Messages API v2.</summary>
public sealed class XDirectMessageSender(IHttpClientFactory clients) : IXDirectMessageSender
{
    private const string Endpoint = "https://api.x.com/2/dm_conversations/with/";

    public async Task<ChannelProviderSendResult> SendAsync(string accessToken, string recipientId, string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(accessToken)
            || string.IsNullOrWhiteSpace(recipientId) || string.IsNullOrWhiteSpace(text)
            || accessToken.Length > 8192 || recipientId.Length > 256 || text.Length > 1000 || accessToken.Any(char.IsControl))
            return new(false, null, "Message details are invalid for X.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(12));
        using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint + Uri.EscapeDataString(recipientId) + "/messages")
        {
            Content = System.Net.Http.Json.JsonContent.Create(new { text })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken.Trim());
        try
        {
            using var response = await clients.CreateClient().SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            var body = await response.Content.ReadAsStringAsync(timeout.Token);
            if (body.Length > 16384) return new(false, null, "X returned an unexpected response.");
            using var json = JsonDocument.Parse(body);
            if (!response.IsSuccessStatusCode) return new(false, null, "X did not accept the message. Check user authorization and Direct Message API access.", (int)response.StatusCode >= 500);
            var id = json.RootElement.TryGetProperty("data", out var data) && data.TryGetProperty("id", out var eventId) ? eventId.ToString() : null;
            if (string.IsNullOrWhiteSpace(id)) return new(false, null, "X returned an unexpected message response.", true);
            return new(true, id, null);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { return new(false, null, "X did not confirm the send. Check the conversation before retrying.", true); }
        catch (HttpRequestException) { return new(false, null, "X did not confirm the send. Check the conversation before retrying.", true); }
        catch (JsonException) { return new(false, null, "X returned an unexpected response.", true); }
    }

}
