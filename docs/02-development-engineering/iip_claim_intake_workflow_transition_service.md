# IIP Claim Intake and Workflow Transition Service

**Product:** Insurance Intelligence Platform (IIP)  
**Staff application:** IIP Operations  
**Document type:** Implementation-ready Application-layer service output  
**Area:** Claims intake and claim workflow transitions  
**Status:** New-repository implementation specification  
**Date:** July 2026  

---

## Shared rules used by this service output

This output follows the same implementation pattern used by the existing IIP Application-layer service-output documents for Command Center, Claims, SLA, Approvals, Documents, Channels, Customers, Sales & Leads, and Audit.

```text
1. Resolve tenant/workspace from ITenantContextAccessor.
2. Enforce the relevant backend permission before any read/write.
3. Apply tenantId and workspaceId filters in the repository/query boundary.
4. Return DTOs that are stable, explicit, and frontend-safe.
5. Write audit events for every state-changing operation and every sensitive read.
6. Never fabricate records when no data exists.
7. Never let an AI/system actor be the final actor for a consequential insurance decision.
8. Surface missing providers as ProviderNotConfiguredException or ProviderUnavailableException where a provider is required.
9. Use ISO 8601 round-trip timestamps for frontend wire strings.
10. Keep Domain free of EF Core, Azure SDKs, and ASP.NET Core.
11. Never trust client-supplied tenant ID, workspace ID, claim number, role, or permission values.
12. Keep test doubles physically isolated in LocalTest/test projects only.
```

Fixed inputs consumed by this service:

```text
ITenantContextAccessor
IPermissionService
IAuditWriter
Permissions.ClaimsCreate
Permissions.ClaimsAssign
Permissions.ClaimsTransition, if missing, add as "claims.transition"
WorkflowConflictException
ValidationException
ForbiddenException
NotFoundOrNotVisibleException
ClaimDetailDto / ClaimAttentionRowDto remain owned by IClaimsQueryService and are not modified here.
```

This document is additive. It does not replace or modify `IClaimsQueryService`, `ClaimDetailDto`, or `ClaimAttentionRowDto`.

---

## 1. Scope of this pass

This pass implements two new Application-layer services:

```text
IClaimIntakeService
IClaimWorkflowTransitionService
```

Implemented capabilities:

```text
- Create a new FNOL / first-notice-of-loss claim intake record.
- Validate mandatory FNOL fields before touching the domain/persistence boundary.
- Generate the canonical claim number server-side; never accept a client-supplied claim number.
- Enforce claims.create before claim intake creation.
- Persist the claim in canonical new status.
- Write claim.intake.created audit event.
- Transition an existing claim through an explicit state-machine graph only.
- Reject arbitrary status writes.
- Enforce claims.assign for assignment-style transition to assigned.
- Enforce claims.transition for normal workflow transitions.
- Require an existing completed human approval record before moving a claim into approved or rejected status.
- Write workflow.changed audit events with before/after status.
```

Not implemented in this pass:

```text
- Claim approval, claim rejection, repudiation, settlement, payment release, or final claims decision logic.
- External core-system write-back.
- Customer notification sending.
- Document upload/extraction/review.
- Controllers or API routes.
- Frontend code.
- Database migrations.
```

Human-decision boundary:

```text
This service may update a claim workflow status only after the required human approval evidence already exists.
It must not itself approve, reject, settle, repudiate, release payment, or make a final consequential insurance decision.
ApprovalDecisionService remains the owner of approval/rejection decisions.
```

---

## 2. Files to create or modify

Use the existing repository structure where present. Do not create parallel architecture if equivalent files already exist.

```text
src/InsuranceIntelligence.Application/Claims/IClaimIntakeService.cs
src/InsuranceIntelligence.Application/Claims/IClaimWorkflowTransitionService.cs
src/InsuranceIntelligence.Application/Claims/ClaimIntakeService.cs
src/InsuranceIntelligence.Application/Claims/ClaimWorkflowTransitionService.cs
src/InsuranceIntelligence.Application/Claims/ClaimIntakeModels.cs
src/InsuranceIntelligence.Application/Claims/ClaimWorkflowModels.cs
src/InsuranceIntelligence.Application/Claims/IClaimIntakeRepository.cs
src/InsuranceIntelligence.Application/Claims/IClaimWorkflowRepository.cs
src/InsuranceIntelligence.Application/Claims/IClaimNumberGenerator.cs
src/InsuranceIntelligence.Application/Claims/IClaimApprovalEvidenceRepository.cs
src/InsuranceIntelligence.Application/Claims/Validation/ClaimIntakeValidators.cs
src/InsuranceIntelligence.Application/Claims/Validation/ClaimWorkflowTransitionValidators.cs
src/InsuranceIntelligence.Application/Common/Authorization/Permissions.cs
src/InsuranceIntelligence.Application/Common/Exceptions/WorkflowConflictException.cs
src/InsuranceIntelligence.Tests/Application/Claims/ClaimIntakeServiceTests.cs
src/InsuranceIntelligence.Tests/Application/Claims/ClaimWorkflowTransitionServiceTests.cs
src/InsuranceIntelligence.Tests/Application/Claims/ClaimIntakeContractMatchesFrontendTypesTest.cs
```

If `Permissions.ClaimsTransition` does not exist yet, add:

```csharp
public const string ClaimsTransition = "claims.transition";
```

If `WorkflowConflictException` does not exist yet, add it in Application common exceptions:

```csharp
namespace InsuranceIntelligence.Application.Common.Exceptions;

public sealed class WorkflowConflictException : Exception
{
    public WorkflowConflictException(string message) : base(message) { }
}
```

