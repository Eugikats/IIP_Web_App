# IIP Reporting Service

**Product:** Insurance Intelligence Platform (IIP)  
**Staff application:** IIP Operations  
**Document type:** Implementation-ready Application-layer service output  
**Service area:** Reporting and export execution  
**Status:** New-repository implementation specification  
**Date:** July 2026

## Changelog

```text
- Fix 1: Added ILogger<ReportingService> as a ReportingService dependency and logged unexpected export/render/storage exceptions before throwing the safe ProviderUnavailableException, because the original exception is captured in the service log rather than by global exception middleware.
- Fix 3: Replaced the local BlobProviderCode magic string with ProviderCodes.AzureBlobStorage from the core ports document, because ProviderCodes is the canonical source for provider-code constants.
```

---

## Shared rules used by this service output

This output follows the same implementation pattern used by the existing IIP Application-layer service-output documents for Command Center, Claims, SLA, Approvals, Documents, Channels, Customers, Sales & Leads, Audit, Claim Intake, Write-Back, Notifications, and Knowledge Ingestion.

```text
1. Resolve tenant/workspace from ITenantContextAccessor on every public method.
2. Enforce backend permissions before any report read or export.
3. Apply tenantId and workspaceId filters at the repository boundary.
4. Aggregate only from real persisted repository data or existing canonical read models.
5. Never estimate, interpolate, smooth, fabricate, seed, or infer operational figures and present them as real.
6. Label incomplete/current-period reports explicitly as partial.
7. Store every export as a durable artifact through the configured Blob Storage provider.
8. Write durable audit events for report exports using AuditEventTypeValues.ReportExported.
9. Never include row-level report contents, raw customer/claim rows, secrets, provider payloads, prompts, tokens, or SAS values in audit summaries.
10. Return empty or not-available metric values when no real denominator exists; do not invent substitute figures.
11. Do not perform cross-tenant aggregation or benchmarking in this service.
12. Keep Domain free of EF Core, Azure SDKs, ASP.NET Core, and provider SDK dependencies.
13. Test doubles may exist only in automated tests or isolated LocalTest assemblies.
14. Protected runtimes must never resolve Mock, InMemory, Simulated, Placeholder, Demo, DevelopmentAuth, SeededDemoBusinessData, or FallbackToMock implementations.
```

Fixed inputs consumed by this service and not redesigned here:

```text
ITenantContextAccessor
IPermissionService
IAuditWriter
ClaimsDailySummaryDto
AuditEventRowDto / IAuditQueryService read model conventions
AuditEventTypeValues.ReportExported
```

This service is additive. It does not replace `IClaimsQueryService`, `IAuditQueryService`, or any existing query service.

---

## 1. Scope of this pass

This pass implements the first IIP reporting service:

```text
IReportingService
```

Implemented capabilities:

```text
- Generate an operational report for a requested period.
- Compute claims volume from real claim records.
- Compute SLA breach rate from real SLA status/due-date records.
- Compute approval turnaround time from real approval decision records or canonical audit/reporting projections.
- Compute document completeness rate from real document completeness records/rules.
- Mark a report as partial when the requested period includes the current incomplete reporting day.
- Persist a report snapshot so it can be exported later by reportId.
- Export an existing report snapshot to CSV or PDF.
- Store export artifacts durably through the configured Blob Storage provider.
- Enforce reports.read for report generation.
- Enforce reports.export for report export.
- Write report_exported audit events for export attempts/outcomes without row-level report content.
```

Not implemented in this pass:

```text
- Report scheduling.
- Dashboard widgets.
- Multi-tenant benchmarking.
- Executive pack generation.
- Emailing reports.
- Row-level data exports.
- PDF renderer library selection.
- Export retention lifecycle jobs.
- Report retry/backoff workflow.
```

Non-negotiable reporting rules:

```text
- A report must never include estimated, interpolated, or smoothed figures presented as real data.
- If a period has incomplete data, the report must label the period as partial.
- Reports are tenant/workspace-scoped. No cross-tenant aggregation is allowed.
- Export artifacts must be durable and re-downloadable; do not generate temporary-only responses.
```

---

## 2. Files to create or modify

Use the existing repository structure where present. Do not create parallel architecture if equivalent files already exist.

Recommended files:

```text
src/InsuranceIntelligence.Application/Reporting/IReportingService.cs
src/InsuranceIntelligence.Application/Reporting/ReportingService.cs // injects ILogger<ReportingService> for safe export-failure logging
src/InsuranceIntelligence.Application/Reporting/ReportingModels.cs
src/InsuranceIntelligence.Application/Reporting/IReportingReadRepository.cs
src/InsuranceIntelligence.Application/Reporting/IReportSnapshotRepository.cs
src/InsuranceIntelligence.Application/Reporting/IReportExportArtifactStorageProvider.cs
src/InsuranceIntelligence.Application/Reporting/IReportExportRenderer.cs
src/InsuranceIntelligence.Application/Reporting/ReportPermissions.cs

src/InsuranceIntelligence.Infrastructure/Reporting/EfReportingReadRepository.cs
src/InsuranceIntelligence.Infrastructure/Reporting/EfReportSnapshotRepository.cs
src/InsuranceIntelligence.Infrastructure/Reporting/BlobReportExportArtifactStorageProvider.cs
src/InsuranceIntelligence.Infrastructure/Reporting/CsvReportExportRenderer.cs
src/InsuranceIntelligence.Infrastructure/Reporting/PdfReportExportRenderer.cs // placeholder adapter boundary only until library is chosen

src/InsuranceIntelligence.Tests/Application/Reporting/ReportingServiceTests.cs
src/InsuranceIntelligence.Tests/Application/Reporting/ReportingContractMatchesFrontendTypesTest.cs
src/InsuranceIntelligence.Tests/Application/Reporting/ReportExportTests.cs
```

If the repository already contains report snapshot/export entities or Blob provider wrappers, extend those files instead of duplicating them.

---

## 3. Permissions to add

Add these constants to the canonical `Permissions` class from the core ports document.

```csharp
namespace InsuranceIntelligence.Application.Common.Authorization;

public static partial class Permissions
{
    public const string ReportsRead = "reports.read";
    public const string ReportsExport = "reports.export";
}
```

