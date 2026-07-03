# IIP Backend Business Logic Output - ISlaRiskService

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

This pass implements **one service only**:

```text
ISlaRiskService
```

Implemented capabilities:

```text
- Compute SLA status from real claim due-date/status data.
- Return at-risk and breached claim items for Command Center `sla_summary` blocks.
- Return SLA status values using the reconciled values: on_track, due_soon, breached.
- Enforce claims.read before returning any SLA claim data.
- Filter every query by tenantId and workspaceId from ITenantContextAccessor.
- Return empty lists when there are no SLA risks; do not fabricate claims.
```

Not implemented in this pass:

```text
- Claim detail/listing logic already belongs to IClaimsQueryService.
- Claim workflow transitions.
- Approval decisions.
- Document workflows.
- Controllers, API routes, frontend code, or migrations.
```

This service is read-only. It does not approve, reject, settle, pay, repudiate, or otherwise make consequential insurance decisions.

---

## 2. Files to create or modify

```text
src/InsuranceIntelligence.Application/Claims/ISlaRiskService.cs
src/InsuranceIntelligence.Application/Claims/SlaRiskService.cs
src/InsuranceIntelligence.Application/Claims/SlaRiskModels.cs
src/InsuranceIntelligence.Application/Claims/IClaimsSlaReadRepository.cs
src/InsuranceIntelligence.Tests/Application/Claims/SlaRiskServiceTests.cs
src/InsuranceIntelligence.Tests/Application/Claims/SlaRiskContractMatchesFrontendTypesTest.cs
```

If these files already exist, extend them instead of creating duplicate services.

---

## 3. Dependencies expected

```csharp
ITenantContextAccessor
IPermissionService
IClock
IAuditWriter // optional for explicit SLA-sensitive read auditing, if project policy requires it.
Permissions.ClaimsRead
ForbiddenException
ValidationException
```

`IClock` is recommended so due-date calculations are testable. If it does not exist, create the smallest application-layer abstraction:

```csharp
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
```

---

## 4. Service contract

```csharp
namespace InsuranceIntelligence.Application.Claims;

public interface ISlaRiskService
{
    Task<IReadOnlyList<SlaRiskItem>> GetAtRiskItemsAsync(
        CancellationToken cancellationToken);

    Task<IReadOnlyList<SlaRiskItem>> GetAtRiskItemsAsync(
        string tenantId,
        string? workspaceId,
        CancellationToken cancellationToken);

    Task<string> CalculateSlaStatusAsync(
        string claimId,
        CancellationToken cancellationToken);
}
```

The overload accepting `tenantId`/`workspaceId` is for internal application services that already resolved server-side context. It must verify the supplied IDs match `ITenantContextAccessor.Current`.

---

## 5. Models

```csharp
namespace InsuranceIntelligence.Application.Claims;

public sealed record SlaRiskItem(
    string ClaimId,
    string ClaimNumber,
    string CustomerName,
    string Severity,
    DateTimeOffset? DueAt);

public sealed record ClaimSlaReadModel(
    string ClaimId,
    string ClaimNumber,
    string CustomerName,
    string Status,
    string Priority,
    DateTimeOffset? SlaDueAt,
    DateTimeOffset LastActivityAt,
    bool IsClosed);
```

Allowed output values:

```text
SlaRiskItem.Severity: warning | danger
SLA status: on_track | due_soon | breached
```

---

## 6. Repository port

```csharp
namespace InsuranceIntelligence.Application.Claims;

public interface IClaimsSlaReadRepository
{
    Task<IReadOnlyList<ClaimSlaReadModel>> GetActiveClaimsWithSlaAsync(
        string tenantId,
        string? workspaceId,
        CancellationToken cancellationToken);

    Task<ClaimSlaReadModel?> GetClaimSlaAsync(
        string tenantId,
        string? workspaceId,
        string claimId,
        CancellationToken cancellationToken);
}
```

The concrete implementation belongs in Infrastructure and must use Azure SQL/EF Core or the existing repository pattern. The application service must not reference EF Core directly unless the repository style already places query services directly over the DbContext in Application by project convention.

---

## 7. Business rules

```text
- Closed claims are not SLA risks.
- Claims without an SLA due date are not marked breached; return on_track unless product rules later define otherwise.
- `breached` means SlaDueAt <= current UTC time.
- `due_soon` means SlaDueAt is in the future but within the configured warning window.
- Default warning window: 24 hours, unless tenant SLA rules are configured later.
- `on_track` means not breached and not due soon.
```

`// TODO: confirm` business decision:

```text
Confirm whether different product lines need different SLA warning windows. Until confirmed, use 24 hours as a service constant and make it easy to replace with tenant-configured SLA rules.
```

---

## 8. Implementation

