using System.Text.RegularExpressions;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Communications;
using InsuranceIntelligence.Domain.Documents;
using InsuranceIntelligence.Domain.Integrations;
using InsuranceIntelligence.Domain.Notifications;
using InsuranceIntelligence.Domain.Workflow;
using InsuranceIntelligence.Infrastructure.Configuration;

namespace InsuranceIntelligence.Infrastructure.Simulated;

[System.Obsolete("DO NOT USE IN PRODUCTION. Build the real Azure integration.")]
public sealed class MockEmailIngestionProvider : IEmailIngestionProvider
{
    public Task<InboundEmailMessage> CreateTestMessageAsync(MailboxConnection mailbox, CancellationToken cancellationToken = default)
    {
        var messageId = Guid.NewGuid();
        var message = new InboundEmailMessage
        {
            MessageId = messageId,
            Scope = mailbox.Scope,
            MailboxConnectionId = mailbox.MailboxConnectionId,
            Sender = "claims@demo.local",
            Subject = "New motor claim notification",
            BodyPreview = "Customer reported a motor incident. Please create or link a claim case.",
            ReceivedAt = DateTimeOffset.UtcNow,
            ProcessingStatus = EmailProcessingStatus.Received,
            Attachments =
            [
                new EmailAttachment(Guid.NewGuid(), messageId, "claim-form-email.pdf", "application/pdf", 2048),
                new EmailAttachment(Guid.NewGuid(), messageId, "photo-damage-email.jpg", "image/jpeg", 4096)
            ]
        };

        return Task.FromResult(message);
    }
}

[System.Obsolete("DO NOT USE IN PRODUCTION. Build the real Azure integration.")]
public sealed class MockEmailAttachmentProcessor : IEmailAttachmentProcessor
{
    public Task<IReadOnlyCollection<CaseDocument>> ProcessAsync(
        ClaimCase claimCase,
        InboundEmailMessage message,
        MailboxConnection? sourceMailbox,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var documents = message.Attachments.Select(attachment => new CaseDocument
        {
            DocumentId = Guid.NewGuid(),
            CaseId = claimCase.CaseId,
            Scope = claimCase.Scope,
            OriginalFileName = attachment.FileName,
            ContentType = attachment.ContentType,
            FileSizeBytes = attachment.SizeBytes,
            ObjectKey = $"mock-email/{message.MessageId:N}/{attachment.FileName}",
            DocumentType = Classify(attachment.FileName),
            ProcessingStatus = DocumentProcessingStatus.Processed,
            Classification = new DocumentClassificationResult(Classify(attachment.FileName), 0.82m, "MockEmailAttachmentProcessor", now),
            Extraction = new DocumentExtractionResult(
                new Dictionary<string, string> { ["source"] = "email_attachment", ["fileName"] = attachment.FileName },
                new Dictionary<string, decimal> { ["source"] = 1m, ["fileName"] = 1m },
                "MockEmailAttachmentProcessor",
                now,
                false,
                null,
                null),
            ValidationWarnings = Array.Empty<DocumentValidationWarning>(),
            UploadedByUserId = actorUserId,
            UploadedAt = now,
            ProcessedAt = now,
            CorrelationId = $"email-{message.MessageId:N}"
        }).ToArray();

        return Task.FromResult<IReadOnlyCollection<CaseDocument>>(documents);
    }

    private static DocumentType Classify(string fileName)
    {
        if (fileName.Contains("claim", StringComparison.OrdinalIgnoreCase))
        {
            return DocumentType.ClaimForm;
        }

        if (fileName.Contains("photo", StringComparison.OrdinalIgnoreCase))
        {
            return DocumentType.PhotoEvidence;
        }

        if (fileName.Contains("police", StringComparison.OrdinalIgnoreCase))
        {
            return DocumentType.PoliceAbstract;
        }

        return DocumentType.Unknown;
    }
}

[System.Obsolete("DO NOT USE IN PRODUCTION. Build the real Azure integration.")]
public sealed class BasicEmailToCaseMatcher(ICaseRepository caseRepository) : IEmailToCaseMatcher
{
    private static readonly Regex GuidPattern = new(
        "[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}",
        RegexOptions.Compiled);

