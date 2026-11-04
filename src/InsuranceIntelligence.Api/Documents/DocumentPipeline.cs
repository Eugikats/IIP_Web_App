using System.Text.Json;
using InsuranceIntelligence.Api.Agents;
using InsuranceIntelligence.Api.Jobs;
using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Application.Documents;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Documents;
using InsuranceIntelligence.Domain.Jobs;

namespace InsuranceIntelligence.Api.Documents;

/// <summary>Does the reading of one stored document: classify and name it, extract, check the
/// picture, offer values for the claim's questions. The same code runs whether an upload does it
/// before returning (inline) or a worker does it afterwards.</summary>
public sealed class DocumentProcessor(
    IDocumentStorageProvider storageProvider,
    IDocumentProcessingProvider processingProvider,
    IDocumentFieldValidationRuleRepository fieldValidationRuleRepository,
    ICaseDocumentRepository documentRepository,
    IAuditRepository auditRepository,
    IDocumentRecognitionService? recognition = null,
    IFieldProposalService? proposals = null,
    IAuthenticityService? authenticity = null)
{
    public Task<DocumentProcessingRun> ProcessAsync(CaseDocument stored, ClaimCase claimCase, string correlationId, CancellationToken cancellationToken) =>
        DocumentProcessingRunner.RunAsync(
            stored, claimCase, storageProvider, processingProvider, fieldValidationRuleRepository, documentRepository, auditRepository,
            correlationId, cancellationToken, recognition, proposals, authenticity);
}

public interface IDocumentPipeline
{
    /// <summary>Hands a stored document to processing. Inline, it is processed before this returns
    /// (the document in the result is the processed one). In the background, it is queued durably and
    /// returns at once with the document as stored ("being checked").</summary>
    Task<DocumentProcessingRun> SubmitAsync(CaseDocument stored, ClaimCase claimCase, string correlationId, CancellationToken cancellationToken);
}

public sealed class DocumentPipeline(
    BackgroundPipelineOptions options,
    DocumentProcessor processor,
    ICaseDocumentRepository documentRepository,
    IBackgroundJobStore jobStore) : IDocumentPipeline
{
    public async Task<DocumentProcessingRun> SubmitAsync(CaseDocument stored, ClaimCase claimCase, string correlationId, CancellationToken cancellationToken)
    {
        if (options.Inline)
        {
            return await processor.ProcessAsync(stored, claimCase, correlationId, cancellationToken);
        }

        // Safely stored and waiting: the customer sees "being checked" until a worker has read it.
        var waiting = stored with
        {
            ProcessingStatus = DocumentProcessingStatus.Uploaded,
            ValidationWarnings = stored.ValidationWarnings.Where(warning => warning.Code != DocumentProcessingRunner.FailedWarningCode).ToArray(),
        };
        await documentRepository.SaveAsync(waiting, cancellationToken);
        await jobStore.EnqueueAsync(
            NewJob(BackgroundQueues.DocumentProcessing, BackgroundJobTypes.DocumentProcess, stored.DocumentId.ToString("N"), claimCase, stored.DocumentId, correlationId, options.MaxAttempts),
            cancellationToken: cancellationToken);
        return new DocumentProcessingRun(waiting, null, Queued: true);
    }

    public static BackgroundJob NewJob(string queue, string type, string key, ClaimCase claimCase, Guid? documentId, string correlationId, int maxAttempts, string? payload = null)
    {
        var now = DateTimeOffset.UtcNow;
        return new BackgroundJob
        {
            JobId = Guid.NewGuid(),
            Scope = claimCase.Scope,
            Queue = queue,
            Type = type,
            Key = key,
            PayloadJson = payload ?? JsonSerializer.Serialize(new { caseId = claimCase.CaseId, documentId }),
            Status = BackgroundJobStatus.Pending,
            MaxAttempts = maxAttempts,
            NotBefore = now,
            CreatedAt = now,
            UpdatedAt = now,
            CorrelationId = correlationId,
            CaseId = claimCase.CaseId,
            DocumentId = documentId,
        };
    }
}

public static class DocumentPipelineHost
{
    /// <summary>Hands the document to the pipeline when one is registered, and otherwise runs it the
    /// way it always ran (some callers are unit tests that build the handler by hand).</summary>
    public static async Task<DocumentProcessingRun> SubmitOrRunAsync(
        HttpContext httpContext,
        CaseDocument stored,
        ClaimCase claimCase,
        IDocumentStorageProvider storageProvider,
        IDocumentProcessingProvider processingProvider,
        IDocumentFieldValidationRuleRepository fieldValidationRuleRepository,
        ICaseDocumentRepository documentRepository,
        IAuditRepository auditRepository,
        string correlationId,
        CancellationToken cancellationToken)
    {
        if (httpContext.RequestServices?.GetService(typeof(IDocumentPipeline)) is IDocumentPipeline pipeline)
        {
            return await pipeline.SubmitAsync(stored, claimCase, correlationId, cancellationToken);
        }

        return await DocumentProcessingRunner.RunAsync(
            stored, claimCase, storageProvider, processingProvider, fieldValidationRuleRepository, documentRepository, auditRepository,
            correlationId, cancellationToken,
            recognition: httpContext.RequestServices?.GetService(typeof(IDocumentRecognitionService)) as IDocumentRecognitionService,
            proposals: httpContext.RequestServices?.GetService(typeof(IFieldProposalService)) as IFieldProposalService,
            authenticity: httpContext.RequestServices?.GetService(typeof(IAuthenticityService)) as IAuthenticityService);
    }
}

