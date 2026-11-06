using InsuranceIntelligence.Api.Documents;
using InsuranceIntelligence.Api.Policies;
using InsuranceIntelligence.Api.Workflow;
using InsuranceIntelligence.Application.Workspace;

namespace InsuranceIntelligence.Api.Workspace;

/// <summary>The staff workspace behind the Kato chat. Analysis and export need ManageSalesLeads, charts
/// need ViewSalesLeads; the route filters in Program.cs enforce that and the service checks again.</summary>
public static class WorkspaceEndpoints
{
    public const int MaxExportProspects = 5000;
    private const string XlsxContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    public static async Task<IResult> AnalyseProspects(
        HttpContext httpContext,
        IWorkspaceService service,
        CancellationToken cancellationToken)
    {
        if (httpContext.Request.ContentType?.StartsWith("multipart/form-data", StringComparison.OrdinalIgnoreCase) != true)
        {
            return Validation("file", "Upload must use multipart/form-data.");
        }

        var form = await httpContext.Request.ReadFormAsync(cancellationToken);
        var file = form.Files.GetFile("file") ?? form.Files.FirstOrDefault();
        if (file is null || file.Length == 0)
        {
            return Validation("file", "A non-empty .csv or .xlsx file is required.");
        }

        if (file.Length > DocumentUploadValidation.MaxUploadFileSizeBytes)
        {
            return Validation("file", $"The file exceeds the {DocumentUploadValidation.MaxUploadFileSizeBytes / (1024 * 1024)}MB upload limit.");
        }

        ParsedImportFile parsed;
        try
        {
            await using var stream = file.OpenReadStream();
            parsed = PolicyImportFileParser.Parse(file.FileName, stream);
        }
        catch (UnsupportedImportFileException exception)
        {
            return Validation("file", exception.Message);
        }
        catch (Exception)
        {
            return Validation("file", "This file could not be read. Confirm it is a valid CSV or Excel (.xlsx) file.");
        }

        try
        {
            var analysis = await service.AnalyseWorkbookAsync(
                Path.GetFileName(file.FileName), parsed.Headers, parsed.Rows, parsed.Truncated,
                WorkflowEndpoints.GetCorrelationId(httpContext), cancellationToken);
            return Results.Ok(analysis);
        }
        catch (WorkspaceValidationException exception)
        {
            return Validation(exception.Field, exception.Message);
        }
        catch (InsuranceIntelligence.Application.Core.PermissionDeniedException)
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }
    }

    public static async Task<IResult> ExportAnalysis(
        WorkbookAnalysis analysis,
        HttpContext httpContext,
        IWorkspaceService service,
        CancellationToken cancellationToken)
    {
        if (analysis is null || analysis.Prospects is null || analysis.Exclusions is null || analysis.Summary is null)
        {
            return Validation("analysis", "An analysis is required to export.");
        }

        if (analysis.Prospects.Count > MaxExportProspects || analysis.Exclusions.Count > MaxExportProspects)
        {
            return Validation("analysis", $"An export holds at most {MaxExportProspects} prospects.");
        }

        try
        {
            await service.RecordExportAsync(
                analysis.AnalysisId, analysis.Prospects.Count, WorkflowEndpoints.GetCorrelationId(httpContext), cancellationToken);
        }
        catch (InsuranceIntelligence.Application.Core.PermissionDeniedException)
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var bytes = ProspectAnalysisWorkbookWriter.Write(analysis);
        return Results.File(bytes, XlsxContentType, $"iip-prospect-analysis-{analysis.AnalysisId}.xlsx");
    }

    public static async Task<IResult> Chart(
        string? metric,
        int? days,
        IWorkspaceService service,
        CancellationToken cancellationToken)
    {
        try
        {
            return Results.Ok(await service.ChartAsync(metric ?? "new_prospects", days ?? 3, cancellationToken));
        }
        catch (WorkspaceValidationException exception)
        {
            return Validation(exception.Field, exception.Message);
        }
        catch (InsuranceIntelligence.Application.Core.PermissionDeniedException)
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }
    }

    private static IResult Validation(string field, string message) =>
        Results.ValidationProblem(new Dictionary<string, string[]> { [field] = [message] });
}
