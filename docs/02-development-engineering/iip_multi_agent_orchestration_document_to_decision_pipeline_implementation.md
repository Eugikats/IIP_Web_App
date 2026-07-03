# IIP Multi-Agent Orchestration — Document-to-Decision Pipeline Implementation

**Product:** Insurance Intelligence Platform (IIP)  
**Staff application:** IIP Operations  
**Document type:** Implementation-ready multi-agent orchestration specification  
**Architecture:** Microsoft Agent Framework SDK patterns with Azure AI Foundry as inference-only  
**Status:** Future-state optional implementation spec  
**Date:** July 2026

---

## Shared rules used by this orchestration output

```text
1. Tenant/workspace isolation is server-side only through ITenantContextAccessor.
2. Every step uses the existing Application service ports; the orchestrator does not bypass service permissions.
3. AI is never the final actor for claim approval, rejection, settlement, payment release, policy binding, premium setting, fraud determination, notification send, or write-back.
4. Azure AI Foundry is used only as an inference layer where needed; evidence, audit, permissions, and workflow state remain in IIP services.
5. No mock/simulated provider behavior is allowed in Staging, Production, or ManagedApplicationReviewer.
6. Empty data stays empty; no pipeline step fabricates missing records or documents.
7. Raw document bytes and OCR text must not flow past the extraction boundary.
8. Every step writes its own normal audit event; no single opaque “agent ran” entry replaces step-level audit.
```

---

## 1. Scope of this pass

This document defines an optional orchestration layer for when IIP needs a true cross-agent document-to-decision pipeline with mid-flow correction and handoff. It does not replace the existing Application services, and it does not require immediate migration from direct service calls.

Current services work correctly without this orchestrator.

Implemented target shape:

```text
Document Extraction Agent
    -> Completeness Check Agent
        -> Triage Agent
            -> Draft Agent
```

Out of scope:

```text
- Final claim decisions.
- Sending approved drafts.
- Executing external write-back.
- Replacing existing service permissions.
- Replacing existing audit events.
- Visual Foundry workflow designer.
- A2A protocol adoption.
```

---

## 2. Agent roles wrapping existing services

| Agent | Existing service wrapped | Purpose | Hard stop |
|---|---|---|---|
| Document Extraction Agent | `IDocumentExtractionWorkflowService` | Runs extraction and produces reviewed field references/statuses. | Raw document bytes/OCR text stops here. |
| Completeness Check Agent | `ICompletenessCheckService` | Checks required documents and missing/low-confidence fields. | Does not mark claim approved/rejected. |
| Triage Agent | `IClaimsQueryService` attention logic + `ISlaRiskService` | Produces attention/SLA triage recommendation only. | Never auto-assigns or escalates directly. |
| Draft Agent | `CommandCenterQueryService.BuildCustomerUpdateDraftAsync` or equivalent draft path | Prepares a customer update draft from safe context. | Never sends; human approval/send pipeline remains separate. |

---

## 3. Handoff payload contracts

### 3.1 Pipeline trigger

```csharp
namespace InsuranceIntelligence.Application.Orchestration;

public sealed record ClaimDocumentPipelineRequest(
    string ClaimId,
    string DocumentId,
    string TriggerType,
    string CorrelationId);
```

### 3.2 Extraction to completeness

```csharp
public sealed record ExtractionToCompletenessHandoff(
    string ClaimId,
    string DocumentId,
    string ExtractionJobId,
    IReadOnlyList<ExtractedFieldReference> FieldReferences,
    bool HasLowConfidenceFields,
    string CorrelationId);

public sealed record ExtractedFieldReference(
    string FieldId,
    string FieldName,
    string Status,
    decimal? Confidence);
```

Boundary rule:

```text
The handoff carries field IDs, names, statuses, and confidence only. It must not carry raw document bytes, OCR text, full extracted values, or provider payloads.
```

### 3.3 Completeness to triage

```csharp
public sealed record CompletenessToTriageHandoff(
    string ClaimId,
    string DocumentId,
    IReadOnlyList<string> MissingDocumentTypes,
    IReadOnlyList<string> LowConfidenceFieldIds,
    string CompletenessStatus,
    string CorrelationId);
```

Boundary rule:

```text
The handoff contains completeness facts and identifiers. Triage must re-read claim summary/attention/SLA data through services rather than trusting stale in-memory context.
```

### 3.4 Triage to draft

