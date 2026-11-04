using InsuranceIntelligence.Api.Local;
using InsuranceIntelligence.Application.Documents;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Documents;

namespace InsuranceIntelligence.Api.Documents;

/// <summary>What happened when a stored document was run through processing.</summary>
public sealed record DocumentProcessingRun(CaseDocument Document, ProviderNotConfiguredException? NotConfigured)
{
    public bool Failed => Document.ProcessingStatus == DocumentProcessingStatus.Failed;
}

/// <summary>
/// Runs one stored document through processing and always leaves it in a truthful state. If the
/// provider is missing or throws, the document is saved as <see cref="DocumentProcessingStatus.Failed"/>
/// with a safe warning and an audit event, so it can be retried; it is never left as Uploaded
/// (which the customer page would show as "Being checked" forever). The same method serves the
/// staff upload, the claimant upload and both retry paths, so they cannot drift apart.
/// </summary>
public static class DocumentProcessingRunner
{
    public const string FailedWarningCode = "document.processing.failed";
    public const string DisagreementWarningCode = "document.classifier.disagrees";

    public static async Task<DocumentProcessingRun> RunAsync(
        CaseDocument stored,
        ClaimCase claimCase,
        IDocumentStorageProvider storageProvider,
        IDocumentProcessingProvider processingProvider,
        IDocumentFieldValidationRuleRepository fieldValidationRuleRepository,
        ICaseDocumentRepository documentRepository,
        IAuditRepository auditRepository,
        string correlationId,
        CancellationToken cancellationToken,
        IDocumentRecognitionService? recognition = null,
        IFieldProposalService? proposals = null,
        IAuthenticityService? authenticity = null)
    {
        var scope = claimCase.Scope;
        try
        {
            var processingResult = await processingProvider.ProcessAsync(
                new DocumentProcessingRequest(
                    stored.DocumentId,
                    stored.CaseId,
                    stored.OriginalFileName,
                    stored.ContentType,
                    stored.FileSizeBytes,
                    stored.ObjectKey,
                    ct => storageProvider.ReadAsync(scope, stored.ObjectKey, ct)),
                cancellationToken);
            var rules = DocumentFieldValidator.ResolveRules(
                scope, await fieldValidationRuleRepository.ListAsync(scope, cancellationToken));
            var fieldWarnings = DocumentFieldValidator.Evaluate(
                rules,
                processingResult.Classification.DocumentType,
                claimCase.ProductLine,
                processingResult.Extraction.Fields);

            var processed = stored with
            {
                DocumentType = processingResult.Classification.DocumentType,
                ProcessingStatus = DocumentProcessingStatus.Processed,
                Classification = processingResult.Classification,
                Extraction = processingResult.Extraction,
                ValidationWarnings = processingResult.ValidationWarnings.Concat(fieldWarnings).ToArray(),
                ProcessedAt = DateTimeOffset.UtcNow
            };

            if (recognition is not null)
            {
                // Name the file and decide where it belongs. A failure here never fails processing:
                // the file is simply left for a person.
                try
                {
                    processed = await recognition.ApplyAsync(processed, claimCase, correlationId, cancellationToken);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    processed = processed with { NeedsReview = true, ReviewReason = "It could not be looked at just now. Please check where it belongs." };
                }
            }

            if (processed.DisagreesWithClassifier)
            {
                // A flag for staff, never an error shown to the customer: the customer's own choice
                // of slot stands, and a person decides whether the file is in the right one.
                processed = processed with
                {
                    ValidationWarnings = processed.ValidationWarnings
                        .Append(new DocumentValidationWarning(
                            DisagreementWarningCode,
                            $"The customer filed this under '{processed.DocumentTypeKey}', but it reads as '{processed.RecognisedKey}'. Check it is in the right place.",
                            "warning"))
                        .ToArray()
                };
            }

            await documentRepository.SaveAsync(processed, cancellationToken);
            await auditRepository.WriteAsync(AuditEvent.DocumentProcessed(processed, correlationId), cancellationToken);
            if (proposals is not null)
            {
                // What the document says is offered for the claim's questions; a person decides.
                // A failure here never undoes a processed document.
                try
                {
                    await proposals.ProposeAsync(processed, claimCase, correlationId, cancellationToken);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                }
            }

            if (authenticity is not null)
            {
                // Advice for a person to look at; never blocks the document or the claim.
                try
                {
                    await authenticity.AnalyseAsync(processed, claimCase, correlationId, cancellationToken);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                }
            }

            return new DocumentProcessingRun(processed, null);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            var notConfigured = exception as ProviderNotConfiguredException;
            var failed = stored with
            {
                ProcessingStatus = DocumentProcessingStatus.Failed,
                ValidationWarnings = stored.ValidationWarnings
                    .Where(warning => warning.Code != FailedWarningCode)
                    .Append(new DocumentValidationWarning(
                        FailedWarningCode,
                        notConfigured is not null
                            ? "Document processing is not configured for this workspace, so this file could not be read yet."
                            : "This file could not be read. It can be retried.",
                        "error"))
                    .ToArray(),
                ProcessedAt = null
            };
            await documentRepository.SaveAsync(failed, cancellationToken);
            await auditRepository.WriteAsync(AuditEvent.DocumentProcessingFailed(failed, correlationId), cancellationToken);
            return new DocumentProcessingRun(failed, notConfigured);
        }
    }
}
