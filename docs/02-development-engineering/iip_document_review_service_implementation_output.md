# IIP Backend Business Logic Output - IDocumentReviewService

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

This pass implements **human review and correction of extracted document fields**:

```text
IDocumentReviewService
```

Implemented capabilities:

```text
- Read extracted fields for document review using ExtractedFieldDto.
- Correct an extracted field as a human reviewer.
- Mark an extracted field accepted or rejected.
- Enforce documents.review permission.
- Filter by tenant/workspace.
- Write audit event for sensitive review reads and all corrections.
```

Not implemented in this pass:

```text
- Upload registration.
- Provider extraction.
- Missing-document detection.
- Controllers, API routes, or frontend code.
```

---

## 2. Files to create or modify

```text
src/InsuranceIntelligence.Application/Documents/IDocumentReviewService.cs
src/InsuranceIntelligence.Application/Documents/DocumentReviewService.cs
src/InsuranceIntelligence.Application/Documents/DocumentReviewModels.cs
src/InsuranceIntelligence.Application/Documents/IDocumentReviewRepository.cs
src/InsuranceIntelligence.Tests/Application/Documents/DocumentReviewServiceTests.cs
```

---

## 3. Service contract

```csharp
namespace InsuranceIntelligence.Application.Documents;

public interface IDocumentReviewService
{
    Task<IReadOnlyList<ExtractedFieldDto>> GetExtractedFieldsAsync(
        string documentId,
        CancellationToken cancellationToken);

    Task<ExtractedFieldDto> CorrectFieldAsync(
        CorrectExtractedFieldRequest request,
        CancellationToken cancellationToken);

    Task<ExtractedFieldDto> SetFieldStatusAsync(
        SetExtractedFieldStatusRequest request,
        CancellationToken cancellationToken);
}
```

---

## 4. Models

```csharp
namespace InsuranceIntelligence.Application.Documents;

public sealed record CorrectExtractedFieldRequest(
    string DocumentId,
    string FieldId,
    string CorrectedValue,
    string? Reason);

public sealed record SetExtractedFieldStatusRequest(
    string DocumentId,
    string FieldId,
    string Status,
    string? Reason);
```

Allowed statuses:

```text
accepted | needs_review | corrected | rejected
```

---

## 5. Repository port

```csharp
namespace InsuranceIntelligence.Application.Documents;

public interface IDocumentReviewRepository
{
    Task<IReadOnlyList<ExtractedFieldDto>> GetFieldsAsync(
        string tenantId,
        string? workspaceId,
        string documentId,
        CancellationToken cancellationToken);

    Task<ExtractedFieldDto?> CorrectFieldAsync(
        string tenantId,
        string? workspaceId,
        string documentId,
        string fieldId,
        string correctedValue,
        string correctedByUserId,
        DateTimeOffset correctedAt,
        CancellationToken cancellationToken);

    Task<ExtractedFieldDto?> SetFieldStatusAsync(
        string tenantId,
        string? workspaceId,
        string documentId,
        string fieldId,
        string status,
        string updatedByUserId,
        DateTimeOffset updatedAt,
        CancellationToken cancellationToken);
}
```

---

## 6. Implementation

