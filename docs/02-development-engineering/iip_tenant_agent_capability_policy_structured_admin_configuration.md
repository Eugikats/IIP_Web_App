# IIP Tenant Agent Capability Policy — Structured Admin Configuration

**Product:** Insurance Intelligence Platform (IIP)  
**Staff application:** IIP Operations  
**Document type:** Implementation-ready Application-layer policy and admin configuration specification  
**Area:** Tenant AI capability governance, admin configuration, human-review policy  
**Status:** New-repository implementation specification  
**Date:** July 2026

---

## Shared rules used by this implementation output

This output follows the same implementation pattern used by the existing IIP backend service-output documents for Core Ports, Command Center, Approvals, Notifications, Knowledge Ingestion, Documents, Reporting, and Conversational Session/Context Architecture.

```text
1. Tenant AI policy is structured configuration, not a free-text policy document and not an AI-interpreted document.
2. Tenant AI policy may only narrow platform-wide AI capability; it must never widen it.
3. AI is never the final actor for a consequential insurance decision: claim approval/rejection, claim settlement, payment release, policy binding, premium setting, underwriting decline/acceptance, or fraud determination.
4. Tenant configuration cannot bypass ApprovalDecisionGuard.EnsureHumanForConsequentialDecision.
5. Tenant configuration cannot disable audit writing.
6. Tenant configuration cannot disable tenant/workspace isolation.
7. Tenant configuration cannot enable mock, simulated, in-memory, seeded-demo, or fallback provider behavior in Staging, Production, or ManagedApplicationReviewer.
8. Resolve tenant/workspace from ITenantContextAccessor on every public service method.
9. Enforce backend permissions before any read or write of tenant policy.
10. Write durable audit events for every policy change.
11. Existing services must treat policy checks as additive restrictions layered on top of existing permissions, provider checks, approval gates, and domain guards.
12. Keep Domain free of EF Core, Azure SDKs, ASP.NET Core, provider SDKs, and frontend concerns.
```

Fixed inputs consumed by this document and not redesigned here:

```text
ITenantContextAccessor
IPermissionService
IAuditWriter
IProviderHealthCheck
ApprovalDecisionGuard.EnsureHumanForConsequentialDecision
IApprovalDecisionService
INotificationDraftService
INotificationSendService
ICommunicationSendingProvider
IKnowledgeSensitivityClassifier
IKnowledgeIngestionService
IRetrievalProvider
CommandCenterQueryService
IDocumentExtractionWorkflowService
Permissions
AuditEventTypeValues
```

This document defines a structured tenant/workspace policy service and enforcement points. It does not replace the global platform ceiling, the approval decision service, the notification draft/send pipeline, the document pipeline, or the knowledge ingestion/retrieval pipeline.

---

## 1. Scope of this pass

This pass implements structured tenant AI capability policy.

Implemented capabilities:

```text
- Define TenantAgentCapabilityPolicy as explicit, enumerable tenant/workspace-scoped configuration.
- Define conservative default policy for newly provisioned tenants.
- Define ITenantAgentCapabilityPolicyService with GetPolicyAsync and UpdatePolicyAsync.
- Enforce Permissions.AgentPolicyManage for policy writes.
- Enforce platform ceiling through TenantAgentCapabilityPolicyValidator.EnsureDoesNotExceedPlatformCeiling.
- Reject any policy write that attempts to allow AI to finalize consequential decisions, bypass human approval, bypass audit, or disable platform safety controls.
- Write agent_policy_changed audit events with before/after safe configuration values.
- Define backend DTOs for admin settings screen read/write.
- Define enforcement points in CommandCenterQueryService, INotificationSendService, IKnowledgeIngestionService/retrieval, and document extraction workflows.
```

Not implemented in this pass:

```text
- Frontend admin settings screen.
- Policy-change approval workflow.
- Tenant commercial packaging or marketplace configuration UX.
- A free-text policy interpreter.
- A tenant-controlled prompt override mechanism.
- Any ability for tenants to enable AI final decisions.
- Any ability for tenants to disable audit logging.
- Any ability for tenants to bypass ApprovalDecisionGuard.
```

---

## 2. Files to create or modify

Use the existing repository structure where present. Do not create a parallel architecture if equivalent files already exist.

### 2.1 Application files

```text
src/InsuranceIntelligence.Application/AgentPolicy/ITenantAgentCapabilityPolicyService.cs
src/InsuranceIntelligence.Application/AgentPolicy/TenantAgentCapabilityPolicyService.cs
src/InsuranceIntelligence.Application/AgentPolicy/TenantAgentCapabilityPolicyModels.cs
src/InsuranceIntelligence.Application/AgentPolicy/TenantAgentCapabilityPolicyValidator.cs
src/InsuranceIntelligence.Application/AgentPolicy/ITenantAgentCapabilityPolicyRepository.cs
src/InsuranceIntelligence.Application/AgentPolicy/TenantAgentCapabilityPolicyDefaults.cs
src/InsuranceIntelligence.Application/AgentPolicy/TenantAgentPolicyAuditActions.cs
src/InsuranceIntelligence.Application/Common/Authorization/Permissions.cs
src/InsuranceIntelligence.Application/Common/Audit/AuditEventTypeValues.cs
```

### 2.2 Infrastructure files

```text
src/InsuranceIntelligence.Infrastructure/AgentPolicy/EfTenantAgentCapabilityPolicyRepository.cs
src/InsuranceIntelligence.Infrastructure/Persistence/Entities/TenantAgentCapabilityPolicyEntity.cs
src/InsuranceIntelligence.Infrastructure/Persistence/Configurations/TenantAgentCapabilityPolicyEntityConfiguration.cs
src/InsuranceIntelligence.Infrastructure/Persistence/Migrations/*_AddTenantAgentCapabilityPolicy.cs
```

### 2.3 API files

