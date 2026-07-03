# IIP Backend Business Logic Output - IDocumentExtractionWorkflowService

**Product:** Insurance Intelligence Platform (IIP)  
**Staff application:** IIP Operations  
**Status:** Implementation-ready service output for repository application layer  
**Date:** July 2026  

---

## Shared rules used by this service output

This output follows the same implementation pattern as the corrected `CommandCenterQueryService` logic:

```text
1. Resolve tenant/workspace from ITenantContextAccessor.
2. Enforce the relevant backend permission before any read/write.
3. Apply tenantId and workspaceId filters in the repository/query boundary.
4. Return the exact reconciled frontend/backend DTO shape where applicable.
5. Write audit events for state-changing operations and sensitive reads.
6. Never fabricate records when no data exists.
7. Never let an AI/system actor be the final actor for a consequential insurance decision.
8. Surface missing providers as ProviderNotConfiguredException or ProviderUnavailableException.
9. Use ISO 8601 round-trip timestamps for frontend wire strings.
10. Keep Domain free of EF Core, Azure SDKs, and ASP.NET Core.
```



## 1. Scope of this pass

This pass implements **document extraction workflow logic only**:

```text
IDocumentExtractionWorkflowService
```

Implemented capabilities:

```text
- Start extraction for an uploaded document.
- Check Azure AI Document Intelligence provider health.
- Load document metadata using tenant/workspace scope.
- Call Document Intelligence through provider interface.
- Convert provider fields into `ExtractedFieldDto` wire-compatible records.
- Store extraction results and update document status.
- Write audit events for extraction started/completed/failed.
```

Not implemented in this pass:

```text
- Human correction/review of extracted fields.
- Completeness-check business rules.
- Controller routes or frontend code.
```

---

## 2. Files to create or modify

```text
src/InsuranceIntelligence.Application/Documents/IDocumentExtractionWorkflowService.cs
src/InsuranceIntelligence.Application/Documents/DocumentExtractionWorkflowService.cs
src/InsuranceIntelligence.Application/Documents/DocumentExtractionModels.cs
src/InsuranceIntelligence.Application/Documents/IDocumentExtractionRepository.cs
src/InsuranceIntelligence.Tests/Application/Documents/DocumentExtractionWorkflowServiceTests.cs
src/InsuranceIntelligence.Tests/Application/Documents/DocumentsContractMatchesFrontendTypesTest.cs
```

---

## 3. Service contract

```csharp
namespace InsuranceIntelligence.Application.Documents;

public interface IDocumentExtractionWorkflowService
{
    Task<IReadOnlyList<ExtractedFieldDto>> ProcessExtractionAsync(
        string documentId,
        CancellationToken cancellationToken);
}
```

---

## 4. Repository and provider ports

```csharp
namespace InsuranceIntelligence.Application.Documents;

public interface IDocumentExtractionRepository
{
    Task<DocumentForExtractionReadModel?> GetForExtractionAsync(
        string tenantId,
        string? workspaceId,
        string documentId,
        CancellationToken cancellationToken);

    Task SaveExtractionAsync(
        DocumentExtractionSaveModel extraction,
        CancellationToken cancellationToken);

    Task MarkExtractionFailedAsync(
        string tenantId,
        string? workspaceId,
        string documentId,
        string safeFailureReason,
        CancellationToken cancellationToken);
}

public sealed record DocumentForExtractionReadModel(
    string DocumentId,
    string TenantId,
    string? WorkspaceId,
    string StoragePath,
    string FileName,
    string ContentType,
    string Status);

public sealed record DocumentExtractionSaveModel(
    string DocumentId,
    string TenantId,
    string? WorkspaceId,
    IReadOnlyList<ExtractedFieldDto> Fields,
    string Status,
    DateTimeOffset ExtractedAt,
    string CorrelationId);
```

Provider result shape:

```csharp
public sealed record DocumentExtractionProviderResult(
    IReadOnlyList<DocumentExtractionProviderField> Fields);

public sealed record DocumentExtractionProviderField(
    string Id,
    string FieldName,
    string Label,
    string? Value,
    decimal Confidence,
    int? SourcePage,
    SourceBoundingBoxDto? SourceBoundingBox);
```

---

## 5. Implementation