Rules:

```text
- reports.read is required for GetOperationalReportAsync.
- reports.export is required for ExportReportAsync.
- reports.export must not imply reports.read unless the role/permission store explicitly grants both.
```

---

## 4. Service contract

```csharp
namespace InsuranceIntelligence.Application.Reporting;

public interface IReportingService
{
    Task<OperationalReportDto> GetOperationalReportAsync(
        ReportPeriod period,
        CancellationToken cancellationToken);

    Task<ReportExportResultDto> ExportReportAsync(
        string reportId,
        string format,
        CancellationToken cancellationToken);
}
```

`GetOperationalReportAsync` persists a report snapshot and returns its `ReportId`. `ExportReportAsync` uses that `ReportId` to render and store a durable artifact.

---

## 5. Models

```csharp
namespace InsuranceIntelligence.Application.Reporting;

public sealed record ReportPeriod(
    DateTimeOffset FromUtc,
    DateTimeOffset ToUtc,
    string? Label = null);

public sealed record OperationalReportDto(
    string ReportId,
    string ReportType,
    ReportPeriodDto Period,
    string CompletenessStatus,
    ClaimsDailySummaryDto? ClaimsDailySummary,
    OperationalReportMetricsDto Metrics,
    IReadOnlyList<ReportMetricDto> MetricCards,
    IReadOnlyList<ReportDataQualityNoteDto> DataQualityNotes,
    DateTimeOffset GeneratedAt);

public sealed record ReportPeriodDto(
    string FromUtc,
    string ToUtc,
    string Label,
    bool IsPartial,
    string PartialReason);

public sealed record OperationalReportMetricsDto(
    int ClaimsVolume,
    decimal? SlaBreachRatePercent,
    decimal? AverageApprovalTurnaroundHours,
    decimal? DocumentCompletenessRatePercent);

public sealed record ReportMetricDto(
    string Code,
    string Label,
    decimal? Value,
    string Unit,
    string DataStatus,
    string Explanation);

public sealed record ReportDataQualityNoteDto(
    string Code,
    string Message);

public sealed record ReportExportResultDto(
    string ExportId,
    string ReportId,
    string Format,
    string Status,
    string ArtifactUri,
    string FileName,
    string ContentType,
    long SizeBytes,
    string CreatedAt,
    string? ExpiresAt);

public static class ReportTypeValues
{
    public const string Operational = "operational";
}

public static class ReportCompletenessStatusValues
{
    public const string Final = "final";
    public const string Partial = "partial";
}

public static class ReportMetricDataStatusValues
{
    public const string Complete = "complete";
    public const string Partial = "partial";
    public const string NotAvailable = "not_available";
}

public static class ReportExportFormatValues
{
    public const string Csv = "csv";
    public const string Pdf = "pdf";
}

public static class ReportExportStatusValues
{
    public const string Pending = "pending";
    public const string Succeeded = "succeeded";
    public const string Failed = "failed";
}
```

`ClaimsDailySummaryDto` is the existing claims summary model from `IClaimsQueryService`. It must not be redefined in a different namespace.

---

## 6. Repository and provider ports

### 6.1 Reporting read repository

The repository must aggregate from real persisted data or canonical reporting projections derived from existing read models. It must not estimate missing values.

```csharp
namespace InsuranceIntelligence.Application.Reporting;

public interface IReportingReadRepository
{
    Task<ClaimsReportAggregateReadModel> GetClaimsAggregateAsync(
        string tenantId,
        string? workspaceId,
        ReportPeriod period,
        CancellationToken cancellationToken);

    Task<SlaReportAggregateReadModel> GetSlaAggregateAsync(
        string tenantId,
        string? workspaceId,
        ReportPeriod period,
        CancellationToken cancellationToken);

    Task<ApprovalTurnaroundAggregateReadModel> GetApprovalTurnaroundAggregateAsync(
        string tenantId,
        string? workspaceId,
        ReportPeriod period,
        CancellationToken cancellationToken);

    Task<DocumentCompletenessAggregateReadModel> GetDocumentCompletenessAggregateAsync(
        string tenantId,
        string? workspaceId,
        ReportPeriod period,
        CancellationToken cancellationToken);
}

public sealed record ClaimsReportAggregateReadModel(
    int ClaimsVolume,
    int OpenClaims,
    int NewClaims,
    int CompletedClaims);

public sealed record SlaReportAggregateReadModel(
    int ClaimsWithSla,
    int BreachedClaims);

public sealed record ApprovalTurnaroundAggregateReadModel(
    int CompletedApprovals,
    decimal? AverageTurnaroundHours);

public sealed record DocumentCompletenessAggregateReadModel(
    int ClaimsWithDocumentRules,
    int CompleteClaims);
```

Repository rules:

```text
- ClaimsVolume must count real claim creation records within the period.
- SLA breach rate denominator must be real ClaimsWithSla, not total claims unless every claim has an SLA rule.
- Approval turnaround must use real requested-at and decided-at timestamps from human decision records or canonical audit/reporting projections.
- Document completeness denominator must be real claims that have configured document rules.
- If a denominator is zero, the metric value is null and dataStatus is not_available.
```

### 6.2 Report snapshot repository

