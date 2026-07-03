# IIP Integration Write-Back Execution Service

**Product:** Insurance Intelligence Platform (IIP)  
**Staff application:** IIP Operations  
**Status:** Implementation-ready service output for repository application layer  
**Date:** July 2026  
**Service area:** Integration write-back execution for external policy administration / core insurance systems

---

## Shared rules used by this service output

This output follows the same implementation pattern used by the existing IIP Application-layer service-output documents for Command Center, Claims, SLA, Approvals, Documents, Channels, Customers, Sales & Leads, Audit, Claim Intake, and Claim Workflow Transition.

```text
1. Resolve tenant/workspace from ITenantContextAccessor.
2. Enforce the relevant backend permission before any sensitive read or state-changing operation.
3. Apply tenantId and workspaceId filters in the repository/provider boundary.
4. Return the exact reconciled frontend/backend DTO shape where applicable.
5. Write audit events for every state-changing operation and every sensitive integration attempt.
6. Never fabricate records when no data exists.
7. Never mark an external write-back as succeeded without real confirmation from the external provider.
8. Surface missing or unavailable providers as ProviderNotConfiguredException / ProviderUnavailableException, never fake success.
9. Use ISO 8601 round-trip timestamps for frontend wire strings.
10. Keep Domain free of EF Core, Azure SDKs, ASP.NET Core, and external system SDK dependencies.
11. Test doubles may exist only in automated tests or isolated LocalTest assemblies.
12. AI/system actors must never be final actors for consequential insurance decisions.
```

This document supplies one new Application-layer service and the ports it requires:

```text
IPolicyAdminWriteBackService
IPolicyAdminWriteBackProvider
IClaimWriteBackHistoryRepository
IWriteBackApprovalReadRepository
```

It does not modify `IClaimsQueryService`, `ClaimDetailDto`, or `ClaimWriteBackHistoryItemDto`. The existing claims query service remains responsible for reading `WriteBackHistory` into `ClaimDetailDto`.

---

## 1. Scope of this pass

This pass implements **external policy administration write-back execution**:

```text
IPolicyAdminWriteBackService.ExecuteAsync(claimId, writeBackType, payload, ct)
```

Implemented capabilities:

```text
- Validate write-back request inputs.
- Enforce either integrations.manage or claims.writeback permission.
- Resolve tenant/workspace/user from ITenantContextAccessor.
- Check provider health for policy_admin_system before attempting the external call.
- Create a pending write-back history record before the external provider call.
- Call IPolicyAdminWriteBackProvider only when the provider is configured and connected.
- Mark the write-back history as succeeded only after real provider confirmation.
- Mark the write-back history as failed when provider health fails, provider call fails, validation fails after history creation, or provider confirmation is negative.
- Write integration.writeback.attempted, integration.writeback.succeeded, and integration.writeback.failed audit events.
- Redact write-back summaries; never audit raw external-system payloads, credentials, tokens, or provider response bodies.
- Return a ClaimWriteBackHistoryItemDto with the fixed existing field shape.
```

Not implemented in this pass:

```text
- A concrete policy administration adapter for any specific insurer.
- Retry/backoff/queue-based retry design.
- Idempotency key strategy.
- Payment execution, settlement release, claim approval, claim rejection, or claim repudiation business decisions.
- Changes to ClaimDetailDto or IClaimsQueryService.
- Controllers or API routes.
- Frontend code.
```

Important boundary:

```text
This service executes an already-authorized integration write-back. It must not decide whether a claim should be approved, rejected, settled, paid, repudiated, or otherwise finally determined.
```

---

## 2. Files to create or modify

Use existing repository structure where present. Do not create parallel architecture if equivalent files already exist.

```text
src/InsuranceIntelligence.Application/Integrations/IPolicyAdminWriteBackService.cs
src/InsuranceIntelligence.Application/Integrations/PolicyAdminWriteBackService.cs
src/InsuranceIntelligence.Application/Integrations/PolicyAdminWriteBackModels.cs
src/InsuranceIntelligence.Application/Integrations/IPolicyAdminWriteBackProvider.cs
src/InsuranceIntelligence.Application/Integrations/IClaimWriteBackHistoryRepository.cs
src/InsuranceIntelligence.Application/Integrations/IWriteBackApprovalReadRepository.cs
src/InsuranceIntelligence.Application/Common/Authorization/Permissions.cs              # extend only
src/InsuranceIntelligence.Application/Common/Providers/ProviderCodes.cs               # extend only
src/InsuranceIntelligence.Application/Common/Audit/AuditEventTypeValues.cs            # extend only if the project uses constants
src/InsuranceIntelligence.Tests/Application/Integrations/PolicyAdminWriteBackServiceTests.cs
src/InsuranceIntelligence.Tests/Application/Integrations/ClaimWriteBackHistoryContractMatchesFrontendTypesTest.cs
```

Infrastructure adapter files are intentionally not created in this pass because the real policy administration system and API contract are insurer-specific and unconfirmed.

---

## 3. Dependencies expected

This service expects the following fixed inputs from the core ports and existing service docs:

```csharp
ITenantContextAccessor
IPermissionService
IAuditWriter
IProviderHealthCheck
Permissions.IntegrationsManage
ProviderNotConfiguredException
ProviderUnavailableException
ForbiddenException
ValidationException
WorkflowConflictException
ClaimWriteBackHistoryItemDto
```