```text
src/InsuranceIntelligence.Api/Controllers/Admin/TenantAgentCapabilityPolicyController.cs
src/InsuranceIntelligence.Api/Contracts/Admin/TenantAgentCapabilityPolicyDto.cs
src/InsuranceIntelligence.Api/Contracts/Admin/TenantAgentCapabilityPolicyUpdateRequest.cs
src/InsuranceIntelligence.Api/DependencyInjection/StrictJsonRegistration.cs
```

### 2.4 Existing service files to modify with additive checks

```text
src/InsuranceIntelligence.Application/CommandCenter/CommandCenterQueryService.cs
src/InsuranceIntelligence.Application/Communications/NotificationSendService.cs
src/InsuranceIntelligence.Application/Knowledge/KnowledgeIngestionService.cs
src/InsuranceIntelligence.Application/Knowledge/* retrieval service or query orchestration using IRetrievalProvider
src/InsuranceIntelligence.Application/Documents/DocumentExtractionWorkflowService.cs
```

### 2.5 Test files

```text
src/InsuranceIntelligence.Tests/Application/AgentPolicy/TenantAgentCapabilityPolicyServiceTests.cs
src/InsuranceIntelligence.Tests/Application/AgentPolicy/TenantAgentCapabilityPolicyValidatorTests.cs
src/InsuranceIntelligence.Tests/Application/AgentPolicy/TenantAgentCapabilityPolicyEnforcementTests.cs
src/InsuranceIntelligence.Tests/Api/Admin/TenantAgentCapabilityPolicyContractTests.cs
src/InsuranceIntelligence.Tests/Security/TenantAgentCapabilityPolicyIsolationTests.cs
```

---

## 3. Permissions and audit constants to add

### 3.1 Permissions

Add this constant to the canonical `Permissions` class.

```csharp
namespace InsuranceIntelligence.Application.Common.Authorization;

public static partial class Permissions
{
    public const string AgentPolicyManage = "agent_policy.manage";
}
```

Rules:

```text
- AgentPolicyManage is required for UpdatePolicyAsync.
- GetPolicyAsync may be used internally by Application services without giving users policy-management rights.
- Admin API reads of the policy should require AgentPolicyManage or an existing admin settings permission according to controller policy.
- AgentPolicyManage does not imply authority to bypass platform ceilings.
```

### 3.2 Audit event type

Add this event type if the audit taxonomy allows policy-specific event types. If not, use `AuditEventTypeValues.DataChanged` with the action string `agent_policy.changed`.

```csharp
namespace InsuranceIntelligence.Application.Common.Audit;

public static partial class AuditEventTypeValues
{
    public const string AgentPolicyChanged = "agent_policy_changed";
}
```

### 3.3 Audit action values

```csharp
namespace InsuranceIntelligence.Application.AgentPolicy;

public static class TenantAgentPolicyAuditActions
{
    public const string PolicyChanged = "agent_policy.changed";
}
```

---

## 4. Domain model and policy values

### 4.1 TenantAgentCapabilityPolicy

The policy is tenant/workspace scoped and explicit. It is not a free-text document.

```csharp
namespace InsuranceIntelligence.Application.AgentPolicy;

public sealed record TenantAgentCapabilityPolicy(
    string PolicyId,
    string TenantId,
    string? WorkspaceId,
    bool AutoDraftCustomerCommunicationsEnabled,
    decimal AutoSendRequiresApprovalAboveAmount,
    bool ManagementSummaryGenerationEnabled,
    IReadOnlySet<string> KnowledgeRetrievalScopeRestriction,
    decimal RequireTwoPersonApprovalAboveAmount,
    bool DocumentAutoExtractionEnabled,
    string PlatformConsequentialDecisionMode,
    bool AuditRequired,
    int Version,
    string UpdatedByUserId,
    DateTimeOffset UpdatedAt);
```

Field semantics:

| Field | Meaning | Platform ceiling |
|---|---|---|
| AutoDraftCustomerCommunicationsEnabled | Whether AI may draft customer communications at all. | Draft only; human approval/send workflow still applies. |
| AutoSendRequiresApprovalAboveAmount | Threshold above which any auto-send workflow, if later allowed by platform policy, requires approval. | Current IIP pipeline still requires human approval for customer sends regardless of this value. |
| ManagementSummaryGenerationEnabled | Whether AI may generate management summaries from reports/metrics. | Summaries must be grounded and auditable; no fabricated metrics. |
| KnowledgeRetrievalScopeRestriction | Additional knowledge sensitivity categories excluded by the tenant. | May only add exclusions beyond platform default; never reduce platform exclusions. |
| RequireTwoPersonApprovalAboveAmount | Amount threshold above which a second human approval is required. | Distinct from the standard single-human approval rule. |
| DocumentAutoExtractionEnabled | Whether automatic document extraction may run without a user manually starting it. | Extraction remains assistive; human review required for corrections and consequential decisions. |
| PlatformConsequentialDecisionMode | Fixed value `human_required`. | Not tenant-configurable. |
| AuditRequired | Fixed value `true`. | Not tenant-configurable. |

### 4.2 Fixed platform values

```csharp
namespace InsuranceIntelligence.Application.AgentPolicy;

public static class PlatformConsequentialDecisionModeValues
{
    public const string HumanRequired = "human_required";
}

public static class KnowledgeSensitivityCategoryValues
{
    public const string Public = "public";
    public const string Internal = "internal";
    public const string Confidential = "confidential";
    public const string Restricted = "restricted";
    public const string CustomerPii = "customer_pii";
    public const string Medical = "medical";
}
```

Category note:

```text
The final category list must come from IKnowledgeSensitivityClassifier. The values above are implementation placeholders aligned to likely classifier categories and must be reconciled against the actual classifier constants before migration.
```

---

## 5. Conservative default policy

New tenants should start with the most restrictive safe policy. A tenant must explicitly opt into more AI assistance, but even opted-in settings remain below the platform ceiling.

