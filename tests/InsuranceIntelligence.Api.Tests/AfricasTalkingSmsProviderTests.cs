using System.Net;
using System.Text;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Communications;
using InsuranceIntelligence.Domain.Workflow;
using InsuranceIntelligence.Infrastructure.Communications;
using InsuranceIntelligence.Infrastructure.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace InsuranceIntelligence.Api.Tests;

public sealed class AfricasTalkingSmsProviderTests
{
    private const string SuccessBody =
        "{\"SMSMessageData\":{\"Message\":\"Sent to 1/1 Total Cost: UGX 25.0000\"," +
        "\"Recipients\":[{\"number\":\"+256772118001\",\"status\":\"Success\",\"messageId\":\"ATXid_abc123\",\"cost\":\"UGX 25.0000\"}]}}";

    [Fact]
    public async Task SendAsync_WhenNotApproved_BlocksForHumanApproval()
    {
        var handler = new CapturingHandler(SuccessBody);
        var provider = Provider(handler, Configured());

        var result = await provider.SendAsync(Request(approved: false));

        Assert.Equal("HumanApprovalRequired", result.DeliveryStatus);
        Assert.Null(handler.LastRequestUri); // never dispatched
    }

    [Fact]
    public async Task SendAsync_WhenLowRiskAndTenantAutoSendEnabled_DispatchesWithoutApproval()
    {
        var handler = new CapturingHandler(SuccessBody);
        var provider = Provider(handler, Configured(autoSendTransactional: true));

        var result = await provider.SendAsync(Request(approved: false, lowRisk: true));

        Assert.Equal("Success", result.DeliveryStatus);
        Assert.NotNull(handler.LastRequestUri);
    }

    [Fact]
    public async Task SendAsync_WhenLowRiskButTenantAutoSendDisabled_BlocksForHumanApproval()
    {
        var handler = new CapturingHandler(SuccessBody);
        var provider = Provider(handler, Configured(autoSendTransactional: false));

        var result = await provider.SendAsync(Request(approved: false, lowRisk: true));

        Assert.Equal("HumanApprovalRequired", result.DeliveryStatus);
        Assert.Null(handler.LastRequestUri);
    }

    [Fact]
    public async Task SendAsync_WhenConsequentialEvenWithAutoSendEnabled_BlocksForHumanApproval()
    {
        var handler = new CapturingHandler(SuccessBody);
        var provider = Provider(handler, Configured(autoSendTransactional: true));

        // Not low-risk == consequential (e.g. a claim-decision SMS): must always require approval.
        var result = await provider.SendAsync(Request(approved: false, lowRisk: false));

        Assert.Equal("HumanApprovalRequired", result.DeliveryStatus);
        Assert.Null(handler.LastRequestUri);
    }

    [Theory]
    [InlineData(true, false, false, true)]   // human-approved -> always send
    [InlineData(false, true, true, true)]    // low-risk + tenant opted in -> send
    [InlineData(false, true, false, false)]  // low-risk but tenant not opted in -> block
    [InlineData(false, false, true, false)]  // consequential + tenant opted in -> block
    [InlineData(false, false, false, false)] // consequential, no opt-in -> block
    public void Guard_TieredPolicy_MatchesMatrix(bool approved, bool lowRisk, bool autoSend, bool expected) =>
        Assert.Equal(
            expected,
            OutboundSmsGuard.IsDispatchPermitted(Request(approved, lowRisk: lowRisk), autoSend));

    [Fact]
    public async Task SendAsync_WhenNotConfigured_ReturnsConfigurationRequired()
    {
        var handler = new CapturingHandler(SuccessBody);
        var provider = Provider(handler, new InfrastructureOptions()); // AfricasTalking username/apikey empty

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
    public async Task SendAsync_WhenApprovedAndConfigured_DispatchesNormalizedRequest()
    {
        var handler = new CapturingHandler(SuccessBody);
        var provider = Provider(handler, Configured());

        var result = await provider.SendAsync(Request(approved: true, recipient: "0772 118 001"));

        Assert.Equal("Success", result.DeliveryStatus);
        Assert.Equal("ATXid_abc123", result.ProviderMessageId);
        Assert.Null(result.SafeError);
        Assert.NotNull(handler.LastRequestUri);
        Assert.EndsWith("/version1/messaging", handler.LastRequestUri!.AbsolutePath);
        Assert.Contains("to=%2B256772118001", handler.LastBody); // E.164 normalized + url-encoded
        Assert.Contains("apiKey", handler.LastHeaderKeys);
    }

    [Fact]
    public async Task SendAsync_WhenGatewayFails_ReturnsProviderErrorWithoutThrowing()
    {
        var handler = new CapturingHandler(SuccessBody, statusCode: HttpStatusCode.InternalServerError);
        var provider = Provider(handler, Configured());

        var result = await provider.SendAsync(Request(approved: true));

        Assert.Equal("ProviderError", result.DeliveryStatus);
        Assert.Equal(string.Empty, result.ProviderMessageId);
        Assert.NotNull(result.SafeError);
    }

    [Theory]
    [InlineData("0772118001", "+256772118001")]
    [InlineData("+256772118001", "+256772118001")]
    [InlineData("256772118001", "+256772118001")]
    [InlineData("772118001", "+256772118001")]
    [InlineData("+256 772 118 001", "+256772118001")]
    public void Normalize_ValidUgandaNumbers_ReturnsE164(string raw, string expected) =>
        Assert.Equal(expected, UgandaMsisdn.Normalize(raw));

    [Theory]
    [InlineData("")]
    [InlineData("12345")]
    [InlineData("0112118001")] // does not start with 7 after trunk
    [InlineData("+1202555017")]
    [InlineData(null)]
    public void Normalize_InvalidNumbers_ReturnsNull(string? raw) =>
        Assert.Null(UgandaMsisdn.Normalize(raw));

    private static InfrastructureOptions Configured(bool autoSendTransactional = false) =>
        new()
        {
            SmsProvider = "AfricasTalking",
            AfricasTalking = new AfricasTalkingOptions
            {
                Username = "sandbox",
                ApiKey = "test-key",
                SenderId = "IIP",
                BaseUrl = "https://api.sandbox.africastalking.com",
                AutoSendTransactionalSmsEnabled = autoSendTransactional,
            },
        };

    private static AfricasTalkingSmsProvider Provider(CapturingHandler handler, InfrastructureOptions options) =>
        new(options, NullLogger<AfricasTalkingSmsProvider>.Instance, new HttpClient(handler));

    private static CommunicationSendRequest Request(
        bool approved,
        string recipient = "+256772118001",
        bool lowRisk = false) =>
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

    private sealed class CapturingHandler(string body, HttpStatusCode statusCode = HttpStatusCode.OK)
        : HttpMessageHandler
    {
        public Uri? LastRequestUri { get; private set; }
        public string LastBody { get; private set; } = string.Empty;
        public string LastHeaderKeys { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri;
            LastHeaderKeys = string.Join(",", request.Headers.Select(header => header.Key));
            LastBody = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            };
        }
    }
}
