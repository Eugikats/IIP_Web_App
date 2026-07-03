# IIP Backend Business Logic Output - Approval Decision Service

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

This pass implements **approval decision logic only**:

```text
Approval decision service: approve / reject / escalate approval tasks
```

Implemented capabilities:

```text
- Approve, reject, or escalate an existing approval task.
- Enforce approvals.review before any decision action.
- Enforce tenant/workspace scope.
- Enforce human-only final actor for consequential decisions.
- Reject AI, System, or Integration as final actor for consequential decisions.
- Write audit event for every approval decision.
- Return updated approval decision result without changing the reconciled ApprovalQueueRowDto shape.
```

Not implemented in this pass:

```text
- Claim settlement, payment release, repudiation, or final claim outcome workflows.
- External write-back execution.
- Controllers, frontend code, or routes.
```

---

## 2. Files to create or modify

```text
src/InsuranceIntelligence.Application/Approvals/IApprovalDecisionService.cs
src/InsuranceIntelligence.Application/Approvals/ApprovalDecisionService.cs
src/InsuranceIntelligence.Application/Approvals/ApprovalDecisionModels.cs
src/InsuranceIntelligence.Application/Approvals/IApprovalDecisionRepository.cs
src/InsuranceIntelligence.Domain/Approvals/ApprovalDecisionGuard.cs
src/InsuranceIntelligence.Tests/Application/Approvals/ApprovalDecisionServiceTests.cs
src/InsuranceIntelligence.Tests/Domain/Approvals/ApprovalDecisionGuardTests.cs
```

---

## 3. Decision models

```csharp
namespace InsuranceIntelligence.Application.Approvals;

public sealed record ApprovalDecisionRequest(
    string ApprovalId,
    string Decision,
    string? Reason,
    DecisionActorType ActorType);

public sealed record ApprovalDecisionResult(
    string ApprovalId,
    string Status,
    string DecidedByUserId,
    string DecidedAt,
    bool RequiresHumanDecision);

public static class ApprovalDecisionValues
{
    public const string Approve = "approve";
    public const string Reject = "reject";
    public const string Escalate = "escalate";
}

public static class ApprovalStatusValues
{
    public const string Pending = "pending";
    public const string Approved = "approved";
    public const string Rejected = "rejected";
    public const string Escalated = "escalated";
}
```

`DecisionActorType` and `ConsequentialDecisionType` belong in Domain as defined by the backend spec.

---

## 4. Repository port

```csharp
namespace InsuranceIntelligence.Application.Approvals;

public interface IApprovalDecisionRepository
{
    Task<ApprovalDecisionReadModel?> GetForDecisionAsync(
        string tenantId,
        string? workspaceId,
        string approvalId,
        CancellationToken cancellationToken);

    Task SaveDecisionAsync(
        ApprovalDecisionWriteModel decision,
        CancellationToken cancellationToken);
}

public sealed record ApprovalDecisionReadModel(
    string ApprovalId,
    string TenantId,
    string? WorkspaceId,
    string Status,
    string RequestedActionType,
    string? RelatedEntityType,
    string? RelatedEntityId,
    bool RequiresHumanDecision,
    string Priority);

public sealed record ApprovalDecisionWriteModel(
    string ApprovalId,
    string TenantId,
    string? WorkspaceId,
    string NewStatus,
    string DecidedByUserId,
    string? Reason,
    DateTimeOffset DecidedAt,
    string CorrelationId);
```

---

## 5. Domain guard

```csharp
namespace InsuranceIntelligence.Domain.Approvals;

public static class ApprovalDecisionGuard
{
    public static void EnsureHumanForConsequentialDecision(
        bool requiresHumanDecision,
        DecisionActorType actorType,
        string requestedActionType)
    {
        if (!requiresHumanDecision)
        {
            return;
        }

        if (actorType != DecisionActorType.Human)
        {
            throw new DomainException(
                $"Action '{requestedActionType}' requires a human actor and cannot be finalized by '{actorType}'.");
        }
    }
}
```

This Domain class must not reference EF Core, ASP.NET Core, Azure SDKs, or application services.

---

## 6. Service contract

```csharp
namespace InsuranceIntelligence.Application.Approvals;

public interface IApprovalDecisionService
{
    Task<ApprovalDecisionResult> DecideAsync(
        ApprovalDecisionRequest request,
        CancellationToken cancellationToken);
}
```

---

## 7. Implementation