```csharp
namespace InsuranceIntelligence.Application.Reporting;

public interface IReportSnapshotRepository
{
    Task SaveSnapshotAsync(
        ReportSnapshotSaveModel snapshot,
        CancellationToken cancellationToken);

    Task<ReportSnapshotReadModel?> GetSnapshotAsync(
        string tenantId,
        string? workspaceId,
        string reportId,
        CancellationToken cancellationToken);

    Task CreateExportAsync(
        ReportExportRecord export,
        CancellationToken cancellationToken);

    Task MarkExportSucceededAsync(
        string tenantId,
        string? workspaceId,
        string exportId,
        string artifactUri,
        long sizeBytes,
        DateTimeOffset completedAt,
        CancellationToken cancellationToken);

    Task MarkExportFailedAsync(
        string tenantId,
        string? workspaceId,
        string exportId,
        string safeFailureReason,
        DateTimeOffset failedAt,
        CancellationToken cancellationToken);
}

public sealed record ReportSnapshotSaveModel(
    string ReportId,
    string TenantId,
    string? WorkspaceId,
    string ReportType,
    ReportPeriod Period,
    string CompletenessStatus,
    OperationalReportMetricsDto Metrics,
    IReadOnlyList<ReportMetricDto> MetricCards,
    IReadOnlyList<ReportDataQualityNoteDto> DataQualityNotes,
    string CreatedByUserId,
    DateTimeOffset CreatedAt);

public sealed record ReportSnapshotReadModel(
    string ReportId,
    string TenantId,
    string? WorkspaceId,
    string ReportType,
    ReportPeriod Period,
    string CompletenessStatus,
    OperationalReportMetricsDto Metrics,
    IReadOnlyList<ReportMetricDto> MetricCards,
    IReadOnlyList<ReportDataQualityNoteDto> DataQualityNotes,
    string CreatedByUserId,
    DateTimeOffset CreatedAt);

public sealed record ReportExportRecord(
    string ExportId,
    string ReportId,
    string TenantId,
    string? WorkspaceId,
    string Format,
    string Status,
    string FileName,
    string ContentType,
    string RequestedByUserId,
    DateTimeOffset RequestedAt);
```

### 6.3 Export renderer port

```csharp
namespace InsuranceIntelligence.Application.Reporting;

public interface IReportExportRenderer
{
    string Format { get; }

    Task<RenderedReportArtifact> RenderAsync(
        ReportSnapshotReadModel snapshot,
        CancellationToken cancellationToken);
}

public sealed record RenderedReportArtifact(
    string FileName,
    string ContentType,
    byte[] Content);
```

Renderer rules:

```text
- CSV and PDF are the only initial formats.
- CSV may be implemented directly in Infrastructure.
- PDF rendering library selection is a TODO and must not be guessed here.
- Renderers must use snapshot metrics and safe report metadata only; no row-level export in this pass.
```

### 6.4 Durable artifact storage provider

This wraps the existing Blob Storage provider so the Application service does not depend on Azure SDKs.

```csharp
namespace InsuranceIntelligence.Application.Reporting;

public interface IReportExportArtifactStorageProvider
{
    Task<ReportArtifactStorageResult> StoreAsync(
        ReportArtifactStorageRequest request,
        CancellationToken cancellationToken);
}

public sealed record ReportArtifactStorageRequest(
    string TenantId,
    string? WorkspaceId,
    string ReportId,
    string ExportId,
    string FileName,
    string ContentType,
    byte[] Content);

public sealed record ReportArtifactStorageResult(
    string ArtifactUri,
    long SizeBytes,
    DateTimeOffset? ExpiresAt);
```

Storage rules:

```text
- Store through the configured Blob Storage provider.
- Do not return fake URLs or local temp paths in protected runtimes.
- Do not embed credentials, SAS tokens, secrets, or raw storage connection strings in audit summaries.
```

---

## 7. Business rules

### 7.1 Period validation

```text
- FromUtc is required.
- ToUtc is required.
- FromUtc must be earlier than ToUtc.
- Periods longer than the configured max report window should be rejected by validation. Default max: 366 days.
- The period is interpreted as [FromUtc, ToUtc), inclusive start and exclusive end.
```

### 7.2 Partial-period labeling

A report is partial if the period includes the current UTC reporting day or ends in the future.

```text
- Let todayStartUtc = current UTC date at 00:00.
- If period.ToUtc > todayStartUtc, IsPartial = true.
- If period.ToUtc <= todayStartUtc, IsPartial = false.
```

Examples:

```text
- Yesterday 00:00 to today 00:00 => final.
- Today 00:00 to tomorrow 00:00 => partial until tomorrow starts.
- This month up to now => partial.
```

### 7.3 Metric calculations

```text
Claims volume:
  ClaimsReportAggregate.ClaimsVolume

SLA breach rate:
  null if ClaimsWithSla == 0
  otherwise BreachedClaims / ClaimsWithSla * 100

Approval turnaround time:
  null if CompletedApprovals == 0
  otherwise repository-provided average over real request/decision timestamps

Document completeness rate:
  null if ClaimsWithDocumentRules == 0
  otherwise CompleteClaims / ClaimsWithDocumentRules * 100
```

Do not use estimates for missing denominators.

### 7.4 Export behavior

```text
- ExportReportAsync loads a tenant/workspace-scoped report snapshot by reportId.
- The service creates a pending export record before rendering/storage.
- On success, the export is marked succeeded with artifact URI and size.
- On failure, the export is marked failed with a safe failure reason.
- The method must not return a successful response unless a durable artifact was stored.
- Every export outcome writes a safe audit event with eventType = report_exported.
```

---

## 8. Full C# implementation

