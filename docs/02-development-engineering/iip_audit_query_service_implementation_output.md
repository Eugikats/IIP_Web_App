# IIP Backend Business Logic Output - IAuditQueryService

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

This pass implements **audit event listing/query logic**:

```text
IAuditQueryService
```

Implemented capabilities:

```text
- Return audit event rows matching `AuditEventRowDto`.
- Enforce audit.read permission.
- Filter by tenant/workspace.
- Support filters by date range, event type, actor user, target type, target id.
- Exclude raw before/after payloads, hidden prompts, system messages, provider exception payloads, and secrets.
- Return empty list when no audit events match.
```

Not implemented in this pass:

```text
- Audit event write logic; that belongs to IAuditWriter.
- Detailed audit event inspection endpoint.
- Regulatory export workflow.
```

---

## 2. Files to create or modify

```text
src/InsuranceIntelligence.Application/Audit/IAuditQueryService.cs
src/InsuranceIntelligence.Application/Audit/AuditQueryService.cs
src/InsuranceIntelligence.Application/Audit/AuditQueryModels.cs
src/InsuranceIntelligence.Application/Audit/IAuditReadRepository.cs
src/InsuranceIntelligence.Tests/Application/Audit/AuditQueryServiceTests.cs
src/InsuranceIntelligence.Tests/Application/Audit/AuditContractMatchesFrontendTypesTest.cs
```

---

## 3. Service contract

```csharp
namespace InsuranceIntelligence.Application.Audit;

public interface IAuditQueryService
{
    Task<IReadOnlyList<AuditEventRowDto>> ListEventsAsync(
        AuditEventListQuery query,
        CancellationToken cancellationToken);
}
```

---

## 4. Query model

```csharp
namespace InsuranceIntelligence.Application.Audit;

public sealed record AuditEventListQuery(
    string? EventType,
    string? ActorUserId,
    string? TargetType,
    string? TargetId,
    DateTimeOffset? FromUtc,
    DateTimeOffset? ToUtc,
    int Page = 1,
    int PageSize = 100);
```

---

## 5. Repository port

```csharp
namespace InsuranceIntelligence.Application.Audit;

public interface IAuditReadRepository
{
    Task<IReadOnlyList<AuditEventReadModel>> ListEventsAsync(
        string tenantId,
        string? workspaceId,
        AuditEventListQuery query,
        CancellationToken cancellationToken);
}

public sealed record AuditEventReadModel(
    string Id,
    string EventType,
    string? ActorUserId,
    string? TargetType,
    string? TargetId,
    string Summary,
    DateTimeOffset OccurredAt,
    string? CorrelationId);
```

---

## 6. Implementation

```csharp
namespace InsuranceIntelligence.Application.Audit;

internal sealed class AuditQueryService : IAuditQueryService
{
    private const int MaxPageSize = 200;

    private readonly ITenantContextAccessor _tenantContextAccessor;
    private readonly IPermissionService _permissionService;
    private readonly IAuditReadRepository _repository;

    public AuditQueryService(
        ITenantContextAccessor tenantContextAccessor,
        IPermissionService permissionService,
        IAuditReadRepository repository)
    {
        _tenantContextAccessor = tenantContextAccessor;
        _permissionService = permissionService;
        _repository = repository;
    }

    public async Task<IReadOnlyList<AuditEventRowDto>> ListEventsAsync(
        AuditEventListQuery query,
        CancellationToken cancellationToken)
    {
        Validate(query);

        var context = _tenantContextAccessor.Current;
        await _permissionService.EnsureAsync(Permissions.AuditRead, cancellationToken);

        var events = await _repository.ListEventsAsync(context.TenantId, context.WorkspaceId, query, cancellationToken);

        return events.Select(e => new AuditEventRowDto(
            Id: e.Id,
            EventType: NormalizeEventType(e.EventType),
            ActorUserId: e.ActorUserId,
            TargetType: e.TargetType,
            TargetId: e.TargetId,
            Summary: e.Summary,
            OccurredAt: e.OccurredAt.ToString("O"),
            CorrelationId: e.CorrelationId))
            .ToList();
    }

    private static void Validate(AuditEventListQuery query)
    {
        if (query.Page < 1) throw new ValidationException("page must be greater than zero.");
        if (query.PageSize < 1 || query.PageSize > MaxPageSize) throw new ValidationException($"pageSize must be between 1 and {MaxPageSize}.");
        if (query.FromUtc is not null && query.ToUtc is not null && query.FromUtc > query.ToUtc) throw new ValidationException("fromUtc must be before toUtc.");
    }

    private static string NormalizeEventType(string eventType)
    {
        var allowed = new[]
        {
            AuditEventTypeValues.UserLogin,
            AuditEventTypeValues.DataViewed,
            AuditEventTypeValues.DataChanged,
            AuditEventTypeValues.DocumentUploaded,
            AuditEventTypeValues.AiExtractionGenerated,
            AuditEventTypeValues.HumanCorrectionMade,
            AuditEventTypeValues.ClaimDecisionMade,
            AuditEventTypeValues.CustomerCommunicationSent,
            AuditEventTypeValues.ConsentCaptured,
            AuditEventTypeValues.WorkflowChanged,
            AuditEventTypeValues.ReportExported
        };

        return allowed.Contains(eventType, StringComparer.Ordinal) ? eventType : AuditEventTypeValues.DataChanged;
    }
}
```

---

## 7. Tests to add

```text
AuditQueryServiceTests
- ListEventsAsync_RequiresAuditReadPermission
- ListEventsAsync_ReturnsOnlyTenantWorkspaceEvents
- ListEventsAsync_ReturnsEmptyWhenNoEventsMatch
- ListEventsAsync_RejectsInvalidPaging
- ListEventsAsync_RejectsInvalidDateRange
- ListEventsAsync_DoesNotExposeRawPayloadsPromptsSecretsOrProviderExceptions
- ListEventsAsync_UsesIsoOccurredAt
```

Contract test:

```text
AuditContractMatchesFrontendTypesTest
- AuditEventRowDto serializes to id, eventType, actorUserId, targetType, targetId, summary, occurredAt, correlationId
- eventType values match frontend AuditEventType union
```

Human-decision guard test:

```text
Not applicable. Audit query service is read-only.
```

Provider failure test:

```text
Not applicable. Audit query reads canonical database audit rows only.
```

---

## 8. Completion report template

```text
Service implemented: IAuditQueryService.
Logic implemented: filtered audit row listing with audit.read and tenant/workspace scope.
TODOs: Confirm targetType value set and whether unknown event types should be rejected instead of normalized.
No fake data: Confirmed.
Recommended next service: move to implementation hardening/contract tests across all services.
```