```csharp
namespace InsuranceIntelligence.Application.Claims;

internal sealed class SlaRiskService : ISlaRiskService
{
    private static readonly TimeSpan DefaultDueSoonWindow = TimeSpan.FromHours(24);

    private readonly ITenantContextAccessor _tenantContextAccessor;
    private readonly IPermissionService _permissionService;
    private readonly IClaimsSlaReadRepository _repository;
    private readonly IClock _clock;

    public SlaRiskService(
        ITenantContextAccessor tenantContextAccessor,
        IPermissionService permissionService,
        IClaimsSlaReadRepository repository,
        IClock clock)
    {
        _tenantContextAccessor = tenantContextAccessor;
        _permissionService = permissionService;
        _repository = repository;
        _clock = clock;
    }

    public async Task<IReadOnlyList<SlaRiskItem>> GetAtRiskItemsAsync(
        CancellationToken cancellationToken)
    {
        var context = _tenantContextAccessor.Current;
        await _permissionService.EnsureAsync(Permissions.ClaimsRead, cancellationToken);
        return await GetAtRiskItemsCoreAsync(context.TenantId, context.WorkspaceId, cancellationToken);
    }

    public async Task<IReadOnlyList<SlaRiskItem>> GetAtRiskItemsAsync(
        string tenantId,
        string? workspaceId,
        CancellationToken cancellationToken)
    {
        var context = _tenantContextAccessor.Current;
        EnsureContextMatches(context, tenantId, workspaceId);
        await _permissionService.EnsureAsync(Permissions.ClaimsRead, cancellationToken);
        return await GetAtRiskItemsCoreAsync(context.TenantId, context.WorkspaceId, cancellationToken);
    }

    public async Task<string> CalculateSlaStatusAsync(
        string claimId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(claimId))
        {
            throw new ValidationException("claimId is required.");
        }

        var context = _tenantContextAccessor.Current;
        await _permissionService.EnsureAsync(Permissions.ClaimsRead, cancellationToken);

        var claim = await _repository.GetClaimSlaAsync(
            context.TenantId,
            context.WorkspaceId,
            claimId,
            cancellationToken);

        if (claim is null)
        {
            throw new NotFoundOrNotVisibleException("Claim was not found or is not visible.");
        }

        return CalculateStatus(claim, _clock.UtcNow);
    }

    private async Task<IReadOnlyList<SlaRiskItem>> GetAtRiskItemsCoreAsync(
        string tenantId,
        string? workspaceId,
        CancellationToken cancellationToken)
    {
        var claims = await _repository.GetActiveClaimsWithSlaAsync(
            tenantId,
            workspaceId,
            cancellationToken);

        return claims
            .Where(c => !c.IsClosed)
            .Select(c => new { Claim = c, Status = CalculateStatus(c, _clock.UtcNow) })
            .Where(x => x.Status is ClaimSlaStatusValues.DueSoon or ClaimSlaStatusValues.Breached)
            .Select(x => new SlaRiskItem(
                ClaimId: x.Claim.ClaimId,
                ClaimNumber: x.Claim.ClaimNumber,
                CustomerName: x.Claim.CustomerName,
                Severity: x.Status == ClaimSlaStatusValues.Breached ? "danger" : "warning",
                DueAt: x.Claim.SlaDueAt))
            .ToList();
    }

    private static string CalculateStatus(ClaimSlaReadModel claim, DateTimeOffset now)
    {
        if (claim.IsClosed || claim.SlaDueAt is null)
        {
            return ClaimSlaStatusValues.OnTrack;
        }

        if (claim.SlaDueAt <= now)
        {
            return ClaimSlaStatusValues.Breached;
        }

        if (claim.SlaDueAt <= now.Add(DefaultDueSoonWindow))
        {
            return ClaimSlaStatusValues.DueSoon;
        }

        return ClaimSlaStatusValues.OnTrack;
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

## 9. Tests to add

```text
SlaRiskServiceTests
- GetAtRiskItemsAsync_ReturnsOnlyCurrentTenantWorkspaceClaims
- GetAtRiskItemsAsync_RequiresClaimsReadPermission
- GetAtRiskItemsAsync_ReturnsEmptyListWhenNoRisksExist
- CalculateSlaStatusAsync_ReturnsBreachedWhenDueDatePast
- CalculateSlaStatusAsync_ReturnsDueSoonWhenWithinWarningWindow
- CalculateSlaStatusAsync_ReturnsOnTrackForClosedClaim
- GetAtRiskItemsAsync_RejectsMismatchedTenantWorkspaceOverload
```

Contract test:

```text
SlaRiskContractMatchesFrontendTypesTest
- serializes SlaRiskItemDto / SlaSummaryBlockDto with type = sla_summary
- emits id, label, severity, dueAt, href as camelCase
- severity is warning or danger only
```

Human-decision guard test:

```text
Not applicable. ISlaRiskService is read-only and contains no consequential decision method.
```

Provider failure test:

```text
Not applicable. ISlaRiskService uses database claim data only and has no external provider dependency.
```

---

## 10. Completion report template

```text
Service implemented: ISlaRiskService.
Logic implemented: Tenant/workspace-scoped SLA risk calculation using real claim due dates and closed/open status.
Permission: claims.read enforced before every query.
Audit: No state changes. Sensitive-read audit not required unless project policy marks SLA risk list as sensitive; add if required.
TODOs: Confirm per-product/per-tenant SLA warning windows.
No fake data: Confirmed. Empty results remain empty.
Recommended next service: IApprovalQueryService / approval decision service.
```
