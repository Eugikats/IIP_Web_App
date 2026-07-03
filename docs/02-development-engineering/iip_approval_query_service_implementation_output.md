# IIP Backend Business Logic Output - IApprovalQueryService

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

This pass implements **approval read/query logic only**:

```text
IApprovalQueryService
```

Implemented capabilities:

```text
- List pending approval tasks for the Approvals workspace.
- Return `ApprovalQueueRowDto` matching backend spec Section 16A.4 and frontend `ApprovalQueueRow`.
- Support Command Center approval queue reads.
- Enforce approvals.read before listing approvals.
- Filter approvals by tenantId and workspaceId from ITenantContextAccessor.
- Optionally filter to approvals visible to the current user/role.
- Return empty lists when no approvals exist.
```

Not implemented in this pass:

```text
- Approve/reject/escalate decisions. See separate ApprovalDecisionService output.
- Claim settlement, payment, repudiation, or final decision workflows.
- Controllers, API routes, frontend code, or migrations.
```

---

## 2. Files to create or modify

```text
src/InsuranceIntelligence.Application/Approvals/IApprovalQueryService.cs
src/InsuranceIntelligence.Application/Approvals/ApprovalQueryService.cs
src/InsuranceIntelligence.Application/Approvals/ApprovalQueryModels.cs
src/InsuranceIntelligence.Application/Approvals/IApprovalReadRepository.cs
src/InsuranceIntelligence.Tests/Application/Approvals/ApprovalQueryServiceTests.cs
src/InsuranceIntelligence.Tests/Application/Approvals/ApprovalsContractMatchesFrontendTypesTest.cs
```

---

## 3. Service contract

```csharp
namespace InsuranceIntelligence.Application.Approvals;

public interface IApprovalQueryService
{
    Task<IReadOnlyList<ApprovalQueueRowDto>> GetPendingApprovalsAsync(
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ApprovalQueueRowDto>> GetPendingApprovalsAsync(
        string tenantId,
        string? workspaceId,
        CancellationToken cancellationToken);
}
```

---

## 4. Repository port

```csharp
namespace InsuranceIntelligence.Application.Approvals;

public interface IApprovalReadRepository
{
    Task<IReadOnlyList<ApprovalReadModel>> GetPendingApprovalsAsync(
        string tenantId,
        string? workspaceId,
        string userId,
        IReadOnlySet<string> permissions,
        CancellationToken cancellationToken);
}

public sealed record ApprovalReadModel(
    string ApprovalId,
    string Title,
    string? RequestedBy,
    string Priority,
    bool RequiresHumanDecision,
    string Status,
    string? AssignedApproverUserId);
```

Allowed priority values:

```text
low | medium | high | critical
```

`Status` is internal to the read model. The wire DTO intentionally excludes status per the reconciled contract.

---

## 5. Implementation

```csharp
namespace InsuranceIntelligence.Application.Approvals;

internal sealed class ApprovalQueryService : IApprovalQueryService
{
    private readonly ITenantContextAccessor _tenantContextAccessor;
    private readonly IPermissionService _permissionService;
    private readonly IApprovalReadRepository _repository;

    public ApprovalQueryService(
        ITenantContextAccessor tenantContextAccessor,
        IPermissionService permissionService,
        IApprovalReadRepository repository)
    {
        _tenantContextAccessor = tenantContextAccessor;
        _permissionService = permissionService;
        _repository = repository;
    }

    public async Task<IReadOnlyList<ApprovalQueueRowDto>> GetPendingApprovalsAsync(
        CancellationToken cancellationToken)
    {
        var context = _tenantContextAccessor.Current;
        await _permissionService.EnsureAsync(Permissions.ApprovalsRead, cancellationToken);
        return await GetPendingApprovalsCoreAsync(context, cancellationToken);
    }

    public async Task<IReadOnlyList<ApprovalQueueRowDto>> GetPendingApprovalsAsync(
        string tenantId,
        string? workspaceId,
        CancellationToken cancellationToken)
    {
        var context = _tenantContextAccessor.Current;
        EnsureContextMatches(context, tenantId, workspaceId);
        await _permissionService.EnsureAsync(Permissions.ApprovalsRead, cancellationToken);
        return await GetPendingApprovalsCoreAsync(context, cancellationToken);
    }

    private async Task<IReadOnlyList<ApprovalQueueRowDto>> GetPendingApprovalsCoreAsync(
        TenantContext context,
        CancellationToken cancellationToken)
    {
        var approvals = await _repository.GetPendingApprovalsAsync(
            context.TenantId,
            context.WorkspaceId,
            context.UserId,
            context.Permissions,
            cancellationToken);

        return approvals
            .Where(a => string.Equals(a.Status, "pending", StringComparison.OrdinalIgnoreCase))
            .Select(a => new ApprovalQueueRowDto(
                Id: a.ApprovalId,
                Title: a.Title,
                RequestedBy: a.RequestedBy,
                Priority: NormalizePriority(a.Priority),
                RequiresHumanDecision: a.RequiresHumanDecision,
                Href: $"/approvals/{a.ApprovalId}"))
            .ToList();
    }

    private static string NormalizePriority(string value) => value switch
    {
        ApprovalPriorityValues.Low => ApprovalPriorityValues.Low,
        ApprovalPriorityValues.Medium => ApprovalPriorityValues.Medium,
        ApprovalPriorityValues.High => ApprovalPriorityValues.High,
        ApprovalPriorityValues.Critical => ApprovalPriorityValues.Critical,
        _ => ApprovalPriorityValues.Medium
    };

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

## 6. Tests to add

```text
ApprovalQueryServiceTests
- GetPendingApprovalsAsync_ReturnsOnlyTenantWorkspaceApprovals
- GetPendingApprovalsAsync_RequiresApprovalsReadPermission
- GetPendingApprovalsAsync_ReturnsEmptyListWhenNonePending
- GetPendingApprovalsAsync_DoesNotReturnCompletedOrRejectedApprovals
- GetPendingApprovalsAsync_RejectsMismatchedTenantWorkspaceOverload
```

Contract test:

```text
ApprovalsContractMatchesFrontendTypesTest
- ApprovalQueueRowDto serializes to id, title, requestedBy, priority, requiresHumanDecision, href
- priority is low|medium|high|critical
- no internal workflow status, tenantId, workspaceId, approver IDs, or audit metadata are emitted
```

Human-decision guard test:

```text
Not applicable in query service. Decision guard belongs to ApprovalDecisionService.
```

Provider failure test:

```text
Not applicable. Approval query service is database-backed and has no external provider dependency.
```

---

## 7. Completion report template

```text
Service implemented: IApprovalQueryService.
Logic implemented: Pending approval queue read, tenant/workspace filtered, permission enforced, DTO contract preserved.
TODOs: Confirm whether approval queue visibility should be assignment-only, role-wide, or both per role.
No fake data: Confirmed. Empty approval queues remain empty.
Recommended next service: ApprovalDecisionService.
```