```csharp
using InsuranceIntelligence.Application.Common.Audit;
using InsuranceIntelligence.Application.Common.Authorization;
using InsuranceIntelligence.Application.Common.Exceptions;
using InsuranceIntelligence.Application.Common.Providers;
using InsuranceIntelligence.Application.Common.Tenancy;
using Microsoft.Extensions.Logging;

namespace InsuranceIntelligence.Application.Reporting;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

internal sealed class ReportingService : IReportingService
{
    private const int MaxReportPeriodDays = 366;

    private readonly ITenantContextAccessor _tenantContextAccessor;
    private readonly IPermissionService _permissionService;
    private readonly IReportingReadRepository _reportingReadRepository;
    private readonly IReportSnapshotRepository _snapshotRepository;
    private readonly IReadOnlyDictionary<string, IReportExportRenderer> _renderers;
    private readonly IReportExportArtifactStorageProvider _artifactStorageProvider;
    private readonly IProviderHealthCheck _providerHealthCheck;
    private readonly IAuditWriter _auditWriter;
    private readonly IClock _clock;
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

    public async Task<OperationalReportDto> GetOperationalReportAsync(
        ReportPeriod period,
        CancellationToken cancellationToken)
    {
        ValidatePeriod(period);

        var context = _tenantContextAccessor.Current;
        await _permissionService.EnsureAsync(Permissions.ReportsRead, cancellationToken);

        var claims = await _reportingReadRepository.GetClaimsAggregateAsync(
            context.TenantId,
            context.WorkspaceId,
            period,
            cancellationToken);

        var sla = await _reportingReadRepository.GetSlaAggregateAsync(
            context.TenantId,
            context.WorkspaceId,
            period,
            cancellationToken);

        var approvals = await _reportingReadRepository.GetApprovalTurnaroundAggregateAsync(
            context.TenantId,
            context.WorkspaceId,
            period,
            cancellationToken);

        var documents = await _reportingReadRepository.GetDocumentCompletenessAggregateAsync(
            context.TenantId,
            context.WorkspaceId,
            period,
            cancellationToken);

        var periodDto = BuildPeriodDto(period, _clock.UtcNow);
        var completenessStatus = periodDto.IsPartial
            ? ReportCompletenessStatusValues.Partial
            : ReportCompletenessStatusValues.Final;

        var metrics = new OperationalReportMetricsDto(
            ClaimsVolume: claims.ClaimsVolume,
            SlaBreachRatePercent: CalculatePercent(sla.BreachedClaims, sla.ClaimsWithSla),
            AverageApprovalTurnaroundHours: approvals.CompletedApprovals == 0 ? null : approvals.AverageTurnaroundHours,
            DocumentCompletenessRatePercent: CalculatePercent(documents.CompleteClaims, documents.ClaimsWithDocumentRules));

        var metricStatus = periodDto.IsPartial
            ? ReportMetricDataStatusValues.Partial
            : ReportMetricDataStatusValues.Complete;

        var metricCards = new List<ReportMetricDto>
        {
            new(
                Code: "claims_volume",
                Label: "Claims volume",
                Value: claims.ClaimsVolume,
                Unit: "count",
                DataStatus: metricStatus,
                Explanation: "Number of real claim records created in the requested period."),
            new(
                Code: "sla_breach_rate",
                Label: "SLA breach rate",
                Value: metrics.SlaBreachRatePercent,
                Unit: "percent",
                DataStatus: sla.ClaimsWithSla == 0 ? ReportMetricDataStatusValues.NotAvailable : metricStatus,
                Explanation: "Breached claims divided by claims with an SLA rule in the requested period."),
            new(
                Code: "approval_turnaround_time",
                Label: "Average approval turnaround time",
                Value: metrics.AverageApprovalTurnaroundHours,
                Unit: "hours",
                DataStatus: approvals.CompletedApprovals == 0 ? ReportMetricDataStatusValues.NotAvailable : metricStatus,
                Explanation: "Average duration from approval request to recorded human decision."),
            new(
                Code: "document_completeness_rate",
                Label: "Document completeness rate",
                Value: metrics.DocumentCompletenessRatePercent,
                Unit: "percent",
                DataStatus: documents.ClaimsWithDocumentRules == 0 ? ReportMetricDataStatusValues.NotAvailable : metricStatus,
                Explanation: "Complete claims divided by claims with configured document rules.")
        };

        var dataQualityNotes = BuildDataQualityNotes(periodDto, sla, approvals, documents);
        var reportId = Guid.NewGuid().ToString("N");
        var generatedAt = _clock.UtcNow;

        var claimsDailySummary = new ClaimsDailySummaryDto(
            OpenClaims: claims.OpenClaims,
            NewToday: claims.NewClaims,
            SlaAtRisk: sla.BreachedClaims,
            CompletedToday: claims.CompletedClaims);

        await _snapshotRepository.SaveSnapshotAsync(new ReportSnapshotSaveModel(
            ReportId: reportId,
            TenantId: context.TenantId,
            WorkspaceId: context.WorkspaceId,
            ReportType: ReportTypeValues.Operational,
            Period: period,
            CompletenessStatus: completenessStatus,
            Metrics: metrics,
            MetricCards: metricCards,
            DataQualityNotes: dataQualityNotes,
            CreatedByUserId: context.UserId,
            CreatedAt: generatedAt), cancellationToken);

        return new OperationalReportDto(
            ReportId: reportId,
            ReportType: ReportTypeValues.Operational,
            Period: periodDto,
            CompletenessStatus: completenessStatus,
            ClaimsDailySummary: claimsDailySummary,
            Metrics: metrics,
            MetricCards: metricCards,
            DataQualityNotes: dataQualityNotes,
            GeneratedAt: generatedAt);
    }

    public async Task<ReportExportResultDto> ExportReportAsync(
        string reportId,
        string format,
        CancellationToken cancellationToken)
    {
        ValidateExportRequest(reportId, format);

        var normalizedFormat = NormalizeFormat(format);
        var context = _tenantContextAccessor.Current;
        await _permissionService.EnsureAsync(Permissions.ReportsExport, cancellationToken);

        var snapshot = await _snapshotRepository.GetSnapshotAsync(
            context.TenantId,
            context.WorkspaceId,
            reportId,
            cancellationToken);

        if (snapshot is null)
        {
            throw new NotFoundOrNotVisibleException("Report was not found or is not visible.");
        }

        var providerHealth = await _providerHealthCheck.CheckAsync(ProviderCodes.AzureBlobStorage, cancellationToken);
        if (providerHealth.Status == ProviderStatus.NotConfigured)
        {
            throw new ProviderNotConfiguredException("Azure Blob Storage is not configured for report exports.");
        }

        if (providerHealth.Status == ProviderStatus.Unavailable)
        {
            throw new ProviderUnavailableException("Azure Blob Storage is unavailable for report exports.");
        }

        if (!_renderers.TryGetValue(normalizedFormat, out var renderer))
        {
            throw new ValidationException("Unsupported report export format.");
        }

        var exportId = Guid.NewGuid().ToString("N");
        var requestedAt = _clock.UtcNow;

        await _snapshotRepository.CreateExportAsync(new ReportExportRecord(
            ExportId: exportId,
            ReportId: snapshot.ReportId,
            TenantId: context.TenantId,
            WorkspaceId: context.WorkspaceId,
            Format: normalizedFormat,
            Status: ReportExportStatusValues.Pending,
            FileName: BuildSafeFileName(snapshot, normalizedFormat),
            ContentType: ContentTypeFor(normalizedFormat),
            RequestedByUserId: context.UserId,
            RequestedAt: requestedAt), cancellationToken);

        try
        {
            var rendered = await renderer.RenderAsync(snapshot, cancellationToken);

            var stored = await _artifactStorageProvider.StoreAsync(new ReportArtifactStorageRequest(
                TenantId: context.TenantId,
                WorkspaceId: context.WorkspaceId,
                ReportId: snapshot.ReportId,
                ExportId: exportId,
                FileName: rendered.FileName,
                ContentType: rendered.ContentType,
                Content: rendered.Content), cancellationToken);

            await _snapshotRepository.MarkExportSucceededAsync(
                context.TenantId,
                context.WorkspaceId,
                exportId,
                stored.ArtifactUri,
                stored.SizeBytes,
                _clock.UtcNow,
                cancellationToken);

            await WriteExportAuditAsync(
                context,
                snapshot,
                exportId,
                normalizedFormat,
                action: "report.export.succeeded",
                summary: BuildSafeAuditSummary(snapshot, normalizedFormat, context.UserId, "succeeded"),
                cancellationToken);

            return new ReportExportResultDto(
                ExportId: exportId,
                ReportId: snapshot.ReportId,
                Format: normalizedFormat,
                Status: ReportExportStatusValues.Succeeded,
                ArtifactUri: stored.ArtifactUri,
                FileName: rendered.FileName,
                ContentType: rendered.ContentType,
                SizeBytes: stored.SizeBytes,
                CreatedAt: _clock.UtcNow.ToString("O"),
                ExpiresAt: stored.ExpiresAt?.ToString("O"));
        }
        catch (ProviderNotConfiguredException)
        {
            await MarkExportFailedAndAuditAsync(context, snapshot, exportId, normalizedFormat, "Provider not configured.", cancellationToken);
            throw;
        }
        catch (ProviderUnavailableException)
        {
            await MarkExportFailedAndAuditAsync(context, snapshot, exportId, normalizedFormat, "Provider unavailable.", cancellationToken);
            throw;
        }
        catch (Exception ex)
        {
            await MarkExportFailedAndAuditAsync(context, snapshot, exportId, normalizedFormat, "Report export failed.", cancellationToken);
            _logger.LogWarning(
                ex,
                "Report export failed. ReportId: {ReportId}. ExportId: {ExportId}. CorrelationId: {CorrelationId}",
                snapshot.ReportId,
                exportId,
                context.CorrelationId);
            throw new ProviderUnavailableException("Report export failed. Use the correlation ID when contacting support.");
        }
    }

    private async Task MarkExportFailedAndAuditAsync(
        TenantContext context,
        ReportSnapshotReadModel snapshot,
        string exportId,
        string format,
        string safeFailureReason,
        CancellationToken cancellationToken)
    {
        await _snapshotRepository.MarkExportFailedAsync(
            context.TenantId,
            context.WorkspaceId,
            exportId,
            safeFailureReason,
            _clock.UtcNow,
            cancellationToken);

        await WriteExportAuditAsync(
            context,
            snapshot,
            exportId,
            format,
            action: "report.export.failed",
            summary: BuildSafeAuditSummary(snapshot, format, context.UserId, "failed"),
            cancellationToken);
    }

    private async Task WriteExportAuditAsync(
        TenantContext context,
        ReportSnapshotReadModel snapshot,
        string exportId,
        string format,
        string action,
        string summary,
        CancellationToken cancellationToken)
    {
        await _auditWriter.WriteAsync(new AuditEventCreateRequest(
            TenantId: context.TenantId,
            WorkspaceId: context.WorkspaceId,
            EventType: AuditEventTypeValues.ReportExported,
            ActorType: "user",
            ActorUserId: context.UserId,
            Action: action,
            EntityType: "report_export",
            EntityId: exportId,
            Summary: summary,
            Reason: $"reportId={snapshot.ReportId}; reportType={snapshot.ReportType}; format={format}; period={snapshot.Period.FromUtc:O} to {snapshot.Period.ToUtc:O}",
            CorrelationId: context.CorrelationId,
            OccurredAt: _clock.UtcNow), cancellationToken);
    }

    private static void ValidatePeriod(ReportPeriod period)
    {
        if (period.FromUtc >= period.ToUtc)
        {
            throw new ValidationException("fromUtc must be before toUtc.");
        }

        if ((period.ToUtc - period.FromUtc).TotalDays > MaxReportPeriodDays)
        {
            throw new ValidationException("Report period exceeds maximum allowed range.");
        }
    }

    private static void ValidateExportRequest(string reportId, string format)
    {
        if (string.IsNullOrWhiteSpace(reportId))
        {
            throw new ValidationException("reportId is required.");
        }

        if (string.IsNullOrWhiteSpace(format))
        {
            throw new ValidationException("format is required.");
        }
    }

    private static ReportPeriodDto BuildPeriodDto(ReportPeriod period, DateTimeOffset now)
    {
        var todayStartUtc = new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero);
        var isPartial = period.ToUtc > todayStartUtc;
        var label = string.IsNullOrWhiteSpace(period.Label)
            ? $"{period.FromUtc:yyyy-MM-dd} to {period.ToUtc:yyyy-MM-dd}"
            : period.Label.Trim();

        return new ReportPeriodDto(
            FromUtc: period.FromUtc.ToString("O"),
            ToUtc: period.ToUtc.ToString("O"),
            Label: label,
            IsPartial: isPartial,
            PartialReason: isPartial
                ? "The selected period includes the current incomplete reporting day. Figures are real but not final."
                : "The selected period ended before the current reporting day.");
    }

    private static decimal? CalculatePercent(int numerator, int denominator)
    {
        if (denominator <= 0)
        {
            return null;
        }

        return Math.Round((decimal)numerator / denominator * 100m, 2, MidpointRounding.AwayFromZero);
    }

    private static IReadOnlyList<ReportDataQualityNoteDto> BuildDataQualityNotes(
        ReportPeriodDto period,
        SlaReportAggregateReadModel sla,
        ApprovalTurnaroundAggregateReadModel approvals,
        DocumentCompletenessAggregateReadModel documents)
    {
        var notes = new List<ReportDataQualityNoteDto>();

        if (period.IsPartial)
        {
            notes.Add(new ReportDataQualityNoteDto(
                Code: "partial_period",
                Message: period.PartialReason));
        }

        if (sla.ClaimsWithSla == 0)
        {
            notes.Add(new ReportDataQualityNoteDto(
                Code: "sla_denominator_empty",
                Message: "No claims with SLA rules exist in the selected period; SLA breach rate is not available."));
        }

        if (approvals.CompletedApprovals == 0)
        {
            notes.Add(new ReportDataQualityNoteDto(
                Code: "approval_denominator_empty",
                Message: "No completed approval decisions exist in the selected period; approval turnaround time is not available."));
        }

        if (documents.ClaimsWithDocumentRules == 0)
        {
            notes.Add(new ReportDataQualityNoteDto(
                Code: "document_denominator_empty",
                Message: "No claims with configured document rules exist in the selected period; document completeness rate is not available."));
        }

        return notes;
    }

    private static string NormalizeFormat(string format)
    {
        var normalized = format.Trim().ToLowerInvariant();
        return normalized switch
        {
            ReportExportFormatValues.Csv => ReportExportFormatValues.Csv,
            ReportExportFormatValues.Pdf => ReportExportFormatValues.Pdf,
            _ => throw new ValidationException("Unsupported report export format.")
        };
    }

    private static string BuildSafeFileName(ReportSnapshotReadModel snapshot, string format)
    {
        return $"iip-{snapshot.ReportType}-report-{snapshot.Period.FromUtc:yyyyMMdd}-{snapshot.Period.ToUtc:yyyyMMdd}.{format}";
    }

    private static string ContentTypeFor(string format) => format switch
    {
        ReportExportFormatValues.Csv => "text/csv",
        ReportExportFormatValues.Pdf => "application/pdf",
        _ => "application/octet-stream"
    };

    private static string BuildSafeAuditSummary(
        ReportSnapshotReadModel snapshot,
        string format,
        string userId,
        string outcome)
    {
        return $"Operational report export {outcome}. reportType={snapshot.ReportType}; periodFrom={snapshot.Period.FromUtc:O}; periodTo={snapshot.Period.ToUtc:O}; format={format}; requestedBy={userId}.";
    }
}
```