If `WorkflowConflictException` does not exist yet, use the same exception shape already used by approval/workflow service docs and place it in the Application common exceptions namespace.

---

## 4. Permission and provider constants

### 4.1 Permission constants

Extend the existing `Permissions` class. Do not rename existing constants.

```csharp
namespace InsuranceIntelligence.Application.Common.Authorization;

public static partial class Permissions
{
    public const string ClaimsWriteBack = "claims.writeback";
}
```

Permission rule:

```text
A caller may execute a write-back if they have either:
- integrations.manage, or
- claims.writeback.
```

This service checks both through the real permission store. It must not accept a client-supplied role, tenant ID, workspace ID, or permission claim as authority.

### 4.2 Provider code

Extend the existing `ProviderCodes` class.

```csharp
namespace InsuranceIntelligence.Application.Common.Providers;

public static partial class ProviderCodes
{
    public const string PolicyAdminSystem = "policy_admin_system";
}
```

`policy_admin_system` is a logical capability code. The concrete adapter may map to an insurer-specific API, middleware gateway, file export endpoint, or policy administration platform later. This pass defines the internal port only.

### 4.3 Write-back status values

These status values are stored in write-back history and surfaced through the existing `ClaimWriteBackHistoryItemDto.Status` field.

```csharp
namespace InsuranceIntelligence.Application.Integrations;

public static class ClaimWriteBackStatusValues
{
    public const string Pending = "pending";
    public const string Succeeded = "succeeded";
    public const string Failed = "failed";
}
```

### 4.4 Write-back type values

```csharp
namespace InsuranceIntelligence.Application.Integrations;

public static class PolicyAdminWriteBackTypeValues
{
    public const string ClaimStatusUpdate = "claim_status_update";
    public const string DocumentReferenceSync = "document_reference_sync";
    public const string PaymentInstructionExport = "payment_instruction_export";
}
```

Consequential write-back note:

```text
claim_status_update and payment_instruction_export can represent customer-impacting or financially consequential activity. The service must verify a completed human approval record before executing those types. It must not treat a requested status string or payload field as proof that a decision was approved.
```

---

## 5. Service contract

```csharp
namespace InsuranceIntelligence.Application.Integrations;

public interface IPolicyAdminWriteBackService
{
    Task<ClaimWriteBackHistoryItemDto> ExecuteAsync(
        string claimId,
        string writeBackType,
        PolicyAdminWriteBackPayload payload,
        CancellationToken cancellationToken);
}
```

---

## 6. Models

### 6.1 Payload model

```csharp
namespace InsuranceIntelligence.Application.Integrations;

public sealed record PolicyAdminWriteBackPayload(
    IReadOnlyDictionary<string, string?> Fields,
    string? HumanApprovalId = null,
    string? ExternalCorrelationId = null);
```

Payload rules:

```text
- Fields must not be null.
- Fields must not contain secrets, credentials, tokens, connection strings, SAS values, or raw provider payloads.
- HumanApprovalId is required for consequential write-back types.
- ExternalCorrelationId may be supplied for traceability, but it is not an idempotency key in this pass.
```

### 6.2 Provider request and result

```csharp
namespace InsuranceIntelligence.Application.Integrations;

public sealed record PolicyAdminWriteBackProviderRequest(
    string TenantId,
    string? WorkspaceId,
    string ClaimId,
    string WriteBackType,
    IReadOnlyDictionary<string, string?> Fields,
    string InternalWriteBackId,
    string CorrelationId);

public sealed record PolicyAdminWriteBackProviderResult(
    bool Confirmed,
    string TargetSystem,
    string? ExternalReference,
    string? SafeSummary);
```

Provider result rule:

```text
Confirmed must be true before this service records succeeded.
If Confirmed is false, null, missing, or represented by an exception, the write-back is failed.
```

### 6.3 Internal history models

The public returned DTO remains the existing `ClaimWriteBackHistoryItemDto` shape. The persistence record may contain additional backend-only fields.

```csharp
namespace InsuranceIntelligence.Application.Integrations;

public sealed record ClaimWriteBackHistoryCreateRequest(
    string TenantId,
    string? WorkspaceId,
    string ClaimId,
    string WriteBackType,
    string TargetSystem,
    string Status,
    string Summary,
    string CreatedByUserId,
    string CorrelationId,
    DateTimeOffset OccurredAt);

public sealed record ClaimWriteBackHistoryUpdateRequest(
    string TenantId,
    string? WorkspaceId,
    string HistoryId,
    string Status,
    string Summary,
    string? ExternalReference,
    DateTimeOffset OccurredAt);
```

---

## 7. Provider and repository ports

### 7.1 Policy admin write-back provider port

The concrete Infrastructure adapter is out of scope for this pass because policy administration systems differ by insurer.

```csharp
namespace InsuranceIntelligence.Application.Integrations;

public interface IPolicyAdminWriteBackProvider
{
    Task<PolicyAdminWriteBackProviderResult> ExecuteAsync(
        PolicyAdminWriteBackProviderRequest request,
        CancellationToken cancellationToken);
}
```

Provider implementation rules for a future Infrastructure adapter:

```text
- Must call a real configured target system.
- Must not return Confirmed = true unless the external system confirms success.
- Must not log raw payloads, credentials, tokens, provider responses, or connection strings.
- Must map provider failures to ProviderUnavailableException or a failed provider result.
- Must include correlation ID in outbound request metadata where supported.
```

