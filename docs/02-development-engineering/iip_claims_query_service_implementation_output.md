# IIP Backend Business Logic Output - IClaimsQueryService

**Product:** Insurance Intelligence Platform (IIP)  
**Service implemented:** `IClaimsQueryService` only  
**Scope:** Claims query/read business logic for .NET 8 / ASP.NET Core backend  
**Status:** Implementation-ready service output for repository application layer  
**Date:** July 2026  

---

## 1. Scope of this pass

This pass implements **one service only** from the priority list:

```text
IClaimsQueryService
```

Implemented capabilities:

```text
- List claims using tenant/workspace scoped filtering.
- Get claim detail using ClaimDetailDto from backend spec Section 16A.3.
- Get claims needing attention for the Command Center claims_table block.
- Get daily claims summary for Command Center management summaries and metrics.
- Get a safe claim read model for customer-update draft preparation.
- Enforce claims.read before any claims query.
- Enforce resource-level read authorization before returning claim detail.
- Write audit event for sensitive claim detail reads and draft-read lookups.
- Preserve the frontend/backend DTO contract exactly.
- Return empty lists when no rows exist; do not fabricate data.
```

Not implemented in this pass:

```text
- ISlaRiskService
- claim intake
- claim workflow transitions
- approvals
- documents
- channels
- customers
- sales leads
- audit query service
- controllers or API routes
- frontend code
- database migrations
```

This service is read-only. It does not approve, reject, repudiate, settle, pay, bind, price, or make any consequential insurance decision.

---

## 2. Files to create or modify

Use the existing repository structure where present. Do not create parallel architecture if equivalent files already exist.

Recommended files:

```text
src/InsuranceIntelligence.Application/Claims/IClaimsQueryService.cs
src/InsuranceIntelligence.Application/Claims/ClaimsQueryService.cs
src/InsuranceIntelligence.Application/Claims/ClaimsQueryModels.cs
src/InsuranceIntelligence.Application/Claims/IClaimsReadRepository.cs
src/InsuranceIntelligence.Application/Claims/Validation/ClaimQueryValidators.cs
src/InsuranceIntelligence.Tests/Application/Claims/ClaimsQueryServiceTests.cs
src/InsuranceIntelligence.Tests/Application/Claims/ClaimsContractMatchesFrontendTypesTest.cs
```

If `IClaimsQueryService`, DTOs, repositories, validators, or test project files already exist, extend those files instead of duplicating them.

---

## 3. Dependencies expected

The service expects the following shared abstractions to already exist, based on the backend specification and Command Center query-service pattern:

```csharp
ITenantContextAccessor
IPermissionService
IAuthorizationDecisionService
IAuditWriter
Permissions.ClaimsRead
TenantContext
AuditEventCreateRequest
ForbiddenException
NotFoundOrNotVisibleException
ValidationException
```

If any of these are missing in the repository, create the smallest compatible abstraction in the expected application/common layer. Do not bypass authorization or audit logging.

---

## 4. IClaimsQueryService contract

```csharp
using InsuranceIntelligence.Application.CommandCenter.Models;

namespace InsuranceIntelligence.Application.Claims;

public interface IClaimsQueryService
{
    Task<IReadOnlyList<ClaimAttentionRowDto>> ListClaimsAsync(
        ClaimsListQuery query,
        CancellationToken cancellationToken);

    Task<ClaimDetailDto?> GetClaimDetailAsync(
        string claimId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ClaimAttentionRowDto>> GetClaimsNeedingAttentionAsync(
        CancellationToken cancellationToken);

    Task<ClaimsDailySummaryDto> GetDailySummaryAsync(
        CancellationToken cancellationToken);

    Task<ClaimForDraftDto?> GetClaimForDraftAsync(
        string claimId,
        CancellationToken cancellationToken);

    // Compatibility methods for application services that already resolved server-side tenant context,
    // such as CommandCenterQueryService. These methods must verify that the supplied tenant/workspace
    // match ITenantContextAccessor.Current and must never be exposed directly to controllers.
    Task<IReadOnlyList<ClaimAttentionRowDto>> GetClaimsNeedingAttentionAsync(
        string tenantId,
        string? workspaceId,
        CancellationToken cancellationToken);

    Task<ClaimsDailySummaryDto> GetDailySummaryAsync(
        string tenantId,
        string? workspaceId,
        CancellationToken cancellationToken);

    Task<ClaimForDraftDto?> GetClaimAsync(
        string claimId,
        string tenantId,
        string? workspaceId,
        CancellationToken cancellationToken);
}
```

---

## 5. Claims query models

```csharp
namespace InsuranceIntelligence.Application.Claims;

public sealed record ClaimsListQuery(
    string? Status,
    string? Priority,
    string? SlaStatus,
    string? AssignedTo,
    string? Search,
    int Page = 1,
    int PageSize = 50);

public sealed record ClaimsDailySummaryDto(
    int OpenClaims,
    int NewToday,
    int SlaAtRisk,
    int CompletedToday);

public sealed record ClaimForDraftDto(
    string ClaimId,
    string ClaimNumber,
    string CustomerName,
    string Status,
    IReadOnlyList<string> MissingDocuments);
```