---

## 3. Claim status constants and transition graph

### 3.1 Status constants

Use only the subset of statuses already referenced elsewhere in the existing IIP service documents.

```csharp
namespace InsuranceIntelligence.Application.Claims;

public static class ClaimStatusValues
{
    public const string New = "new";
    public const string Assigned = "assigned";
    public const string AwaitingDocuments = "awaiting_documents";
    public const string PendingApproval = "pending_approval";
    public const string Approved = "approved";
    public const string Rejected = "rejected";
    public const string Closed = "closed";
    public const string Escalated = "escalated";
}
```

`// TODO: confirm`: final canonical claim status enum and full state machine. This document intentionally uses only the subset needed to support the currently drafted IIP services.

### 3.2 Transition graph

Allowed graph for this pass:

```text
new -> assigned
assigned -> awaiting_documents
awaiting_documents -> pending_approval
pending_approval -> approved
pending_approval -> rejected
approved -> closed
rejected -> closed

Any non-terminal state -> escalated
escalated -> assigned
escalated -> awaiting_documents
escalated -> pending_approval

closed is terminal
```

Important rule:

```text
pending_approval -> approved
pending_approval -> rejected
```

is permitted only if a corresponding completed human approval decision already exists for the claim and target outcome. The transition service does not perform that approval/rejection decision. It only verifies the evidence and then updates workflow status.

```csharp
namespace InsuranceIntelligence.Application.Claims;

public static class ClaimWorkflowTransitionGraph
{
    private static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> AllowedTransitions =
        new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)
        {
            [ClaimStatusValues.New] = new HashSet<string>(StringComparer.Ordinal)
            {
                ClaimStatusValues.Assigned,
                ClaimStatusValues.Escalated
            },
            [ClaimStatusValues.Assigned] = new HashSet<string>(StringComparer.Ordinal)
            {
                ClaimStatusValues.AwaitingDocuments,
                ClaimStatusValues.Escalated
            },
            [ClaimStatusValues.AwaitingDocuments] = new HashSet<string>(StringComparer.Ordinal)
            {
                ClaimStatusValues.PendingApproval,
                ClaimStatusValues.Escalated
            },
            [ClaimStatusValues.PendingApproval] = new HashSet<string>(StringComparer.Ordinal)
            {
                ClaimStatusValues.Approved,
                ClaimStatusValues.Rejected,
                ClaimStatusValues.Escalated
            },
            [ClaimStatusValues.Approved] = new HashSet<string>(StringComparer.Ordinal)
            {
                ClaimStatusValues.Closed
            },
            [ClaimStatusValues.Rejected] = new HashSet<string>(StringComparer.Ordinal)
            {
                ClaimStatusValues.Closed
            },
            [ClaimStatusValues.Escalated] = new HashSet<string>(StringComparer.Ordinal)
            {
                ClaimStatusValues.Assigned,
                ClaimStatusValues.AwaitingDocuments,
                ClaimStatusValues.PendingApproval
            },
            [ClaimStatusValues.Closed] = new HashSet<string>(StringComparer.Ordinal)
        };

    public static bool IsAllowed(string currentStatus, string requestedStatus)
    {
        return AllowedTransitions.TryGetValue(currentStatus, out var allowed)
            && allowed.Contains(requestedStatus);
    }

    public static bool IsTerminal(string status) => status is ClaimStatusValues.Closed;

    public static bool IsApprovalOutcomeStatus(string status) =>
        status is ClaimStatusValues.Approved or ClaimStatusValues.Rejected;

    public static IReadOnlyDictionary<string, IReadOnlySet<string>> Snapshot() => AllowedTransitions;
}
```

---

## 4. Claim intake service contract

```csharp
namespace InsuranceIntelligence.Application.Claims;

public interface IClaimIntakeService
{
    Task<ClaimIntakeResultDto> CreateFnolAsync(
        ClaimIntakeRequest request,
        CancellationToken cancellationToken);
}
```

---

## 5. Claim intake models

### 5.1 Request

```csharp
namespace InsuranceIntelligence.Application.Claims;

public sealed record ClaimIntakeRequest(
    string CustomerId,
    string PolicyNumber,
    string ProductLine,
    string LossType,
    DateTimeOffset LossDate,
    string LossLocation,
    string IncidentDescription,
    string ReportedByName,
    string ReportedByContact,
    string Channel,
    IReadOnlyDictionary<string, string>? FnolDetails = null);
```

Rules:

```text
- The request must not contain ClaimId or ClaimNumber.
- The backend generates ClaimId and canonical ClaimNumber.
- TenantId and WorkspaceId are not accepted from the client; they come from ITenantContextAccessor.Current.
- FnolDetails may carry product-specific fields, but no final business rule should depend on unconfirmed fields.
```

`// TODO: confirm`: FNOL required-field set per product line. This pass validates only the shared minimum fields listed above.

### 5.2 Response

```csharp
namespace InsuranceIntelligence.Application.Claims;

public sealed record ClaimIntakeResultDto(
    string ClaimId,
    string ClaimNumber,
    string Status,
    string CreatedAt);
```

Wire contract:

```json
{
  "claimId": "clm_...",
  "claimNumber": "CLM-...",
  "status": "new",
  "createdAt": "2026-07-02T10:15:30.0000000+00:00"
}
```

---

## 6. Claim workflow transition service contract

```csharp
namespace InsuranceIntelligence.Application.Claims;

public interface IClaimWorkflowTransitionService
{
    Task<ClaimWorkflowTransitionResultDto> TransitionAsync(
        string claimId,
        string requestedStatus,
        string? reason,
        CancellationToken cancellationToken);
}
```

