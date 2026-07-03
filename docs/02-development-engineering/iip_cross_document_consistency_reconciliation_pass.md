# IIP Cross-Document Consistency Reconciliation Pass

**Product:** Insurance Intelligence Platform (IIP)  
**Document type:** Audit/reconciliation report  
**Scope:** Core Ports, Backend Wiring, Application services, additive services, Delivery Tooling, Session/Context/Multi-Agent, Email Ingestion, Tenant Agent Policy  
**Status:** Findings and snippets for correction  
**Date:** July 2026

---

## Method

This pass reviewed the produced Markdown implementation documents for compile/runtime/security/architecture conflicts: undefined types, duplicated constants, `InfrastructureOptions` drift, C# syntax/escaping issues, competing designs, permission/audit coverage gaps, and namespace/project placement drift.

It does not flag harmless stylistic differences.

---

## Findings table

| Issue # | Category | Document(s) | Quote / Reference | Why it is a problem | Fix |
|---:|---|---|---|---|---|
| 1 | Duplicated constant | `iip_notification_send_execution_service.md`; canonical reference `iip_approval_decision_service_implementation_output.md` | `if (!string.Equals(approval.Status, "approved", StringComparison.Ordinal))` | Approval status constants already exist in `ApprovalStatusValues`. A raw literal drifts from the canonical approval decision document. | Add `using InsuranceIntelligence.Application.Approvals;` and compare to `ApprovalStatusValues.Approved`. |
| 2 | Duplicated provider code | `iip_reporting_service.md`; canonical reference Core Ports | `private const string BlobProviderCode = "azure_blob_storage";` | Provider codes are centralized in `ProviderCodes`. A local constant can drift and bypass provider-code governance. | Remove local constant and use `ProviderCodes.AzureBlobStorage`. |
| 3 | Exception logging contradiction | `iip_reporting_service.md` | Catch block creates a new `ProviderUnavailableException` but says global middleware logs the original exception. | The original exception is swallowed before middleware can see it. | Inject `ILogger<ReportingService>` and log the original exception before throwing the safe wrapper. |
| 4 | InfrastructureOptions field-name drift | `iip_delivery_tooling_openapi_iac_testdata_cicd.md` | `Infrastructure:AzureAI:* / AzureOpenAI:*` | `AzureAI` is not a real `InfrastructureOptions` section; the same section says not to invent option names. | Use `Infrastructure:AzureOpenAI:*` only. |
| 5 | Knowledge approval status literal | `iip_knowledge_base_indexing_ingestion_service.md` | `document.ApprovalStatus, "approved"` | The document defines sensitivity classification values but not a canonical knowledge approval-status constant. Repeating literals will drift as approval statuses evolve. | Add `KnowledgeApprovalStatusValues.Approved` and compare to it. |
| 6 | Ambiguous notification draft table ownership | Notification Send + Workstream schema implications | Notification docs define draft send repository but no single canonical persistence ownership note. | Later schema work can accidentally create a duplicate draft table instead of extending existing draft persistence. | Add explicit note: `NotificationDrafts` is the single draft aggregate table; send execution extends it with provider send fields. |
| 7 | Email provider naming needs canonicalization | Email Ingestion + InfrastructureOptions | Valid values described as Gmail/Zoho/ImapSmtp or closest naming convention. | Without canonical constants, provider registration can drift between `ImapSmtp`, `imap_smtp`, and `IMAPSMTP`. | Add `EmailProviderValues.Graph`, `Gmail`, `Zoho`, `ImapSmtp` in the configuration document. |

---

## Category status summary

| Category | Status |
|---|---|
| Undefined type references | Findings above are limited to missing canonical constants/ownership notes; no fully blocking undefined C# type was confirmed in the corrected documents reviewed. |
| Duplicated constants | Findings 1, 2, 5, 7. |
| InfrastructureOptions drift | Finding 4. |
| C# syntax/escaping errors | Zero confirmed current findings in corrected documents. The doubled braces inside `$@"..."` classifier JSON examples are valid C# escaping and should not be changed. |
| Competing designs | Finding 6: notification draft persistence ownership needs a single canonical statement. |
| Permission/audit-event coverage | No blocking missing permission/audit event found in the reviewed additive docs; several new constants must be added to canonical classes during implementation. |
| Project/namespace placement drift | No blocking finding in corrected docs; implementation must continue using `InsuranceIntelligence.Application.Common.*` for core ports. |

