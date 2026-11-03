using System.Net;
using InsuranceIntelligence.Domain.Channels;
using InsuranceIntelligence.Infrastructure.Connections;

namespace InsuranceIntelligence.Api.Tests;

public sealed class XDirectMessageSenderTests
{
    [Fact]
    public async Task SendAsync_UsesV2ConversationEndpointAndUserBearerToken()
    {
        using var handler = new CaptureHandler();
        var sender = new XDirectMessageSender(new SingleClientFactory(new HttpClient(handler)));

        var result = await sender.SendAsync("user-token", "123 456", "Claim update");

        Assert.True(result.Accepted);
        Assert.Equal("message-1", result.ProviderMessageId);
        Assert.Equal(HttpMethod.Post, handler.Method);
        Assert.Equal("https://api.x.com/2/dm_conversations/with/123%20456/messages", handler.Uri!.AbsoluteUri);
        Assert.Equal("Bearer user-token", handler.Authorization);
        Assert.Equal("{\"text\":\"Claim update\"}", handler.Body);
    }

    private sealed class SingleClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class CaptureHandler : HttpMessageHandler
    {
        public HttpMethod? Method { get; private set; }
        public Uri? Uri { get; private set; }
        public string? Authorization { get; private set; }
        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Method = request.Method;
            Uri = request.RequestUri;
            Authorization = request.Headers.Authorization?.ToString();
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"data\":{\"id\":\"message-1\",\"text\":\"Claim update\"}}"),
            };
        }
    }
}