---

## 7. Claim workflow models

```csharp
namespace InsuranceIntelligence.Application.Claims;

public sealed record ClaimWorkflowTransitionResultDto(
    string ClaimId,
    string ClaimNumber,
    string PreviousStatus,
    string CurrentStatus,
    string TransitionedByUserId,
    string TransitionedAt);
```

Internal read/write models:

```csharp
namespace InsuranceIntelligence.Application.Claims;

public sealed record ClaimForWorkflowReadModel(
    string ClaimId,
    string ClaimNumber,
    string TenantId,
    string? WorkspaceId,
    string Status,
    string? AssignedToUserId,
    DateTimeOffset UpdatedAt);

public sealed record ClaimWorkflowTransitionWriteModel(
    string ClaimId,
    string TenantId,
    string? WorkspaceId,
    string PreviousStatus,
    string NewStatus,
    string TransitionedByUserId,
    string? Reason,
    DateTimeOffset TransitionedAt,
    string CorrelationId);
```

---

## 8. Repository and supporting ports

These are Application-layer ports. Concrete implementations belong in Infrastructure and should follow the existing EF repository convention.

### 8.1 Claim intake repository

```csharp
namespace InsuranceIntelligence.Application.Claims;

public interface IClaimIntakeRepository
{
    Task CreateAsync(
        ClaimIntakeCreateModel claim,
        CancellationToken cancellationToken);
}

public sealed record ClaimIntakeCreateModel(
    string ClaimId,
    string ClaimNumber,
    string TenantId,
    string? WorkspaceId,
    string CustomerId,
    string PolicyNumber,
    string ProductLine,
    string LossType,
    DateTimeOffset LossDate,
    string LossLocation,
    string IncidentDescription,
    string ReportedByName,
    string ReportedByContact,
    string Channel,
    IReadOnlyDictionary<string, string>? FnolDetails,
    string Status,
    string CreatedByUserId,
    DateTimeOffset CreatedAt,
    string CorrelationId);
```

### 8.2 Claim workflow repository

```csharp
namespace InsuranceIntelligence.Application.Claims;

public interface IClaimWorkflowRepository
{
    Task<ClaimForWorkflowReadModel?> GetForTransitionAsync(
        string tenantId,
        string? workspaceId,
        string claimId,
        CancellationToken cancellationToken);

    Task SaveTransitionAsync(
        ClaimWorkflowTransitionWriteModel transition,
        CancellationToken cancellationToken);
}
```

Repository rules:

```text
- GetForTransitionAsync must filter by tenantId and workspaceId.
- SaveTransitionAsync must update only the matching tenant/workspace claim.
- SaveTransitionAsync should use optimistic concurrency where supported.
- SaveTransitionAsync must not accept arbitrary tenant/workspace from the client.
```

### 8.3 Claim number generator

```csharp
namespace InsuranceIntelligence.Application.Claims;

public interface IClaimNumberGenerator
{
    Task<string> GenerateAsync(
        string tenantId,
        string? workspaceId,
        string productLine,
        CancellationToken cancellationToken);
}
```

Rules:

```text
- Claim numbers are generated server-side only.
- The generator must be backed by a durable sequence/table or another collision-resistant production mechanism.
- Do not generate claim numbers in the browser.
- Do not trust a submitted claim number.
- Do not use random-only display numbers without a collision strategy.
```

### 8.4 Approval evidence repository

```csharp
namespace InsuranceIntelligence.Application.Claims;

public interface IClaimApprovalEvidenceRepository
{
    Task<bool> HasCompletedHumanApprovalForOutcomeAsync(
        string tenantId,
        string? workspaceId,
        string claimId,
        string requestedOutcomeStatus,
        CancellationToken cancellationToken);
}
```

Expected behavior:

```text
- requestedOutcomeStatus = approved requires a completed human approval outcome that permits approved.
- requestedOutcomeStatus = rejected requires a completed human approval outcome that permits rejected.
- Agent/System/Integration approval records do not satisfy this check.
- Pending, escalated, withdrawn, or failed approvals do not satisfy this check.
```

---

## 9. Validation

Use FluentValidation if already installed. If not, keep this minimal validation pattern until the repository validation framework is confirmed.

```csharp
namespace InsuranceIntelligence.Application.Claims.Validation;

public static class ClaimIntakeValidator
{
    public static void ValidateAndThrow(ClaimIntakeRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        Required(request.CustomerId, "customerId");
        Required(request.PolicyNumber, "policyNumber");
        Required(request.ProductLine, "productLine");
        Required(request.LossType, "lossType");
        Required(request.LossLocation, "lossLocation");
        Required(request.IncidentDescription, "incidentDescription");
        Required(request.ReportedByName, "reportedByName");
        Required(request.ReportedByContact, "reportedByContact");
        Required(request.Channel, "channel");

        if (request.LossDate == default)
        {
            throw new ValidationException("lossDate is required.");
        }

        if (request.LossDate > DateTimeOffset.UtcNow.AddMinutes(5))
        {
            throw new ValidationException("lossDate cannot be in the future.");
        }
    }

    private static void Required(string? value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ValidationException($"{name} is required.");
        }
    }
}
```