Implementation notes:

```text
- The catch block intentionally converts unexpected export/render/storage failures to ProviderUnavailableException with a safe message.
- The original exception is captured by ReportingService through ILogger<ReportingService> with report ID, export ID, and correlation ID before the safe wrapper is thrown.
- The global exception middleware receives the safe ProviderUnavailableException and returns only the support correlation ID to the client.
- The failed export record remains visible for operations support.
- The audit Reason contains metadata only, not report rows.
```

---

## 9. Renderer examples

### 9.1 CSV renderer

```csharp
using System.Text;

namespace InsuranceIntelligence.Infrastructure.Reporting;

public sealed class CsvReportExportRenderer : IReportExportRenderer
{
    public string Format => ReportExportFormatValues.Csv;

    public Task<RenderedReportArtifact> RenderAsync(
        ReportSnapshotReadModel snapshot,
        CancellationToken cancellationToken)
    {
        var builder = new StringBuilder();
        builder.AppendLine("metric,label,value,unit,dataStatus,explanation");

        foreach (var metric in snapshot.MetricCards)
        {
            builder.Append(Escape(metric.Code)).Append(',');
            builder.Append(Escape(metric.Label)).Append(',');
            builder.Append(metric.Value?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty).Append(',');
            builder.Append(Escape(metric.Unit)).Append(',');
            builder.Append(Escape(metric.DataStatus)).Append(',');
            builder.Append(Escape(metric.Explanation)).AppendLine();
        }

        var fileName = $"iip-{snapshot.ReportType}-report-{snapshot.Period.FromUtc:yyyyMMdd}-{snapshot.Period.ToUtc:yyyyMMdd}.csv";
        var content = Encoding.UTF8.GetBytes(builder.ToString());

        return Task.FromResult(new RenderedReportArtifact(
            FileName: fileName,
            ContentType: "text/csv",
            Content: content));
    }

    private static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var escaped = value.Replace("\"", "\"\"");
        return $"\"{escaped}\"";
    }
}
```

