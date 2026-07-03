# IIP Backend Business Logic Output - ICompletenessCheckService

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

This pass implements **missing-document and completeness-check logic**:

```text
ICompletenessCheckService
```

Implemented capabilities:

```text
- Detect missing required documents for claims.
- Detect low-confidence documents requiring review.
- Return document gaps using `DocumentGapItemDto` contract where needed by Command Center.
- Enforce documents.review or claims.read depending on read path.
- Filter by tenant/workspace.
- Write audit event when a completeness check is executed for a claim.
```

Not implemented in this pass:

```text
- Upload registration.
- OCR/extraction.
- Human correction.
- Tenant-configurable rule editor UI.
```

---

## 2. Files to create or modify

```text
src/InsuranceIntelligence.Application/Documents/ICompletenessCheckService.cs
src/InsuranceIntelligence.Application/Documents/CompletenessCheckService.cs
src/InsuranceIntelligence.Application/Documents/CompletenessCheckModels.cs
src/InsuranceIntelligence.Application/Documents/ICompletenessRuleRepository.cs
src/InsuranceIntelligence.Application/Documents/IClaimDocumentReadRepository.cs
src/InsuranceIntelligence.Tests/Application/Documents/CompletenessCheckServiceTests.cs
```

---

## 3. Service contract

```csharp
namespace InsuranceIntelligence.Application.Documents;

public interface ICompletenessCheckService
{
    Task<IReadOnlyList<DocumentGapItemDto>> GetMissingDocumentsAsync(
        CancellationToken cancellationToken);

    Task<IReadOnlyList<DocumentGapItemDto>> GetMissingDocumentsAsync(
        string tenantId,
        string? workspaceId,
        CancellationToken cancellationToken);

    Task<ClaimCompletenessResult> CheckClaimCompletenessAsync(
        string claimId,
        CancellationToken cancellationToken);
}
```

---

## 4. Models

```csharp
namespace InsuranceIntelligence.Application.Documents;

public sealed record ClaimCompletenessResult(
    string ClaimId,
    int RequiredCount,
    int ReceivedCount,
    int MissingCount,
    IReadOnlyList<DocumentGapItemDto> Gaps,
    string CheckedAt);

public sealed record RequiredDocumentRule(
    string DocumentType,
    string Reason,
    bool Required);

public sealed record ClaimDocumentPresence(
    string ClaimId,
    string ClaimNumber,
    string DocumentType,
    string Status,
    decimal? Confidence,
    string? DocumentId);
```

Document gap statuses:

```text
missing | low_confidence | needs_review
```

---

## 5. Business rules

```text
- A required document with no matching received document creates status = missing.
- A received document with confidence below threshold creates status = low_confidence.
- A received document with status needs_review creates status = needs_review.
- Accepted/corrected documents above threshold satisfy the requirement.
```

Default confidence threshold:

```text
0.80
```

`// TODO: confirm`: per-product, per-claim-type, and per-tenant required document rules. Until confirmed, repository must read configured rules if present; if no rule exists, return no gaps rather than inventing requirements.

---

## 6. Repository ports

```csharp
namespace InsuranceIntelligence.Application.Documents;

public interface ICompletenessRuleRepository
{
    Task<IReadOnlyList<RequiredDocumentRule>> GetRulesForClaimAsync(
        string tenantId,
        string? workspaceId,
        string claimId,
        CancellationToken cancellationToken);
}

public interface IClaimDocumentReadRepository
{
    Task<IReadOnlyList<ClaimDocumentPresence>> GetDocumentsForClaimAsync(
        string tenantId,
        string? workspaceId,
        string claimId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<string>> GetVisibleOpenClaimIdsAsync(
        string tenantId,
        string? workspaceId,
        CancellationToken cancellationToken);
}
```

---

## 7. Implementation