`ClaimForDraftDto` is intentionally minimal and customer-communication-safe. It must not contain internal risk notes, fraud indicators, reserves, privileged staff notes, hidden prompts, or raw provider payloads.

---

## 6. Claims read repository port

This is an application-layer port. The concrete implementation belongs in Infrastructure and may use EF Core / Azure SQL. The application service must not depend directly on Azure SDKs or raw provider payloads.

```csharp
namespace InsuranceIntelligence.Application.Claims;

public interface IClaimsReadRepository
{
    Task<IReadOnlyList<ClaimListReadModel>> ListAsync(
        string tenantId,
        string? workspaceId,
        ClaimsListQuery query,
        CancellationToken cancellationToken);

    Task<ClaimDetailReadModel?> GetDetailAsync(
        string tenantId,
        string? workspaceId,
        string claimId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ClaimAttentionReadModel>> GetOpenClaimSummariesAsync(
        string tenantId,
        string? workspaceId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ClaimAttentionReadModel>> GetClaimsNeedingAttentionAsync(
        string tenantId,
        string? workspaceId,
        CancellationToken cancellationToken);

    Task<ClaimsDailySummaryReadModel> GetDailySummaryAsync(
        string tenantId,
        string? workspaceId,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken);

    Task<ClaimForDraftReadModel?> GetClaimForDraftAsync(
        string tenantId,
        string? workspaceId,
        string claimId,
        CancellationToken cancellationToken);
}

public sealed record ClaimListReadModel(
    string TenantId,
    string? WorkspaceId,
    string ClaimId,
    string ClaimNumber,
    string CustomerName,
    string Status,
    string SlaStatus,
    string Priority,
    string? AssignedTo,
    DateTimeOffset LastActivityAt);

public sealed record ClaimAttentionReadModel(
    string TenantId,
    string? WorkspaceId,
    string ClaimId,
    string ClaimNumber,
    string CustomerName,
    string Status,
    string SlaStatus,
    string Priority,
    string? AssignedTo,
    DateTimeOffset LastActivityAt,
    bool HasMissingDocuments,
    bool HasLowConfidenceExtraction,
    bool HasPendingApproval);

public sealed record ClaimsDailySummaryReadModel(
    int OpenClaims,
    int NewToday,
    int SlaAtRisk,
    int CompletedToday);

public sealed record ClaimForDraftReadModel(
    string TenantId,
    string? WorkspaceId,
    string ClaimId,
    string ClaimNumber,
    string CustomerName,
    string Status,
    IReadOnlyList<string> MissingDocuments);
```

---

## 7. Claim detail read model

The read model may contain richer data than the frontend DTO, but the service must only return the reconciled DTO fields defined in backend spec Section 16A.3.

```csharp
using InsuranceIntelligence.Application.CommandCenter.Models;

namespace InsuranceIntelligence.Application.Claims;

public sealed record ClaimDetailReadModel(
    string TenantId,
    string? WorkspaceId,
    string ClaimId,
    string ClaimNumber,
    string PolicyNumber,
    string Status,
    string Priority,
    string SlaStatus,
    string? AssignedOfficer,
    string? ClaimSummary,
    IReadOnlyDictionary<string, string>? CustomerDetails,
    IReadOnlyDictionary<string, string>? PolicyContext,
    IReadOnlyDictionary<string, string>? FnolDetails,
    IReadOnlyList<ClaimTimelineItemReadModel> Timeline,
    IReadOnlyList<ClaimDocumentSummaryReadModel> Documents,
    IReadOnlyList<ClaimCommunicationSummaryReadModel> CustomerCommunicationHistory,
    IReadOnlyList<ClaimInternalNoteReadModel> InternalNotes,
    ClaimAiBriefReadModel? AiClaimBrief,
    IReadOnlyList<AuditEventRowDto> AuditTrail,
    IReadOnlyList<ApprovalQueueRowDto> HumanApprovalLog,
    IReadOnlyList<ClaimWriteBackHistoryItemDto> WriteBackHistory);

public sealed record ClaimTimelineItemReadModel(
    string Id,
    string Title,
    DateTimeOffset OccurredAt,
    string? Description);

public sealed record ClaimDocumentSummaryReadModel(
    string DocumentId,
    string DocumentType,
    string Status,
    decimal? Confidence,
    string? Href);

public sealed record ClaimCommunicationSummaryReadModel(
    string Id,
    string Channel,
    string Summary,
    DateTimeOffset LastActivityAt);

public sealed record ClaimInternalNoteReadModel(
    string Id,
    string? AuthorName,
    string Body,
    DateTimeOffset CreatedAt);

public sealed record ClaimAiBriefReadModel(
    string ShortSummary,
    DateTimeOffset? DateOfLoss,
    string? LocationOfLoss,
    string? PolicyStatus,
    string? CoverageContext,
    IReadOnlyList<string> DocumentsReceived,
    IReadOnlyList<string> DocumentsMissing,
    IReadOnlyList<string> LowConfidenceExtractions,
    string? SlaRisk,
    string? RecommendedNextHumanAction,
    IReadOnlyList<EvidenceSourceDto> SourcesUsed);
```