---

## Corrected snippets

### Issue 1 — NotificationSendService approval status constant

```csharp
using InsuranceIntelligence.Application.Approvals;

// ...

if (!string.Equals(approval.Status, ApprovalStatusValues.Approved, StringComparison.Ordinal))
{
    throw new WorkflowConflictException("Linked approval decision is not approved.");
}
```

### Issue 2 — ReportingService provider code constant

```csharp
using InsuranceIntelligence.Application.Common.Providers;

// Remove:
// private const string BlobProviderCode = "azure_blob_storage";

var providerHealth = await _providerHealthCheck.CheckAsync(
    ProviderCodes.AzureBlobStorage,
    cancellationToken);
```

### Issue 3 — ReportingService original exception logging

```csharp
private readonly ILogger<ReportingService> _logger;

public ReportingService(
    ITenantContextAccessor tenantContextAccessor,
    IPermissionService permissionService,
    IReportingReadRepository reportingReadRepository,
    IReportSnapshotRepository snapshotRepository,
    IEnumerable<IReportExportRenderer> renderers,
    IReportExportArtifactStorageProvider artifactStorageProvider,
    IProviderHealthCheck providerHealthCheck,
    IAuditWriter auditWriter,
    IClock clock,
    ILogger<ReportingService> logger)
{
    _tenantContextAccessor = tenantContextAccessor;
    _permissionService = permissionService;
    _reportingReadRepository = reportingReadRepository;
    _snapshotRepository = snapshotRepository;
    _renderers = renderers.ToDictionary(x => x.Format, StringComparer.OrdinalIgnoreCase);
    _artifactStorageProvider = artifactStorageProvider;
    _providerHealthCheck = providerHealthCheck;
    _auditWriter = auditWriter;
    _clock = clock;
    _logger = logger;
}

catch (Exception ex)
{
    _logger.LogWarning(
        ex,
        "Report export failed. ReportId: {ReportId}. ExportId: {ExportId}. CorrelationId: {CorrelationId}",
        snapshot.ReportId,
        exportId,
        context.CorrelationId);

    await MarkExportFailedAndAuditAsync(
        context,
        snapshot,
        exportId,
        normalizedFormat,
        "Report export failed.",
        cancellationToken);

    throw new ProviderUnavailableException("Report export failed. Use the correlation ID when contacting support.");
}
```

### Issue 4 — Delivery Tooling AzureOpenAI mapping

```text
Infrastructure:AzureOpenAI:* -> Azure OpenAI endpoint/deployment settings
```

### Issue 5 — Knowledge approval status constant

```csharp
namespace InsuranceIntelligence.Application.Knowledge;

public static class KnowledgeApprovalStatusValues
{
    public const string Approved = "approved";
}

// ...

if (!string.Equals(document.ApprovalStatus, KnowledgeApprovalStatusValues.Approved, StringComparison.OrdinalIgnoreCase))
{
    reasons.Add("Document approval status is not approved.");
}
```

### Issue 6 — Single notification draft persistence ownership note

```text
NotificationDrafts is the single persisted aggregate for customer communication drafts.
Notification send execution must extend that table/aggregate with provider send fields
such as ProviderCode, ProviderMessageId, SentAt, SendFailedAt, and SafeFailureReason.
Do not create a second notification-send table that duplicates the draft lifecycle.
```

### Issue 7 — Canonical email provider values

```csharp
namespace InsuranceIntelligence.Infrastructure.Configuration;

public static class EmailProviderValues
{
    public const string Graph = "Graph";
    public const string Gmail = "Gmail";
    public const string Zoho = "Zoho";
    public const string ImapSmtp = "ImapSmtp";
}
```

---

## Completion report

This reconciliation pass produced a findings table and corrected snippets only. It did not modify the source documents in place. The highest-priority corrections are already reflected in previously corrected versions of Reporting, Notification Send, and Delivery Tooling; Knowledge approval constants and email-provider canonical values remain recommended small updates before code generation.