### 7.2 Write-back history repository

```csharp
namespace InsuranceIntelligence.Application.Integrations;

public interface IClaimWriteBackHistoryRepository
{
    Task<ClaimWriteBackHistoryItemDto> CreatePendingAsync(
        ClaimWriteBackHistoryCreateRequest request,
        CancellationToken cancellationToken);

    Task<ClaimWriteBackHistoryItemDto> MarkSucceededAsync(
        ClaimWriteBackHistoryUpdateRequest request,
        CancellationToken cancellationToken);

    Task<ClaimWriteBackHistoryItemDto> MarkFailedAsync(
        ClaimWriteBackHistoryUpdateRequest request,
        CancellationToken cancellationToken);
}
```

Repository rules:

```text
- The history record must be tenant/workspace scoped.
- The history record must be linked to the claim.
- The public DTO must preserve the existing ClaimWriteBackHistoryItemDto field shape.
- The repository may persist additional internal columns such as payloadHash, externalReference, failureCode, createdByUserId, and correlationId, but those must not leak to the public DTO unless the frontend/backend contract is explicitly updated.
```

### 7.3 Approval verification repository

This port verifies that a human approval exists before a consequential write-back type is executed.

```csharp
namespace InsuranceIntelligence.Application.Integrations;

public interface IWriteBackApprovalReadRepository
{
    Task<bool> HasCompletedHumanApprovalAsync(
        string tenantId,
        string? workspaceId,
        string claimId,
        string writeBackType,
        string? humanApprovalId,
        CancellationToken cancellationToken);
}
```

Approval verification rules:

```text
- Must verify the approval belongs to the same tenant/workspace and claim.
- Must verify the approval is completed/approved by a human actor.
- Must verify the approval relates to the requested write-back type.
- Must not accept a payload field or client-supplied status string as proof of approval.
```

---

## 8. Full implementation