```csharp
namespace InsuranceIntelligence.Application.AgentPolicy;

public static class TenantAgentCapabilityPolicyDefaults
{
    public static TenantAgentCapabilityPolicy CreateDefault(
        string tenantId,
        string? workspaceId,
        string createdByUserId,
        DateTimeOffset now)
    {
        return new TenantAgentCapabilityPolicy(
            PolicyId: Guid.NewGuid().ToString("N"),
            TenantId: tenantId,
            WorkspaceId: workspaceId,
            AutoDraftCustomerCommunicationsEnabled: false,
            AutoSendRequiresApprovalAboveAmount: 0m,
            ManagementSummaryGenerationEnabled: false,
            KnowledgeRetrievalScopeRestriction: new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                KnowledgeSensitivityCategoryValues.Restricted,
                KnowledgeSensitivityCategoryValues.CustomerPii,
                KnowledgeSensitivityCategoryValues.Medical
            },
            RequireTwoPersonApprovalAboveAmount: 0m,
            DocumentAutoExtractionEnabled: false,
            PlatformConsequentialDecisionMode: PlatformConsequentialDecisionModeValues.HumanRequired,
            AuditRequired: true,
            Version: 1,
            UpdatedByUserId: createdByUserId,
            UpdatedAt: now);
    }
}
```

Default behavior:

```text
- Auto-draft customer communications: disabled.
- Customer sends: current platform pipeline still requires human approval.
- Management summaries: disabled until tenant opts in.
- Knowledge retrieval: excludes additional sensitive categories until tenant confirms allowed retrieval scope.
- Two-person approval: required above zero amount for amount-bearing consequential workflows until tenant policy is reviewed.
- Document auto extraction: disabled until tenant opts in.
- AI final consequential decision: never allowed.
- Audit: always required.
```

---

## 6. Repository port

```csharp
namespace InsuranceIntelligence.Application.AgentPolicy;

public interface ITenantAgentCapabilityPolicyRepository
{
    Task<TenantAgentCapabilityPolicy?> GetAsync(
        string tenantId,
        string? workspaceId,
        CancellationToken cancellationToken);

    Task UpsertAsync(
        TenantAgentCapabilityPolicy policy,
        CancellationToken cancellationToken);
}
```

Repository rules:

```text
- GetAsync and UpsertAsync must filter by tenantId and workspaceId.
- A workspace policy may override a tenant-level policy only by further narrowing capability, not by widening it beyond the tenant-level policy if tenant-level inheritance is implemented later.
- Repository must not accept or store free-text policy documents.
- Repository must not store prompt overrides as part of this policy.
```

---

## 7. Service contract

```csharp
namespace InsuranceIntelligence.Application.AgentPolicy;

public interface ITenantAgentCapabilityPolicyService
{
    Task<TenantAgentCapabilityPolicyDto> GetPolicyAsync(
        CancellationToken cancellationToken);

    Task<TenantAgentCapabilityPolicyDto> UpdatePolicyAsync(
        TenantAgentCapabilityPolicyUpdateRequest request,
        CancellationToken cancellationToken);
}
```

---

## 8. Backend DTO contract

These DTOs are the backend contract for an Admin Settings screen. Use camelCase JSON serialization consistently.

```csharp
namespace InsuranceIntelligence.Application.AgentPolicy;

public sealed record TenantAgentCapabilityPolicyDto(
    string policyId,
    string tenantId,
    string? workspaceId,
    bool autoDraftCustomerCommunicationsEnabled,
    decimal autoSendRequiresApprovalAboveAmount,
    bool managementSummaryGenerationEnabled,
    IReadOnlyList<string> knowledgeRetrievalScopeRestriction,
    decimal requireTwoPersonApprovalAboveAmount,
    bool documentAutoExtractionEnabled,
    string platformConsequentialDecisionMode,
    bool auditRequired,
    int version,
    string updatedByUserId,
    string updatedAt);

public sealed record TenantAgentCapabilityPolicyUpdateRequest(
    bool autoDraftCustomerCommunicationsEnabled,
    decimal autoSendRequiresApprovalAboveAmount,
    bool managementSummaryGenerationEnabled,
    IReadOnlyList<string> knowledgeRetrievalScopeRestriction,
    decimal requireTwoPersonApprovalAboveAmount,
    bool documentAutoExtractionEnabled,
    string? platformConsequentialDecisionMode = null,
    bool? auditRequired = null);
```

Important contract rule:

```text
platformConsequentialDecisionMode and auditRequired appear in the update request only so attempts to set unsafe values can be rejected explicitly rather than silently ignored. Valid writes must leave them null or set them to the platform-fixed values human_required and true. Unknown JSON members must be rejected at the API boundary.
```

Strict JSON rule:

```csharp
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow;
});
```

For MVC controllers:

```csharp
builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow;
});
```

---

## 9. Policy validator

The validator is the non-negotiable ceiling enforcement point. Every policy write must pass through it.