---

## 8. ClaimsQueryService implementation

```csharp
using InsuranceIntelligence.Application.Audit;
using InsuranceIntelligence.Application.Authorization;
using InsuranceIntelligence.Application.CommandCenter.Models;
using InsuranceIntelligence.Application.Common.Abstractions;
using InsuranceIntelligence.Domain.Audit;
using Microsoft.Extensions.Logging;

namespace InsuranceIntelligence.Application.Claims;

internal sealed class ClaimsQueryService : IClaimsQueryService
{
    private static readonly IReadOnlySet<string> AttentionStatuses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "awaiting_documents",
        "pending_approval",
        "customer_update_pending",
        "escalated",
        "failed"
    };

    private static readonly IReadOnlySet<string> TerminalStatuses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "closed",
        "settled"
    };

    private readonly ITenantContextAccessor _tenantContextAccessor;
    private readonly IPermissionService _permissionService;
    private readonly IAuthorizationDecisionService _authorizationDecisionService;
    private readonly IClaimsReadRepository _repository;
    private readonly IAuditWriter _auditWriter;
    private readonly ILogger<ClaimsQueryService> _logger;

    public ClaimsQueryService(
        ITenantContextAccessor tenantContextAccessor,
        IPermissionService permissionService,
        IAuthorizationDecisionService authorizationDecisionService,
        IClaimsReadRepository repository,
        IAuditWriter auditWriter,
        ILogger<ClaimsQueryService> logger)
    {
        _tenantContextAccessor = tenantContextAccessor;
        _permissionService = permissionService;
        _authorizationDecisionService = authorizationDecisionService;
        _repository = repository;
        _auditWriter = auditWriter;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ClaimAttentionRowDto>> ListClaimsAsync(
        ClaimsListQuery query,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);
        ClaimsListQueryValidator.ValidateAndThrow(query);

        var context = ResolveContext();
        await _permissionService.EnsureAsync(Permissions.ClaimsRead, ct);

        var rows = await _repository.ListAsync(
            context.TenantId,
            context.WorkspaceId,
            query,
            ct);

        return rows
            .Where(row => IsVisibleToContext(row.TenantId, row.WorkspaceId, context))
            .Select(MapListRow)
            .ToList();
    }

    public async Task<ClaimDetailDto?> GetClaimDetailAsync(
        string claimId,
        CancellationToken ct)
    {
        ClaimIdValidator.ValidateAndThrow(claimId);

        var context = ResolveContext();
        await _permissionService.EnsureAsync(Permissions.ClaimsRead, ct);

        var claim = await _repository.GetDetailAsync(
            context.TenantId,
            context.WorkspaceId,
            claimId,
            ct);

        if (claim is null)
        {
            return null;
        }

        EnsureVisible(claim.TenantId, claim.WorkspaceId, context);
        await _authorizationDecisionService.EnsureCanReadClaimAsync(claim.ClaimId, ct);

        await WriteSensitiveReadAuditAsync(
            context,
            claim.ClaimId,
            claim.ClaimNumber,
            action: "claim.detail.viewed",
            ct);

        return MapDetail(claim);
    }

    public async Task<IReadOnlyList<ClaimAttentionRowDto>> GetClaimsNeedingAttentionAsync(
        CancellationToken ct)
    {
        var context = ResolveContext();
        return await GetClaimsNeedingAttentionInternalAsync(context, ct);
    }

    public async Task<IReadOnlyList<ClaimAttentionRowDto>> GetClaimsNeedingAttentionAsync(
        string tenantId,
        string? workspaceId,
        CancellationToken ct)
    {
        var context = ResolveContextAndVerifyServerSuppliedScope(tenantId, workspaceId);
        return await GetClaimsNeedingAttentionInternalAsync(context, ct);
    }

    public async Task<ClaimsDailySummaryDto> GetDailySummaryAsync(CancellationToken ct)
    {
        var context = ResolveContext();
        return await GetDailySummaryInternalAsync(context, ct);
    }

    public async Task<ClaimsDailySummaryDto> GetDailySummaryAsync(
        string tenantId,
        string? workspaceId,
        CancellationToken ct)
    {
        var context = ResolveContextAndVerifyServerSuppliedScope(tenantId, workspaceId);
        return await GetDailySummaryInternalAsync(context, ct);
    }

    public async Task<ClaimForDraftDto?> GetClaimForDraftAsync(
        string claimId,
        CancellationToken ct)
    {
        ClaimIdValidator.ValidateAndThrow(claimId);

        var context = ResolveContext();
        await _permissionService.EnsureAsync(Permissions.ClaimsRead, ct);

        var claim = await _repository.GetClaimForDraftAsync(
            context.TenantId,
            context.WorkspaceId,
            claimId,
            ct);

        if (claim is null)
        {
            return null;
        }

        EnsureVisible(claim.TenantId, claim.WorkspaceId, context);
        await _authorizationDecisionService.EnsureCanReadClaimAsync(claim.ClaimId, ct);

        await WriteSensitiveReadAuditAsync(
            context,
            claim.ClaimId,
            claim.ClaimNumber,
            action: "claim.read_for_draft",
            ct);

        return new ClaimForDraftDto(
            ClaimId: claim.ClaimId,
            ClaimNumber: claim.ClaimNumber,
            CustomerName: claim.CustomerName,
            Status: claim.Status,
            MissingDocuments: claim.MissingDocuments);
    }

    public async Task<ClaimForDraftDto?> GetClaimAsync(
        string claimId,
        string tenantId,
        string? workspaceId,
        CancellationToken ct)
    {
        var context = ResolveContextAndVerifyServerSuppliedScope(tenantId, workspaceId);
        ClaimIdValidator.ValidateAndThrow(claimId);
        await _permissionService.EnsureAsync(Permissions.ClaimsRead, ct);

        var claim = await _repository.GetClaimForDraftAsync(
            context.TenantId,
            context.WorkspaceId,
            claimId,
            ct);

        if (claim is null)
        {
            return null;
        }

        EnsureVisible(claim.TenantId, claim.WorkspaceId, context);
        await _authorizationDecisionService.EnsureCanReadClaimAsync(claim.ClaimId, ct);

        await WriteSensitiveReadAuditAsync(
            context,
            claim.ClaimId,
            claim.ClaimNumber,
            action: "claim.read_for_command_center_draft",
            ct);

        return new ClaimForDraftDto(
            ClaimId: claim.ClaimId,
            ClaimNumber: claim.ClaimNumber,
            CustomerName: claim.CustomerName,
            Status: claim.Status,
            MissingDocuments: claim.MissingDocuments);
    }

    private async Task<IReadOnlyList<ClaimAttentionRowDto>> GetClaimsNeedingAttentionInternalAsync(
        TenantContext context,
        CancellationToken ct)
    {
        await _permissionService.EnsureAsync(Permissions.ClaimsRead, ct);

        var rows = await _repository.GetClaimsNeedingAttentionAsync(
            context.TenantId,
            context.WorkspaceId,
            ct);

        return rows
            .Where(row => IsVisibleToContext(row.TenantId, row.WorkspaceId, context))
            .Where(IsAttentionClaim)
            .OrderByDescending(row => PriorityRank(row.Priority))
            .ThenBy(row => SlaRank(row.SlaStatus))
            .ThenByDescending(row => row.LastActivityAt)
            .Select(MapAttentionRow)
            .ToList();
    }

    private async Task<ClaimsDailySummaryDto> GetDailySummaryInternalAsync(
        TenantContext context,
        CancellationToken ct)
    {
        await _permissionService.EnsureAsync(Permissions.ClaimsRead, ct);

        var now = DateTimeOffset.UtcNow;
        var fromUtc = new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero);
        var toUtc = fromUtc.AddDays(1);

        var summary = await _repository.GetDailySummaryAsync(
            context.TenantId,
            context.WorkspaceId,
            fromUtc,
            toUtc,
            ct);

        return new ClaimsDailySummaryDto(
            OpenClaims: summary.OpenClaims,
            NewToday: summary.NewToday,
            SlaAtRisk: summary.SlaAtRisk,
            CompletedToday: summary.CompletedToday);
    }

    private TenantContext ResolveContext()
    {
        var context = _tenantContextAccessor.Current;
        if (string.IsNullOrWhiteSpace(context.TenantId))
        {
            throw new UnauthorizedAccessException("Tenant context not resolved.");
        }

        if (string.IsNullOrWhiteSpace(context.UserId))
        {
            throw new UnauthorizedAccessException("User context not resolved.");
        }

        return context;
    }

    private TenantContext ResolveContextAndVerifyServerSuppliedScope(string tenantId, string? workspaceId)
    {
        var context = ResolveContext();

        if (!string.Equals(context.TenantId, tenantId, StringComparison.Ordinal))
        {
            throw new ForbiddenException("Requested tenant scope does not match resolved tenant context.");
        }

        if (!string.Equals(context.WorkspaceId, workspaceId, StringComparison.Ordinal))
        {
            throw new ForbiddenException("Requested workspace scope does not match resolved workspace context.");
        }

        return context;
    }

    private static bool IsVisibleToContext(string tenantId, string? workspaceId, TenantContext context)
    {
        return string.Equals(tenantId, context.TenantId, StringComparison.Ordinal)
            && string.Equals(workspaceId, context.WorkspaceId, StringComparison.Ordinal);
    }

    private static void EnsureVisible(string tenantId, string? workspaceId, TenantContext context)
    {
        if (!IsVisibleToContext(tenantId, workspaceId, context))
        {
            throw new ForbiddenException("The requested claim is not visible in the current tenant/workspace context.");
        }
    }

    private static bool IsAttentionClaim(ClaimAttentionReadModel row)
    {
        return row.SlaStatus is ClaimSlaStatusValues.DueSoon or ClaimSlaStatusValues.Breached
            || row.Priority is ClaimPriorityValues.High or ClaimPriorityValues.Critical
            || AttentionStatuses.Contains(row.Status)
            || row.HasMissingDocuments
            || row.HasLowConfidenceExtraction
            || row.HasPendingApproval;
    }

    private static int PriorityRank(string priority) => priority switch
    {
        ClaimPriorityValues.Critical => 4,
        ClaimPriorityValues.High => 3,
        ClaimPriorityValues.Medium => 2,
        ClaimPriorityValues.Low => 1,
        _ => 0
    };

    private static int SlaRank(string slaStatus) => slaStatus switch
    {
        ClaimSlaStatusValues.Breached => 1,
        ClaimSlaStatusValues.DueSoon => 2,
        ClaimSlaStatusValues.OnTrack => 3,
        _ => 4
    };

    private static ClaimAttentionRowDto MapListRow(ClaimListReadModel row)
    {
        return new ClaimAttentionRowDto(
            ClaimId: row.ClaimId,
            ClaimNumber: row.ClaimNumber,
            CustomerName: row.CustomerName,
            Status: row.Status,
            SlaStatus: row.SlaStatus,
            Priority: row.Priority,
            AssignedTo: row.AssignedTo,
            LastActivityAt: row.LastActivityAt.ToString("O"));
    }

    private static ClaimAttentionRowDto MapAttentionRow(ClaimAttentionReadModel row)
    {
        return new ClaimAttentionRowDto(
            ClaimId: row.ClaimId,
            ClaimNumber: row.ClaimNumber,
            CustomerName: row.CustomerName,
            Status: row.Status,
            SlaStatus: row.SlaStatus,
            Priority: row.Priority,
            AssignedTo: row.AssignedTo,
            LastActivityAt: row.LastActivityAt.ToString("O"));
    }

    private static ClaimDetailDto MapDetail(ClaimDetailReadModel claim)
    {
        return new ClaimDetailDto(
            ClaimId: claim.ClaimId,
            ClaimNumber: claim.ClaimNumber,
            PolicyNumber: claim.PolicyNumber,
            Status: claim.Status,
            Priority: claim.Priority,
            SlaStatus: claim.SlaStatus,
            AssignedOfficer: claim.AssignedOfficer,
            ClaimSummary: claim.ClaimSummary,
            CustomerDetails: claim.CustomerDetails,
            PolicyContext: claim.PolicyContext,
            FnolDetails: claim.FnolDetails,
            Timeline: claim.Timeline.Select(x => new ClaimTimelineItemDto(
                Id: x.Id,
                Title: x.Title,
                OccurredAt: x.OccurredAt.ToString("O"),
                Description: x.Description)).ToList(),
            Documents: claim.Documents.Select(x => new ClaimDocumentSummaryDto(
                DocumentId: x.DocumentId,
                DocumentType: x.DocumentType,
                Status: x.Status,
                Confidence: x.Confidence,
                Href: x.Href)).ToList(),
            CustomerCommunicationHistory: claim.CustomerCommunicationHistory.Select(x => new ClaimCommunicationSummaryDto(
                Id: x.Id,
                Channel: x.Channel,
                Summary: x.Summary,
                LastActivityAt: x.LastActivityAt.ToString("O"))).ToList(),
            InternalNotes: claim.InternalNotes.Select(x => new ClaimInternalNoteDto(
                Id: x.Id,
                AuthorName: x.AuthorName,
                Body: x.Body,
                CreatedAt: x.CreatedAt.ToString("O"))).ToList(),
            AiClaimBrief: claim.AiClaimBrief is null ? null : new ClaimAiBriefDto(
                ShortSummary: claim.AiClaimBrief.ShortSummary,
                DateOfLoss: claim.AiClaimBrief.DateOfLoss?.ToString("O"),
                LocationOfLoss: claim.AiClaimBrief.LocationOfLoss,
                PolicyStatus: claim.AiClaimBrief.PolicyStatus,
                CoverageContext: claim.AiClaimBrief.CoverageContext,
                DocumentsReceived: claim.AiClaimBrief.DocumentsReceived,
                DocumentsMissing: claim.AiClaimBrief.DocumentsMissing,
                LowConfidenceExtractions: claim.AiClaimBrief.LowConfidenceExtractions,
                SlaRisk: claim.AiClaimBrief.SlaRisk,
                RecommendedNextHumanAction: claim.AiClaimBrief.RecommendedNextHumanAction,
                SourcesUsed: claim.AiClaimBrief.SourcesUsed),
            AuditTrail: claim.AuditTrail,
            HumanApprovalLog: claim.HumanApprovalLog,
            WriteBackHistory: claim.WriteBackHistory);
    }

    private async Task WriteSensitiveReadAuditAsync(
        TenantContext context,
        string claimId,
        string claimNumber,
        string action,
        CancellationToken ct)
    {
        await _auditWriter.WriteAsync(new AuditEventCreateRequest(
            TenantId: context.TenantId,
            WorkspaceId: context.WorkspaceId,
            EventType: AuditEventTypeValues.DataViewed,
            ActorType: "user",
            ActorUserId: context.UserId,
            Action: action,
            EntityType: "claim",
            EntityId: claimId,
            Summary: $"Claim {claimNumber} read.",
            Reason: "Sensitive claim read.",
            CorrelationId: context.CorrelationId,
            OccurredAt: DateTimeOffset.UtcNow), ct);
    }
}
```