### 9.2 PDF renderer boundary

```csharp
namespace InsuranceIntelligence.Infrastructure.Reporting;

public sealed class PdfReportExportRenderer : IReportExportRenderer
{
    public string Format => ReportExportFormatValues.Pdf;

    public Task<RenderedReportArtifact> RenderAsync(
        ReportSnapshotReadModel snapshot,
        CancellationToken cancellationToken)
    {
        // TODO: confirm PDF rendering library/approach.
        // Acceptable production options may include a licensed server-side renderer,
        // QuestPDF, Playwright-to-PDF, or another approved library after security review.
        throw new ProviderNotConfiguredException("PDF report rendering is not configured.");
    }
}
```

The PDF renderer class may be registered only after the approved PDF library/approach is selected and configured. Until then, PDF export should fail closed with `ProviderNotConfiguredException`, not fake a PDF.

---

## 10. Tests to add

```text
ReportingServiceTests
- GetOperationalReportAsync_RequiresReportsReadPermission
- GetOperationalReportAsync_UsesTenantWorkspaceScopeForEveryAggregate
- GetOperationalReportAsync_DoesNotCrossTenantAggregate
- GetOperationalReportAsync_LabelsCurrentPeriodAsPartial
- GetOperationalReportAsync_LabelsPastClosedPeriodAsFinal
- GetOperationalReportAsync_UsesNullMetricWhenDenominatorIsZero
- GetOperationalReportAsync_PersistsReportSnapshot
- ExportReportAsync_RequiresReportsExportPermission
- ExportReportAsync_RejectsMissingReportId
- ExportReportAsync_RejectsUnsupportedFormat
- ExportReportAsync_ThrowsNotFoundWhenReportNotVisible
- ExportReportAsync_ThrowsProviderNotConfiguredWhenBlobStorageMissing
- ExportReportAsync_ProducesDurableArtifactNotEphemeralResponse
- ExportReportAsync_MarksExportFailedWhenStorageFails
- ExportReportAsync_WritesAuditForSucceededExport
- ExportReportAsync_WritesAuditForFailedExport
- ExportReportAsync_AuditDoesNotContainRowLevelData
```

### 10.1 Tenant/workspace scoping test