```csharp
namespace InsuranceIntelligence.Application.Documents;

internal sealed class DocumentExtractionWorkflowService : IDocumentExtractionWorkflowService
{
    private readonly ITenantContextAccessor _tenantContextAccessor;
    private readonly IPermissionService _permissionService;
    private readonly IProviderHealthCheck _providerHealth;
    private readonly IDocumentExtractionProvider _documentExtractionProvider;
    private readonly IDocumentExtractionRepository _repository;
    private readonly IAuditWriter _auditWriter;

    public DocumentExtractionWorkflowService(
        ITenantContextAccessor tenantContextAccessor,
        IPermissionService permissionService,
        IProviderHealthCheck providerHealth,
        IDocumentExtractionProvider documentExtractionProvider,
        IDocumentExtractionRepository repository,
        IAuditWriter auditWriter)
    {
        _tenantContextAccessor = tenantContextAccessor;
        _permissionService = permissionService;
        _providerHealth = providerHealth;
        _documentExtractionProvider = documentExtractionProvider;
        _repository = repository;
        _auditWriter = auditWriter;
    }

    public async Task<IReadOnlyList<ExtractedFieldDto>> ProcessExtractionAsync(
        string documentId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(documentId))
        {
            throw new ValidationException("documentId is required.");
        }

        var context = _tenantContextAccessor.Current;
        await _permissionService.EnsureAsync(Permissions.DocumentsReview, cancellationToken);

        var providerHealth = await _providerHealth.CheckAsync("azure_document_intelligence", cancellationToken);
        if (providerHealth.Status != ProviderStatus.Connected)
        {
            throw new ProviderNotConfiguredException("Azure AI Document Intelligence is not configured.");
        }

        var document = await _repository.GetForExtractionAsync(
            context.TenantId,
            context.WorkspaceId,
            documentId,
            cancellationToken);

        if (document is null)
        {
            throw new NotFoundOrNotVisibleException("Document was not found or is not visible.");
        }

        await WriteAuditAsync(context, documentId, "document.extracted", "extraction_started", "Document extraction started.", cancellationToken);

        try
        {
            var result = await _documentExtractionProvider.ExtractAsync(
                new DocumentExtractionRequest(
                    TenantId: context.TenantId,
                    WorkspaceId: context.WorkspaceId,
                    DocumentId: document.DocumentId,
                    StoragePath: document.StoragePath,
                    ContentType: document.ContentType,
                    CorrelationId: context.CorrelationId),
                cancellationToken);

            var fields = result.Fields.Select(MapField).ToList();

            await _repository.SaveExtractionAsync(new DocumentExtractionSaveModel(
                DocumentId: document.DocumentId,
                TenantId: context.TenantId,
                WorkspaceId: context.WorkspaceId,
                Fields: fields,
                Status: "extracted",
                ExtractedAt: DateTimeOffset.UtcNow,
                CorrelationId: context.CorrelationId), cancellationToken);

            await WriteAuditAsync(context, documentId, "document.extracted", "extraction_completed", "Document extraction completed.", cancellationToken);

            return fields;
        }
        catch (ProviderUnavailableException)
        {
            await _repository.MarkExtractionFailedAsync(context.TenantId, context.WorkspaceId, documentId, "Provider unavailable.", cancellationToken);
            await WriteAuditAsync(context, documentId, "document.extracted", "extraction_failed", "Document extraction failed due to provider unavailability.", cancellationToken);
            throw;
        }
    }

    private static ExtractedFieldDto MapField(DocumentExtractionProviderField field)
    {
        var status = field.Confidence < 0.8m
            ? ExtractedFieldStatusValues.NeedsReview
            : ExtractedFieldStatusValues.Accepted;

        return new ExtractedFieldDto(
            Id: field.Id,
            FieldName: field.FieldName,
            Label: field.Label,
            Value: field.Value ?? string.Empty,
            Confidence: field.Confidence,
            SourcePage: field.SourcePage,
            SourceBoundingBox: field.SourceBoundingBox,
            Status: status);
    }

    private Task WriteAuditAsync(TenantContext context, string documentId, string eventType, string action, string summary, CancellationToken ct) =>
        _auditWriter.WriteAsync(new AuditEventCreateRequest(
            TenantId: context.TenantId,
            WorkspaceId: context.WorkspaceId,
            EventType: eventType,
            ActorType: "system",
            ActorUserId: context.UserId,
            Action: action,
            EntityType: "document",
            EntityId: documentId,
            Summary: summary,
            Reason: null,
            CorrelationId: context.CorrelationId,
            OccurredAt: DateTimeOffset.UtcNow), ct);
}
```

---

## 6. Tests to add

```text
DocumentExtractionWorkflowServiceTests
- ProcessExtractionAsync_RequiresDocumentsReviewPermission
- ProcessExtractionAsync_UsesTenantWorkspaceScope
- ProcessExtractionAsync_ThrowsProviderNotConfiguredWhenDocumentIntelligenceUnavailable
- ProcessExtractionAsync_WritesAuditForStartAndCompletion
- ProcessExtractionAsync_MarksLowConfidenceFieldsNeedsReview
- ProcessExtractionAsync_DoesNotReturnFakeFieldsWhenProviderReturnsEmpty
```

Contract test:

```text
DocumentsContractMatchesFrontendTypesTest
- ExtractedFieldDto serializes to id, fieldName, label, value, confidence, sourcePage, sourceBoundingBox, status
- SourceBoundingBoxDto serializes to x, y, width, height
- status is accepted|needs_review|corrected|rejected
```

Human-decision guard test:

```text
Not applicable. Extraction is not a consequential insurance decision.
```

---

## 7. Completion report template

```text
Service implemented: IDocumentExtractionWorkflowService.
Provider failure: Azure Document Intelligence not configured/unavailable returns safe provider exception.
Audit: extraction started/completed/failed events written.
TODOs: Confirm confidence threshold per document type; default 0.80 used as best-effort.
No fake data: Confirmed.
Recommended next service: IDocumentReviewService.
```