```csharp
using InsuranceIntelligence.Application.Common.Audit;
using InsuranceIntelligence.Application.Common.Authorization;
using InsuranceIntelligence.Application.Common.Exceptions;
using InsuranceIntelligence.Application.Common.Providers;
using InsuranceIntelligence.Application.Common.Tenancy;

namespace InsuranceIntelligence.Application.Integrations;

internal sealed class PolicyAdminWriteBackService : IPolicyAdminWriteBackService
{
    private static readonly IReadOnlySet<string> AllowedWriteBackTypes = new HashSet<string>(StringComparer.Ordinal)
    {
        PolicyAdminWriteBackTypeValues.ClaimStatusUpdate,
        PolicyAdminWriteBackTypeValues.DocumentReferenceSync,
        PolicyAdminWriteBackTypeValues.PaymentInstructionExport
    };

    private static readonly IReadOnlySet<string> ConsequentialWriteBackTypes = new HashSet<string>(StringComparer.Ordinal)
    {
        PolicyAdminWriteBackTypeValues.ClaimStatusUpdate,
        PolicyAdminWriteBackTypeValues.PaymentInstructionExport
    };

    private static readonly IReadOnlySet<string> SensitivePayloadKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "password",
        "secret",
        "token",
        "access_token",
        "refresh_token",
        "connection_string",
        "connectionString",
        "sas",
        "sas_token",
        "api_key",
        "apikey",
        "client_secret"
    };

    private readonly ITenantContextAccessor _tenantContextAccessor;
    private readonly IPermissionService _permissionService;
    private readonly IProviderHealthCheck _providerHealthCheck;
    private readonly IPolicyAdminWriteBackProvider _provider;
    private readonly IClaimWriteBackHistoryRepository _historyRepository;
    private readonly IWriteBackApprovalReadRepository _approvalReadRepository;
    private readonly IAuditWriter _auditWriter;

    public PolicyAdminWriteBackService(
        ITenantContextAccessor tenantContextAccessor,
        IPermissionService permissionService,
        IProviderHealthCheck providerHealthCheck,
        IPolicyAdminWriteBackProvider provider,
        IClaimWriteBackHistoryRepository historyRepository,
        IWriteBackApprovalReadRepository approvalReadRepository,
        IAuditWriter auditWriter)
    {
        _tenantContextAccessor = tenantContextAccessor;
        _permissionService = permissionService;
        _providerHealthCheck = providerHealthCheck;
        _provider = provider;
        _historyRepository = historyRepository;
        _approvalReadRepository = approvalReadRepository;
        _auditWriter = auditWriter;
    }

    public async Task<ClaimWriteBackHistoryItemDto> ExecuteAsync(
        string claimId,
        string writeBackType,
        PolicyAdminWriteBackPayload payload,
        CancellationToken cancellationToken)
    {
        ValidateRequest(claimId, writeBackType, payload);

        var context = _tenantContextAccessor.Current;
        await EnsureWriteBackPermissionAsync(cancellationToken);

        if (ConsequentialWriteBackTypes.Contains(writeBackType))
        {
            var hasApproval = await _approvalReadRepository.HasCompletedHumanApprovalAsync(
                context.TenantId,
                context.WorkspaceId,
                claimId,
                writeBackType,
                payload.HumanApprovalId,
                cancellationToken);

            if (!hasApproval)
            {
                throw new WorkflowConflictException(
                    "A completed human approval is required before this write-back can be executed.");
            }
        }

        var now = DateTimeOffset.UtcNow;
        var pending = await _historyRepository.CreatePendingAsync(
            new ClaimWriteBackHistoryCreateRequest(
                TenantId: context.TenantId,
                WorkspaceId: context.WorkspaceId,
                ClaimId: claimId,
                WriteBackType: writeBackType,
                TargetSystem: "policy_admin_system",
                Status: ClaimWriteBackStatusValues.Pending,
                Summary: BuildSafeSummary(writeBackType, payload),
                CreatedByUserId: context.UserId,
                CorrelationId: context.CorrelationId,
                OccurredAt: now),
            cancellationToken);

        await WriteAuditAsync(
            context,
            claimId,
            pending.Id,
            eventType: IntegrationWriteBackAuditEventTypes.Attempted,
            action: "attempted",
            summary: BuildAuditSummary("attempted", writeBackType, payload),
            reason: payload.ExternalCorrelationId,
            cancellationToken);

        var health = await _providerHealthCheck.CheckAsync(
            ProviderCodes.PolicyAdminSystem,
            cancellationToken);

        if (health.Status == ProviderStatus.NotConfigured)
        {
            var failed = await MarkFailedAndAuditAsync(
                context,
                claimId,
                pending.Id,
                writeBackType,
                payload,
                "Policy administration system is not configured.",
                cancellationToken);

            throw new ProviderNotConfiguredException(
                $"Policy administration system is not configured. Write-back history id: {failed.Id}.");
        }

        if (health.Status == ProviderStatus.Unavailable)
        {
            var failed = await MarkFailedAndAuditAsync(
                context,
                claimId,
                pending.Id,
                writeBackType,
                payload,
                "Policy administration system is unavailable.",
                cancellationToken);

            throw new ProviderUnavailableException(
                $"Policy administration system is unavailable. Write-back history id: {failed.Id}.");
        }

        try
        {
            var providerResult = await _provider.ExecuteAsync(
                new PolicyAdminWriteBackProviderRequest(
                    TenantId: context.TenantId,
                    WorkspaceId: context.WorkspaceId,
                    ClaimId: claimId,
                    WriteBackType: writeBackType,
                    Fields: payload.Fields,
                    InternalWriteBackId: pending.Id,
                    CorrelationId: context.CorrelationId),
                cancellationToken);

            if (!providerResult.Confirmed)
            {
                return await MarkFailedAndAuditAsync(
                    context,
                    claimId,
                    pending.Id,
                    writeBackType,
                    payload,
                    "External system did not confirm success.",
                    cancellationToken);
            }

            var succeeded = await _historyRepository.MarkSucceededAsync(
                new ClaimWriteBackHistoryUpdateRequest(
                    TenantId: context.TenantId,
                    WorkspaceId: context.WorkspaceId,
                    HistoryId: pending.Id,
                    Status: ClaimWriteBackStatusValues.Succeeded,
                    Summary: SafeProviderSummary(providerResult.SafeSummary, writeBackType),
                    ExternalReference: providerResult.ExternalReference,
                    OccurredAt: DateTimeOffset.UtcNow),
                cancellationToken);

            await WriteAuditAsync(
                context,
                claimId,
                succeeded.Id,
                eventType: IntegrationWriteBackAuditEventTypes.Succeeded,
                action: "succeeded",
                summary: BuildAuditSummary("succeeded", writeBackType, payload),
                reason: providerResult.ExternalReference,
                cancellationToken);

            return succeeded;
        }
        catch (ProviderUnavailableException)
        {
            var failed = await MarkFailedAndAuditAsync(
                context,
                claimId,
                pending.Id,
                writeBackType,
                payload,
                "External provider became unavailable during write-back.",
                cancellationToken);

            throw new ProviderUnavailableException(
                $"Policy administration write-back failed. Write-back history id: {failed.Id}.");
        }
        catch (Exception ex) when (ex is not ProviderNotConfiguredException and not ProviderUnavailableException)
        {
            await MarkFailedAndAuditAsync(
                context,
                claimId,
                pending.Id,
                writeBackType,
                payload,
                "External write-back failed. See server logs with correlation id.",
                cancellationToken);

            throw new ProviderUnavailableException(
                "Policy administration write-back failed. Use the correlation ID when contacting support.");
        }
    }

    private async Task EnsureWriteBackPermissionAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _permissionService.EnsureAsync(Permissions.ClaimsWriteBack, cancellationToken);
        }
        catch (ForbiddenException)
        {
            await _permissionService.EnsureAsync(Permissions.IntegrationsManage, cancellationToken);
        }
    }

    private async Task<ClaimWriteBackHistoryItemDto> MarkFailedAndAuditAsync(
        TenantContext context,
        string claimId,
        string historyId,
        string writeBackType,
        PolicyAdminWriteBackPayload payload,
        string safeFailureSummary,
        CancellationToken cancellationToken)
    {
        var failed = await _historyRepository.MarkFailedAsync(
            new ClaimWriteBackHistoryUpdateRequest(
                TenantId: context.TenantId,
                WorkspaceId: context.WorkspaceId,
                HistoryId: historyId,
                Status: ClaimWriteBackStatusValues.Failed,
                Summary: safeFailureSummary,
                ExternalReference: null,
                OccurredAt: DateTimeOffset.UtcNow),
            cancellationToken);

        await WriteAuditAsync(
            context,
            claimId,
            failed.Id,
            eventType: IntegrationWriteBackAuditEventTypes.Failed,
            action: "failed",
            summary: BuildAuditSummary("failed", writeBackType, payload),
            reason: safeFailureSummary,
            cancellationToken);

        return failed;
    }

    private async Task WriteAuditAsync(
        TenantContext context,
        string claimId,
        string historyId,
        string eventType,
        string action,
        string summary,
        string? reason,
        CancellationToken cancellationToken)
    {
        await _auditWriter.WriteAsync(
            new AuditEventCreateRequest(
                TenantId: context.TenantId,
                WorkspaceId: context.WorkspaceId,
                EventType: eventType,
                ActorType: "user",
                ActorUserId: context.UserId,
                Action: action,
                EntityType: "claim_write_back",
                EntityId: historyId,
                Summary: summary,
                Reason: RedactAndTruncate(reason, 500),
                CorrelationId: context.CorrelationId,
                OccurredAt: DateTimeOffset.UtcNow),
            cancellationToken);
    }

    private static void ValidateRequest(
        string claimId,
        string writeBackType,
        PolicyAdminWriteBackPayload payload)
    {
        if (string.IsNullOrWhiteSpace(claimId))
        {
            throw new ValidationException("claimId is required.");
        }

        if (string.IsNullOrWhiteSpace(writeBackType))
        {
            throw new ValidationException("writeBackType is required.");
        }

        if (!AllowedWriteBackTypes.Contains(writeBackType))
        {
            throw new ValidationException("Unsupported writeBackType.");
        }

        if (payload is null)
        {
            throw new ValidationException("payload is required.");
        }

        if (payload.Fields is null)
        {
            throw new ValidationException("payload.fields is required.");
        }

        if (payload.Fields.Keys.Any(key => SensitivePayloadKeys.Contains(key)))
        {
            throw new ValidationException("payload contains a sensitive field that must not be sent through this service.");
        }

        if (ConsequentialWriteBackTypes.Contains(writeBackType) && string.IsNullOrWhiteSpace(payload.HumanApprovalId))
        {
            throw new ValidationException("humanApprovalId is required for this writeBackType.");
        }
    }

    private static string BuildSafeSummary(string writeBackType, PolicyAdminWriteBackPayload payload)
    {
        var fieldNames = payload.Fields.Keys
            .Where(key => !SensitivePayloadKeys.Contains(key))
            .OrderBy(key => key, StringComparer.Ordinal)
            .Take(10)
            .ToArray();

        var fieldList = fieldNames.Length == 0 ? "no fields" : string.Join(", ", fieldNames);
        return $"Policy admin write-back {writeBackType} prepared with fields: {fieldList}.";
    }

    private static string BuildAuditSummary(string outcome, string writeBackType, PolicyAdminWriteBackPayload payload)
    {
        var safeSummary = BuildSafeSummary(writeBackType, payload);
        return RedactAndTruncate($"Policy admin write-back {outcome}. {safeSummary}", 1000) ?? string.Empty;
    }

    private static string SafeProviderSummary(string? providerSummary, string writeBackType)
    {
        if (string.IsNullOrWhiteSpace(providerSummary))
        {
            return $"Policy admin write-back {writeBackType} succeeded.";
        }

        return RedactAndTruncate(providerSummary, 1000) ?? $"Policy admin write-back {writeBackType} succeeded.";
    }

    private static string? RedactAndTruncate(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        var sanitized = value
            .Replace("\r", " ")
            .Replace("\n", " ")
            .Trim();

        return sanitized.Length <= maxLength ? sanitized : sanitized[..maxLength];
    }
}

public static class IntegrationWriteBackAuditEventTypes
{
    public const string Attempted = "integration.writeback.attempted";
    public const string Succeeded = "integration.writeback.succeeded";
    public const string Failed = "integration.writeback.failed";
}
```