```csharp
namespace InsuranceIntelligence.Application.Documents;

internal sealed class DocumentReviewService : IDocumentReviewService
{
    private readonly ITenantContextAccessor _tenantContextAccessor;
    private readonly IPermissionService _permissionService;
    private readonly IDocumentReviewRepository _repository;
    private readonly IAuditWriter _auditWriter;

    public DocumentReviewService(
        ITenantContextAccessor tenantContextAccessor,
        IPermissionService permissionService,
        IDocumentReviewRepository repository,
        IAuditWriter auditWriter)
    {
        _tenantContextAccessor = tenantContextAccessor;
        _permissionService = permissionService;
        _repository = repository;
        _auditWriter = auditWriter;
    }

    public async Task<IReadOnlyList<ExtractedFieldDto>> GetExtractedFieldsAsync(
        string documentId,
        CancellationToken cancellationToken)
    {
        ValidateId(documentId, nameof(documentId));
        var context = _tenantContextAccessor.Current;
        await _permissionService.EnsureAsync(Permissions.DocumentsReview, cancellationToken);

        var fields = await _repository.GetFieldsAsync(
            context.TenantId,
            context.WorkspaceId,
            documentId,
            cancellationToken);

        await WriteAuditAsync(context, documentId, "data_viewed", "document_fields_viewed", "Document extraction fields viewed.", null, cancellationToken);
        return fields;
    }

    public async Task<ExtractedFieldDto> CorrectFieldAsync(
        CorrectExtractedFieldRequest request,
        CancellationToken cancellationToken)
    {
        ValidateId(request.DocumentId, nameof(request.DocumentId));
        ValidateId(request.FieldId, nameof(request.FieldId));
        if (string.IsNullOrWhiteSpace(request.CorrectedValue)) throw new ValidationException("correctedValue is required.");

        var context = _tenantContextAccessor.Current;
        await _permissionService.EnsureAsync(Permissions.DocumentsReview, cancellationToken);

        var result = await _repository.CorrectFieldAsync(
            context.TenantId,
            context.WorkspaceId,
            request.DocumentId,
            request.FieldId,
            request.CorrectedValue,
            context.UserId,
            DateTimeOffset.UtcNow,
            cancellationToken);

        if (result is null)
        {
            throw new NotFoundOrNotVisibleException("Field was not found or is not visible.");
        }

        await WriteAuditAsync(context, request.DocumentId, "document.reviewed", "field_corrected", "Document extracted field corrected by human reviewer.", request.Reason, cancellationToken);
        return result;
    }

    public async Task<ExtractedFieldDto> SetFieldStatusAsync(
        SetExtractedFieldStatusRequest request,
        CancellationToken cancellationToken)
    {
        ValidateId(request.DocumentId, nameof(request.DocumentId));
        ValidateId(request.FieldId, nameof(request.FieldId));
        ValidateStatus(request.Status);

        var context = _tenantContextAccessor.Current;
        await _permissionService.EnsureAsync(Permissions.DocumentsReview, cancellationToken);

        var result = await _repository.SetFieldStatusAsync(
            context.TenantId,
            context.WorkspaceId,
            request.DocumentId,
            request.FieldId,
            request.Status,
            context.UserId,
            DateTimeOffset.UtcNow,
            cancellationToken);

        if (result is null)
        {
            throw new NotFoundOrNotVisibleException("Field was not found or is not visible.");
        }

        await WriteAuditAsync(context, request.DocumentId, "document.reviewed", $"field_{request.Status}", "Document extracted field status updated by human reviewer.", request.Reason, cancellationToken);
        return result;
    }

    private static void ValidateId(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ValidationException($"{name} is required.");
    }

    private static void ValidateStatus(string status)
    {
        var allowed = new[]
        {
            ExtractedFieldStatusValues.Accepted,
            ExtractedFieldStatusValues.NeedsReview,
            ExtractedFieldStatusValues.Corrected,
            ExtractedFieldStatusValues.Rejected
        };

        if (!allowed.Contains(status, StringComparer.Ordinal))
        {
            throw new ValidationException("Invalid extracted field status.");
        }
    }

    private Task WriteAuditAsync(TenantContext context, string documentId, string eventType, string action, string summary, string? reason, CancellationToken ct) =>
        _auditWriter.WriteAsync(new AuditEventCreateRequest(
            TenantId: context.TenantId,
            WorkspaceId: context.WorkspaceId,
            EventType: eventType,
            ActorType: "user",
            ActorUserId: context.UserId,
            Action: action,
            EntityType: "document",
            EntityId: documentId,
            Summary: summary,
            Reason: reason,
            CorrelationId: context.CorrelationId,
            OccurredAt: DateTimeOffset.UtcNow), ct);
}
```

---

## 7. Tests to add

```text
DocumentReviewServiceTests
- GetExtractedFieldsAsync_RequiresDocumentsReviewPermission
- GetExtractedFieldsAsync_ReturnsOnlyTenantWorkspaceFields
- GetExtractedFieldsAsync_WritesSensitiveReadAudit
- CorrectFieldAsync_WritesAuditEvent
- CorrectFieldAsync_RejectsEmptyCorrectedValue
- SetFieldStatusAsync_RejectsUnsupportedStatus
- SetFieldStatusAsync_DoesNotModifyCrossTenantField
```

Contract test:

```text
DocumentsContractMatchesFrontendTypesTest should already cover ExtractedFieldDto.
```

Human-decision guard test:

```text
Not applicable. Field review/correction is human-owned operational data correction, not final claim decision.
```

Provider failure test:

```text
Not applicable. Review service does not call external providers.
```

---

## 8. Completion report template

```text
Service implemented: IDocumentReviewService.
Logic implemented: field review read, correction, status update, audit writing, tenant/permission enforcement.
TODOs: Confirm whether rejected field must require reason.
No fake data: Confirmed.
Recommended next service: ICompletenessCheckService.
```
