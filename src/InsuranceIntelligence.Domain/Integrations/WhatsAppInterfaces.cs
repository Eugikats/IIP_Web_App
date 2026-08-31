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

    IReadOnlyList<WhatsAppInboundMediaMessage> ParseInboundMediaMessages(string jsonPayload);

    Task<WhatsAppMediaContent> DownloadMediaAsync(string mediaId, CancellationToken cancellationToken = default);
}

public interface IWhatsAppOutboundProvider
{
    bool IsConfigured { get; }
    Task<WhatsAppProviderSendResult> SendAsync(
        WhatsAppProviderSendRequest request,
        CancellationToken cancellationToken = default);
}

public interface IWhatsAppMessageRepository
{
    Task SaveAsync(WhatsAppMessage message, CancellationToken cancellationToken = default);
    Task<WhatsAppMessage?> GetAsync(TenantScope scope, Guid messageId, CancellationToken cancellationToken = default);
    Task<WhatsAppMessage?> FindByProviderMessageIdAsync(TenantScope scope, string providerMessageId, CancellationToken cancellationToken = default);
    Task<WhatsAppMessage?> FindByIdempotencyKeyAsync(TenantScope scope, string idempotencyKey, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WhatsAppMessage>> ListThreadAsync(TenantScope scope, string threadId, CancellationToken cancellationToken = default);
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