```csharp
[Fact]
public async Task GetOperationalReportAsync_UsesTenantWorkspaceScopeForEveryAggregate()
{
    var context = TestTenantContext.For("tenant-a", "workspace-a", "user-1", Permissions.ReportsRead);
    var repository = new Mock<IReportingReadRepository>();

    repository.Setup(x => x.GetClaimsAggregateAsync("tenant-a", "workspace-a", It.IsAny<ReportPeriod>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync(new ClaimsReportAggregateReadModel(10, 8, 3, 2));
    repository.Setup(x => x.GetSlaAggregateAsync("tenant-a", "workspace-a", It.IsAny<ReportPeriod>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync(new SlaReportAggregateReadModel(10, 1));
    repository.Setup(x => x.GetApprovalTurnaroundAggregateAsync("tenant-a", "workspace-a", It.IsAny<ReportPeriod>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync(new ApprovalTurnaroundAggregateReadModel(4, 6.5m));
    repository.Setup(x => x.GetDocumentCompletenessAggregateAsync("tenant-a", "workspace-a", It.IsAny<ReportPeriod>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync(new DocumentCompletenessAggregateReadModel(10, 7));

    var service = BuildService(context, reportingReadRepository: repository.Object);

    await service.GetOperationalReportAsync(TestPeriods.Yesterday(), CancellationToken.None);

    repository.VerifyAll();
}
```

### 10.2 Permission enforcement test

```csharp
[Fact]
public async Task ExportReportAsync_RequiresReportsExportPermission()
{
    var permissionService = new Mock<IPermissionService>();
    permissionService.Setup(x => x.EnsureAsync(Permissions.ReportsExport, It.IsAny<CancellationToken>()))
        .ThrowsAsync(new ForbiddenException("Permission denied."));

    var snapshotRepository = new Mock<IReportSnapshotRepository>();
    var service = BuildService(permissionService: permissionService.Object, snapshotRepository: snapshotRepository.Object);

    await Assert.ThrowsAsync<ForbiddenException>(() =>
        service.ExportReportAsync("rpt-1", ReportExportFormatValues.Csv, CancellationToken.None));

    snapshotRepository.Verify(x => x.GetSnapshotAsync(
        It.IsAny<string>(),
        It.IsAny<string?>(),
        It.IsAny<string>(),
        It.IsAny<CancellationToken>()), Times.Never);
}
```

### 10.3 Durable export artifact test

```csharp
[Fact]
public async Task ExportReportAsync_ProducesDurableArtifactNotEphemeralResponse()
{
    var storage = new Mock<IReportExportArtifactStorageProvider>();
    storage.Setup(x => x.StoreAsync(It.IsAny<ReportArtifactStorageRequest>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync(new ReportArtifactStorageResult(
            ArtifactUri: "blob://reports/tenant-a/rpt-1/export-1.csv",
            SizeBytes: 128,
            ExpiresAt: null));

    var service = BuildService(artifactStorageProvider: storage.Object);

    var result = await service.ExportReportAsync("rpt-1", ReportExportFormatValues.Csv, CancellationToken.None);

    Assert.Equal(ReportExportStatusValues.Succeeded, result.Status);
    Assert.StartsWith("blob://reports/", result.ArtifactUri, StringComparison.Ordinal);
    Assert.True(result.SizeBytes > 0);

    storage.Verify(x => x.StoreAsync(
        It.Is<ReportArtifactStorageRequest>(req => req.ReportId == "rpt-1" && req.Content.Length > 0),
        It.IsAny<CancellationToken>()), Times.Once);
}
```

### 10.4 Partial-period labeling test

```csharp
[Fact]
public async Task GetOperationalReportAsync_LabelsCurrentPeriodAsPartial()
{
    var clock = new FixedClock(new DateTimeOffset(2026, 7, 2, 12, 0, 0, TimeSpan.Zero));
    var service = BuildService(clock: clock);

    var result = await service.GetOperationalReportAsync(
        new ReportPeriod(
            FromUtc: new DateTimeOffset(2026, 7, 2, 0, 0, 0, TimeSpan.Zero),
            ToUtc: new DateTimeOffset(2026, 7, 3, 0, 0, 0, TimeSpan.Zero),
            Label: "Today"),
        CancellationToken.None);

    Assert.True(result.Period.IsPartial);
    Assert.Equal(ReportCompletenessStatusValues.Partial, result.CompletenessStatus);
    Assert.Contains(result.DataQualityNotes, note => note.Code == "partial_period");
}
```

### 10.5 Audit does not contain row-level data test

```csharp
[Fact]
public async Task ExportReportAsync_AuditDoesNotContainRowLevelData()
{
    var auditWriter = new Mock<IAuditWriter>();
    var service = BuildService(auditWriter: auditWriter.Object);

    await service.ExportReportAsync("rpt-1", ReportExportFormatValues.Csv, CancellationToken.None);

    auditWriter.Verify(x => x.WriteAsync(
        It.Is<AuditEventCreateRequest>(evt =>
            evt.EventType == AuditEventTypeValues.ReportExported &&
            evt.Action == "report.export.succeeded" &&
            !evt.Summary.Contains("CLM-", StringComparison.OrdinalIgnoreCase) &&
            !evt.Summary.Contains("customer", StringComparison.OrdinalIgnoreCase) &&
            !evt.Summary.Contains("policyNumber", StringComparison.OrdinalIgnoreCase) &&
            !evt.Summary.Contains("payload", StringComparison.OrdinalIgnoreCase)),
        It.IsAny<CancellationToken>()), Times.Once);
}
```

### 10.6 Contract test for report/export DTO shape

```csharp
[Fact]
public void ReportingContractMatchesFrontendTypesTest()
{
    var dto = new OperationalReportDto(
        ReportId: "rpt-1",
        ReportType: ReportTypeValues.Operational,
        Period: new ReportPeriodDto(
            FromUtc: DateTimeOffset.UtcNow.AddDays(-1).ToString("O"),
            ToUtc: DateTimeOffset.UtcNow.ToString("O"),
            Label: "Yesterday",
            IsPartial: false,
            PartialReason: "The selected period ended before the current reporting day."),
        CompletenessStatus: ReportCompletenessStatusValues.Final,
        ClaimsDailySummary: new ClaimsDailySummaryDto(10, 2, 1, 3),
        Metrics: new OperationalReportMetricsDto(10, 10.0m, 4.5m, 80.0m),
        MetricCards: Array.Empty<ReportMetricDto>(),
        DataQualityNotes: Array.Empty<ReportDataQualityNoteDto>(),
        GeneratedAt: DateTimeOffset.UtcNow);

    var json = JsonSerializer.SerializeToDocument(dto, new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    });

    var names = json.RootElement.EnumerateObject().Select(p => p.Name).ToHashSet();

    var expected = new[]
    {
        "reportId",
        "reportType",
        "period",
        "completenessStatus",
        "claimsDailySummary",
        "metrics",
        "metricCards",
        "dataQualityNotes",
        "generatedAt"
    };

    Assert.Equal(expected.OrderBy(x => x), names.OrderBy(x => x));
}
```