```csharp
namespace InsuranceIntelligence.Application.Approvals;

internal sealed class ApprovalDecisionService : IApprovalDecisionService
{
    private readonly ITenantContextAccessor _tenantContextAccessor;
    private readonly IPermissionService _permissionService;
    private readonly IApprovalDecisionRepository _repository;
    private readonly IAuditWriter _auditWriter;

    public ApprovalDecisionService(
        ITenantContextAccessor tenantContextAccessor,
        IPermissionService permissionService,
        IApprovalDecisionRepository repository,
        IAuditWriter auditWriter)
    {
        _tenantContextAccessor = tenantContextAccessor;
        _permissionService = permissionService;
        _repository = repository;
        _auditWriter = auditWriter;
    }

    public async Task<ApprovalDecisionResult> DecideAsync(
        ApprovalDecisionRequest request,
        CancellationToken cancellationToken)
    {
        Validate(request);

        var context = _tenantContextAccessor.Current;
        await _permissionService.EnsureAsync(Permissions.ApprovalsReview, cancellationToken);

        var approval = await _repository.GetForDecisionAsync(
            context.TenantId,
            context.WorkspaceId,
            request.ApprovalId,
            cancellationToken);

        if (approval is null)
        {
            throw new NotFoundOrNotVisibleException("Approval was not found or is not visible.");
        }

        if (!string.Equals(approval.Status, ApprovalStatusValues.Pending, StringComparison.OrdinalIgnoreCase))
        {
            throw new WorkflowConflictException("Only pending approvals can be decided.");
        }

        ApprovalDecisionGuard.EnsureHumanForConsequentialDecision(
            approval.RequiresHumanDecision,
            request.ActorType,
            approval.RequestedActionType);

        var newStatus = request.Decision switch
        {
            ApprovalDecisionValues.Approve => ApprovalStatusValues.Approved,
            ApprovalDecisionValues.Reject => ApprovalStatusValues.Rejected,
            ApprovalDecisionValues.Escalate => ApprovalStatusValues.Escalated,
            _ => throw new ValidationException("Unsupported approval decision.")
        };

        var decidedAt = DateTimeOffset.UtcNow;

        await _repository.SaveDecisionAsync(new ApprovalDecisionWriteModel(
            ApprovalId: approval.ApprovalId,
            TenantId: context.TenantId,
            WorkspaceId: context.WorkspaceId,
            NewStatus: newStatus,
            DecidedByUserId: context.UserId,
            Reason: request.Reason,
            DecidedAt: decidedAt,
            CorrelationId: context.CorrelationId), cancellationToken);

        await _auditWriter.WriteAsync(new AuditEventCreateRequest(
            TenantId: context.TenantId,
            WorkspaceId: context.WorkspaceId,
            EventType: "approval.completed",
            ActorType: "user",
            ActorUserId: context.UserId,
            Action: newStatus,
            EntityType: "approval",
            EntityId: approval.ApprovalId,
            Summary: $"Approval {approval.ApprovalId} marked {newStatus}.",
            Reason: request.Reason,
            CorrelationId: context.CorrelationId,
            OccurredAt: decidedAt), cancellationToken);

        return new ApprovalDecisionResult(
            ApprovalId: approval.ApprovalId,
            Status: newStatus,
            DecidedByUserId: context.UserId,
            DecidedAt: decidedAt.ToString("O"),
            RequiresHumanDecision: approval.RequiresHumanDecision);
    }

    private static void Validate(ApprovalDecisionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ApprovalId))
        {
            throw new ValidationException("approvalId is required.");
        }

        if (request.Decision is not (ApprovalDecisionValues.Approve or ApprovalDecisionValues.Reject or ApprovalDecisionValues.Escalate))
        {
            throw new ValidationException("decision must be approve, reject, or escalate.");
        }
    }
}
```

---

## 8. Tests to add

```text
ApprovalDecisionServiceTests
- DecideAsync_RequiresApprovalsReviewPermission
- DecideAsync_DoesNotReturnOrModifyCrossTenantApproval
- DecideAsync_WritesAuditEventForEveryDecision
- DecideAsync_RejectsDecisionWhenApprovalIsNotPending
- DecideAsync_BlocksAgentActorForHumanRequiredApproval
- DecideAsync_AllowsHumanActorForHumanRequiredApproval
- DecideAsync_UsesIsoTimestampInResult
```

Domain guard test:

```text
ApprovalDecisionGuardTests
- EnsureHumanForConsequentialDecision_ThrowsForAgent
- EnsureHumanForConsequentialDecision_ThrowsForSystem
- EnsureHumanForConsequentialDecision_ThrowsForIntegration
- EnsureHumanForConsequentialDecision_AllowsHuman
```

Provider failure test:

```text
Not applicable. Approval decision service is database-backed and has no external provider dependency.
```

---

## 9. Completion report template

```text
Service implemented: ApprovalDecisionService.
Human-decision boundary: enforced in Domain guard and application service.
Permission: approvals.review enforced before decision.
Audit: approval.completed written for approve/reject/escalate.
TODOs: Confirm whether escalation should create a new approval task, reassign current task, or both.
No fake data: Confirmed.
Recommended next service: IDocumentUploadService.
```