---

## 9. Validation

Use FluentValidation if already installed. If not, keep this minimal pattern until the repository validation framework is confirmed.

```csharp
namespace InsuranceIntelligence.Application.Claims.Validation;

public static class ClaimIdValidator
{
    public static void ValidateAndThrow(string claimId)
    {
        if (string.IsNullOrWhiteSpace(claimId))
        {
            throw new ValidationException("claimId is required.");
        }

        if (claimId.Length > 128)
        {
            throw new ValidationException("claimId exceeds maximum length.");
        }
    }
}

public static class ClaimsListQueryValidator
{
    public static void ValidateAndThrow(ClaimsListQuery query)
    {
        if (query.Page < 1)
        {
            throw new ValidationException("page must be greater than zero.");
        }

        if (query.PageSize is < 1 or > 250)
        {
            throw new ValidationException("pageSize must be between 1 and 250.");
        }

        if (!string.IsNullOrWhiteSpace(query.Priority) && !AllowedPriorities.Contains(query.Priority))
        {
            throw new ValidationException("priority is not a supported value.");
        }

        if (!string.IsNullOrWhiteSpace(query.SlaStatus) && !AllowedSlaStatuses.Contains(query.SlaStatus))
        {
            throw new ValidationException("slaStatus is not a supported value.");
        }
    }

    private static readonly IReadOnlySet<string> AllowedPriorities = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ClaimPriorityValues.Low,
        ClaimPriorityValues.Medium,
        ClaimPriorityValues.High,
        ClaimPriorityValues.Critical
    };

    private static readonly IReadOnlySet<string> AllowedSlaStatuses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ClaimSlaStatusValues.OnTrack,
        ClaimSlaStatusValues.DueSoon,
        ClaimSlaStatusValues.Breached
    };
}
```