---

## 11. TODOs and confirmation points

| Area | Current decision | TODO / confirmation needed |
|---|---|---|
| Metric set | Starts with claims volume, SLA breach rate, approval turnaround time, and document completeness rate | Confirm exact metric set beyond the four listed metrics. |
| PDF rendering | Boundary defined with `IReportExportRenderer`; CSV can be implemented now | Choose and approve PDF rendering library/approach after security and licensing review. |
| Export retention | Artifacts are stored durably in Blob Storage | Confirm retention period, purge policy, and whether legal hold is required. |
| Snapshot schema | Report snapshots store aggregate metrics and safe notes | Confirm exact database entity shape and indexing strategy. |
| Re-download route | Export result returns durable artifact URI | Confirm whether users receive pre-signed download URLs, proxied API downloads, or internal blob references. |
| Authorization | `reports.read` and `reports.export` are introduced | Confirm role mappings for Manager, Admin, Auditor, Executive Viewer, and Operations roles. |
| Partial-period policy | Any report including current UTC day is partial | Confirm insurer timezone policy if reporting day should use tenant-local timezone instead of UTC. |

---

## 12. Completion report template

```text
Service implemented: IReportingService.

Files to create or modify:
- src/InsuranceIntelligence.Application/Reporting/IReportingService.cs
- src/InsuranceIntelligence.Application/Reporting/ReportingService.cs
- src/InsuranceIntelligence.Application/Reporting/ReportingModels.cs
- src/InsuranceIntelligence.Application/Reporting/IReportingReadRepository.cs
- src/InsuranceIntelligence.Application/Reporting/IReportSnapshotRepository.cs
- src/InsuranceIntelligence.Application/Reporting/IReportExportRenderer.cs
- src/InsuranceIntelligence.Application/Reporting/IReportExportArtifactStorageProvider.cs
- src/InsuranceIntelligence.Infrastructure/Reporting/EfReportingReadRepository.cs
- src/InsuranceIntelligence.Infrastructure/Reporting/EfReportSnapshotRepository.cs
- src/InsuranceIntelligence.Infrastructure/Reporting/BlobReportExportArtifactStorageProvider.cs
- src/InsuranceIntelligence.Infrastructure/Reporting/CsvReportExportRenderer.cs
- src/InsuranceIntelligence.Infrastructure/Reporting/PdfReportExportRenderer.cs, after PDF approach is approved
- src/InsuranceIntelligence.Tests/Application/Reporting/ReportingServiceTests.cs
- src/InsuranceIntelligence.Tests/Application/Reporting/ReportingContractMatchesFrontendTypesTest.cs
- src/InsuranceIntelligence.Tests/Application/Reporting/ReportExportTests.cs

Permissions added:
- reports.read
- reports.export

Logic implemented:
- Tenant/workspace-scoped operational reporting.
- Real aggregate metrics for claims volume, SLA breach rate, approval turnaround time, and document completeness rate.
- Partial-period labeling.
- Durable CSV/PDF export boundary through Blob Storage.
- Export snapshot/history persistence.
- Safe report_exported audit events.
- Service-level warning log captures the original export/render/storage exception before a safe ProviderUnavailableException is thrown.

Security impact:
- Reports are permission-gated through reports.read and reports.export.
- Reports never trust client-supplied tenant/workspace identifiers.
- Exports do not include row-level content in audit summaries.
- No cross-tenant benchmarking or aggregation is introduced.

Tenant/workspace impact:
- Every aggregate query receives TenantId and WorkspaceId from ITenantContextAccessor.
- Report snapshots and export records are tenant/workspace-scoped.

Authentication/authorization impact:
- Requires reports.read for operational report reads.
- Requires reports.export for export generation.
- Role/permission store must be updated with new permission grants.

Data model or migration impact:
- Requires report snapshot table/entity.
- Requires report export table/entity.
- May require reporting projection/indexes for claim creation dates, SLA statuses, approval decision timestamps, and document completeness statuses.

Provider impact:
- Export storage requires configured Azure Blob Storage provider.
- PDF export requires an approved renderer before production registration.

Audit impact:
- Every export outcome writes AuditEventTypeValues.ReportExported.
- Audit summary includes report type, period, format, requesting user, and outcome only.
- Audit summary does not include row-level report content.

Tests added or required:
- Tenant/workspace scoping tests.
- Permission enforcement tests.
- Export durable-artifact test.
- Partial-period labeling test.
- Audit-does-not-contain-row-level-data test.
- Contract tests for OperationalReportDto and ReportExportResultDto.

TODOs:
- Confirm exact metric set beyond the first four metrics.
- Confirm PDF rendering library/approach.
- Confirm export artifact retention period.
- Confirm tenant timezone policy for reporting periods.
- Confirm report re-download route/security model.

Missing dependencies:
- Concrete EF reporting aggregate queries.
- Concrete report snapshot/export entities and migrations.
- Blob report artifact storage adapter.
- Approved PDF renderer.
- Role/permission seed or migration for reports.read and reports.export.

No fake data confirmation:
- No estimated, interpolated, smoothed, seeded, simulated, mock, in-memory, placeholder, or fake operational metrics are introduced.
- Empty or unavailable denominators return null/not_available metrics rather than fabricated substitutes.
- Export succeeds only after durable Blob artifact storage succeeds.

Recommended next step:
- Add report snapshot/export persistence entities and implement `EfReportingReadRepository` using the same tenant/workspace-scoped read-model sources used by Claims, Approvals, Documents, and Audit services.
```
