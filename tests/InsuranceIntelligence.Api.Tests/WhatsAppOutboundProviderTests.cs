using System.Net;
using System.Text;
using InsuranceIntelligence.Domain.Integrations;
using InsuranceIntelligence.Infrastructure.WhatsApp;

namespace InsuranceIntelligence.Api.Tests;

public sealed class WhatsAppOutboundProviderTests
{
    [Fact]
    public async Task SendAsync_Text_UsesConfiguredPhoneNumberAndReturnsProviderReference()
    {
        var handler = new CapturingHandler(HttpStatusCode.OK, """{"messages":[{"id":"wamid.outbound"}]}""");
        var provider = new WhatsAppCloudApiOutboundProvider(new StaticWhatsAppRuntimeSettingsProvider(ConfiguredSettings()), new HttpClient(handler));

        var result = await provider.SendAsync(new WhatsAppProviderSendRequest(
            "phone-1", "256772118001", "Your claim is under review.", null, "en", null, "send-1"));

        Assert.True(result.Accepted);
        Assert.Equal("wamid.outbound", result.ProviderMessageId);
        Assert.EndsWith("/phone-1/messages", handler.Uri?.AbsoluteUri);
        Assert.Contains("\"type\":\"text\"", handler.Body);
        Assert.Contains("Bearer", handler.Authorization);
    }

    [Fact]
    public async Task SendAsync_Template_UsesNamedParametersAndDoesNotClaimDelivery()
    {
        var handler = new CapturingHandler(HttpStatusCode.OK, """{"messages":[{"id":"wamid.template"}]}""");
        var provider = new WhatsAppCloudApiOutboundProvider(new StaticWhatsAppRuntimeSettingsProvider(ConfiguredSettings()), new HttpClient(handler));

        var result = await provider.SendAsync(new WhatsAppProviderSendRequest(
            "phone-1", "256772118001", "Rendered customer copy", "claim_update", "en_US", null, "send-2",
            new Dictionary<string, string> { ["claim_reference"] = "CLM-1" }));

        Assert.True(result.Accepted);
        Assert.Contains("\"type\":\"template\"", handler.Body);
        Assert.Contains("\"parameter_name\":\"claim_reference\"", handler.Body);
    }

    [Fact]
    public async Task SendAsync_ProviderFailure_ReturnsSafeFailureAndCode()
    {
        var handler = new CapturingHandler(HttpStatusCode.BadRequest, """{"error":{"code":131047,"message":"provider detail"}}""");
        var provider = new WhatsAppCloudApiOutboundProvider(new StaticWhatsAppRuntimeSettingsProvider(ConfiguredSettings()), new HttpClient(handler));

        var result = await provider.SendAsync(new WhatsAppProviderSendRequest(
            "phone-1", "256772118001", "Customer copy", null, "en", null, "send-3"));

        Assert.False(result.Accepted);
        Assert.Equal("131047", result.ProviderErrorCode);
        Assert.DoesNotContain("provider detail", result.SafeError);
    }

    private static WhatsAppEffectiveSettings ConfiguredSettings() => new(
        PhoneNumberId: "phone-1",
        AccessToken: "token",
        AppSecret: "secret",
        VerifyToken: "verify",
        GraphBaseUrl: "https://graph.facebook.test/v24.0",
        RequestTimeoutSeconds: 20,
        TenantId: "tenant-a",
        WorkspaceId: "workspace-a",
        IntegrationUserId: "whatsapp-integration",
        Source: "EnvironmentConfigured");

    private sealed class CapturingHandler(HttpStatusCode status, string responseBody) : HttpMessageHandler
    {
        public Uri? Uri { get; private set; }
        public string Body { get; private set; } = string.Empty;
        public string Authorization { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Uri = request.RequestUri;
            Body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            Authorization = request.Headers.Authorization?.Scheme ?? string.Empty;
            return new HttpResponseMessage(status) { Content = new StringContent(responseBody, Encoding.UTF8, "application/json") };
        }
    }
}