---

## 9. Important implementation notes

### 9.1 No optimistic success path

The following is forbidden:

```csharp
// Forbidden pattern
await _provider.ExecuteAsync(request, ct);
return MarkSucceededAsync(...);
```

The service must inspect a real provider result and require:

```csharp
providerResult.Confirmed == true
```

before recording `succeeded`.

### 9.2 Failure visibility

A failed provider health check or provider execution failure must not disappear into logs only. The claim detail screen depends on `ClaimDetailDto.WriteBackHistory`, so this service must persist a failed history row before surfacing the provider exception.

### 9.3 Provider-not-configured behavior

If `IProviderHealthCheck.CheckAsync("policy_admin_system")` returns `NotConfigured`, the service must:

```text
1. Create the pending history row if validation and authorization passed.
2. Write integration.writeback.attempted audit event.
3. Mark the history row as failed.
4. Write integration.writeback.failed audit event.
5. Throw ProviderNotConfiguredException.
```

It must not return a fake success or skip the history row.

### 9.4 Consequential decision boundary

This service can export or synchronize approved state. It must not create the approval decision itself. For consequential write-back types, it must verify an existing completed human approval record through `IWriteBackApprovalReadRepository`.

---

## 10. Tests to add

### 10.1 Provider-not-configured test

```csharp
[Fact]
public async Task ExecuteAsync_ThrowsProviderNotConfigured_WhenPolicyAdminProviderNotConfigured()
{
    var providerHealth = new Mock<IProviderHealthCheck>();
    providerHealth.Setup(x => x.CheckAsync(ProviderCodes.PolicyAdminSystem, It.IsAny<CancellationToken>()))
        .ReturnsAsync(new ProviderHealthResult(
            ProviderCodes.PolicyAdminSystem,
            ProviderStatus.NotConfigured,
            "Not configured.",
            DateTimeOffset.UtcNow));

    var historyRepository = new Mock<IClaimWriteBackHistoryRepository>();
    historyRepository.Setup(x => x.CreatePendingAsync(It.IsAny<ClaimWriteBackHistoryCreateRequest>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync(new ClaimWriteBackHistoryItemDto("wb-1", "policy_admin_system", ClaimWriteBackStatusValues.Pending, DateTimeOffset.UtcNow.ToString("O"), "Pending."));
    historyRepository.Setup(x => x.MarkFailedAsync(It.IsAny<ClaimWriteBackHistoryUpdateRequest>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync(new ClaimWriteBackHistoryItemDto("wb-1", "policy_admin_system", ClaimWriteBackStatusValues.Failed, DateTimeOffset.UtcNow.ToString("O"), "Failed."));

    var service = BuildService(providerHealth: providerHealth.Object, historyRepository: historyRepository.Object);

    await Assert.ThrowsAsync<ProviderNotConfiguredException>(() =>
        service.ExecuteAsync(
            "clm-1",
            PolicyAdminWriteBackTypeValues.DocumentReferenceSync,
            new PolicyAdminWriteBackPayload(new Dictionary<string, string?> { ["documentId"] = "doc-1" }),
            CancellationToken.None));

    historyRepository.Verify(x => x.MarkFailedAsync(It.IsAny<ClaimWriteBackHistoryUpdateRequest>(), It.IsAny<CancellationToken>()), Times.Once);
}
```