```csharp
namespace InsuranceIntelligence.Application.Claims.Validation;

public static class ClaimWorkflowTransitionValidator
{
    public static void ValidateAndThrow(string claimId, string requestedStatus)
    {
        if (string.IsNullOrWhiteSpace(claimId))
        {
            throw new ValidationException("claimId is required.");
        }

        if (string.IsNullOrWhiteSpace(requestedStatus))
        {
            throw new ValidationException("requestedStatus is required.");
        }

        if (!KnownStatuses.Contains(requestedStatus))
        {
            throw new ValidationException("requestedStatus is not a supported claim status.");
        }
    }

    private static readonly IReadOnlySet<string> KnownStatuses = new HashSet<string>(StringComparer.Ordinal)
    {
        ClaimStatusValues.New,
        ClaimStatusValues.Assigned,
        ClaimStatusValues.AwaitingDocuments,
        ClaimStatusValues.PendingApproval,
        ClaimStatusValues.Approved,
        ClaimStatusValues.Rejected,
        ClaimStatusValues.Closed,
        ClaimStatusValues.Escalated
    };
}
```

---

## 10. Full C# implementation — ClaimIntakeService

```csharp
using InsuranceIntelligence.Application.Claims.Validation;
using InsuranceIntelligence.Application.Common.Audit;
using InsuranceIntelligence.Application.Common.Authorization;
using InsuranceIntelligence.Application.Common.Tenancy;

namespace InsuranceIntelligence.Application.Claims;

internal sealed class ClaimIntakeService : IClaimIntakeService
{
    private readonly ITenantContextAccessor _tenantContextAccessor;
    private readonly IPermissionService _permissionService;
    private readonly IClaimIntakeRepository _repository;
    private readonly IClaimNumberGenerator _claimNumberGenerator;
    private readonly IAuditWriter _auditWriter;

    public ClaimIntakeService(
        ITenantContextAccessor tenantContextAccessor,
        IPermissionService permissionService,
        IClaimIntakeRepository repository,
        IClaimNumberGenerator claimNumberGenerator,
        IAuditWriter auditWriter)
    {
        _tenantContextAccessor = tenantContextAccessor;
        _permissionService = permissionService;
        _repository = repository;
        _claimNumberGenerator = claimNumberGenerator;
        _auditWriter = auditWriter;
    }

    public async Task<ClaimIntakeResultDto> CreateFnolAsync(
        ClaimIntakeRequest request,
        CancellationToken cancellationToken)
    {
        ClaimIntakeValidator.ValidateAndThrow(request);

        var context = _tenantContextAccessor.Current;
        await _permissionService.EnsureAsync(Permissions.ClaimsCreate, cancellationToken);

        var createdAt = DateTimeOffset.UtcNow;
        var claimId = Guid.NewGuid().ToString("N");
        var claimNumber = await _claimNumberGenerator.GenerateAsync(
            context.TenantId,
            context.WorkspaceId,
            request.ProductLine,
            cancellationToken);

        var createModel = new ClaimIntakeCreateModel(
            ClaimId: claimId,
            ClaimNumber: claimNumber,
            TenantId: context.TenantId,
            WorkspaceId: context.WorkspaceId,
            CustomerId: request.CustomerId,
            PolicyNumber: request.PolicyNumber,
            ProductLine: request.ProductLine,
            LossType: request.LossType,
            LossDate: request.LossDate,
            LossLocation: request.LossLocation,
            IncidentDescription: request.IncidentDescription,
            ReportedByName: request.ReportedByName,
            ReportedByContact: request.ReportedByContact,
            Channel: request.Channel,
            FnolDetails: request.FnolDetails,
            Status: ClaimStatusValues.New,
            CreatedByUserId: context.UserId,
            CreatedAt: createdAt,
            CorrelationId: context.CorrelationId);

        await _repository.CreateAsync(createModel, cancellationToken);

        await _auditWriter.WriteAsync(new AuditEventCreateRequest(
            TenantId: context.TenantId,
            WorkspaceId: context.WorkspaceId,
            EventType: AuditEventTypeValues.DataChanged,
            ActorType: "user",
            ActorUserId: context.UserId,
            Action: "claim.intake.created",
            EntityType: "claim",
            EntityId: claimId,
            Summary: $"Claim {claimNumber} intake created.",
            Reason: "FNOL intake created.",
            CorrelationId: context.CorrelationId,
            OccurredAt: createdAt), cancellationToken);

        return new ClaimIntakeResultDto(
            ClaimId: claimId,
            ClaimNumber: claimNumber,
            Status: ClaimStatusValues.New,
            CreatedAt: createdAt.ToString("O"));
    }
}
```

Implementation notes:

```text
- The service never reads tenant/workspace from request payload.
- The service never accepts a client-supplied claim number.
- The service always writes claim.intake.created audit event after durable create.
- The service returns only claimId, claimNumber, status, and createdAt.
- The service does not create approvals, send messages, or write back to external core systems.
```

---

## 11. Full C# implementation — ClaimWorkflowTransitionService

