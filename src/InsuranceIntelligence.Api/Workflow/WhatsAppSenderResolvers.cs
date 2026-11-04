using InsuranceIntelligence.Api.Local.Mock;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Integrations;
using InsuranceIntelligence.Domain.Workflow;
using InsuranceIntelligence.Infrastructure.WhatsApp;

namespace InsuranceIntelligence.Api.Workflow;

/// <summary>The real WhatsApp sender: the workspace's connected business number and the Cloud API provider.
/// Null while no connection is active, so a send says "not configured" instead of pretending.</summary>
public sealed class ConfiguredWhatsAppSenderResolver(
    IWhatsAppRuntimeSettingsProvider settingsProvider,
    IWhatsAppOutboundProvider outboundProvider) : IWhatsAppSenderResolver
{
    public Task<WhatsAppSender?> ResolveAsync(TenantScope scope, CancellationToken cancellationToken = default)
    {
        var settings = settingsProvider.Current;
        return Task.FromResult<WhatsAppSender?>(settings is null || string.IsNullOrWhiteSpace(settings.PhoneNumberId)
            ? null
            : new WhatsAppSender(settings.PhoneNumberId, outboundProvider));
    }
}

/// <summary>A pretend WhatsApp sender for a developer runtime: it records what would have been sent into the
/// mock outbox (read at /api/v1/dev/mock-outbox) and nothing leaves the process.</summary>
public sealed class MockWhatsAppSenderResolver(MockOutbox outbox) : IWhatsAppSenderResolver
{
    public const string MockBusinessPhoneNumberId = "mock-business-number";

    public Task<WhatsAppSender?> ResolveAsync(TenantScope scope, CancellationToken cancellationToken = default) =>
        Task.FromResult<WhatsAppSender?>(new WhatsAppSender(MockBusinessPhoneNumberId, new MockWhatsAppOutboundProvider(outbox, scope)));
}

public sealed class MockWhatsAppOutboundProvider(MockOutbox outbox, TenantScope scope) : IWhatsAppOutboundProvider
{
    public bool IsConfigured => true;

    public Task<WhatsAppProviderSendResult> SendAsync(WhatsAppProviderSendRequest request, CancellationToken cancellationToken = default)
    {
        var body = string.IsNullOrWhiteSpace(request.TemplateName)
            ? request.CustomerSafeBody
            : $"[template {request.TemplateName} ({request.Language})] "
              + string.Join(", ", (request.TemplateParameters ?? new Dictionary<string, string>()).Select(pair => $"{pair.Key}={pair.Value}"));
        var message = outbox.Add(scope.TenantId, scope.WorkspaceId, CommunicationChannel.WhatsApp, request.RecipientWaId, string.Empty, body);
        return Task.FromResult(new WhatsAppProviderSendResult(true, $"mock-wamid-{message.MessageId:N}", null, null));
    }

    public Task<WhatsAppProviderSendResult> SendVoiceNoteAsync(
        string businessPhoneNumberId, string recipientWaId, byte[] oggOpusAudio, string? replyToProviderMessageId, CancellationToken cancellationToken = default) =>
        Task.FromResult(new WhatsAppProviderSendResult(false, null, null, "Voice notes are not supported by the mock sender."));
}