```csharp
using InsuranceIntelligence.Application.Common.Exceptions;

namespace InsuranceIntelligence.Application.AgentPolicy;

public static class TenantAgentCapabilityPolicyValidator
{
    public static void EnsureDoesNotExceedPlatformCeiling(TenantAgentCapabilityPolicy policy)
    {
        if (!string.Equals(policy.PlatformConsequentialDecisionMode, PlatformConsequentialDecisionModeValues.HumanRequired, StringComparison.Ordinal))
        {
            throw new ValidationException("Tenant policy cannot allow AI to finalize consequential insurance decisions.");
        }

        if (!policy.AuditRequired)
        {
            throw new ValidationException("Tenant policy cannot disable audit writing.");
        }

        if (policy.AutoSendRequiresApprovalAboveAmount < 0m)
        {
            throw new ValidationException("autoSendRequiresApprovalAboveAmount cannot be negative.");
        }

        if (policy.RequireTwoPersonApprovalAboveAmount < 0m)
        {
            throw new ValidationException("requireTwoPersonApprovalAboveAmount cannot be negative.");
        }

        if (policy.KnowledgeRetrievalScopeRestriction.Any(string.IsNullOrWhiteSpace))
        {
            throw new ValidationException("knowledgeRetrievalScopeRestriction cannot contain empty categories.");
        }
    }

    public static void ValidateUpdateRequest(TenantAgentCapabilityPolicyUpdateRequest request)
    {
        if (request is null)
        {
            throw new ValidationException("Policy update request is required.");
        }

        if (request.platformConsequentialDecisionMode is not null &&
            !string.Equals(request.platformConsequentialDecisionMode, PlatformConsequentialDecisionModeValues.HumanRequired, StringComparison.Ordinal))
        {
            throw new ValidationException("platformConsequentialDecisionMode is fixed to human_required and cannot be changed by tenant policy.");
        }

        if (request.auditRequired is false)
        {
            throw new ValidationException("auditRequired is fixed to true and cannot be disabled by tenant policy.");
        }

        if (request.autoSendRequiresApprovalAboveAmount < 0m)
        {
            throw new ValidationException("autoSendRequiresApprovalAboveAmount cannot be negative.");
        }

        if (request.requireTwoPersonApprovalAboveAmount < 0m)
        {
            throw new ValidationException("requireTwoPersonApprovalAboveAmount cannot be negative.");
        }

        if (request.knowledgeRetrievalScopeRestriction is null)
        {
            throw new ValidationException("knowledgeRetrievalScopeRestriction is required.");
        }

        if (request.knowledgeRetrievalScopeRestriction.Any(string.IsNullOrWhiteSpace))
        {
            throw new ValidationException("knowledgeRetrievalScopeRestriction cannot contain empty categories.");
        }
    }

    public static void EnsureRuntimeActionAllowed(
        TenantAgentCapabilityPolicy policy,
        string actionCode)
    {
        EnsureDoesNotExceedPlatformCeiling(policy);

        switch (actionCode)
        {
            case TenantAgentPolicyActionCodes.CustomerCommunicationDraft:
                if (!policy.AutoDraftCustomerCommunicationsEnabled)
                {
                    throw new WorkflowConflictException("Customer communication drafting is disabled by tenant AI policy.");
                }
                break;

            case TenantAgentPolicyActionCodes.ManagementSummaryGenerate:
                if (!policy.ManagementSummaryGenerationEnabled)
                {
                    throw new WorkflowConflictException("Management summary generation is disabled by tenant AI policy.");
                }
                break;

            case TenantAgentPolicyActionCodes.DocumentAutoExtraction:
                if (!policy.DocumentAutoExtractionEnabled)
                {
                    throw new WorkflowConflictException("Automatic document extraction is disabled by tenant AI policy.");
                }
                break;
        }
    }
}

public static class TenantAgentPolicyActionCodes
{
    public const string CustomerCommunicationDraft = "customer_communication.draft";
    public const string ManagementSummaryGenerate = "management_summary.generate";
    public const string DocumentAutoExtraction = "document.auto_extract";
}
```

Validator rules:

```text
- This validator is not a replacement for ApprovalDecisionGuard.
- ApprovalDecisionGuard remains the final domain guard for consequential human-decision enforcement.
- Tenant policy simply prevents services from starting actions that the tenant has chosen to disable or further restrict.
```

---

## 10. Full C# implementation

