using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Documents;
using InsuranceIntelligence.Domain.Integrations;
using InsuranceIntelligence.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;

namespace InsuranceIntelligence.Infrastructure.WhatsApp;

/// <summary>
/// Which workspace a webhook belongs to, decided from the business phone number it names and proved by
/// the signature of that workspace's own app secret (build-loop item 27). Status is "Routed",
/// "ConfigurationRequired" (nothing is connected, or the channel is not live) or "InvalidSignature"
/// (a number nobody has connected is answered the same way as a bad signature, so a stranger cannot
/// learn which numbers are connected).
/// </summary>
public sealed record WhatsAppWebhookRouting(string Status, IReadOnlyList<WhatsAppRoutedBatch> Batches, string? SafeError)
{
    public bool IsRouted => Status == "Routed";
}

/// <summary>One workspace's share of a webhook: its connection and the scope everything in it is stored under.</summary>
public sealed record WhatsAppRoutedBatch(WhatsAppEffectiveSettings Settings)
{
    public TenantScope Scope => TenantScope.Create(Settings.TenantId, Settings.WorkspaceId);
}

/// <summary>
/// Orchestrates inbound WhatsApp media → case match → document storage. A webhook is first routed to the
/// workspace that owns the business phone number it names (see <see cref="RouteAsync"/>); each workspace's
/// media is then matched only against that workspace's own claims. Does not create cases from WhatsApp
/// text; unmatched media stays manual-review.
/// </summary>
public sealed class WhatsAppInboundDocumentIngestionService(
    InfrastructureOptions options,
    IWhatsAppRuntimeSettingsProvider settingsProvider,
    IWhatsAppInboundProvider whatsAppProvider,
    IWhatsAppToCaseMatcher matcher,
    IWhatsAppAttachmentProcessor attachmentProcessor,
    ICaseRepository caseRepository,
    ICaseDocumentRepository documentRepository,
    IAuditRepository auditRepository,
    ILogger<WhatsAppInboundDocumentIngestionService> logger)
{
    // Same system-actor string WhatsAppMessagingService.WriteIntegrationAuditAsync already uses
    // for this exact unauthenticated integration path, reused rather than inventing a second one.
    private const string SystemActorId = "system:whatsapp-webhook";

    /// <summary>Works out which workspace(s) a webhook is for and checks each one's signature with its own
    /// app secret. A request naming a number nobody has connected, or signed with the wrong secret, is
    /// refused whole.</summary>
    public async Task<WhatsAppWebhookRouting> RouteAsync(
        ReadOnlyMemory<byte> rawBody,
        string? signatureHeader,
        CancellationToken cancellationToken = default)
    {
        await settingsProvider.RefreshIfStaleAsync(cancellationToken);
        if (!whatsAppProvider.IsConfigured)
        {
            return new WhatsAppWebhookRouting("ConfigurationRequired", [], "WhatsApp inbound is not configured.");
        }

        var json = System.Text.Encoding.UTF8.GetString(rawBody.Span);
        var numbers = WhatsAppWebhookParser.ParsePhoneNumberIds(json);
        if (numbers.Count == 0)
        {
            // Nothing names a number, so there is no secret to check it against, except on a deployment
            // with exactly one connection.
            var sole = settingsProvider.Current;
            if (sole is null)
            {
                return new WhatsAppWebhookRouting("InvalidSignature", [], "Webhook signature was invalid.");
            }

            if (!WhatsAppChannelProviderSupport.IsLive(options, sole))
            {
                return new WhatsAppWebhookRouting("ConfigurationRequired", [], "WhatsApp inbound is not configured.");
            }

            return whatsAppProvider.IsValidSignature(rawBody.Span, signatureHeader)
                ? new WhatsAppWebhookRouting("Routed", [], null)
                : new WhatsAppWebhookRouting("InvalidSignature", [], "Webhook signature was invalid.");
        }

        var batches = new List<WhatsAppRoutedBatch>();
        foreach (var number in numbers)
        {
            var settings = await settingsProvider.FindByPhoneNumberAsync(number, cancellationToken);
            if (settings is null || !whatsAppProvider.IsValidSignature(rawBody.Span, signatureHeader, number))
            {
                logger.LogWarning("WhatsApp webhook rejected: no connected workspace could vouch for it (invalid or unknown business number).");
                return new WhatsAppWebhookRouting("InvalidSignature", [], "Webhook signature was invalid.");
            }

            if (!WhatsAppChannelProviderSupport.IsLive(options, settings))
            {
                return new WhatsAppWebhookRouting("ConfigurationRequired", [], "WhatsApp inbound is not configured.");
            }

            batches.Add(new WhatsAppRoutedBatch(settings));
        }

        return new WhatsAppWebhookRouting("Routed", batches, null);
    }

    public async Task<WhatsAppIngestionResult> IngestWebhookPayloadAsync(
        ReadOnlyMemory<byte> rawBody,
        string? signatureHeader,
        CancellationToken cancellationToken = default)
    {
        var routing = await RouteAsync(rawBody, signatureHeader, cancellationToken);
        if (!routing.IsRouted)
        {
            return new WhatsAppIngestionResult(routing.Status, 0, 0, 0, routing.SafeError);
        }

        return await IngestRoutedMediaAsync(routing, rawBody, cancellationToken);
    }

    /// <summary>Stores the media in a routed webhook on the claims of the workspace that owns the number each
    /// message was sent to.</summary>
    public async Task<WhatsAppIngestionResult> IngestRoutedMediaAsync(
        WhatsAppWebhookRouting routing,
        ReadOnlyMemory<byte> rawBody,
        CancellationToken cancellationToken = default)
    {
        var json = System.Text.Encoding.UTF8.GetString(rawBody.Span);
        var mediaMessages = whatsAppProvider.ParseInboundMediaMessages(json);
        if (mediaMessages.Count == 0)
        {
            return new WhatsAppIngestionResult("Ignored", 0, 0, 0, null);
        }

        var documentsStored = 0;
        var manualReview = 0;

        foreach (var message in mediaMessages)
        {
            var batch = routing.Batches.FirstOrDefault(item =>
                string.Equals(item.Settings.PhoneNumberId, message.BusinessPhoneNumberId, StringComparison.Ordinal));
            if (batch is null)
            {
                logger.LogWarning(
                    "WhatsApp media ignored: phone_number_id is not one this webhook was routed for. Actual={Actual}",
                    message.BusinessPhoneNumberId);
                manualReview++;
                continue;
            }

            var settings = batch.Settings;
            var scope = batch.Scope;
            // IntegrationUserId attributes a document upload to a real staff account for audit; it is
            // an optional deployment-level refinement, not a precondition for the channel to work at
            // all. A tenant that connects WhatsApp from the settings screen has set nothing like it
            // (there is no field for it on that screen), and this used to reject every single message,
            // text included, until an operator learned of an env var no UI ever mentioned.
            var actor = string.IsNullOrWhiteSpace(settings.IntegrationUserId) ? SystemActorId : settings.IntegrationUserId!;

            var match = await matcher.MatchAsync(message, scope, cancellationToken);
            ClaimCase claimCase;
            if (match.CaseId is { } matchedCaseId && !match.RequiresManualReview)
            {
                var existingCase = await caseRepository.GetAsync(matchedCaseId, cancellationToken);
                if (existingCase is null || !existingCase.Scope.Matches(scope))
                {
                    manualReview++;
                    continue;
                }

                claimCase = existingCase;
            }
            else
            {
                manualReview++;
                await auditRepository.WriteAsync(
                    ChannelAudit(
                        scope,
                        null,
                        "whatsapp.inbound.manual_review_required",
                        [message.ProviderMessageId],
                        actor,
                        message.ProviderMessageId,
                        "WhatsAppCloudApi"),
                    cancellationToken);
                continue;
            }

            var correlationId = $"whatsapp-{message.ProviderMessageId}";
            var existingDocuments = await documentRepository.ListForCaseAsync(claimCase.CaseId, scope, cancellationToken);
            if (existingDocuments.Any(document => string.Equals(document.CorrelationId, correlationId, StringComparison.Ordinal)))
            {
                continue;
            }

            try
            {
                var documents = await attachmentProcessor.ProcessAsync(claimCase, message, actor, cancellationToken);
                foreach (var document in documents)
                {
                    await documentRepository.SaveAsync(document, cancellationToken);
                    await auditRepository.WriteAsync(
                        AuditEvent.DocumentUploaded(document, actor, document.CorrelationId),
                        cancellationToken);
                    documentsStored++;
                }

                await auditRepository.WriteAsync(
                    ChannelAudit(
                        scope,
                        claimCase.CaseId,
                        "whatsapp.inbound.document_routed",
                        [message.ProviderMessageId, .. documents.Select(d => d.DocumentId.ToString())],
                        actor,
                        message.ProviderMessageId,
                        "WhatsAppCloudApi"),
                    cancellationToken);
            }
            catch (Exception exception)
                when (exception is InvalidOperationException or HttpRequestException or NotSupportedException)
            {
                manualReview++;
                logger.LogWarning(
                    exception,
                    "WhatsApp media processing failed safely. MessageId={MessageId}",
                    message.ProviderMessageId);
                await auditRepository.WriteAsync(
                    ChannelAudit(
                        scope,
                        claimCase.CaseId,
                        "whatsapp.inbound.processing_failed",
                        [message.ProviderMessageId],
                        actor,
                        message.ProviderMessageId,
                        "WhatsAppCloudApi"),
                    cancellationToken);
            }
        }

        return new WhatsAppIngestionResult(
            "Accepted",
            mediaMessages.Count,
            documentsStored,
            manualReview,
            null);
    }

    private static AuditEvent ChannelAudit(
        TenantScope scope,
        Guid? caseId,
        string action,
        IReadOnlyList<string> targetIds,
        string actor,
        string correlationId,
        string provider) =>
        new(
            Guid.NewGuid(),
            scope,
            caseId,
            DocumentId: null,
            UserId: actor,
            ActorType.Integration,
            action,
            DateTimeOffset.UtcNow,
            BeforeStatus: null,
            AfterStatus: null,
            EvidenceIds: targetIds,
            ModelProvider: provider,
            Confidence: null,
            ValidationResult: "accepted",
            ApprovalId: null,
            AuditReason: null,
            CorrelationId: correlationId);
}
