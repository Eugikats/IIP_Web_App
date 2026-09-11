using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Communications;
using InsuranceIntelligence.Domain.Workflow;
using InsuranceIntelligence.Domain.Integrations;
using InsuranceIntelligence.Infrastructure.Configuration;
using InsuranceIntelligence.Infrastructure.Graph;

namespace InsuranceIntelligence.Api.Tests;

/// <summary>
/// Regression coverage for the SMS bug fixed in this change: SendSmsAsync on both
/// ICommunicationSendingProvider implementations was hardcoded to always return
/// ConfigurationRequired, regardless of SmsProvider or IntegrationConnection state — because
/// CommunicationProvider ("GraphEmail"/"Disabled") only ever governed email mode, but SMS's own
/// independent resolution was never actually reached through this interface. See
/// docs/01-strategy-requirements-roadmap/IIP_HACKATHON_READINESS_BLUEPRINT.md §14 Phase 0/1.
/// </summary>
public sealed class CommunicationSendingProviderSmsTests
{
    private static readonly TenantScope Scope = TenantScope.Create("tenant-demo", "workspace-claims");

    [Fact]
    public async Task GraphCommunicationSendingProvider_SendSmsAsync_DelegatesToInjectedSmsProvider()
    {
        var smsProvider = new RecordingSmsProvider("Sent");
        var provider = new GraphCommunicationSendingProvider(new DisabledEmailSendingProvider(), smsProvider);

        var result = await provider.SendSmsAsync(Request());

        Assert.Equal("Sent", result.DeliveryStatus);
        Assert.True(smsProvider.WasCalled);
    }

    [Fact]
    public async Task DisabledCommunicationSendingProvider_SendSmsAsync_DelegatesToInjectedSmsProvider()
    {
        // The historical bug, precisely: CommunicationProvider="Disabled" used to make SMS
        // unconditionally fail even when SMS_PROVIDER=AfricasTalking/EgoSms was fully configured.
        // "Disabled" only ever meant "email sending is disabled" — SMS must not have been coupled
        // to it at all.
        var smsProvider = new RecordingSmsProvider("Sent");
        var provider = new DisabledCommunicationSendingProvider(new DisabledEmailSendingProvider(), smsProvider);

        var result = await provider.SendSmsAsync(Request());

        Assert.Equal("Sent", result.DeliveryStatus);
        Assert.True(smsProvider.WasCalled);
    }

    [Fact]
    public async Task DisabledCommunicationSendingProvider_SendEmailAsync_DelegatesToInjectedEmailProvider()
    {
        // Email now resolves through the same Connection model as SMS (blueprint §7.2/§14 Phase 1)
        // — DisabledCommunicationSendingProvider no longer hardcodes the email outcome either.
        var emailProvider = new RecordingEmailProvider("Sent");
        var provider = new DisabledCommunicationSendingProvider(emailProvider, new RecordingSmsProvider("Sent"));

        var result = await provider.SendEmailAsync(Request());

        Assert.Equal("Sent", result.DeliveryStatus);
        Assert.True(emailProvider.WasCalled);
    }

    [Fact]
    public async Task GraphCommunicationSendingProvider_SendEmailAsync_DelegatesToInjectedEmailProvider()
    {
        var emailProvider = new RecordingEmailProvider("Sent");
        var provider = new GraphCommunicationSendingProvider(emailProvider, new RecordingSmsProvider("Sent"));

        var result = await provider.SendEmailAsync(Request());

        Assert.Equal("Sent", result.DeliveryStatus);
        Assert.True(emailProvider.WasCalled);
    }

    private static CommunicationSendRequest Request() =>
        new(Scope, Guid.NewGuid(), CommunicationChannel.SMS, "+256772118001", "Test message", "officer-1", Approved: true, LowRisk: false, "corr-1");

    private sealed class RecordingSmsProvider(string deliveryStatus) : ISmsProvider
    {
        public bool WasCalled { get; private set; }

        public Task<CommunicationSendResult> SendAsync(CommunicationSendRequest request, CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            return Task.FromResult(new CommunicationSendResult(deliveryStatus, "msg-1", null));
        }
    }

    private sealed class RecordingEmailProvider(string deliveryStatus) : IEmailSendingProvider
    {
        public bool WasCalled { get; private set; }

        public Task<CommunicationSendResult> SendEmailAsync(CommunicationSendRequest request, CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            return Task.FromResult(new CommunicationSendResult(deliveryStatus, "msg-1", null));
        }
    }
}