```csharp
using System.Text.Json;
using InsuranceIntelligence.Application.Common.Audit;
using InsuranceIntelligence.Application.Common.Authorization;
using InsuranceIntelligence.Application.Common.Exceptions;
using InsuranceIntelligence.Application.Common.Tenancy;

namespace InsuranceIntelligence.Application.AgentPolicy;

internal sealed class TenantAgentCapabilityPolicyService : ITenantAgentCapabilityPolicyService
{
    private readonly ITenantContextAccessor _tenantContextAccessor;
    private readonly IPermissionService _permissionService;
    private readonly ITenantAgentCapabilityPolicyRepository _repository;
    private readonly IAuditWriter _auditWriter;
    private readonly IClock _clock;

    public TenantAgentCapabilityPolicyService(
        ITenantContextAccessor tenantContextAccessor,
        IPermissionService permissionService,
        ITenantAgentCapabilityPolicyRepository repository,
        IAuditWriter auditWriter,
        IClock clock)
    {
        _tenantContextAccessor = tenantContextAccessor;
        _permissionService = permissionService;
        _repository = repository;
        _auditWriter = auditWriter;
        _clock = clock;
    }

    public async Task<TenantAgentCapabilityPolicyDto> GetPolicyAsync(CancellationToken cancellationToken)
    {
        var context = _tenantContextAccessor.Current;

        var policy = await _repository.GetAsync(
            context.TenantId,
            context.WorkspaceId,
            cancellationToken);

        policy ??= TenantAgentCapabilityPolicyDefaults.CreateDefault(
            context.TenantId,
            context.WorkspaceId,
            createdByUserId: "system",
            now: _clock.UtcNow);

        TenantAgentCapabilityPolicyValidator.EnsureDoesNotExceedPlatformCeiling(policy);
        return ToDto(policy);
    }

    public async Task<TenantAgentCapabilityPolicyDto> UpdatePolicyAsync(
        TenantAgentCapabilityPolicyUpdateRequest request,
        CancellationToken cancellationToken)
    {
        TenantAgentCapabilityPolicyValidator.ValidateUpdateRequest(request);

        var context = _tenantContextAccessor.Current;
        await _permissionService.EnsureAsync(Permissions.AgentPolicyManage, cancellationToken);

        var existing = await _repository.GetAsync(
            context.TenantId,
            context.WorkspaceId,
            cancellationToken);

        existing ??= TenantAgentCapabilityPolicyDefaults.CreateDefault(
            context.TenantId,
            context.WorkspaceId,
            createdByUserId: "system",
            now: _clock.UtcNow);

        var updated = existing with
        {
            AutoDraftCustomerCommunicationsEnabled = request.autoDraftCustomerCommunicationsEnabled,
            AutoSendRequiresApprovalAboveAmount = request.autoSendRequiresApprovalAboveAmount,
            ManagementSummaryGenerationEnabled = request.managementSummaryGenerationEnabled,
            KnowledgeRetrievalScopeRestriction = request.knowledgeRetrievalScopeRestriction
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToHashSet(StringComparer.OrdinalIgnoreCase),
            RequireTwoPersonApprovalAboveAmount = request.requireTwoPersonApprovalAboveAmount,
            DocumentAutoExtractionEnabled = request.documentAutoExtractionEnabled,
            PlatformConsequentialDecisionMode = PlatformConsequentialDecisionModeValues.HumanRequired,
            AuditRequired = true,
            Version = existing.Version + 1,
            UpdatedByUserId = context.UserId,
            UpdatedAt = _clock.UtcNow
        };

        TenantAgentCapabilityPolicyValidator.EnsureDoesNotExceedPlatformCeiling(updated);

        await _repository.UpsertAsync(updated, cancellationToken);

        await WritePolicyChangedAuditAsync(
            context,
            before: existing,
            after: updated,
            cancellationToken);

        return ToDto(updated);
    }

    private async Task WritePolicyChangedAuditAsync(
        TenantContext context,
        TenantAgentCapabilityPolicy before,
        TenantAgentCapabilityPolicy after,
        CancellationToken cancellationToken)
    {
        var beforeJson = JsonSerializer.Serialize(ToSafeAuditShape(before));
        var afterJson = JsonSerializer.Serialize(ToSafeAuditShape(after));

        await _auditWriter.WriteAsync(
            new AuditEventCreateRequest(
                TenantId: context.TenantId,
                WorkspaceId: context.WorkspaceId,
                EventType: AuditEventTypeValues.AgentPolicyChanged,
                ActorType: "user",
                ActorUserId: context.UserId,
                Action: TenantAgentPolicyAuditActions.PolicyChanged,
                EntityType: "tenant_agent_capability_policy",
                EntityId: after.PolicyId,
                Summary: $"Tenant agent capability policy changed by user {context.UserId}.",
                Reason: $"before={beforeJson}; after={afterJson}",
                CorrelationId: context.CorrelationId,
                OccurredAt: _clock.UtcNow),
            cancellationToken);
    }

    private static object ToSafeAuditShape(TenantAgentCapabilityPolicy policy)
    {
        return new
        {
            policy.PolicyId,
            policy.TenantId,
            policy.WorkspaceId,
            policy.AutoDraftCustomerCommunicationsEnabled,
            policy.AutoSendRequiresApprovalAboveAmount,
            policy.ManagementSummaryGenerationEnabled,
            knowledgeRetrievalScopeRestriction = policy.KnowledgeRetrievalScopeRestriction.OrderBy(x => x).ToArray(),
            policy.RequireTwoPersonApprovalAboveAmount,
            policy.DocumentAutoExtractionEnabled,
            policy.PlatformConsequentialDecisionMode,
            policy.AuditRequired,
            policy.Version
        };
    }

    private static TenantAgentCapabilityPolicyDto ToDto(TenantAgentCapabilityPolicy policy)
    {
        return new TenantAgentCapabilityPolicyDto(
            policyId: policy.PolicyId,
            tenantId: policy.TenantId,
            workspaceId: policy.WorkspaceId,
            autoDraftCustomerCommunicationsEnabled: policy.AutoDraftCustomerCommunicationsEnabled,
            autoSendRequiresApprovalAboveAmount: policy.AutoSendRequiresApprovalAboveAmount,
            managementSummaryGenerationEnabled: policy.ManagementSummaryGenerationEnabled,
            knowledgeRetrievalScopeRestriction: policy.KnowledgeRetrievalScopeRestriction.OrderBy(x => x).ToArray(),
            requireTwoPersonApprovalAboveAmount: policy.RequireTwoPersonApprovalAboveAmount,
            documentAutoExtractionEnabled: policy.DocumentAutoExtractionEnabled,
            platformConsequentialDecisionMode: policy.PlatformConsequentialDecisionMode,
            auditRequired: policy.AuditRequired,
            version: policy.Version,
            updatedByUserId: policy.UpdatedByUserId,
            updatedAt: policy.UpdatedAt.ToString("O"));
    }
}
```

`IClock` is the same clock abstraction used by the reporting/session service specs where present. If the repository has a different shared clock interface, use that existing interface instead of creating a duplicate.

---

## 11. API controller contract

```csharp
using InsuranceIntelligence.Application.AgentPolicy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InsuranceIntelligence.Api.Controllers.Admin;

[ApiController]
[Route("api/v1/admin/agent-policy")]
[Authorize]
public sealed class TenantAgentCapabilityPolicyController : ControllerBase
{
    private readonly ITenantAgentCapabilityPolicyService _policyService;

    public TenantAgentCapabilityPolicyController(ITenantAgentCapabilityPolicyService policyService)
    {
        _policyService = policyService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(TenantAgentCapabilityPolicyDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<TenantAgentCapabilityPolicyDto>> GetAsync(CancellationToken cancellationToken)
    {
        return Ok(await _policyService.GetPolicyAsync(cancellationToken));
    }

    [HttpPut]
    [ProducesResponseType(typeof(TenantAgentCapabilityPolicyDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<TenantAgentCapabilityPolicyDto>> UpdateAsync(
        [FromBody] TenantAgentCapabilityPolicyUpdateRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await _policyService.UpdatePolicyAsync(request, cancellationToken));
    }
}
```

Route note:

```text
If the backend wiring route map has an existing admin settings route convention, place this controller under that existing convention. Do not duplicate admin routes.
```

---

## 12. Enforcement points in existing services

Tenant policy checks are additive restrictions. They do not replace permissions, provider health checks, tenant context, audit writing, ApprovalDecisionGuard, or human approval workflows.

### 12.1 CommandCenterQueryService customer update draft

Before building a customer update draft:

```csharp
var policy = await _tenantAgentCapabilityPolicyService.GetPolicyAsync(cancellationToken);

TenantAgentCapabilityPolicyValidator.EnsureRuntimeActionAllowed(
    ToPolicy(policy),
    TenantAgentPolicyActionCodes.CustomerCommunicationDraft);
```

Required behavior:

```text
- Check policy before drafting, not after drafting.
- If disabled, return a safe workflow conflict response explaining that drafting is disabled by tenant AI policy.
- Do not call IChatCompletionProvider to draft a customer message when policy disables drafting.
- Existing human approval/send gates still apply when drafting is enabled.
```

### 12.2 NotificationSendService approval thresholds

The existing `INotificationSendService` already requires a linked, approved human decision before sending. Tenant policy adds further restrictions.

Required additive checks:

```text
- Load TenantAgentCapabilityPolicy before send execution.
- The current platform ceiling still requires a real human approval for customer sends regardless of AutoSendRequiresApprovalAboveAmount.
- If the notification is associated with an amount-bearing claim/policy/payment action and amount > RequireTwoPersonApprovalAboveAmount, require evidence of a second human approval before send/write-back if the send is tied to that consequential action.
- If amount is unavailable for a workflow where amount determines the policy gate, fail closed and require manual review.
```

Example helper:

```csharp
public static class TenantAgentPolicyApprovalGate
{
    public static bool RequiresTwoPersonApproval(
        TenantAgentCapabilityPolicy policy,
        decimal? amount)
    {
        if (amount is null)
        {
            return true;
        }

        return amount.Value > policy.RequireTwoPersonApprovalAboveAmount;
    }
}
```

Important rule:

```text
AutoSendRequiresApprovalAboveAmount must not be interpreted as permission to auto-send below the threshold. Current IIP send pipeline remains human-approved. This threshold is reserved for future low-risk automation, if the platform ceiling is ever revised through a separate architecture decision.
```

### 12.3 Knowledge ingestion and retrieval

Required additive checks:

```text
- Knowledge ingestion must classify content using IKnowledgeSensitivityClassifier.
- Retrieval must apply platform-default exclusions first.
- Retrieval must then apply TenantAgentCapabilityPolicy.KnowledgeRetrievalScopeRestriction as additional exclusions.
- Tenant policy may exclude more categories, never fewer than platform default.
- If a requested answer would require excluded content, the agent must state that the content is outside the tenant's configured AI retrieval scope and route to human/manual review.
```

Example filter composition:

```csharp
var policy = await _tenantAgentCapabilityPolicyService.GetPolicyAsync(cancellationToken);
var excluded = platformDefaultExcludedCategories
    .Concat(policy.knowledgeRetrievalScopeRestriction)
    .ToHashSet(StringComparer.OrdinalIgnoreCase);

var retrievalRequest = request with
{
    ExcludedSensitivityCategories = excluded.ToArray()
};
```

### 12.4 Document extraction workflow

Required additive checks:

```text
- If a user manually requests extraction and has document permissions, existing document extraction workflow may proceed according to existing rules.
- If an automatic/background extraction trigger is about to run, check DocumentAutoExtractionEnabled first.
- If disabled, mark the document as awaiting manual extraction/review rather than calling the provider.
- Do not call Azure Document Intelligence when automatic extraction is disabled by tenant policy.
```

Example:

```csharp
var policy = await _tenantAgentCapabilityPolicyService.GetPolicyAsync(cancellationToken);

TenantAgentCapabilityPolicyValidator.EnsureRuntimeActionAllowed(
    ToPolicy(policy),
    TenantAgentPolicyActionCodes.DocumentAutoExtraction);
```

### 12.5 Report Insight Agent / management summaries

Required additive checks:

```text
- Management summary generation must check ManagementSummaryGenerationEnabled before calling IChatCompletionProvider.
- ReportingService may still compute factual metrics; the policy controls AI-generated management summary prose, not the underlying report read itself.
```

---

## 13. Persistence model

```csharp
namespace InsuranceIntelligence.Infrastructure.Persistence.Entities;

public sealed class TenantAgentCapabilityPolicyEntity
{
    public string PolicyId { get; set; } = default!;
    public string TenantId { get; set; } = default!;
    public string? WorkspaceId { get; set; }
    public bool AutoDraftCustomerCommunicationsEnabled { get; set; }
    public decimal AutoSendRequiresApprovalAboveAmount { get; set; }
    public bool ManagementSummaryGenerationEnabled { get; set; }
    public string KnowledgeRetrievalScopeRestrictionJson { get; set; } = "[]";
    public decimal RequireTwoPersonApprovalAboveAmount { get; set; }
    public bool DocumentAutoExtractionEnabled { get; set; }
    public string PlatformConsequentialDecisionMode { get; set; } = default!;
    public bool AuditRequired { get; set; }
    public int Version { get; set; }
    public string UpdatedByUserId { get; set; } = default!;
    public DateTimeOffset UpdatedAt { get; set; }
}
```

Indexing rules:

```text
- Unique index on TenantId + WorkspaceId.
- WorkspaceId may be null for tenant-level policy where supported.
- If both tenant-level and workspace-level policies exist later, effective policy must be the stricter composition, not the looser one.
```

---

## 14. Tests to add

```text
TenantAgentCapabilityPolicyServiceTests
- GetPolicyAsync_ReturnsMostRestrictiveDefaultWhenMissing
- UpdatePolicyAsync_RequiresAgentPolicyManage
- UpdatePolicyAsync_WritesAuditWithBeforeAfterValues
- UpdatePolicyAsync_CannotDisableAuditRequired
- UpdatePolicyAsync_CannotSetPlatformConsequentialDecisionModeAwayFromHumanRequired
- UpdatePolicyAsync_RejectsNegativeThresholds
- UpdatePolicyAsync_IsTenantWorkspaceScoped

TenantAgentCapabilityPolicyValidatorTests
- EnsureDoesNotExceedPlatformCeiling_RejectsAiFinalDecisionMode
- EnsureDoesNotExceedPlatformCeiling_RejectsAuditDisabled
- EnsureDoesNotExceedPlatformCeiling_AllowsRestrictivePolicy

TenantAgentCapabilityPolicyEnforcementTests
- CommandCenter_DoesNotDraftCustomerUpdateWhenPolicyDisabled
- NotificationSend_StillRequiresHumanApprovalRegardlessOfThreshold
- NotificationSend_RequiresTwoPersonApprovalAboveThresholdWhenAmountPresent
- NotificationSend_FailsClosedWhenAmountMissingForThresholdedWorkflow
- KnowledgeRetrieval_AppliesTenantSensitivityExclusions
- DocumentExtraction_DoesNotAutoExtractWhenPolicyDisabled
- ReportInsight_DoesNotGenerateManagementSummaryWhenPolicyDisabled

TenantAgentCapabilityPolicyContractTests
- DtoSerializesCamelCase
- UpdateRejectsUnknownJsonMembers
- UpdateRejectsUnknownBypassField
```