    public async Task<EmailToCaseMatchResult> MatchAsync(InboundEmailMessage message, TenantScope scope, CancellationToken cancellationToken = default)
    {
        var text = $"{message.Subject} {message.BodyPreview}";
        var cases = await caseRepository.ListAsync(scope, cancellationToken);
        var guid = GuidPattern.Match(text);
        if (guid.Success && Guid.TryParse(guid.Value, out var caseId) && cases.Any(claimCase => claimCase.CaseId == caseId))
        {
            return new EmailToCaseMatchResult(caseId, "case_id", false);
        }

        var byPolicyOrClaimNumber = cases.FirstOrDefault(claimCase =>
            !string.IsNullOrWhiteSpace(claimCase.PolicyNumber)
            && text.Contains(claimCase.PolicyNumber, StringComparison.OrdinalIgnoreCase));
        if (byPolicyOrClaimNumber is not null)
        {
            return new EmailToCaseMatchResult(byPolicyOrClaimNumber.CaseId, "claim_or_policy_number", false);
        }

        return new EmailToCaseMatchResult(null, "no_match", true);
    }
}

[System.Obsolete("DO NOT USE IN PRODUCTION. Build the real Azure integration.")]
public sealed class MockCommunicationSendingProvider(InfrastructureOptions options) : ICommunicationSendingProvider
{
    public Task<CommunicationSendResult> SendEmailAsync(CommunicationSendRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(Send(request, "GraphEmail", "notification.email.simulated"));

    public Task<CommunicationSendResult> SendSmsAsync(CommunicationSendRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(Send(request, "AzureCommunicationServicesSms", "notification.sms.simulated"));

    private CommunicationSendResult Send(CommunicationSendRequest request, string liveMode, string simulatedId)
    {
        if (request.Channel == CommunicationChannel.SMS && !request.Recipient.StartsWith("+", StringComparison.Ordinal))
        {
            return new CommunicationSendResult("Failed", string.Empty, "SMS recipient must use international format.");
        }

        if (!request.Approved && !request.LowRisk)
        {
            return new CommunicationSendResult("ApprovalRequired", string.Empty, "Customer communication requires approval before sending.");
        }

        if (options.CommunicationProvider.Equals("Mock", StringComparison.OrdinalIgnoreCase))
        {
            return new CommunicationSendResult("SimulatedSent", simulatedId, null);
        }

        if (options.CommunicationProvider.Equals(liveMode, StringComparison.OrdinalIgnoreCase))
        {
            return new CommunicationSendResult("ConfigurationRequired", string.Empty, $"{liveMode} is selected but credentials are not configured.");
        }

        return new CommunicationSendResult("ConfigurationRequired", string.Empty, "Selected communication provider is not configured.");
    }
}

[System.Obsolete("DO NOT USE IN PRODUCTION. Build the real Azure integration.")]
public sealed class MockSmsProvider(ICommunicationSendingProvider communicationProvider) : ISmsProvider
{
    public Task<CommunicationSendResult> SendAsync(CommunicationSendRequest request, CancellationToken cancellationToken = default) =>
        communicationProvider.SendSmsAsync(request, cancellationToken);
}

[System.Obsolete("DO NOT USE IN PRODUCTION. Build the real Azure integration.")]
public sealed class MockTeamsNotificationProvider(InfrastructureOptions options) : ITeamsNotificationProvider
{
    public Task<string> SendAsync(TeamsNotification notification, CancellationToken cancellationToken = default)
    {
        if (!options.NotificationProvider.Equals("Mock", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult("ConfigurationRequired");
        }

        return Task.FromResult("notification.teams.simulated");
    }
}

[System.Obsolete("DO NOT USE IN PRODUCTION. Build the real Azure integration.")]
public sealed class MockWebhookSubscriptionProvider : IWebhookSubscriptionProvider
{
    public Task<string> ValidateAsync(string payload, CancellationToken cancellationToken = default) =>
        Task.FromResult(string.IsNullOrWhiteSpace(payload) ? "EmptyWebhookPayload" : "MockWebhookAccepted");
}