```csharp
using InsuranceIntelligence.Application.Claims.Validation;
using InsuranceIntelligence.Application.Common.Audit;
using InsuranceIntelligence.Application.Common.Authorization;
using InsuranceIntelligence.Application.Common.Exceptions;
using InsuranceIntelligence.Application.Common.Tenancy;

namespace InsuranceIntelligence.Application.Claims;

internal sealed class ClaimWorkflowTransitionService : IClaimWorkflowTransitionService
{
    private readonly ITenantContextAccessor _tenantContextAccessor;
    private readonly IPermissionService _permissionService;
    private readonly IClaimWorkflowRepository _workflowRepository;
    private readonly IClaimApprovalEvidenceRepository _approvalEvidenceRepository;
    private readonly IAuditWriter _auditWriter;

    public ClaimWorkflowTransitionService(
        ITenantContextAccessor tenantContextAccessor,
        IPermissionService permissionService,
        IClaimWorkflowRepository workflowRepository,
        IClaimApprovalEvidenceRepository approvalEvidenceRepository,
        IAuditWriter auditWriter)
    {
        _tenantContextAccessor = tenantContextAccessor;
        _permissionService = permissionService;
        _workflowRepository = workflowRepository;
        _approvalEvidenceRepository = approvalEvidenceRepository;
        _auditWriter = auditWriter;
    }

    public async Task<ClaimWorkflowTransitionResultDto> TransitionAsync(
        string claimId,
        string requestedStatus,
        string? reason,
        CancellationToken cancellationToken)
    {
        ClaimWorkflowTransitionValidator.ValidateAndThrow(claimId, requestedStatus);

        var context = _tenantContextAccessor.Current;

        await EnsureTransitionPermissionAsync(requestedStatus, cancellationToken);

        var claim = await _workflowRepository.GetForTransitionAsync(
            context.TenantId,
            context.WorkspaceId,
            claimId,
            cancellationToken);

        if (claim is null)
        {
            throw new NotFoundOrNotVisibleException("Claim was not found or is not visible.");
        }

        if (ClaimWorkflowTransitionGraph.IsTerminal(claim.Status))
        {
            throw new WorkflowConflictException("Closed claims cannot be transitioned.");
        }

        if (!ClaimWorkflowTransitionGraph.IsAllowed(claim.Status, requestedStatus))
        {
            throw new WorkflowConflictException(
                $"Transition from '{claim.Status}' to '{requestedStatus}' is not allowed.");
        }

        if (ClaimWorkflowTransitionGraph.IsApprovalOutcomeStatus(requestedStatus))
        {
            await EnsureCompletedHumanApprovalExistsAsync(
                context,
                claim.ClaimId,
                requestedStatus,
                cancellationToken);
        }

        var transitionedAt = DateTimeOffset.UtcNow;

        await _workflowRepository.SaveTransitionAsync(new ClaimWorkflowTransitionWriteModel(
            ClaimId: claim.ClaimId,
            TenantId: context.TenantId,
            WorkspaceId: context.WorkspaceId,
            PreviousStatus: claim.Status,
            NewStatus: requestedStatus,
            TransitionedByUserId: context.UserId,
            Reason: reason,
            TransitionedAt: transitionedAt,
            CorrelationId: context.CorrelationId), cancellationToken);

        await _auditWriter.WriteAsync(new AuditEventCreateRequest(
            TenantId: context.TenantId,
            WorkspaceId: context.WorkspaceId,
            EventType: AuditEventTypeValues.WorkflowChanged,
            ActorType: "user",
            ActorUserId: context.UserId,
            Action: "workflow.changed",
            EntityType: "claim",
            EntityId: claim.ClaimId,
            Summary: $"Claim {claim.ClaimNumber} workflow changed from {claim.Status} to {requestedStatus}.",
            Reason: reason,
            CorrelationId: context.CorrelationId,
            OccurredAt: transitionedAt), cancellationToken);

        return new ClaimWorkflowTransitionResultDto(
            ClaimId: claim.ClaimId,
            ClaimNumber: claim.ClaimNumber,
            PreviousStatus: claim.Status,
            CurrentStatus: requestedStatus,
            TransitionedByUserId: context.UserId,
            TransitionedAt: transitionedAt.ToString("O"));
    }

    private async Task EnsureTransitionPermissionAsync(
        string requestedStatus,
        CancellationToken cancellationToken)
    {
        if (requestedStatus == ClaimStatusValues.Assigned)
        {
            await _permissionService.EnsureAsync(Permissions.ClaimsAssign, cancellationToken);
            return;
        }

        await _permissionService.EnsureAsync(Permissions.ClaimsTransition, cancellationToken);
    }

    private async Task EnsureCompletedHumanApprovalExistsAsync(
        TenantContext context,
        string claimId,
        string requestedOutcomeStatus,
        CancellationToken cancellationToken)
    {
        var hasApproval = await _approvalEvidenceRepository.HasCompletedHumanApprovalForOutcomeAsync(
            context.TenantId,
            context.WorkspaceId,
            claimId,
            requestedOutcomeStatus,
            cancellationToken);

        if (!hasApproval)
        {
            throw new WorkflowConflictException(
                $"Transition to '{requestedOutcomeStatus}' requires an existing completed human approval decision.");
        }
    }
}
```

Implementation notes:

```text
- The service enforces an explicit graph; it never writes arbitrary requestedStatus values.
- The service can move a claim into approved/rejected only after ApprovalDecisionService or its repository has already recorded the completed human approval outcome.
- The service does not decide the approval outcome itself.
- The service does not send customer communications or write back to a core claims system.
- The service writes workflow.changed audit events with before/after status.
```

---

## 12. Dependency injection registration

Add these services beside the existing Claims Application service registrations:

```csharp
services.AddScoped<IClaimIntakeService, ClaimIntakeService>();
services.AddScoped<IClaimWorkflowTransitionService, ClaimWorkflowTransitionService>();
```

Infrastructure must provide concrete EF-backed repositories:

```csharp
services.AddScoped<IClaimIntakeRepository, EfClaimIntakeRepository>();
services.AddScoped<IClaimWorkflowRepository, EfClaimWorkflowRepository>();
services.AddScoped<IClaimNumberGenerator, EfClaimNumberGenerator>();
services.AddScoped<IClaimApprovalEvidenceRepository, EfClaimApprovalEvidenceRepository>();
```

Registration rules:

```text
- EF implementations belong in Infrastructure.
- Application services must not reference DbContext directly unless the repository pattern is intentionally not used across the new repo.
- No in-memory intake repository may be registered in deployable runtime.
- No fake approval evidence repository may be registered in deployable runtime.
```

---

## 13. Tests to add

### 13.1 Claim intake tests

```text
ClaimIntakeServiceTests
- CreateFnolAsync_RequiresClaimsCreatePermission
- CreateFnolAsync_UsesTenantWorkspaceFromContext
- CreateFnolAsync_RejectsMissingMandatoryFields
- CreateFnolAsync_GeneratesCanonicalClaimNumberServerSide
- CreateFnolAsync_CreatesClaimWithNewStatus
- CreateFnolAsync_WritesClaimIntakeCreatedAuditEvent
- CreateFnolAsync_DoesNotAcceptClientSuppliedClaimNumber
```

Example:

```csharp
[Fact]
public async Task CreateFnolAsync_CreatesClaimWithNewStatus()
{
    var context = TestTenantContext.For(
        tenantId: "tenant-a",
        workspaceId: "workspace-claims",
        userId: "user-1",
        permissions: new[] { Permissions.ClaimsCreate });

    var repository = new Mock<IClaimIntakeRepository>();
    var generator = new Mock<IClaimNumberGenerator>();
    generator.Setup(x => x.GenerateAsync("tenant-a", "workspace-claims", "motor", It.IsAny<CancellationToken>()))
        .ReturnsAsync("CLM-MOT-000001");

    var service = BuildIntakeService(context, repository.Object, generator.Object);

    var result = await service.CreateFnolAsync(TestClaimIntake.ValidMotorFnol(), CancellationToken.None);

    Assert.Equal("CLM-MOT-000001", result.ClaimNumber);
    Assert.Equal(ClaimStatusValues.New, result.Status);

    repository.Verify(x => x.CreateAsync(
        It.Is<ClaimIntakeCreateModel>(m =>
            m.TenantId == "tenant-a" &&
            m.WorkspaceId == "workspace-claims" &&
            m.Status == ClaimStatusValues.New &&
            m.ClaimNumber == "CLM-MOT-000001"),
        It.IsAny<CancellationToken>()), Times.Once);
}
```

### 13.2 Claim workflow transition tests

```text
ClaimWorkflowTransitionServiceTests
- TransitionAsync_ValidTransitionSucceeds
- TransitionAsync_InvalidTransitionRejectedWithWorkflowConflictException
- TransitionAsync_TransitionToApprovedRequiresExistingHumanApprovalRecord
- TransitionAsync_TransitionToRejectedRequiresExistingHumanApprovalRecord
- TransitionAsync_RejectsClosedClaimTransition
- TransitionAsync_RequiresClaimsAssignForAssignedStatus
- TransitionAsync_RequiresClaimsTransitionForWorkflowStatusChange
- TransitionAsync_UsesTenantWorkspaceScope
- TransitionAsync_ReturnsNotFoundWhenClaimNotVisible
- TransitionAsync_WritesWorkflowChangedAuditEvent
```

Example valid transition:

```csharp
[Fact]
public async Task TransitionAsync_ValidTransitionSucceeds()
{
    var context = TestTenantContext.For(
        tenantId: "tenant-a",
        workspaceId: "workspace-claims",
        userId: "user-1",
        permissions: new[] { Permissions.ClaimsAssign });

    var repository = new Mock<IClaimWorkflowRepository>();
    repository.Setup(x => x.GetForTransitionAsync("tenant-a", "workspace-claims", "clm-1", It.IsAny<CancellationToken>()))
        .ReturnsAsync(new ClaimForWorkflowReadModel(
            ClaimId: "clm-1",
            ClaimNumber: "CLM-001",
            TenantId: "tenant-a",
            WorkspaceId: "workspace-claims",
            Status: ClaimStatusValues.New,
            AssignedToUserId: null,
            UpdatedAt: DateTimeOffset.UtcNow));

    var service = BuildWorkflowService(context, repository.Object);

    var result = await service.TransitionAsync("clm-1", ClaimStatusValues.Assigned, "Assigning to claims team.", CancellationToken.None);

    Assert.Equal(ClaimStatusValues.New, result.PreviousStatus);
    Assert.Equal(ClaimStatusValues.Assigned, result.CurrentStatus);

    repository.Verify(x => x.SaveTransitionAsync(
        It.Is<ClaimWorkflowTransitionWriteModel>(m =>
            m.ClaimId == "clm-1" &&
            m.PreviousStatus == ClaimStatusValues.New &&
            m.NewStatus == ClaimStatusValues.Assigned),
        It.IsAny<CancellationToken>()), Times.Once);
}
```

Example invalid transition:

```csharp
[Fact]
public async Task TransitionAsync_InvalidTransitionRejectedWithWorkflowConflictException()
{
    var context = TestTenantContext.For(
        tenantId: "tenant-a",
        workspaceId: "workspace-claims",
        userId: "user-1",
        permissions: new[] { Permissions.ClaimsTransition });

    var repository = new Mock<IClaimWorkflowRepository>();
    repository.Setup(x => x.GetForTransitionAsync("tenant-a", "workspace-claims", "clm-1", It.IsAny<CancellationToken>()))
        .ReturnsAsync(new ClaimForWorkflowReadModel(
            ClaimId: "clm-1",
            ClaimNumber: "CLM-001",
            TenantId: "tenant-a",
            WorkspaceId: "workspace-claims",
            Status: ClaimStatusValues.New,
            AssignedToUserId: null,
            UpdatedAt: DateTimeOffset.UtcNow));

    var service = BuildWorkflowService(context, repository.Object);

    await Assert.ThrowsAsync<WorkflowConflictException>(() =>
        service.TransitionAsync("clm-1", ClaimStatusValues.Approved, "Invalid direct approval.", CancellationToken.None));

    repository.Verify(x => x.SaveTransitionAsync(
        It.IsAny<ClaimWorkflowTransitionWriteModel>(),
        It.IsAny<CancellationToken>()), Times.Never);
}
```