### 14.1 Policy cannot bypass ApprovalDecisionGuard test

```csharp
[Fact]
public void EnsureDoesNotExceedPlatformCeiling_RejectsAiFinalDecisionMode()
{
    var policy = TestPolicies.RestrictiveDefault() with
    {
        PlatformConsequentialDecisionMode = "ai_allowed"
    };

    var ex = Assert.Throws<ValidationException>(() =>
        TenantAgentCapabilityPolicyValidator.EnsureDoesNotExceedPlatformCeiling(policy));

    Assert.Contains("cannot allow AI", ex.Message, StringComparison.OrdinalIgnoreCase);
}
```

### 14.2 Tenant/workspace isolation test

```csharp
[Fact]
public async Task UpdatePolicyAsync_IsTenantWorkspaceScoped()
{
    var context = TestTenantContext.For("tenant-a", "workspace-a", "admin-1", Permissions.AgentPolicyManage);
    var repository = new Mock<ITenantAgentCapabilityPolicyRepository>();
    var service = BuildService(context, repository: repository.Object);

    await service.UpdatePolicyAsync(TestPolicyUpdates.Restrictive(), CancellationToken.None);

    repository.Verify(x => x.UpsertAsync(
        It.Is<TenantAgentCapabilityPolicy>(p =>
            p.TenantId == "tenant-a" &&
            p.WorkspaceId == "workspace-a"),
        It.IsAny<CancellationToken>()), Times.Once);
}
```

### 14.3 Default policy is most restrictive test

```csharp
[Fact]
public async Task GetPolicyAsync_ReturnsMostRestrictiveDefaultWhenMissing()
{
    var service = BuildService(repositoryReturns: null);

    var result = await service.GetPolicyAsync(CancellationToken.None);

    Assert.False(result.autoDraftCustomerCommunicationsEnabled);
    Assert.False(result.managementSummaryGenerationEnabled);
    Assert.False(result.documentAutoExtractionEnabled);
    Assert.Equal(0m, result.autoSendRequiresApprovalAboveAmount);
    Assert.Equal(0m, result.requireTwoPersonApprovalAboveAmount);
    Assert.Equal(PlatformConsequentialDecisionModeValues.HumanRequired, result.platformConsequentialDecisionMode);
    Assert.True(result.auditRequired);
}
```

### 14.4 Enforcement-point test

```csharp
[Fact]
public async Task CommandCenter_DoesNotDraftCustomerUpdateWhenPolicyDisabled()
{
    var policyService = new Mock<ITenantAgentCapabilityPolicyService>();
    policyService.Setup(x => x.GetPolicyAsync(It.IsAny<CancellationToken>()))
        .ReturnsAsync(TestPolicyDtos.CustomerDraftDisabled());

    var chatProvider = new Mock<IChatCompletionProvider>();
    var service = BuildCommandCenterService(
        policyService: policyService.Object,
        chatCompletionProvider: chatProvider.Object);

    await Assert.ThrowsAsync<WorkflowConflictException>(() =>
        service.BuildCustomerUpdateDraftAsync("claim-1", CancellationToken.None));

    chatProvider.Verify(x => x.CompleteAsync(
        It.IsAny<ChatCompletionRequest>(),
        It.IsAny<CancellationToken>()), Times.Never);
}
```

### 14.5 Audit-write test

```csharp
[Fact]
public async Task UpdatePolicyAsync_WritesAuditWithBeforeAfterValues()
{
    var auditWriter = new Mock<IAuditWriter>();
    var service = BuildService(auditWriter: auditWriter.Object);

    await service.UpdatePolicyAsync(TestPolicyUpdates.AllowManagementSummariesOnly(), CancellationToken.None);

    auditWriter.Verify(x => x.WriteAsync(
        It.Is<AuditEventCreateRequest>(evt =>
            evt.EventType == AuditEventTypeValues.AgentPolicyChanged &&
            evt.Action == TenantAgentPolicyAuditActions.PolicyChanged &&
            evt.Summary.Contains("Tenant agent capability policy changed", StringComparison.Ordinal) &&
            evt.Reason != null &&
            evt.Reason.Contains("before=", StringComparison.Ordinal) &&
            evt.Reason.Contains("after=", StringComparison.Ordinal)),
        It.IsAny<CancellationToken>()), Times.Once);
}
```

### 14.6 Permission enforcement test

```csharp
[Fact]
public async Task UpdatePolicyAsync_RequiresAgentPolicyManage()
{
    var permissionService = new Mock<IPermissionService>();
    permissionService.Setup(x => x.EnsureAsync(Permissions.AgentPolicyManage, It.IsAny<CancellationToken>()))
        .ThrowsAsync(new ForbiddenException("Permission denied."));

    var repository = new Mock<ITenantAgentCapabilityPolicyRepository>();
    var service = BuildService(permissionService: permissionService.Object, repository: repository.Object);

    await Assert.ThrowsAsync<ForbiddenException>(() =>
        service.UpdatePolicyAsync(TestPolicyUpdates.Restrictive(), CancellationToken.None));

    repository.Verify(x => x.UpsertAsync(
        It.IsAny<TenantAgentCapabilityPolicy>(),
        It.IsAny<CancellationToken>()), Times.Never);
}
```

---

## 15. TODOs and confirmation points

