using InsuranceIntelligence.Api.Local.Mock;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Integrations;
using InsuranceIntelligence.Domain.Workflow;
using InsuranceIntelligence.Infrastructure.WhatsApp;

namespace InsuranceIntelligence.Api.Workflow;

/// <summary>The real WhatsApp sender for one workspace: that workspace's own connected business number and the
/// Cloud API provider. Null while the workspace has no active connection, so a send says "not configured"
/// instead of pretending, and never borrows another workspace's number (build-loop item 27).</summary>
public sealed class ConfiguredWhatsAppSenderResolver(
    IWhatsAppRuntimeSettingsProvider settingsProvider,
    IWhatsAppOutboundProvider outboundProvider) : IWhatsAppSenderResolver
{
    public async Task<WhatsAppSender?> ResolveAsync(TenantScope scope, CancellationToken cancellationToken = default)
    {
        await settingsProvider.RefreshIfStaleAsync(cancellationToken);
        var settings = settingsProvider.ForScope(scope);
        return settings is null || string.IsNullOrWhiteSpace(settings.PhoneNumberId)
            ? null
            : new WhatsAppSender(settings.PhoneNumberId, new NumberBoundWhatsAppOutboundProvider(settings.PhoneNumberId, outboundProvider));
    }
}

/// <summary>An outbound provider that will send only from the one number it was made for. The sender resolver already
/// hands each workspace its own number, so this is the second lock: a caller that built a request for some other
/// workspace's number gets a refusal, not a send.</summary>
public sealed class NumberBoundWhatsAppOutboundProvider(string businessPhoneNumberId, IWhatsAppOutboundProvider inner) : IWhatsAppOutboundProvider
{
    private const string Refusal = "The WhatsApp sender is not configured for this tenant workspace.";

    public bool IsConfigured => inner.IsConfigured;

    public Task<WhatsAppProviderSendResult> SendAsync(WhatsAppProviderSendRequest request, CancellationToken cancellationToken = default) =>
        string.Equals(request.BusinessPhoneNumberId, businessPhoneNumberId, StringComparison.Ordinal)
            ? inner.SendAsync(request, cancellationToken)
            : Task.FromResult(new WhatsAppProviderSendResult(false, null, null, Refusal));

    public Task<bool> ShowTypingAsync(string phoneNumberId, string inboundProviderMessageId, CancellationToken cancellationToken = default) =>
        string.Equals(phoneNumberId, businessPhoneNumberId, StringComparison.Ordinal)
            ? inner.ShowTypingAsync(phoneNumberId, inboundProviderMessageId, cancellationToken)
            : Task.FromResult(false);

    public Task<WhatsAppProviderSendResult> SendVoiceNoteAsync(
        string phoneNumberId, string recipientWaId, byte[] oggOpusAudio, string? replyToProviderMessageId, CancellationToken cancellationToken = default) =>
        string.Equals(phoneNumberId, businessPhoneNumberId, StringComparison.Ordinal)
            ? inner.SendVoiceNoteAsync(phoneNumberId, recipientWaId, oggOpusAudio, replyToProviderMessageId, cancellationToken)
            : Task.FromResult(new WhatsAppProviderSendResult(false, null, null, Refusal));
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