### 10.2 Permission enforcement test

```csharp
[Fact]
public async Task ExecuteAsync_RequiresClaimsWriteBackOrIntegrationsManagePermission()
{
    var permissionService = new Mock<IPermissionService>();
    permissionService.Setup(x => x.EnsureAsync(Permissions.ClaimsWriteBack, It.IsAny<CancellationToken>()))
        .ThrowsAsync(new ForbiddenException("Missing claims.writeback."));
    permissionService.Setup(x => x.EnsureAsync(Permissions.IntegrationsManage, It.IsAny<CancellationToken>()))
        .ThrowsAsync(new ForbiddenException("Missing integrations.manage."));

    var provider = new Mock<IPolicyAdminWriteBackProvider>();
    var service = BuildService(permissionService: permissionService.Object, provider: provider.Object);

    await Assert.ThrowsAsync<ForbiddenException>(() =>
        service.ExecuteAsync(
            "clm-1",
            PolicyAdminWriteBackTypeValues.DocumentReferenceSync,
            new PolicyAdminWriteBackPayload(new Dictionary<string, string?>()),
            CancellationToken.None));

    provider.Verify(x => x.ExecuteAsync(It.IsAny<PolicyAdminWriteBackProviderRequest>(), It.IsAny<CancellationToken>()), Times.Never);
}
```

### 10.3 Audit-write-on-every-outcome tests

```text
PolicyAdminWriteBackServiceTests
- ExecuteAsync_WritesAttemptedAndSucceededAudit_WhenProviderConfirms
- ExecuteAsync_WritesAttemptedAndFailedAudit_WhenProviderNotConfigured
- ExecuteAsync_WritesAttemptedAndFailedAudit_WhenProviderUnavailable
- ExecuteAsync_WritesAttemptedAndFailedAudit_WhenProviderDoesNotConfirm
```

Example assertion:

```csharp
auditWriter.Verify(x => x.WriteAsync(
    It.Is<AuditEventCreateRequest>(evt => evt.EventType == IntegrationWriteBackAuditEventTypes.Attempted),
    It.IsAny<CancellationToken>()), Times.Once);

auditWriter.Verify(x => x.WriteAsync(
    It.Is<AuditEventCreateRequest>(evt => evt.EventType == IntegrationWriteBackAuditEventTypes.Failed),
    It.IsAny<CancellationToken>()), Times.Once);
```

### 10.4 Write-back-history-persisted-on-failure test

This test is required and must not be skipped.

```csharp
[Fact]
public async Task ExecuteAsync_PersistsFailedWriteBackHistory_WhenProviderCallFails()
{
    var provider = new Mock<IPolicyAdminWriteBackProvider>();
    provider.Setup(x => x.ExecuteAsync(It.IsAny<PolicyAdminWriteBackProviderRequest>(), It.IsAny<CancellationToken>()))
        .ThrowsAsync(new ProviderUnavailableException("Provider unavailable."));

    var historyRepository = new Mock<IClaimWriteBackHistoryRepository>();
    historyRepository.Setup(x => x.CreatePendingAsync(It.IsAny<ClaimWriteBackHistoryCreateRequest>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync(new ClaimWriteBackHistoryItemDto("wb-1", "policy_admin_system", ClaimWriteBackStatusValues.Pending, DateTimeOffset.UtcNow.ToString("O"), "Pending."));
    historyRepository.Setup(x => x.MarkFailedAsync(It.IsAny<ClaimWriteBackHistoryUpdateRequest>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync(new ClaimWriteBackHistoryItemDto("wb-1", "policy_admin_system", ClaimWriteBackStatusValues.Failed, DateTimeOffset.UtcNow.ToString("O"), "Failed."));

    var service = BuildService(provider: provider.Object, historyRepository: historyRepository.Object);

    await Assert.ThrowsAsync<ProviderUnavailableException>(() =>
        service.ExecuteAsync(
            "clm-1",
            PolicyAdminWriteBackTypeValues.DocumentReferenceSync,
            new PolicyAdminWriteBackPayload(new Dictionary<string, string?> { ["documentId"] = "doc-1" }),
            CancellationToken.None));

    historyRepository.Verify(x => x.CreatePendingAsync(It.IsAny<ClaimWriteBackHistoryCreateRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    historyRepository.Verify(x => x.MarkFailedAsync(It.IsAny<ClaimWriteBackHistoryUpdateRequest>(), It.IsAny<CancellationToken>()), Times.Once);
}
```