---

## 10. Required tests

### 10.1 Tenant isolation test

```csharp
[Fact]
public async Task ListClaimsAsync_DoesNotReturnCrossTenantRecords()
{
    var context = TestTenantContext.For("tenant-a", "workspace-a", "user-1", Permissions.ClaimsRead);
    var repository = new Mock<IClaimsReadRepository>();

    repository.Setup(x => x.ListAsync("tenant-a", "workspace-a", It.IsAny<ClaimsListQuery>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync(new List<ClaimListReadModel>
        {
            new("tenant-a", "workspace-a", "clm-1", "CLM-001", "A Customer", "new", "on_track", "medium", null, DateTimeOffset.UtcNow),
            new("tenant-b", "workspace-b", "clm-2", "CLM-002", "B Customer", "new", "on_track", "medium", null, DateTimeOffset.UtcNow)
        });

    var service = BuildService(context, repository.Object);

    var result = await service.ListClaimsAsync(new ClaimsListQuery(null, null, null, null, null), CancellationToken.None);

    Assert.Single(result);
    Assert.Equal("clm-1", result[0].ClaimId);
}
```

### 10.2 Permission enforcement test

```csharp
[Fact]
public async Task GetClaimDetailAsync_RequiresClaimsReadPermission()
{
    var permissionService = new Mock<IPermissionService>();
    permissionService.Setup(x => x.EnsureAsync(Permissions.ClaimsRead, It.IsAny<CancellationToken>()))
        .ThrowsAsync(new ForbiddenException("Permission denied."));

    var repository = new Mock<IClaimsReadRepository>();
    var service = BuildService(permissionService: permissionService.Object, repository: repository.Object);

    await Assert.ThrowsAsync<ForbiddenException>(() =>
        service.GetClaimDetailAsync("clm-1", CancellationToken.None));

    repository.Verify(x => x.GetDetailAsync(
        It.IsAny<string>(),
        It.IsAny<string?>(),
        It.IsAny<string>(),
        It.IsAny<CancellationToken>()), Times.Never);
}
```