Example approval-evidence test:

```csharp
[Fact]
public async Task TransitionAsync_TransitionToApprovedRequiresExistingHumanApprovalRecord()
{
    var context = TestTenantContext.For(
        tenantId: "tenant-a",
        workspaceId: "workspace-claims",
        userId: "user-1",
        permissions: new[] { Permissions.ClaimsTransition });

    var repository = new Mock<IClaimWorkflowRepository>();
    repository.Setup(x => x.GetForTransitionAsync("tenant-a", "workspace-claims", "clm-1", It.IsAny<CancellationToken>()))
        .ReturnsAsync(new ClaimForWorkflowReadModel(
            ClaimId: "clm-1",
            ClaimNumber: "CLM-001",
            TenantId: "tenant-a",
            WorkspaceId: "workspace-claims",
            Status: ClaimStatusValues.PendingApproval,
            AssignedToUserId: null,
            UpdatedAt: DateTimeOffset.UtcNow));

    var approvals = new Mock<IClaimApprovalEvidenceRepository>();
    approvals.Setup(x => x.HasCompletedHumanApprovalForOutcomeAsync(
            "tenant-a",
            "workspace-claims",
            "clm-1",
            ClaimStatusValues.Approved,
            It.IsAny<CancellationToken>()))
        .ReturnsAsync(false);

    var service = BuildWorkflowService(context, repository.Object, approvals.Object);

    await Assert.ThrowsAsync<WorkflowConflictException>(() =>
        service.TransitionAsync("clm-1", ClaimStatusValues.Approved, "Approval not yet recorded.", CancellationToken.None));

    repository.Verify(x => x.SaveTransitionAsync(
        It.IsAny<ClaimWorkflowTransitionWriteModel>(),
        It.IsAny<CancellationToken>()), Times.Never);
}
```

### 13.3 Audit-write tests

```text
ClaimIntakeServiceTests
- CreateFnolAsync_WritesClaimIntakeCreatedAuditEvent

ClaimWorkflowTransitionServiceTests
- TransitionAsync_WritesWorkflowChangedAuditEventWithBeforeAfterStatus
```

Expected audit event values:

```text
Intake:
EventType = data_changed
Action = claim.intake.created
EntityType = claim
EntityId = created claimId

Transition:
EventType = workflow_changed
Action = workflow.changed
EntityType = claim
EntityId = claimId
Summary includes previous and new status.
```

### 13.4 Contract test for intake response shape

```csharp
[Fact]
public void ClaimIntakeResultDto_ContractMatchesExpectedFrontendShape()
{
    var dto = new ClaimIntakeResultDto(
        ClaimId: "clm-1",
        ClaimNumber: "CLM-001",
        Status: ClaimStatusValues.New,
        CreatedAt: DateTimeOffset.UtcNow.ToString("O"));

    var json = JsonSerializer.SerializeToDocument(dto, new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    });

    var names = json.RootElement.EnumerateObject()
        .Select(p => p.Name)
        .ToHashSet(StringComparer.Ordinal);

    var expected = new[]
    {
        "claimId",
        "claimNumber",
        "status",
        "createdAt"
    };

    Assert.Equal(expected.OrderBy(x => x), names.OrderBy(x => x));
}
```

### 13.5 Human-decision boundary tests

```text
ClaimWorkflowTransitionServiceTests
- TransitionAsync_DoesNotApproveClaimWithoutApprovalDecisionServiceEvidence
- TransitionAsync_DoesNotRejectClaimWithoutApprovalDecisionServiceEvidence
```

These tests prove that this service is not an independent claim approval/rejection mechanism.

### 13.6 Provider failure tests

```text
Not applicable. Claim intake and workflow transitions use canonical database repositories and do not directly call Blob Storage, Azure AI Document Intelligence, Azure AI Search, Azure OpenAI, Microsoft Graph, or Service Bus.
```

---

## 14. Actual logic implemented

### Tenant and workspace isolation

The services resolve `TenantContext` from `ITenantContextAccessor.Current`. They pass `TenantId` and `WorkspaceId` into repository ports. The client cannot submit tenant/workspace as authority.

### Permission enforcement

FNOL intake enforces:

```csharp
await _permissionService.EnsureAsync(Permissions.ClaimsCreate, cancellationToken);
```

Transitions enforce:

```csharp
claims.assign      for transition into assigned
claims.transition  for all other workflow transitions
```

### Claim number safety

The intake service calls `IClaimNumberGenerator.GenerateAsync`. It never accepts a client-supplied claim number and never lets the frontend decide the canonical claim reference.

### Workflow state machine

The transition service uses `ClaimWorkflowTransitionGraph`. It rejects unsupported transitions using `WorkflowConflictException` and never allows arbitrary status writes.

### Approval outcome protection

Transitions into `approved` or `rejected` require:

```csharp
IClaimApprovalEvidenceRepository.HasCompletedHumanApprovalForOutcomeAsync(...)
```

A submitted status string alone is not enough. This keeps ApprovalDecisionService as the owner of consequential claim approval/rejection decisions.

### Audit behavior

The services write:

```text
claim.intake.created
workflow.changed
```

with tenant/workspace/user/correlation context.

### No fake data

The services do not fabricate claims, claim numbers, approval decisions, documents, customers, policies, or workflow outcomes. Empty or missing repository results produce null/not found/conflict outcomes, not fake success.

---

## 15. TODO fields left as best-effort

| Area | Current implementation | Business decision still needed |
|---|---|---|
| Canonical claim status enum | Uses subset: new, assigned, awaiting_documents, pending_approval, approved, rejected, closed, escalated | Confirm final status enum and whether statuses like failed, settled, routed, customer_update_pending, withdrawn, repudiated, paid are needed. |
| Full transition graph | Implements the explicit graph in this document only | Confirm product-line-specific transitions, supervisor escalation paths, and whether rejected/approved are statuses or approval outcomes only. |
| Transition permission mapping | assigned uses claims.assign; other statuses use claims.transition | Confirm whether escalation requires a distinct permission such as claims.escalate. |
| FNOL required fields | Validates shared minimum fields only | Confirm mandatory FNOL fields per product line and channel. |
| Claim number format | Uses IClaimNumberGenerator abstraction | Confirm final claim numbering format by tenant, branch, product line, and year. |
| Approval evidence lookup | Requires completed human approval for approved/rejected | Confirm exact approval entity fields and outcome mapping. |

---

## 16. Dependencies that may still be missing

These are likely repository-specific and may need to be created or aligned during implementation:

```text
IClaimIntakeRepository concrete EF Core implementation
IClaimWorkflowRepository concrete EF Core implementation
IClaimNumberGenerator durable EF implementation
IClaimApprovalEvidenceRepository concrete EF implementation
WorkflowConflictException if not already added by backend wiring document
Permissions.ClaimsTransition if not already added
Actual Claim entity/table fields
Actual Approval entity/table fields for completed human decision evidence
Sequential EF Core migration for intake/workflow fields if missing
```

If any dependency is missing, add only the smallest required abstraction or stop and report the blocker. Do not introduce mock provider paths into deployable runtime.

---

## 17. Completion report template

```text
Service implemented:
- IClaimIntakeService
- IClaimWorkflowTransitionService

Files changed:
- src/InsuranceIntelligence.Application/Claims/IClaimIntakeService.cs
- src/InsuranceIntelligence.Application/Claims/IClaimWorkflowTransitionService.cs
- src/InsuranceIntelligence.Application/Claims/ClaimIntakeService.cs
- src/InsuranceIntelligence.Application/Claims/ClaimWorkflowTransitionService.cs
- src/InsuranceIntelligence.Application/Claims/ClaimIntakeModels.cs
- src/InsuranceIntelligence.Application/Claims/ClaimWorkflowModels.cs
- src/InsuranceIntelligence.Application/Claims/IClaimIntakeRepository.cs
- src/InsuranceIntelligence.Application/Claims/IClaimWorkflowRepository.cs
- src/InsuranceIntelligence.Application/Claims/IClaimNumberGenerator.cs
- src/InsuranceIntelligence.Application/Claims/IClaimApprovalEvidenceRepository.cs
- src/InsuranceIntelligence.Application/Common/Authorization/Permissions.cs, if ClaimsTransition was missing
- src/InsuranceIntelligence.Application/Common/Exceptions/WorkflowConflictException.cs, if missing
- tests listed in this document

Logic implemented:
- FNOL claim creation with mandatory-field validation, claims.create, server-generated claim number, new status, and claim.intake.created audit.
- Workflow transition service with explicit transition graph, permission checks, approval-evidence requirement for approved/rejected states, and workflow.changed audit.

Security impact:
- Tenant/workspace context is resolved server-side only.
- Claim numbers are generated server-side only.
- Arbitrary status writes are rejected.
- Approval/rejection transitions cannot be used as a backdoor around ApprovalDecisionService.

Tenant/workspace isolation impact:
- Every repository call receives TenantId and WorkspaceId from ITenantContextAccessor.Current.
- No client-supplied tenant/workspace values are accepted as authority.

Authentication/authorization impact:
- claims.create is required for FNOL creation.
- claims.assign is required for transition into assigned.
- claims.transition is required for all other workflow transitions.

Data model or migration impact:
- May require claim intake fields, status field, workflow transition history, claim number sequence, and approval evidence lookup indexes.

AI/human approval impact:
- No AI actor or system actor can use this service to finalize a consequential decision.
- approved/rejected workflow status requires existing completed human approval evidence.

Tests added or required:
- valid-transition-succeeds
- invalid-transition-rejected-with-WorkflowConflictException
- transition-to-approved-requires-existing-human-approval-record
- transition-to-rejected-requires-existing-human-approval-record
- tenant/permission enforcement tests
- audit-write tests
- intake response shape contract test

TODOs:
- Confirm final canonical claim status enum.
- Confirm full transition graph.
- Confirm FNOL required-field set per product line.
- Confirm final claim number format.
- Confirm approval evidence mapping.

Missing dependencies:
- Concrete EF repositories and migrations may still need implementation.
- ClaimsTransition permission constant may need to be added.
- WorkflowConflictException may need to be added if not already present.

No fake data confirmation:
- No mock, in-memory, simulated, placeholder, seeded-demo, fallback, or fake operational data path is introduced.
- Empty/missing repository results do not produce fake claim records or fake approval evidence.

Recommended next step:
- Implement EF Core repositories and migrations for claim intake, claim workflow transitions, durable claim-number generation, and approval evidence lookup, then wire these services into ClaimsController routes in the backend wiring document.
```
