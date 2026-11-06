using System.Globalization;
using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Application.CustomerAssistant;
using InsuranceIntelligence.Application.SalesLeads;
using InsuranceIntelligence.Domain.Acquisition;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Application.Workspace;

public sealed record ChartPoint(string Label, int Value);

/// <summary>A chart the workspace can draw, with the period and source named so a reader can check it.
/// <see cref="IsEmpty"/> is true when no records exist for the period; the points are then all zero and
/// the screen shows an empty state instead of inventing a trend.</summary>
public sealed record ChartSeries(
    string Metric,
    string Title,
    string ChartType,
    string Period,
    string TimeZone,
    string XAxis,
    string YAxis,
    IReadOnlyList<ChartPoint> Points,
    int Total,
    bool IsEmpty,
    string Insight,
    string Source);

public sealed class WorkspaceValidationException(string field, string message) : Exception(message)
{
    public string Field { get; } = field;
}

public interface IWorkspaceService
{
    Task<WorkbookAnalysis> AnalyseWorkbookAsync(
        string fileName,
        IReadOnlyList<string> headers,
        IReadOnlyList<IReadOnlyList<string>> rows,
        bool truncated,
        string correlationId,
        CancellationToken cancellationToken = default);

    Task RecordExportAsync(string analysisId, int prospects, string correlationId, CancellationToken cancellationToken = default);

    Task<ChartSeries> ChartAsync(string metric, int days, CancellationToken cancellationToken = default);
}

