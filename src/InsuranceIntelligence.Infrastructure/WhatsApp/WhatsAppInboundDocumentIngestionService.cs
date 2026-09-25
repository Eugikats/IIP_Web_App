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
    CaseIntakeService caseIntakeService,
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
        var settings = settingsProvider.Current;
        if (!whatsAppProvider.IsConfigured || !WhatsAppChannelProviderSupport.IsLive(options, settings))
        {
            return new WhatsAppIngestionResult(
                "ConfigurationRequired",
                0,
                0,
                0,
                "WhatsApp inbound is not configured.");
        }

        if (!whatsAppProvider.IsValidSignature(rawBody.Span, signatureHeader))
        {
            logger.LogWarning("WhatsApp webhook rejected: invalid X-Hub-Signature-256.");
            return new WhatsAppIngestionResult("InvalidSignature", 0, 0, 0, "Webhook signature was invalid.");
        }

        if (settings is null)
        {
            return new WhatsAppIngestionResult(
                "ConfigurationRequired",
                0,
                0,
                0,
                "WhatsApp channel mapping (tenant/workspace) is not configured.");
        }

        var json = System.Text.Encoding.UTF8.GetString(rawBody.Span);
        var mediaMessages = whatsAppProvider.ParseInboundMediaMessages(json);
        if (mediaMessages.Count == 0)
        {
            return new WhatsAppIngestionResult("Ignored", 0, 0, 0, null);
        }

        var expectedPhoneNumberId = settings.PhoneNumberId;
        var scope = TenantScope.Create(settings.TenantId, settings.WorkspaceId);
        // IntegrationUserId attributes a document upload to a real staff account for audit; it is
        // an optional deployment-level refinement, not a precondition for the channel to work at
        // all. A tenant that connects WhatsApp from the settings screen has set nothing like it
        // (there is no field for it on that screen), and this used to reject every single message,
        // text included, until an operator learned of an env var no UI ever mentioned.
        var actor = string.IsNullOrWhiteSpace(settings.IntegrationUserId) ? SystemActorId : settings.IntegrationUserId!;
        var documentsStored = 0;
        var manualReview = 0;

        foreach (var message in mediaMessages)
        {
            if (!string.Equals(message.BusinessPhoneNumberId, expectedPhoneNumberId, StringComparison.Ordinal))
            {
                logger.LogWarning(
                    "WhatsApp media ignored: phone_number_id mismatch. Expected={Expected} Actual={Actual}",
                    expectedPhoneNumberId,
                    message.BusinessPhoneNumberId);
                manualReview++;
                continue;
            }

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
            else if (match.IdentifiedSender is { } identifiedSender)
            {
                claimCase = await caseIntakeService.CreateCaseAsync(
                    new CreateCaseCommand(
                        scope,
                        identifiedSender.CustomerName,
                        identifiedSender.CustomerEmail,
                        identifiedSender.CustomerPhone,
                        identifiedSender.PolicyNumber,
                        identifiedSender.ProductLine,
                        ClaimType: "WhatsApp Intake",
                        Channel: "WhatsApp",
                        IncidentDate: DateOnly.FromDateTime(message.ReceivedAt.UtcDateTime.Date),
                        IncidentDescription: string.IsNullOrWhiteSpace(message.Caption)
                            ? "Reported via WhatsApp, no description provided; documents attached for review."
                            : message.Caption!,
                        CreatedByUserId: actor,
                        CorrelationId: $"whatsapp-{message.ProviderMessageId}",
                        RequiresConfirmation: true),
                    cancellationToken);
                await auditRepository.WriteAsync(
                    ChannelAudit(
                        scope,
                        claimCase.CaseId,
                        "whatsapp.inbound.case_auto_created",
                        [message.ProviderMessageId],
                        actor,
                        message.ProviderMessageId,
                        "WhatsAppCloudApi"),
                    cancellationToken);
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
