using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Documents;
using InsuranceIntelligence.Domain.Integrations;
using InsuranceIntelligence.Infrastructure.Configuration;

namespace InsuranceIntelligence.Infrastructure.Graph;

public sealed class GraphEmailAttachmentProcessor(
    InfrastructureOptions options,
    IGraphMailClient graphClient,
    IDocumentStorageProvider storageProvider,
    IDocumentProcessingProvider processingProvider,
    InsuranceIntelligence.Application.Documents.IDocumentRecognitionService? recognition = null,
    InsuranceIntelligence.Application.Documents.IFieldProposalService? proposals = null,
    InsuranceIntelligence.Application.Documents.IAuthenticityService? authenticity = null,
    IDocumentChecklistProvider? checklistProvider = null,
    ICaseDocumentRepository? documentRepository = null) : IEmailAttachmentProcessor
{
    private readonly InsuranceIntelligence.Infrastructure.Email.EmailAttachmentDocumentBuilder builder = new(
        storageProvider, processingProvider, recognition, proposals, authenticity, checklistProvider, documentRepository);

    public async Task<IReadOnlyCollection<CaseDocument>> ProcessAsync(
        ClaimCase claimCase,
        InboundEmailMessage message,
        MailboxConnection? sourceMailbox,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        if (message.Attachments.Count == 0)
        {
            return Array.Empty<CaseDocument>();
        }

        if (!options.EmailProvider.Equals("Graph", StringComparison.OrdinalIgnoreCase))
        {
            return Array.Empty<CaseDocument>();
        }

        if (sourceMailbox is null)
        {
            throw new InvalidOperationException("Graph email attachment processing requires the source mailbox connection.");
        }

        if (string.IsNullOrWhiteSpace(message.ExternalMessageId))
        {
            throw new InvalidOperationException("Graph email attachment processing requires the Graph message id.");
        }

        var processedDocuments = new List<CaseDocument>();
        foreach (var attachment in message.Attachments)
        {
            if (string.IsNullOrWhiteSpace(attachment.ExternalAttachmentId))
            {
                throw new InvalidOperationException("Graph email attachment processing requires the Graph attachment id.");
            }

            var content = await graphClient.DownloadAttachmentAsync(
                sourceMailbox.MailboxAddress,
                message.ExternalMessageId,
                attachment.ExternalAttachmentId,
                sourceMailbox.EntraTenantId,
                cancellationToken);
            if (content.ContentBytes.Length == 0)
            {
                throw new InvalidOperationException("Graph email attachment content was empty.");
            }

            processedDocuments.Add(await builder.BuildAsync(
                claimCase,
                attachment,
                content,
                actorUserId,
                "GraphEmailAttachment",
                $"graph-email-{message.MessageId:N}-{attachment.AttachmentId:N}",
                cancellationToken));
        }

        return processedDocuments;
    }
}
