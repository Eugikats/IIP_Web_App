using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Documents;

namespace InsuranceIntelligence.Domain.Integrations;

/// <summary>
/// Provider-agnostic WhatsApp inbound port. Implementations talk to Meta Cloud API (or a BSP);
/// Application/API code never depends on Graph SDK types.
/// </summary>
public interface IWhatsAppInboundProvider
{
    bool IsConfigured { get; }

    bool TryVerifySubscription(string mode, string verifyToken, string challenge, out string? challengeResponse);

    bool IsValidSignature(ReadOnlySpan<byte> payload, string? signatureHeader);

    /// <summary>Signature check against the app secret of the insurer that owns the given WhatsApp
    /// number. With several insurers on one webhook, a payload must verify under the secret of the
    /// number it is about, never under any secret that happens to match.</summary>
    bool IsValidSignatureFor(string businessPhoneNumberId, ReadOnlySpan<byte> payload, string? signatureHeader) =>
        IsValidSignature(payload, signatureHeader);

    IReadOnlyList<WhatsAppInboundMediaMessage> ParseInboundMediaMessages(string jsonPayload);

    Task<WhatsAppMediaContent> DownloadMediaAsync(string mediaId, CancellationToken cancellationToken = default);

    /// <summary>Downloads media with the credentials of the insurer that owns the number it arrived on.</summary>
    Task<WhatsAppMediaContent> DownloadMediaAsync(string mediaId, string businessPhoneNumberId, CancellationToken cancellationToken = default) =>
        DownloadMediaAsync(mediaId, cancellationToken);
}

public interface IWhatsAppOutboundProvider
{
    bool IsConfigured { get; }
    Task<WhatsAppProviderSendResult> SendAsync(
        WhatsAppProviderSendRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Marks the customer's message as read and shows "typing..." in their chat until our
    /// reply arrives or 25 seconds pass, whichever is first. Purely cosmetic: returns false rather
    /// than throwing, and a provider without the capability does nothing.</summary>
    Task<bool> ShowTypingAsync(
        string businessPhoneNumberId,
        string inboundProviderMessageId,
        CancellationToken cancellationToken = default) => Task.FromResult(false);

    /// <summary>Sends an OGG/Opus voice note. A provider without the capability refuses it, and
    /// the caller sends text instead.</summary>
    Task<WhatsAppProviderSendResult> SendVoiceNoteAsync(
        string businessPhoneNumberId,
        string recipientWaId,
        byte[] oggOpusAudio,
        string? replyToProviderMessageId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new WhatsAppProviderSendResult(false, null, null, "Voice notes are not supported by this provider."));
}

public interface IWhatsAppMessageRepository
{
    Task SaveAsync(WhatsAppMessage message, CancellationToken cancellationToken = default);
    Task<WhatsAppMessage?> GetAsync(TenantScope scope, Guid messageId, CancellationToken cancellationToken = default);
    Task<WhatsAppMessage?> FindByProviderMessageIdAsync(TenantScope scope, string providerMessageId, CancellationToken cancellationToken = default);
    Task<WhatsAppMessage?> FindByIdempotencyKeyAsync(TenantScope scope, string idempotencyKey, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WhatsAppMessage>> ListThreadAsync(TenantScope scope, string threadId, CancellationToken cancellationToken = default);

    /// <summary>Records that the assistant is finished with an inbound message.</summary>
    async Task MarkAssistantHandledAsync(TenantScope scope, string providerMessageId, CancellationToken cancellationToken = default)
    {
        var message = await FindByProviderMessageIdAsync(scope, providerMessageId, cancellationToken);
        if (message is { AssistantHandledAt: null })
        {
            await SaveAsync(message with { AssistantHandledAt = DateTimeOffset.UtcNow }, cancellationToken);
        }
    }

    /// <summary>Messages in the workspace created since the given time, oldest first.</summary>
    Task<IReadOnlyList<WhatsAppMessage>> ListSinceAsync(TenantScope scope, DateTimeOffset since, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<WhatsAppMessage>>([]);
}

/// <summary>
/// Downloads WhatsApp media and stores it as a case document via <see cref="IDocumentStorageProvider"/>,
/// mirroring <see cref="IEmailAttachmentProcessor"/>.
/// </summary>
public interface IWhatsAppAttachmentProcessor
{
    Task<IReadOnlyCollection<CaseDocument>> ProcessAsync(
        ClaimCase claimCase,
        WhatsAppInboundMediaMessage message,
        string actorUserId,
        CancellationToken cancellationToken = default);
}

public interface IWhatsAppToCaseMatcher
{
    Task<WhatsAppToCaseMatchResult> MatchAsync(
        WhatsAppInboundMediaMessage message,
        TenantScope scope,
        CancellationToken cancellationToken = default);
}