```csharp
namespace InsuranceIntelligence.Application.Documents;

internal sealed class CompletenessCheckService : ICompletenessCheckService
{
    private const decimal DefaultConfidenceThreshold = 0.80m;

    private readonly ITenantContextAccessor _tenantContextAccessor;
    private readonly IPermissionService _permissionService;
    private readonly ICompletenessRuleRepository _ruleRepository;
    private readonly IClaimDocumentReadRepository _documentRepository;
    private readonly IAuditWriter _auditWriter;

    public CompletenessCheckService(
        ITenantContextAccessor tenantContextAccessor,
        IPermissionService permissionService,
        ICompletenessRuleRepository ruleRepository,
        IClaimDocumentReadRepository documentRepository,
        IAuditWriter auditWriter)
    {
        _tenantContextAccessor = tenantContextAccessor;
        _permissionService = permissionService;
        _ruleRepository = ruleRepository;
        _documentRepository = documentRepository;
        _auditWriter = auditWriter;
    }

    public async Task<IReadOnlyList<DocumentGapItemDto>> GetMissingDocumentsAsync(CancellationToken cancellationToken)
    {
        var context = _tenantContextAccessor.Current;
        await _permissionService.EnsureAsync(Permissions.DocumentsReview, cancellationToken);
        return await GetMissingDocumentsCoreAsync(context, cancellationToken);
    }

    public async Task<IReadOnlyList<DocumentGapItemDto>> GetMissingDocumentsAsync(string tenantId, string? workspaceId, CancellationToken cancellationToken)
    {
        var context = _tenantContextAccessor.Current;
        EnsureContextMatches(context, tenantId, workspaceId);
        await _permissionService.EnsureAsync(Permissions.DocumentsReview, cancellationToken);
        return await GetMissingDocumentsCoreAsync(context, cancellationToken);
    }

    public async Task<ClaimCompletenessResult> CheckClaimCompletenessAsync(string claimId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(claimId)) throw new ValidationException("claimId is required.");

        var context = _tenantContextAccessor.Current;
        await _permissionService.EnsureAsync(Permissions.DocumentsReview, cancellationToken);

        var gaps = await BuildGapsForClaimAsync(context, claimId, cancellationToken);
        var rules = await _ruleRepository.GetRulesForClaimAsync(context.TenantId, context.WorkspaceId, claimId, cancellationToken);
        var docs = await _documentRepository.GetDocumentsForClaimAsync(context.TenantId, context.WorkspaceId, claimId, cancellationToken);

        await _auditWriter.WriteAsync(new AuditEventCreateRequest(
            TenantId: context.TenantId,
            WorkspaceId: context.WorkspaceId,
            EventType: "document.reviewed",
            ActorType: "user",
            ActorUserId: context.UserId,
            Action: "completeness_checked",
            EntityType: "claim",
            EntityId: claimId,
            Summary: "Claim document completeness checked.",
            Reason: null,
            CorrelationId: context.CorrelationId,
            OccurredAt: DateTimeOffset.UtcNow), cancellationToken);

        return new ClaimCompletenessResult(
            ClaimId: claimId,
            RequiredCount: rules.Count(r => r.Required),
            ReceivedCount: docs.Select(d => d.DocumentType).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
            MissingCount: gaps.Count,
            Gaps: gaps,
            CheckedAt: DateTimeOffset.UtcNow.ToString("O"));
    }

    private async Task<IReadOnlyList<DocumentGapItemDto>> GetMissingDocumentsCoreAsync(TenantContext context, CancellationToken ct)
    {
        var claimIds = await _documentRepository.GetVisibleOpenClaimIdsAsync(context.TenantId, context.WorkspaceId, ct);
        var gaps = new List<DocumentGapItemDto>();

        foreach (var claimId in claimIds)
        {
            gaps.AddRange(await BuildGapsForClaimAsync(context, claimId, ct));
        }

        return gaps;
    }

    private async Task<IReadOnlyList<DocumentGapItemDto>> BuildGapsForClaimAsync(TenantContext context, string claimId, CancellationToken ct)
    {
        var rules = await _ruleRepository.GetRulesForClaimAsync(context.TenantId, context.WorkspaceId, claimId, ct);
        var docs = await _documentRepository.GetDocumentsForClaimAsync(context.TenantId, context.WorkspaceId, claimId, ct);
        var gaps = new List<DocumentGapItemDto>();

        foreach (var rule in rules.Where(r => r.Required))
        {
            var matching = docs.FirstOrDefault(d => string.Equals(d.DocumentType, rule.DocumentType, StringComparison.OrdinalIgnoreCase));
            if (matching is null)
            {
                gaps.Add(new DocumentGapItemDto(Guid.NewGuid().ToString("N"), claimId, rule.DocumentType, rule.Reason, CommandCenterDocumentGapStatusValues.Missing, $"/claims/{claimId}/documents"));
                continue;
            }

            if (matching.Confidence is not null && matching.Confidence < DefaultConfidenceThreshold)
            {
                gaps.Add(new DocumentGapItemDto(matching.DocumentId ?? Guid.NewGuid().ToString("N"), claimId, rule.DocumentType, "Document confidence is below review threshold.", CommandCenterDocumentGapStatusValues.LowConfidence, $"/claims/{claimId}/documents"));
            }
            else if (string.Equals(matching.Status, ExtractedFieldStatusValues.NeedsReview, StringComparison.Ordinal))
            {
                gaps.Add(new DocumentGapItemDto(matching.DocumentId ?? Guid.NewGuid().ToString("N"), claimId, rule.DocumentType, "Document requires human review.", CommandCenterDocumentGapStatusValues.NeedsReview, $"/claims/{claimId}/documents"));
            }
        }

        return gaps;
    }

    private static void EnsureContextMatches(TenantContext context, string tenantId, string? workspaceId)
    {
        if (!string.Equals(context.TenantId, tenantId, StringComparison.Ordinal) ||
            !string.Equals(context.WorkspaceId, workspaceId, StringComparison.Ordinal))
        {
            throw new ForbiddenException("Requested tenant/workspace does not match authenticated context.");
        }
    }
}
```

---

## 8. Tests to add

```text
CompletenessCheckServiceTests
- GetMissingDocumentsAsync_RequiresDocumentsReviewPermission
- GetMissingDocumentsAsync_ReturnsOnlyTenantWorkspaceGaps
- GetMissingDocumentsAsync_ReturnsEmptyWhenNoRulesExist
- CheckClaimCompletenessAsync_WritesAuditEvent
- CheckClaimCompletenessAsync_CreatesMissingGapWhenRequiredDocumentAbsent
- CheckClaimCompletenessAsync_CreatesLowConfidenceGapWhenConfidenceBelowThreshold
- CheckClaimCompletenessAsync_RejectsMismatchedTenantWorkspaceOverload
```

Contract test:

```text
CommandCenterBlockContractMatchesFrontendTypesTest should include DocumentGapItemDto values.
```

Provider failure test:

```text
Not applicable. Completeness rules are database-backed and do not call external providers.
```

---

## 9. Completion report template

```text
Service implemented: ICompletenessCheckService.
Logic implemented: configured document-rule based missing/low-confidence detection.
Audit: completeness check writes document.reviewed audit event.
TODOs: Confirm tenant/product-specific required-document rules and confidence thresholds.
No fake data: Confirmed. No configured rules means no invented missing documents.
Recommended next service: Channels query service.
```