```csharp
public sealed record TriageToDraftHandoff(
    string ClaimId,
    string TriageLevel,
    IReadOnlyList<string> RecommendedHumanActions,
    bool CustomerUpdateRecommended,
    string CorrelationId);
```

Boundary rule:

```text
The handoff contains recommendation metadata only. Draft generation retrieves safe customer-update context through existing Command Center/Claims services.
```

### 3.5 Draft result

```csharp
public sealed record DraftAgentPipelineResult(
    string ClaimId,
    string? DraftId,
    string Status,
    bool RequiresHumanApproval,
    string CorrelationId);
```

Boundary rule:

```text
The draft agent may create a draft if allowed by existing draft service/policy gates. It must not call SendApprovedDraftAsync.
```

---

## 4. Orchestrator contract

```csharp
namespace InsuranceIntelligence.Application.Orchestration;

public interface IClaimProcessingOrchestrator
{
    Task<ClaimProcessingPipelineResult> RunDocumentToDecisionPreparationAsync(
        ClaimDocumentPipelineRequest request,
        CancellationToken cancellationToken);

    Task<ClaimProcessingPipelineResult> ResumeAfterHumanFieldCorrectionAsync(
        HumanFieldCorrectionHandoff request,
        CancellationToken cancellationToken);
}

public sealed record HumanFieldCorrectionHandoff(
    string ClaimId,
    string DocumentId,
    IReadOnlyList<string> CorrectedFieldIds,
    string CorrectedByUserId,
    string CorrelationId);

public sealed record ClaimProcessingPipelineResult(
    string ClaimId,
    string DocumentId,
    string Status,
    IReadOnlyList<string> CompletedSteps,
    IReadOnlyList<string> BlockedReasons,
    string CorrelationId);
```

---

## 5. Sequential default pipeline

Default path uses the Microsoft Agent Framework SDK Sequential pattern conceptually:

```text
Sequential:
  1. Document Extraction Agent
  2. Completeness Check Agent
  3. Triage Agent
  4. Draft Agent, only when customer update is recommended and tenant policy permits drafting
```

Implementation guidance:

```csharp
public sealed class ClaimProcessingOrchestrator : IClaimProcessingOrchestrator
{
    private readonly IDocumentExtractionWorkflowService _extraction;
    private readonly ICompletenessCheckService _completeness;
    private readonly IClaimsQueryService _claims;
    private readonly ISlaRiskService _slaRisk;
    private readonly ICommandCenterDraftFacade _draftFacade;

    public async Task<ClaimProcessingPipelineResult> RunDocumentToDecisionPreparationAsync(
        ClaimDocumentPipelineRequest request,
        CancellationToken cancellationToken)
    {
        // Sequential orchestration delegates to services.
        // Each service enforces its own permissions and writes its own audit events.
        // No step calls approval decision, notification send, or write-back execution.
        throw new NotImplementedException("Implementation belongs in the orchestration pass.");
    }
}
```

`ICommandCenterDraftFacade` is a narrow wrapper over the existing Command Center draft path if the current method is private/internal. Do not duplicate customer-update drafting logic.

---

## 6. Handoff pattern for mid-flow human correction

Human correction can interrupt the sequence after extraction or completeness. The pipeline must not continue on stale data.

Required behavior:

```text
1. Human corrects extracted field(s) through the existing Document Review service.
2. Document Review writes its normal correction audit event.
3. Orchestrator receives HumanFieldCorrectionHandoff.
4. Completeness Check Agent reruns from the corrected persisted fields.
5. Triage Agent reruns after completeness.
6. Draft Agent only proceeds after updated triage and tenant-policy checks.
```

No handoff payload may carry the corrected field values themselves beyond the review boundary. The next agent re-reads persisted state.

---

## 7. Prohibited calls

The orchestrator and its agents must never call these directly:

```text
IApprovalDecisionService.DecideAsync
INotificationSendService.SendApprovedDraftAsync
IPolicyAdminWriteBackService.ExecuteAsync
```

Those remain separate human-triggered operations.

The orchestrator may prepare context or drafts that later appear in approval queues, but final decision/send/write-back is outside this pipeline.

---

## 8. Permission and audit behavior