### 10.3 Audit event test for sensitive read

```csharp
[Fact]
public async Task GetClaimDetailAsync_WritesAuditEventForSensitiveRead()
{
    var repository = new Mock<IClaimsReadRepository>();
    repository.Setup(x => x.GetDetailAsync("tenant-a", "workspace-a", "clm-1", It.IsAny<CancellationToken>()))
        .ReturnsAsync(TestClaims.Detail("tenant-a", "workspace-a", "clm-1"));

    var auditWriter = new Mock<IAuditWriter>();
    var service = BuildService(repository: repository.Object, auditWriter: auditWriter.Object);

    var result = await service.GetClaimDetailAsync("clm-1", CancellationToken.None);

    Assert.NotNull(result);
    auditWriter.Verify(x => x.WriteAsync(
        It.Is<AuditEventCreateRequest>(evt =>
            evt.TenantId == "tenant-a" &&
            evt.WorkspaceId == "workspace-a" &&
            evt.EventType == AuditEventTypeValues.DataViewed &&
            evt.EntityType == "claim" &&
            evt.EntityId == "clm-1" &&
            evt.Action == "claim.detail.viewed"),
        It.IsAny<CancellationToken>()), Times.Once);
}
```

### 10.4 Empty result test

```csharp
[Fact]
public async Task ListClaimsAsync_ReturnsEmptyList_WhenNoRowsExist()
{
    var repository = new Mock<IClaimsReadRepository>();
    repository.Setup(x => x.ListAsync("tenant-a", "workspace-a", It.IsAny<ClaimsListQuery>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync(Array.Empty<ClaimListReadModel>());

    var service = BuildService(repository: repository.Object);

    var result = await service.ListClaimsAsync(new ClaimsListQuery(null, null, null, null, null), CancellationToken.None);

    Assert.Empty(result);
}
```

