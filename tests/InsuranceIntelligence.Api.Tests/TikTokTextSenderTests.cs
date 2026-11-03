using System.Net;
using System.Text.Json;
using InsuranceIntelligence.Infrastructure.Connections;

namespace InsuranceIntelligence.Api.Tests;

public sealed class TikTokTextSenderTests
{
    [Fact]
    public async Task SendAsync_UsesBusinessMessagingTextContract()
    {
        using var handler = new CaptureHandler();
        var sender = new TikTokTextSender(new SingleClientFactory(new HttpClient(handler)));

        var result = await sender.SendAsync("business-1", "token-1", "conversation-1", "Claim update");

        Assert.True(result.Accepted);
        Assert.Equal("message-1", result.ProviderMessageId);
        Assert.Equal("https://business-api.tiktok.com/open_api/v1.3/business/message/send/", handler.Uri!.AbsoluteUri);
        Assert.Equal("token-1", handler.AccessToken);
        using var payload = JsonDocument.Parse(handler.Body!);
        Assert.Equal("business-1", payload.RootElement.GetProperty("business_id").GetString());
        Assert.Equal("CONVERSATION", payload.RootElement.GetProperty("recipient_type").GetString());
        Assert.Equal("conversation-1", payload.RootElement.GetProperty("recipient").GetString());
        Assert.Equal("TEXT", payload.RootElement.GetProperty("message_type").GetString());
        Assert.Equal("Claim update", payload.RootElement.GetProperty("text").GetProperty("body").GetString());
    }

    private sealed class SingleClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class CaptureHandler : HttpMessageHandler
    {
        public Uri? Uri { get; private set; }
        public string? AccessToken { get; private set; }
        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Uri = request.RequestUri;
            AccessToken = request.Headers.GetValues("Access-Token").Single();
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"code\":0,\"data\":{\"message\":{\"message_id\":\"message-1\"}}}"),
            };
        }
    }
}