| Step | Permission/audit rule |
|---|---|
| Extraction | Uses `IDocumentExtractionWorkflowService`; service enforces document permissions and provider health and writes extraction audit. |
| Completeness | Uses `ICompletenessCheckService`; service enforces document/claim visibility and writes completeness/audit events as already specified. |
| Triage | Uses `IClaimsQueryService` and `ISlaRiskService`; services enforce claims/SLA permissions. |
| Draft | Uses existing Command Center/customer update draft path; draft is human-approval-gated and audited. |

There must be no single “orchestrator override” permission that bypasses these checks.

---

## 9. Tests to add

```text
ClaimProcessingOrchestratorTests
- RunDocumentToDecisionPreparationAsync_StopsRawDocumentBytesAtExtractionBoundary
- RunDocumentToDecisionPreparationAsync_DoesNotPassOcrTextToCompletenessTriageOrDraft
- RunDocumentToDecisionPreparationAsync_RunsCompletenessBeforeTriage
- RunDocumentToDecisionPreparationAsync_DoesNotDraftWhenCompletenessBlocksCustomerUpdate
- ResumeAfterHumanFieldCorrectionAsync_RerunsCompletenessBeforeTriage
- ResumeAfterHumanFieldCorrectionAsync_DoesNotUseStaleCompletenessResult
- PipelineFailure_SurfacesSafeStepFailureAndStopsLaterSteps
- EveryStep_UsesExistingServicePermissionChecks
- EveryStep_WritesExistingStepAuditEvents
- Orchestrator_DoesNotCallDecideAsyncSendApprovedDraftAsyncOrWriteBackExecuteAsync
```

Example boundary assertion:

```csharp
[Fact]
public async Task ExtractionToCompletenessHandoff_DoesNotContainRawDocumentContent()
{
    var properties = typeof(ExtractionToCompletenessHandoff)
        .GetProperties()
        .Select(x => x.Name)
        .ToArray();

    Assert.DoesNotContain(properties, x => x.Contains("Bytes", StringComparison.OrdinalIgnoreCase));
    Assert.DoesNotContain(properties, x => x.Contains("Ocr", StringComparison.OrdinalIgnoreCase));
    Assert.DoesNotContain(properties, x => x.Contains("Raw", StringComparison.OrdinalIgnoreCase));
    Assert.DoesNotContain(properties, x => x.Contains("Content", StringComparison.OrdinalIgnoreCase));
}
```

---

## 10. Completion report template

```text
Implementation document: IIP Multi-Agent Orchestration — Document-to-Decision Pipeline Implementation.

Files to create or modify:
- src/InsuranceIntelligence.Application/Orchestration/IClaimProcessingOrchestrator.cs
- src/InsuranceIntelligence.Application/Orchestration/ClaimProcessingOrchestrator.cs
- src/InsuranceIntelligence.Application/Orchestration/ClaimProcessingHandoffModels.cs
- src/InsuranceIntelligence.Application/Orchestration/ICommandCenterDraftFacade.cs, only if needed to expose the existing draft path safely
- tests/InsuranceIntelligence.Tests/Application/Orchestration/ClaimProcessingOrchestratorTests.cs

Security impact:
- No raw document bytes/OCR text cross the extraction boundary.
- No agent makes consequential decisions.
- No step bypasses existing service permissions.

Tenant/workspace isolation impact:
- All wrapped services continue using ITenantContextAccessor.
- Handoff payloads never carry client-supplied tenant/workspace authority.

AI/human-review impact:
- Pipeline prepares evidence, triage, and drafts only.
- Human correction can restart downstream steps.
- Final approval/send/write-back remain human-triggered and outside this orchestrator.

Tests added or required:
- Handoff-boundary enforcement tests.
- Mid-pipeline correction re-trigger tests.
- Failure surfacing tests.
- Per-step permission/audit verification tests.
- Prohibited-call tests.

TODOs:
- Confirm exact Microsoft Agent Framework SDK package/API names at implementation time.
- Confirm whether ICommandCenterDraftFacade is needed or whether draft method can be exposed cleanly from existing Command Center service.
- Confirm persistence model for pipeline run status if long-running orchestration is required.

No fake data confirmation:
- The orchestrator does not fabricate missing documents, claim facts, SLA values, or customer messages.
- Empty service results stop or degrade the pipeline safely.

Recommended next step:
- Do not implement this orchestrator until the underlying direct Application services are implemented and tested. If cross-agent mid-flow correction becomes necessary, implement this after Document Extraction, Completeness, SLA, and Notification Draft services are stable.
```