### 10.5 Provider confirmation required test

```csharp
[Fact]
public async Task ExecuteAsync_DoesNotMarkSucceeded_WhenProviderDoesNotConfirm()
{
    var provider = new Mock<IPolicyAdminWriteBackProvider>();
    provider.Setup(x => x.ExecuteAsync(It.IsAny<PolicyAdminWriteBackProviderRequest>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync(new PolicyAdminWriteBackProviderResult(
            Confirmed: false,
            TargetSystem: "policy_admin_system",
            ExternalReference: null,
            SafeSummary: "Not confirmed."));

    var historyRepository = new Mock<IClaimWriteBackHistoryRepository>();
    historyRepository.Setup(x => x.CreatePendingAsync(It.IsAny<ClaimWriteBackHistoryCreateRequest>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync(new ClaimWriteBackHistoryItemDto("wb-1", "policy_admin_system", ClaimWriteBackStatusValues.Pending, DateTimeOffset.UtcNow.ToString("O"), "Pending."));
    historyRepository.Setup(x => x.MarkFailedAsync(It.IsAny<ClaimWriteBackHistoryUpdateRequest>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync(new ClaimWriteBackHistoryItemDto("wb-1", "policy_admin_system", ClaimWriteBackStatusValues.Failed, DateTimeOffset.UtcNow.ToString("O"), "Failed."));

    var service = BuildService(provider: provider.Object, historyRepository: historyRepository.Object);

    var result = await service.ExecuteAsync(
        "clm-1",
        PolicyAdminWriteBackTypeValues.DocumentReferenceSync,
        new PolicyAdminWriteBackPayload(new Dictionary<string, string?> { ["documentId"] = "doc-1" }),
        CancellationToken.None);

    Assert.Equal(ClaimWriteBackStatusValues.Failed, result.Status);
    historyRepository.Verify(x => x.MarkSucceededAsync(It.IsAny<ClaimWriteBackHistoryUpdateRequest>(), It.IsAny<CancellationToken>()), Times.Never);
}
```

### 10.6 Consequential write-back human approval test

```csharp
[Fact]
public async Task ExecuteAsync_ClaimStatusUpdateRequiresCompletedHumanApproval()
{
    var approvals = new Mock<IWriteBackApprovalReadRepository>();
    approvals.Setup(x => x.HasCompletedHumanApprovalAsync(
            It.IsAny<string>(),
            It.IsAny<string?>(),
            "clm-1",
            PolicyAdminWriteBackTypeValues.ClaimStatusUpdate,
            "approval-1",
            It.IsAny<CancellationToken>()))
        .ReturnsAsync(false);

    var provider = new Mock<IPolicyAdminWriteBackProvider>();
    var service = BuildService(approvalReadRepository: approvals.Object, provider: provider.Object);

    await Assert.ThrowsAsync<WorkflowConflictException>(() =>
        service.ExecuteAsync(
            "clm-1",
            PolicyAdminWriteBackTypeValues.ClaimStatusUpdate,
            new PolicyAdminWriteBackPayload(
                new Dictionary<string, string?> { ["status"] = "approved" },
                HumanApprovalId: "approval-1"),
            CancellationToken.None));

    provider.Verify(x => x.ExecuteAsync(It.IsAny<PolicyAdminWriteBackProviderRequest>(), It.IsAny<CancellationToken>()), Times.Never);
}
```

### 10.7 Tenant and workspace enforcement tests

```text
PolicyAdminWriteBackServiceTests
- ExecuteAsync_UsesTenantWorkspaceFromTenantContext
- ExecuteAsync_PassesTenantWorkspaceToProviderRequest
- ExecuteAsync_PassesTenantWorkspaceToHistoryRepository
- ExecuteAsync_DoesNotAcceptTenantWorkspaceFromPayload
```

### 10.8 Contract test for ClaimWriteBackHistoryItemDto

```csharp
[Fact]
public void ClaimWriteBackHistoryItemDto_MatchesFrontendContract()
{
    var dto = new ClaimWriteBackHistoryItemDto(
        Id: "wb-1",
        TargetSystem: "policy_admin_system",
        Status: ClaimWriteBackStatusValues.Succeeded,
        OccurredAt: DateTimeOffset.UtcNow.ToString("O"),
        Summary: "Write-back succeeded.");

    using var json = JsonSerializer.SerializeToDocument(dto, new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    });

    var names = json.RootElement.EnumerateObject().Select(p => p.Name).OrderBy(x => x).ToArray();

    Assert.Equal(new[]
    {
        "id",
        "occurredAt",
        "status",
        "summary",
        "targetSystem"
    }, names);
}
```

---

## 11. TODOs and confirmation points

