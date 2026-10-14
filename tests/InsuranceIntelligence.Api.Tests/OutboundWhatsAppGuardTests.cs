using InsuranceIntelligence.Domain.Integrations;
using InsuranceIntelligence.Infrastructure.WhatsApp;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace InsuranceIntelligence.Api.Tests;

public sealed class OutboundWhatsAppGuardTests
{
    [Theory]
    [InlineData("Your claim documents are at https://stiiph5girbnzrayqe.blob.core.windows.net/raw/file.pdf")]
    [InlineData("Open https://files.example.test/doc.pdf?x=1&sig=abc123")]
    [InlineData("Use header Authorization: Bearer abcdefghijklmnop1234")]
    [InlineData("The api_key is in the settings")]
    [InlineData("Please send us your PIN to confirm")]
    [InlineData("Reply with the one-time code you received")]
    [InlineData("Share your password so we can check")]
    public void RefusesWhatIsNeverRightToSend(string text)
    {
        var verdict = OutboundMessageGuard.Check(text);
        Assert.False(verdict.Allowed);
        Assert.False(string.IsNullOrWhiteSpace(verdict.SafeReason));
    }

    [Theory]
    [InlineData("Never share your PIN or password with anyone, not even us.")]
    [InlineData("Do not share your one-time code with anyone.")]
    [InlineData("Don't give your password to anyone.")]
    [InlineData("Your claim was approved by Jane on 3 October. Payment follows within 5 working days.")]
    [InlineData("Open your claim here: https://app.insuranceintelligenceplatform.com/access/verify?token=abc&t=tenant&w=workspace")]
    [InlineData("Please send the police abstract and two photos of the damage.")]
    [InlineData("")]
    [InlineData(null)]
    public void LetsOrdinaryCustomerMessagesThrough(string? text) =>
        Assert.True(OutboundMessageGuard.Check(text).Allowed);

    [Fact]
    public async Task RefusedMessageNeverReachesTheSender_AndComesBackAsAFailedSendWithAReason()
    {
        var inner = new RecordingProvider();
        var guarded = Guarded(inner, perHour: 60);

        var result = await guarded.SendAsync(Request("Send your PIN here"));

        Assert.False(result.Accepted);
        Assert.Equal("egress_credential_request", result.ProviderErrorCode);
        Assert.NotNull(result.SafeError);
        Assert.Equal(0, inner.Sent);
    }

    [Fact]
    public async Task LimitHoldsBackTheMessageThatCrossesIt_ButNotOtherNumbers()
    {
        var inner = new RecordingProvider();
        var guarded = Guarded(inner, perHour: 3);

        for (var i = 0; i < 3; i++)
        {
            Assert.True((await guarded.SendAsync(Request("Hello", "256700000001"))).Accepted);
        }

        var fourth = await guarded.SendAsync(Request("Hello", "256700000001"));
        var otherNumber = await guarded.SendAsync(Request("Hello", "256700000002"));

        Assert.False(fourth.Accepted);
        Assert.Equal("egress_rate_limited", fourth.ProviderErrorCode);
        Assert.True(otherNumber.Accepted);
        Assert.Equal(4, inner.Sent);
    }

    [Fact]
    public async Task VoiceNotesCountAgainstTheSameLimit()
    {
        var inner = new RecordingProvider();
        var guarded = Guarded(inner, perHour: 1);

        Assert.True((await guarded.SendVoiceNoteAsync("biz", "256700000001", [1, 2, 3], null)).Accepted);
        Assert.False((await guarded.SendAsync(Request("Hello", "256700000001"))).Accepted);
    }

    private static GuardedWhatsAppOutboundProvider Guarded(IWhatsAppOutboundProvider inner, int perHour)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["WhatsApp:Outbound:MaxPerRecipientPerHour"] = perHour.ToString(),
        }).Build();
        var limiter = new OutboundRateLimiter(new MemoryCache(new MemoryCacheOptions()), configuration, TimeProvider.System);
        return new GuardedWhatsAppOutboundProvider(inner, limiter, NullLogger<GuardedWhatsAppOutboundProvider>.Instance);
    }

    private static WhatsAppProviderSendRequest Request(string body, string recipient = "256700000001") =>
        new("biz", recipient, body, null, "en", null, Guid.NewGuid().ToString("N"));

    private sealed class RecordingProvider : IWhatsAppOutboundProvider
    {
        public int Sent { get; private set; }

        public bool IsConfigured => true;

        public Task<WhatsAppProviderSendResult> SendAsync(WhatsAppProviderSendRequest request, CancellationToken cancellationToken = default)
        {
            Sent++;
            return Task.FromResult(new WhatsAppProviderSendResult(true, "wamid.test", null, null));
        }

        public Task<WhatsAppProviderSendResult> SendVoiceNoteAsync(
            string businessPhoneNumberId, string recipientWaId, byte[] oggOpusAudio, string? replyToProviderMessageId, CancellationToken cancellationToken = default)
        {
            Sent++;
            return Task.FromResult(new WhatsAppProviderSendResult(true, "wamid.voice", null, null));
        }
    }
}