/// <summary>
/// The background pipeline for one uploaded document (build-loop item 17): read it, name it, check
/// it, offer its values, rebuild the slot's PDF, refresh the claim's completeness, and ask for the
/// claim to be reviewed again. A provider failure is retried with a back-off while the customer
/// keeps seeing "being checked"; only when the attempts run out does the document show as unread,
/// with a "Try again" for the customer and a dead-lettered job an administrator can see.
/// </summary>
public sealed class DocumentProcessJobHandler : IBackgroundJobHandler
{
    public const string SystemActorId = "system:document-pipeline";

    public string Queue => BackgroundQueues.DocumentProcessing;

    public string Type => BackgroundJobTypes.DocumentProcess;

    public async Task HandleAsync(BackgroundJob job, IServiceProvider services, CancellationToken cancellationToken)
    {
        services.GetRequiredService<ITenantContextSetter>().SetCurrent(new TenantContext(
            job.Scope.TenantId, job.Scope.WorkspaceId, SystemActorId, [PlatformRole.ClaimsSupervisor.ToString()]));
        if (job.CaseId is not { } caseId || job.DocumentId is not { } documentId)
        {
            throw new JobFailure("The job does not say which document to read.", retry: false);
        }

        var caseRepository = services.GetRequiredService<ICaseRepository>();
        var documentRepository = services.GetRequiredService<ICaseDocumentRepository>();
        var claim = await caseRepository.GetAsync(caseId, cancellationToken);
        var document = claim is null || claim.Scope != job.Scope ? null : await documentRepository.GetAsync(caseId, documentId, job.Scope, cancellationToken);
        if (claim is null || document is null || document.IsSuperseded)
        {
            // The claim or the file is gone, or the customer replaced it: nothing left to read.
            return;
        }

        if (document.ProcessingStatus == DocumentProcessingStatus.Failed)
        {
            document = document with
            {
                ProcessingStatus = DocumentProcessingStatus.Uploaded,
                ValidationWarnings = document.ValidationWarnings.Where(warning => warning.Code != DocumentProcessingRunner.FailedWarningCode).ToArray(),
            };
            await documentRepository.SaveAsync(document, cancellationToken);
        }

        var run = await services.GetRequiredService<DocumentProcessor>().ProcessAsync(document, claim, job.CorrelationId, cancellationToken);
        var lastAttempt = job.Attempts >= job.MaxAttempts;
        if (run.Failed && run.NotConfigured is null && !lastAttempt)
        {
            // Not the end: the customer keeps seeing "being checked" while the next attempt waits.
            await documentRepository.SaveAsync(
                run.Document with
                {
                    ProcessingStatus = DocumentProcessingStatus.Uploaded,
                    ValidationWarnings = run.Document.ValidationWarnings.Where(warning => warning.Code != DocumentProcessingRunner.FailedWarningCode).ToArray(),
                },
                cancellationToken);
        }

        var done = run.Document;
        // The request already built the PDF of a slot the customer chose; only a document the
        // recogniser filed into a slot afterwards still needs it.
        if (!run.Failed && done.SlotKind == DocumentKind.Document && done.DocumentTypeKey is { } slotKey
            && !string.Equals(document.DocumentTypeKey, slotKey, StringComparison.OrdinalIgnoreCase))
        {
            await services.GetRequiredService<ISlotPdfService>().RebuildAsync(job.Scope, caseId, slotKey, job.CorrelationId, cancellationToken);
        }

        await RefreshCompletenessAsync(services, caseId, job.Scope, job.CorrelationId, cancellationToken);

        if (run.Failed)
        {
            throw run.NotConfigured is not null
                ? new JobFailure("Document processing is not configured for this workspace.", retry: false)
                : new JobFailure("The file could not be read by the document service.");
        }

        await services.GetRequiredService<IClaimTriageQueue>().EnqueueAsync(job.Scope, caseId, job.CorrelationId, "document_processed", cancellationToken);
    }

    private static async Task RefreshCompletenessAsync(IServiceProvider services, Guid caseId, InsuranceIntelligence.Domain.Common.TenantScope scope, string correlationId, CancellationToken cancellationToken)
    {
        var caseRepository = services.GetRequiredService<ICaseRepository>();
        var claim = await caseRepository.GetAsync(caseId, cancellationToken);
        if (claim is null || claim.Scope != scope)
        {
            return;
        }

        var documents = await services.GetRequiredService<ICaseDocumentRepository>().ListForCaseAsync(caseId, scope, cancellationToken);
        var checklist = await services.GetRequiredService<IDocumentChecklistProvider>().BuildChecklistAsync(claim, documents, cancellationToken);
        if (claim.CompletenessScore == checklist.CompletenessScore.Percentage)
        {
            return;
        }

        var updated = claim with { CompletenessScore = checklist.CompletenessScore.Percentage, UpdatedAt = DateTimeOffset.UtcNow };
        await caseRepository.SaveAsync(updated, cancellationToken);
        await services.GetRequiredService<IAuditRepository>().WriteAsync(
            AuditEvent.CaseCompletenessUpdated(claim.ToAuditSnapshot(), updated.ToAuditSnapshot(), updated.CompletenessScore, correlationId),
            cancellationToken);
    }
}