| Area | Current implementation decision | TODO / confirmation needed |
|---|---|---|
| Policy admin target systems | Defines `IPolicyAdminWriteBackProvider` only | Confirm the actual insurer policy administration system(s), APIs, authentication, payload format, and confirmation semantics. |
| Provider code | Uses logical provider code `policy_admin_system` | Confirm whether provider health should be per tenant, per workspace, per product line, or per integration connection. |
| Retry/backoff | Explicitly out of scope | Define retry policy, dead-letter behavior, max attempts, and operator-visible retry controls. |
| Idempotency | Explicitly out of scope | Define idempotency key strategy to prevent double-writing after retry or network ambiguity. |
| Consequential write-back approval | Requires completed human approval for claim status and payment write-backs | Confirm exact approval entity fields and matching rules for writeBackType and payload hash. |
| Payload validation | Blocks sensitive key names and audits only summaries | Confirm per-writeBackType required fields and strict schema validation. |
| Audit taxonomy | Uses integration.writeback.attempted/succeeded/failed | Confirm whether these should be formal event types or stored as `data_changed` with action names. |
| Write-back status values | Uses pending/succeeded/failed | Confirm whether additional statuses such as queued, retrying, cancelled, or manual_intervention_required are needed after retry design. |

---

## 12. Completion report template

```text
Service implemented: IPolicyAdminWriteBackService.

Files to create or modify:
- src/InsuranceIntelligence.Application/Integrations/IPolicyAdminWriteBackService.cs
- src/InsuranceIntelligence.Application/Integrations/PolicyAdminWriteBackService.cs
- src/InsuranceIntelligence.Application/Integrations/PolicyAdminWriteBackModels.cs
- src/InsuranceIntelligence.Application/Integrations/IPolicyAdminWriteBackProvider.cs
- src/InsuranceIntelligence.Application/Integrations/IClaimWriteBackHistoryRepository.cs
- src/InsuranceIntelligence.Application/Integrations/IWriteBackApprovalReadRepository.cs
- src/InsuranceIntelligence.Application/Common/Authorization/Permissions.cs
- src/InsuranceIntelligence.Application/Common/Providers/ProviderCodes.cs
- src/InsuranceIntelligence.Tests/Application/Integrations/PolicyAdminWriteBackServiceTests.cs
- src/InsuranceIntelligence.Tests/Application/Integrations/ClaimWriteBackHistoryContractMatchesFrontendTypesTest.cs

Logic implemented:
- Validates claimId, writeBackType, and payload.
- Enforces claims.writeback or integrations.manage.
- Resolves tenant/workspace/user from ITenantContextAccessor.
- Requires completed human approval for consequential write-back types.
- Creates pending write-back history before provider call.
- Checks policy_admin_system provider health before provider execution.
- Executes the provider call only when provider is connected.
- Marks succeeded only when provider returns Confirmed = true.
- Marks failed and writes failure audit for provider not configured, provider unavailable, provider non-confirmation, and provider execution failure.
- Returns the fixed ClaimWriteBackHistoryItemDto shape.

Security impact:
- Prevents fake external-system success.
- Prevents raw payload/credential logging.
- Prevents client-provided tenant/workspace authority.
- Prevents write-back execution without required permission.
- Prevents consequential write-back without completed human approval.

Tenant/workspace isolation impact:
- All history repository, approval verification, provider, and audit calls receive TenantId and WorkspaceId from TenantContext.
- Payload cannot override tenant/workspace.

Authentication/authorization impact:
- Requires claims.writeback or integrations.manage.
- Permission evaluation remains server-side through IPermissionService.

Data model or migration impact:
- Requires persistent claim write-back history storage linked to claim, tenant, workspace, status, target system, summary, external reference, correlation ID, and occurredAt.
- ClaimDetailDto already has WriteBackHistory; IClaimsQueryService must read from this persistence model without changing its DTO shape.

AI/human approval impact:
- No AI decision logic is implemented.
- Service does not approve/reject/settle/pay claims.
- Consequential write-backs require an already completed human approval record.

Tests added or required:
- Provider-not-configured test.
- Permission enforcement test.
- Audit-write-on-every-outcome tests.
- Write-back-history-persisted-on-failure test.
- Provider confirmation required test.
- Human approval required for consequential write-back test.
- Tenant/workspace enforcement tests.
- ClaimWriteBackHistoryItemDto contract test.

TODOs:
- Confirm target policy administration systems and APIs.
- Confirm retry/backoff policy.
- Confirm idempotency key strategy.
- Confirm exact approval matching logic for consequential write-backs.
- Confirm per-writeBackType payload schemas.

Missing dependencies:
- Concrete IPolicyAdminWriteBackProvider implementation is intentionally out of scope and depends on insurer-specific systems.
- EF repository implementation for IClaimWriteBackHistoryRepository is required.
- EF repository implementation for IWriteBackApprovalReadRepository is required.
- Provider health registration for policy_admin_system is required.

No fake data confirmation:
- No mock, simulated, in-memory, seeded-demo, placeholder, fallback, or fake external write-back path is introduced.
- The service never records succeeded without real provider confirmation.
- ProviderNotConfiguredException is returned when the provider is not configured.

Recommended next step:
- Implement EF persistence for claim write-back history and approval verification, then create the insurer-specific Infrastructure adapter for IPolicyAdminWriteBackProvider once the target policy administration system is confirmed.
```
