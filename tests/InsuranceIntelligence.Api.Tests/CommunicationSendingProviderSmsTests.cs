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
        var provider = new GraphCommunicationSendingProvider(
            new InfrastructureOptions { CommunicationProvider = "GraphEmail" },
            new StubGraphMailClient(),
            smsProvider);

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
        var provider = new DisabledCommunicationSendingProvider(smsProvider);

        var result = await provider.SendSmsAsync(Request());

        Assert.Equal("Sent", result.DeliveryStatus);
        Assert.True(smsProvider.WasCalled);
    }

    [Fact]
    public async Task DisabledCommunicationSendingProvider_SendEmailAsync_StillReportsConfigurationRequired()
    {
        // Email behavior is deliberately untouched by this fix — only SMS's dead-end was real.
        var provider = new DisabledCommunicationSendingProvider(new RecordingSmsProvider("Sent"));

        var result = await provider.SendEmailAsync(Request());

        Assert.Equal("ConfigurationRequired", result.DeliveryStatus);
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

    private sealed class StubGraphMailClient : IGraphMailClient
    {
        public Task<IReadOnlyCollection<GraphMailboxMessage>> ReadMessagesAsync(MailboxConnection mailbox, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Not exercised by these SMS-focused tests.");

        public Task<GraphAttachmentContent> DownloadAttachmentAsync(string mailboxAddress, string externalMessageId, string externalAttachmentId, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Not exercised by these SMS-focused tests.");

        public Task MoveMessageAsync(string mailboxAddress, string externalMessageId, string folderName, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Not exercised by these SMS-focused tests.");

        public Task<GraphMailSendResult> SendMailAsync(GraphMailSendRequest request, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Not exercised by these SMS-focused tests.");
    }
}