### 10.5 Claim detail DTO JSON contract test

```csharp
[Fact]
public void ClaimsContractMatchesFrontendTypesTest()
{
    var dto = new ClaimDetailDto(
        ClaimId: "clm-1",
        ClaimNumber: "CLM-001",
        PolicyNumber: "POL-001",
        Status: "new",
        Priority: ClaimPriorityValues.High,
        SlaStatus: ClaimSlaStatusValues.DueSoon,
        AssignedOfficer: "Jane Officer",
        ClaimSummary: "Summary",
        CustomerDetails: new Dictionary<string, string> { ["name"] = "Test Customer" },
        PolicyContext: new Dictionary<string, string> { ["product"] = "Motor" },
        FnolDetails: new Dictionary<string, string> { ["lossType"] = "accident" },
        Timeline: new[] { new ClaimTimelineItemDto("t1", "Reported", DateTimeOffset.UtcNow.ToString("O"), "FNOL received") },
        Documents: new[] { new ClaimDocumentSummaryDto("doc-1", "claim_form", "accepted", 0.99m, "/documents/doc-1/review") },
        CustomerCommunicationHistory: new[] { new ClaimCommunicationSummaryDto("msg-1", "email", "Customer emailed claim", DateTimeOffset.UtcNow.ToString("O")) },
        InternalNotes: new[] { new ClaimInternalNoteDto("note-1", "Officer", "Reviewed", DateTimeOffset.UtcNow.ToString("O")) },
        AiClaimBrief: null,
        AuditTrail: Array.Empty<AuditEventRowDto>(),
        HumanApprovalLog: Array.Empty<ApprovalQueueRowDto>(),
        WriteBackHistory: Array.Empty<ClaimWriteBackHistoryItemDto>());

    var json = JsonSerializer.SerializeToDocument(dto, new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    });

    var names = json.RootElement.EnumerateObject().Select(p => p.Name).ToHashSet();

    var expected = new[]
    {
        "claimId",
        "claimNumber",
        "policyNumber",
        "status",
        "priority",
        "slaStatus",
        "assignedOfficer",
        "claimSummary",
        "customerDetails",
        "policyContext",
        "fnolDetails",
        "timeline",
        "documents",
        "customerCommunicationHistory",
        "internalNotes",
        "aiClaimBrief",
        "auditTrail",
        "humanApprovalLog",
        "writeBackHistory"
    };

    Assert.Equal(expected.OrderBy(x => x), names.OrderBy(x => x));
}
```

### 10.6 Human decision guard test

No human-decision guard test is required for `IClaimsQueryService` because this service is read-only and exposes no consequential decision method.

Do not add claim approval, rejection, settlement, payment, repudiation, policy binding, premium change, or fraud-determination methods to this service.

### 10.7 Provider not-configured test

No provider not-configured test is required for `IClaimsQueryService` because this service depends only on the transactional claims read repository and does not call Blob Storage, Azure AI Document Intelligence, Azure AI Search, Azure OpenAI, Microsoft Graph, or Service Bus.

---

## 11. Actual logic implemented

### Tenant and workspace isolation

The service resolves `TenantContext` through `ITenantContextAccessor` on every public call. It passes `TenantId` and `WorkspaceId` to the claims repository and then applies a second in-service visibility guard before mapping to DTOs.

The compatibility overloads that accept `tenantId` and `workspaceId` are safe only for server-side application services that already resolved context. They verify that supplied scope equals `ITenantContextAccessor.Current` and throw `ForbiddenException` if it does not match.

### Permission enforcement

Every method calls:

```csharp
await _permissionService.EnsureAsync(Permissions.ClaimsRead, ct);
```

before repository access. Unauthorized roles receive a denial before any data is queried.

### Resource-level authorization

Claim detail and draft-read methods also call:

```csharp
await _authorizationDecisionService.EnsureCanReadClaimAsync(claim.ClaimId, ct);
```

This preserves assignment, sensitivity, role, and workflow-stage restrictions beyond simple tenant filtering.

### Audit behavior

Sensitive reads write audit events through `IAuditWriter`:

```text
claim.detail.viewed
claim.read_for_draft
claim.read_for_command_center_draft
```

The event category uses `data_viewed`, matching the audit contract. List queries are not audited in this pass to avoid high-volume noise, unless the product owner decides list views are sensitive enough to log.

### No fake data

Empty repository results return empty lists or null. The service never fabricates claims, customers, policies, documents, audit entries, approvals, or metrics.

### DTO contract

The service maps only to the reconciled frontend/backend DTOs:

```text
ClaimAttentionRowDto
ClaimDetailDto
ClaimTimelineItemDto
ClaimDocumentSummaryDto
ClaimCommunicationSummaryDto
ClaimInternalNoteDto
ClaimAiBriefDto
ClaimWriteBackHistoryItemDto
ClaimsDailySummaryDto
ClaimForDraftDto
```

Date/time fields serialize as ISO 8601 round-trip strings using `ToString("O")`.

---

## 12. TODO fields left as best-effort

The backend specification marks several claim fields as `TODO: confirm`. This implementation keeps them best-effort and does not invent final business rules.

| Field or rule | Current implementation | Business decision still needed |
|---|---|---|
| `ClaimDetailDto.Status` | Returned as stored canonical string | Confirm final claim status value set and state machine. |
| `CustomerDetails` | Returned as dictionary | Confirm structured Customer 360 reuse or exact customer detail schema. |
| `PolicyContext` | Returned as dictionary | Confirm policy summary fields and source-system mapping. |
| `FnolDetails` | Returned as dictionary | Confirm FNOL fields by product line. |
| `ClaimDocumentSummaryDto.Status` | Returned as stored document status string | Confirm document status value set for claim detail. |
| `ClaimWriteBackHistoryItemDto.Status` | Returned as stored write-back status string | Confirm write-back status value set. |
| `AttentionStatuses` | Uses explicit known workflow statuses only | Confirm final claim attention logic and status rules. |
| `TerminalStatuses` | Uses `closed` and `settled` | Confirm whether `repudiated`, `withdrawn`, `paid`, or other terminal statuses exist. |

---

## 13. Dependencies that may still be missing

These are likely repository-specific and may need to be created or aligned during implementation:

```text
IClaimsReadRepository concrete EF Core implementation
actual Claim entity/table field names
actual Customer/Policy/FNOL relationship mappings
actual ApprovalQueueRowDto namespace
actual AuditEventRowDto namespace
AuditEventCreateRequest shape if not already matching Command Center logic
ForbiddenException and ValidationException namespace
IPermissionService namespace
IAuthorizationDecisionService behavior for assignment/sensitivity rules
```

If any dependency is missing, add only the smallest required abstraction or stop and report the blocker. Do not introduce mock provider paths into deployable runtime.

---

## 14. Completion report

### 14.1 Service implemented

```text
IClaimsQueryService
```

### 14.2 Tests added

```text
ClaimsQueryService_ListClaimsAsync_DoesNotReturnCrossTenantRecords
ClaimsQueryService_GetClaimDetailAsync_RequiresClaimsReadPermission
ClaimsQueryService_GetClaimDetailAsync_WritesAuditEventForSensitiveRead
ClaimsQueryService_ListClaimsAsync_ReturnsEmptyList_WhenNoRowsExist
ClaimsContractMatchesFrontendTypesTest
```

No human-decision guard test was added because this service is read-only and contains no consequential decision method.

No provider not-configured test was added because this service does not depend on external providers.

### 14.3 Security impact

Improves server-side tenant isolation, workspace isolation, permission enforcement, and sensitive read auditing for claims queries.

### 14.4 Tenant/workspace impact

All queries filter by resolved tenant and workspace. Compatibility methods reject tenant/workspace mismatch against the resolved server-side context.

### 14.5 Authentication/authorization impact

Requires `claims.read` for all methods. Claim detail and draft-read methods also call resource-level authorization.

### 14.6 Data model or migration impact

No migration required in this pass unless the concrete repository discovers missing indexes or fields. The service works against read models and existing claim tables.

### 14.7 AI/human approval impact

No AI output and no consequential human decision are implemented in this service. The service only provides safe read models used by Command Center and claims screens.

### 14.8 UI impact

The returned DTO shapes match the current frontend contracts for claim detail and Command Center claim table rows. No frontend code is changed.

### 14.9 No fake data confirmation

No mock, simulated, seeded-demo, placeholder, hardcoded, fallback, or fake operational data path is introduced into deployable runtime by this service design.

### 14.10 Recommended next service

Next service to implement:

```text
ISlaRiskService
```

Reason: `IClaimsQueryService` now exposes tenant-scoped claim data and daily summaries. `ISlaRiskService` can build on the same repository patterns to compute due-soon and breached SLA items from real due-date/status data.
