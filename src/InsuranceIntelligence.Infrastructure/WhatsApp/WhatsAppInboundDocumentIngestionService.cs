using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Documents;
using InsuranceIntelligence.Domain.Integrations;
using InsuranceIntelligence.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;

namespace InsuranceIntelligence.Infrastructure.WhatsApp;

/// <summary>
/// Orchestrates inbound WhatsApp media → case match → document storage. Uses the configured
/// single-instance channel mapping on <see cref="WhatsAppOptions"/> (one insurer per Managed App
/// instance). Does not create cases from WhatsApp text; unmatched media stays manual-review.
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

    public async Task<WhatsAppIngestionResult> IngestWebhookPayloadAsync(
        ReadOnlyMemory<byte> rawBody,
        string? signatureHeader,
        CancellationToken cancellationToken = default)
    {
        if (!whatsAppProvider.IsConfigured || !WhatsAppChannelProviderSupport.IsLive(options, settingsProvider))
        {
            return new WhatsAppIngestionResult(
                "ConfigurationRequired",
                0,
                0,
                0,
                "WhatsApp inbound is not configured.");
        }

        var json = System.Text.Encoding.UTF8.GetString(rawBody.Span);
        IReadOnlyList<string> phoneNumberIds;
        try
        {
            phoneNumberIds = WhatsAppWebhookParser.ReadPhoneNumberIds(json);
        }
        catch (System.Text.Json.JsonException)
        {
            // Unreadable, so it cannot be attributed to any insurer and cannot be verified.
            return new WhatsAppIngestionResult("InvalidSignature", 0, 0, 0, "Webhook signature was invalid.");
        }

        // The payload says which WhatsApp number it is about; that number alone decides which
        // insurer's app secret must have signed it. Numbers that belong to no insurer here are
        // ignored without being processed. Every number that does belong to one must verify.
        var ownedNumbers = phoneNumberIds.Where(id => settingsProvider.ForPhoneNumberId(id) is not null).ToArray();
        if (ownedNumbers.Length == 0)
        {
            return phoneNumberIds.Count == 0
                ? new WhatsAppIngestionResult("InvalidSignature", 0, 0, 0, "Webhook signature was invalid.")
                : new WhatsAppIngestionResult("Ignored", 0, 0, 0, null);
        }

        if (ownedNumbers.Any(id => !whatsAppProvider.IsValidSignatureFor(id, rawBody.Span, signatureHeader)))
        {
            logger.LogWarning("WhatsApp webhook rejected: invalid X-Hub-Signature-256.");
            return new WhatsAppIngestionResult("InvalidSignature", 0, 0, 0, "Webhook signature was invalid.");
        }

        var mediaMessages = whatsAppProvider.ParseInboundMediaMessages(json);
        if (mediaMessages.Count == 0)
        {
            return new WhatsAppIngestionResult("Ignored", 0, 0, 0, null);
        }

        var documentsStored = 0;
        var manualReview = 0;

        foreach (var message in mediaMessages)
        {
            // Which insurer owns the number this arrived on. Routing never falls back to a default.
            var settings = settingsProvider.ForPhoneNumberId(message.BusinessPhoneNumberId);
            if (settings is null)
            {
                logger.LogWarning(
                    "WhatsApp media ignored: phone_number_id belongs to no connected insurer. Actual={Actual}",
                    message.BusinessPhoneNumberId);
                manualReview++;
                continue;
            }

            var scope = TenantScope.Create(settings.TenantId, settings.WorkspaceId);
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