/// <summary>
/// The staff workspace behind the Kato chat. It prepares work for people to review: it reads a customer
/// workbook and recommends covers, and it draws charts from the records that exist. It never sends a
/// message. Every analysis and export is audited with the acting user.
/// </summary>
public sealed class WorkspaceService(
    ITenantContextAccessor tenantContextAccessor,
    IPermissionService permissionService,
    ISalesLeadReadRepository leadRepository,
    IAuditWriter auditWriter) : IWorkspaceService
{
    // Uganda observes no daylight saving, so a fixed offset is exact. Charts bucket days in East Africa Time.
    private static readonly TimeSpan Offset = TimeSpan.FromHours(3);
    private const string TimeZoneLabel = "East Africa Time (UTC+3)";

    public async Task<WorkbookAnalysis> AnalyseWorkbookAsync(
        string fileName,
        IReadOnlyList<string> headers,
        IReadOnlyList<IReadOnlyList<string>> rows,
        bool truncated,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        await permissionService.RequirePermissionAsync(Permission.ManageSalesLeads, cancellationToken);
        if (headers.Count == 0 || rows.Count == 0)
        {
            throw new WorkspaceValidationException("file", "The file has no customer rows under its headings.");
        }

        var now = DateTimeOffset.UtcNow;
        var analysis = WorkbookProspectAnalyzer.Analyse(fileName, headers, rows, truncated, now, KatoBrand.Current);
        await WriteAuditAsync(
            "workspace.prospects_analysed",
            $"Analysed {analysis.Summary.TotalRecords} records from '{Path.GetFileName(fileName)}' as {analysis.AnalysisId}: "
            + $"{analysis.Summary.Eligible} eligible, {analysis.Summary.Excluded} excluded.",
            correlationId,
            cancellationToken);
        return analysis;
    }

    public async Task RecordExportAsync(string analysisId, int prospects, string correlationId, CancellationToken cancellationToken = default)
    {
        await permissionService.RequirePermissionAsync(Permission.ManageSalesLeads, cancellationToken);
        await WriteAuditAsync(
            "workspace.analysis_exported",
            $"Exported analysis {analysisId} ({prospects} prospects) to Excel.",
            correlationId,
            cancellationToken);
    }

    public async Task<ChartSeries> ChartAsync(string metric, int days, CancellationToken cancellationToken = default)
    {
        await permissionService.RequirePermissionAsync(Permission.ViewSalesLeads, cancellationToken);
        var context = tenantContextAccessor.Current;
        var leads = await leadRepository.ListLeadsAsync(context.TenantId, context.WorkspaceId, cancellationToken);
        var normalised = (metric ?? string.Empty).Trim().ToLowerInvariant();

        return normalised switch
        {
            "new_prospects" => NewProspectsByDay(leads, Math.Clamp(days, 1, 90)),
            "stage" => ByStage(leads),
            "product_interest" => ByProductInterest(leads),
            "channel" => ByChannel(leads),
            _ => throw new WorkspaceValidationException("metric", "Choose one of: new_prospects, stage, product_interest, channel."),
        };
    }

    private static ChartSeries NewProspectsByDay(IReadOnlyList<SalesLeadDetailReadModel> leads, int days)
    {
        var today = DateOnly.FromDateTime(DateTimeOffset.UtcNow.ToOffset(Offset).DateTime);
        var from = today.AddDays(-(days - 1));
        var counts = leads
            .Where(lead => lead.CreatedAt is not null)
            .Select(lead => DateOnly.FromDateTime(lead.CreatedAt!.Value.ToOffset(Offset).DateTime))
            .Where(day => day >= from && day <= today)
            .GroupBy(day => day)
            .ToDictionary(group => group.Key, group => group.Count());

        var points = Enumerable.Range(0, days)
            .Select(index => from.AddDays(index))
            .Select(day => new ChartPoint(day.ToString("ddd d MMM", CultureInfo.InvariantCulture), counts.GetValueOrDefault(day)))
            .ToArray();
        var total = points.Sum(point => point.Value);

        string insight;
        if (total == 0)
        {
            insight = "No prospects were created in this period.";
        }
        else
        {
            var peak = points.MaxBy(point => point.Value)!;
            insight = $"{total} new prospect{(total == 1 ? string.Empty : "s")} in {days} day{(days == 1 ? string.Empty : "s")}. "
                    + $"The busiest day was {peak.Label} with {peak.Value}.";
        }

        return new ChartSeries(
            "new_prospects",
            "New prospects per day",
            "bar",
            $"{from:d MMM yyyy} to {today:d MMM yyyy}",
            TimeZoneLabel,
            "Day",
            "New prospects",
            points,
            total,
            total == 0,
            insight,
            "Prospect records in this workspace, counted by the day each was created.");
    }

    private static ChartSeries ByStage(IReadOnlyList<SalesLeadDetailReadModel> leads)
    {
        var counts = leads
            .Select(lead => LeadStages.Derive(lead.Facts, lead.Interests, lead.AppliedFor is not null, lead.HandedToPersonAt is not null))
            .GroupBy(stage => stage)
            .Select(group => new ChartPoint(SpaceOut(group.Key.ToString()), group.Count()))
            .OrderByDescending(point => point.Value)
            .ToArray();
        return Categorical("stage", "Prospects by stage", "Stage", counts, "the stage derived from what is recorded about each prospect.");
    }

    private static ChartSeries ByProductInterest(IReadOnlyList<SalesLeadDetailReadModel> leads)
    {
        var counts = leads
            .SelectMany(lead => lead.Interests.Select(interest => interest.Product).Distinct(StringComparer.OrdinalIgnoreCase))
            .GroupBy(product => product, StringComparer.OrdinalIgnoreCase)
            .Select(group => new ChartPoint(group.Key, group.Count()))
            .OrderByDescending(point => point.Value)
            .ToArray();
        return Categorical("product_interest", "Prospects by product interest", "Product", counts, "interest recorded from conversations and lists. One prospect can count under several covers.");
    }

    private static ChartSeries ByChannel(IReadOnlyList<SalesLeadDetailReadModel> leads)
    {
        var counts = leads
            .GroupBy(lead => string.IsNullOrWhiteSpace(lead.PrimaryChannel) ? "Not recorded" : lead.PrimaryChannel!)
            .Select(group => new ChartPoint(group.Key, group.Count()))
            .OrderByDescending(point => point.Value)
            .ToArray();
        return Categorical("channel", "Prospects by channel", "Channel", counts, "the main channel each prospect came through.");
    }

    private static ChartSeries Categorical(string metric, string title, string xAxis, ChartPoint[] points, string basis)
    {
        var total = points.Sum(point => point.Value);
        var insight = total == 0
            ? "No records exist for this view yet."
            : $"{points[0].Label} leads with {points[0].Value} of {total}.";
        return new ChartSeries(
            metric, title, "bar", "All time", TimeZoneLabel, xAxis, "Prospects", points, total, total == 0, insight,
            $"Prospect records in this workspace, grouped by {basis}");
    }

    private static string SpaceOut(string value) =>
        System.Text.RegularExpressions.Regex.Replace(value, "(?<!^)([A-Z])", " $1");

    private async Task WriteAuditAsync(string action, string reason, string correlationId, CancellationToken cancellationToken)
    {
        var context = tenantContextAccessor.Current;
        await auditWriter.WriteAsync(
            new AuditEvent(
                Guid.NewGuid(), context.Scope, CaseId: null, DocumentId: null, UserId: context.UserId, ActorType.User, action,
                DateTimeOffset.UtcNow, BeforeStatus: null, AfterStatus: null, EvidenceIds: [], ModelProvider: "KatoWorkspace",
                Confidence: null, ValidationResult: "recorded", ApprovalId: null,
                AuditReason: reason, CorrelationId: correlationId),
            cancellationToken);
    }
}