| Area | Current decision | TODO / confirmation needed |
|---|---|---|
| Configurable policy fields | First-pass structured fields defined | Confirm exact full list of thresholds/toggles against real insurer requirements. |
| Two-person approval | Threshold field defined | Confirm exact approval evidence model and whether two approvals must be sequential, independent, or role-separated. |
| Auto-send threshold | Stored as future-facing/additive restriction | Confirm whether any low-risk auto-send workflow will ever be allowed; current platform still requires human approval. |
| Knowledge sensitivity categories | Placeholder constants included | Reconcile with actual IKnowledgeSensitivityClassifier category constants before migration. |
| Policy-change approval | Not implemented | Confirm whether loosening AI policy should itself require a secondary approval workflow. |
| Workspace inheritance | Policy is tenant/workspace scoped | Confirm whether workspace policy can override tenant policy and how strict composition is calculated. |
| Admin read permission | GetPolicyAsync is internal-read friendly | Confirm API route permission for admin read view: AgentPolicyManage, AdminManage, or separate AgentPolicyRead. |
| Audit detail | Before/after configuration values are logged | Confirm whether policy values should also be visible in a compliance/audit UI. |

---

## 16. Completion report template

```text
Implementation document: IIP Tenant Agent Capability Policy — Structured Admin Configuration.

Files to create or modify:
- src/InsuranceIntelligence.Application/AgentPolicy/ITenantAgentCapabilityPolicyService.cs
- src/InsuranceIntelligence.Application/AgentPolicy/TenantAgentCapabilityPolicyService.cs
- src/InsuranceIntelligence.Application/AgentPolicy/TenantAgentCapabilityPolicyModels.cs
- src/InsuranceIntelligence.Application/AgentPolicy/TenantAgentCapabilityPolicyValidator.cs
- src/InsuranceIntelligence.Application/AgentPolicy/ITenantAgentCapabilityPolicyRepository.cs
- src/InsuranceIntelligence.Application/AgentPolicy/TenantAgentCapabilityPolicyDefaults.cs
- src/InsuranceIntelligence.Application/AgentPolicy/TenantAgentPolicyAuditActions.cs
- src/InsuranceIntelligence.Application/Common/Authorization/Permissions.cs
- src/InsuranceIntelligence.Application/Common/Audit/AuditEventTypeValues.cs
- src/InsuranceIntelligence.Infrastructure/AgentPolicy/EfTenantAgentCapabilityPolicyRepository.cs
- src/InsuranceIntelligence.Infrastructure/Persistence/Entities/TenantAgentCapabilityPolicyEntity.cs
- src/InsuranceIntelligence.Api/Controllers/Admin/TenantAgentCapabilityPolicyController.cs
- src/InsuranceIntelligence.Api/Contracts/Admin/TenantAgentCapabilityPolicyDto.cs
- src/InsuranceIntelligence.Api/Contracts/Admin/TenantAgentCapabilityPolicyUpdateRequest.cs
- existing CommandCenterQueryService, NotificationSendService, KnowledgeIngestion/retrieval, and DocumentExtractionWorkflowService with additive policy checks
- src/InsuranceIntelligence.Tests/Application/AgentPolicy/* tests

Service logic enabled:
- Tenant/workspace-scoped structured AI capability policy.
- Conservative default policy on tenant provisioning.
- Admin update path with permission enforcement and audit.
- Additive enforcement checks in drafting, sending, knowledge retrieval, document extraction, and report summaries.

Security impact:
- Tenant policy cannot permit AI final decisions.
- Tenant policy cannot bypass ApprovalDecisionGuard.
- Tenant policy cannot disable audit writing.
- Tenant policy cannot enable fake provider behavior.
- Unknown JSON fields are rejected to prevent silent unsafe policy expansion.

Tenant/workspace isolation impact:
- Policy reads and writes use TenantId and WorkspaceId from ITenantContextAccessor.
- Repository filters by tenant/workspace.
- Policy defaults are generated per tenant/workspace.

Authentication/authorization impact:
- UpdatePolicyAsync requires Permissions.AgentPolicyManage.
- Existing service permissions remain in force.
- AgentPolicyManage does not bypass human approval or platform guardrails.

Provider impact:
- No provider behavior is changed.
- Policy may prevent services from calling AI/document providers for disabled capabilities.
- Provider health checks still apply where provider-backed services execute.

Audit impact:
- Every policy update writes AuditEventTypeValues.AgentPolicyChanged or equivalent DataChanged action.
- Audit includes safe before/after configuration values and admin user ID.
- Audit cannot be disabled by tenant policy.

Data model or migration impact:
- Requires TenantAgentCapabilityPolicy table/entity.
- Requires unique tenant/workspace policy index.
- Requires migration for structured policy fields and JSON list of excluded knowledge categories.

AI/human-review impact:
- AI capability is narrowed per tenant policy.
- AI remains assistive only.
- Human approval and ApprovalDecisionGuard remain non-negotiable platform controls.
- Customer communication drafts, management summaries, knowledge retrieval, and automatic document extraction can be disabled by tenant/workspace.

Tests added or required:
- Policy cannot be configured to bypass ApprovalDecisionGuard.
- Tenant/workspace isolation tests for reads/writes.
- Default-policy-is-most-restrictive test.
- Enforcement-point tests for consuming services.
- Audit-write test for policy changes.
- Permissions.AgentPolicyManage enforcement test.
- Unknown JSON member rejection test.

TODOs:
- Confirm exact full list of configurable thresholds/toggles beyond this first pass.
- Confirm whether policy changes require secondary approval.
- Confirm exact knowledge sensitivity category constants.
- Confirm two-person approval evidence model.
- Confirm admin read permission model.

No fake data confirmation:
- No mock, simulated, in-memory, placeholder, seeded-demo, or fallback AI capability is introduced.
- Tenant policy can only restrict existing real services.

Recommended next step:
- Implement the policy repository and default policy migration first, then wire read-only enforcement into CommandCenterQueryService customer drafting and DocumentExtractionWorkflowService automatic extraction before exposing the admin update endpoint.
```
