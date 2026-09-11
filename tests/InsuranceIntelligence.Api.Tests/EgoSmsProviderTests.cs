using System.Net;
using System.Text;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Communications;
using InsuranceIntelligence.Domain.Workflow;
using InsuranceIntelligence.Domain.Integrations;
using InsuranceIntelligence.Infrastructure.Communications;
using Microsoft.Extensions.Logging.Abstractions;

namespace InsuranceIntelligence.Api.Tests;

public sealed class EgoSmsProviderTests
{
    private const string SuccessBody = "{\"Status\":\"OK\",\"Message\":\"Successfully Sent!\",\"Cost\":\"35\",\"MsgFollowUpUniqueCode\":\"ApiMSG.123456789\"}";
    private const string FailedBody = "{\"Status\":\"Failed\",\"Message\":\"Insufficient balance\"}";

    [Fact]
    public async Task SendAsync_WhenNotApproved_BlocksForHumanApproval()
    {
        var handler = new CapturingHandler(SuccessBody);
        var provider = Provider(handler, Configured());

        var result = await provider.SendAsync(Request(approved: false));

        Assert.Equal("HumanApprovalRequired", result.DeliveryStatus);
        Assert.Null(handler.LastRequestUri);
    }

    [Fact]
    public async Task SendAsync_WhenNotConfigured_ReturnsConfigurationRequired()
    {
        var handler = new CapturingHandler(SuccessBody);
        var provider = Provider(handler, new EgoSmsSettings("", "", null, EgoSmsSettings.LiveBaseUrl, 15));

        var result = await provider.SendAsync(Request(approved: true));

        Assert.Equal("ConfigurationRequired", result.DeliveryStatus);
        Assert.Null(handler.LastRequestUri);
    }

    [Fact]
    public async Task SendAsync_WhenRecipientInvalid_ReturnsInvalidRecipient()
    {
        var handler = new CapturingHandler(SuccessBody);
        var provider = Provider(handler, Configured());

        var result = await provider.SendAsync(Request(approved: true, recipient: "12345"));

        Assert.Equal("InvalidRecipient", result.DeliveryStatus);
        Assert.Null(handler.LastRequestUri);
    }

    [Fact]
    public async Task SendAsync_WhenApprovedAndConfigured_DispatchesWithoutLeadingPlus()
    {
        var handler = new CapturingHandler(SuccessBody);
        var provider = Provider(handler, Configured());

        var result = await provider.SendAsync(Request(approved: true, recipient: "0772 118 001"));

        Assert.Equal("Sent", result.DeliveryStatus);
        Assert.Equal("ApiMSG.123456789", result.ProviderMessageId);
        Assert.Null(result.SafeError);
        Assert.NotNull(handler.LastBody);
        // EgoSMS documents numbers without '+' (e.g. "256700111222"), unlike Africa's Talking's E.164 "+256...".
        Assert.Contains("\"number\":\"256772118001\"", handler.LastBody, StringComparison.Ordinal);
        Assert.DoesNotContain("\"number\":\"+256772118001\"", handler.LastBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendAsync_WhenGatewayReportsFailedStatus_ReturnsProviderErrorWithoutThrowing()
    {
        var handler = new CapturingHandler(FailedBody);
        var provider = Provider(handler, Configured());

        var result = await provider.SendAsync(Request(approved: true));

        Assert.Equal("ProviderError", result.DeliveryStatus);
        Assert.Equal(string.Empty, result.ProviderMessageId);
        Assert.NotNull(result.SafeError);
    }

    [Fact]
    public async Task SendAsync_WhenGatewayUnreachable_ReturnsProviderErrorWithoutThrowing()
    {
        var handler = new CapturingHandler(SuccessBody, statusCode: HttpStatusCode.InternalServerError);
        var provider = Provider(handler, Configured());

        var result = await provider.SendAsync(Request(approved: true));

        Assert.Equal("ProviderError", result.DeliveryStatus);
        Assert.NotNull(result.SafeError);
    }

    [Theory]
    [InlineData("IIP", true)]
    [InlineData("TwelveChars1", false)] // 12 characters — exceeds the 11-character limit
    public void TryParse_EnforcesSenderIdLengthLimit(string senderId, bool expectedSuccess)
    {
        var settings = new Dictionary<string, string> { ["username"] = "u", ["apiKey"] = "k", ["senderId"] = senderId };

        var success = EgoSmsSettings.TryParse(settings, out _, out var error);

        Assert.Equal(expectedSuccess, success);
        if (!expectedSuccess) Assert.Contains("11 characters", error);
    }

    [Fact]
    public void TryParse_SandboxFlag_SelectsSandboxBaseUrl()
    {
        var settings = new Dictionary<string, string> { ["username"] = "u", ["apiKey"] = "k", ["sandbox"] = "true" };

        Assert.True(EgoSmsSettings.TryParse(settings, out var parsed, out _));
        Assert.Equal(EgoSmsSettings.SandboxBaseUrl, parsed!.BaseUrl);
    }

    private static EgoSmsSettings Configured() =>
        new("sandbox-user", "test-key", "IIP", EgoSmsSettings.LiveBaseUrl, 15);

    private static EgoSmsProvider Provider(CapturingHandler handler, EgoSmsSettings settings, bool autoSendTransactional = false) =>
        new(settings, autoSendTransactional, NullLogger<EgoSmsProvider>.Instance, new HttpClient(handler));

    private static CommunicationSendRequest Request(bool approved, string recipient = "+256772118001", bool lowRisk = false) =>
        new(
            TenantScope.Create("tenant-demo", "workspace-claims"),
            Guid.NewGuid(),
            CommunicationChannel.SMS,
            recipient,
            "Your claim CLM-4471 has been reviewed and approved by our claims officer.",
            "officer-1",
            approved,
            LowRisk: lowRisk,
            "corr-1");

    private sealed class CapturingHandler(string body, HttpStatusCode statusCode = HttpStatusCode.OK) : HttpMessageHandler
    {
        public Uri? LastRequestUri { get; private set; }
        public string LastBody { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri;
            LastBody = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(statusCode) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }
}
