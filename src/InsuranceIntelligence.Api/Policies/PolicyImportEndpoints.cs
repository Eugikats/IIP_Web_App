using InsuranceIntelligence.Api.Documents;
using InsuranceIntelligence.Application.Policies;
using InsuranceIntelligence.Domain.Policies;
using Microsoft.AspNetCore.Http.Features;

namespace InsuranceIntelligence.Api.Policies;

/// <summary>Outcome N: "upload an export of policies... map columns on screen, see a validation
/// report, load read only." Two calls, not one: Parse reads the file and hands back headers/rows
/// and a best-effort column-mapping suggestion for the person to confirm on screen; Commit takes
/// that confirmed mapping back with the same rows (no server-side session, the file is never
/// stored) and actually imports. Between the two, the caller owns the column-mapping decision.</summary>
public static class PolicyImportEndpoints
{
    public static async Task<IResult> Parse(HttpContext httpContext, CancellationToken cancellationToken)
    {
        if (!IsMultipartFormData(httpContext.Request.ContentType))
        {
            return Validation("file", "Upload must use multipart/form-data.");
        }

        var form = httpContext.Features.Get<IFormFeature>()?.Form
            ?? await httpContext.Request.ReadFormAsync(cancellationToken);
        var file = form.Files.GetFile("file") ?? form.Files.FirstOrDefault();
        if (file is null)
        {
            return Validation("file", "A .csv or .xlsx file is required.");
        }

        if (file.Length == 0)
        {
            return Validation("file", "The uploaded file is empty.");
        }

        if (file.Length > DocumentUploadValidation.MaxUploadFileSizeBytes)
        {
            return Validation("file", $"The file exceeds the {DocumentUploadValidation.MaxUploadFileSizeBytes / (1024 * 1024)}MB upload limit.");
        }

        try
        {
            await using var stream = file.OpenReadStream();
            var parsed = PolicyImportFileParser.Parse(file.FileName, stream);
            if (parsed.Headers.Count == 0)
            {
                return Validation("file", "No rows were found in this file.");
            }

            var suggestedMapping = parsed.Headers.ToDictionary(
                header => header,
                header => PolicyImportFields.SuggestFieldFor(header) ?? string.Empty);

            return Results.Ok(new PolicyImportParseResponse(
                parsed.Headers,
                parsed.Rows,
                parsed.Truncated,
                suggestedMapping,
                PolicyImportFields.All.Select(field => new PolicyImportFieldOption(field, PolicyImportFields.HumanLabel(field), PolicyImportFields.Required.Contains(field))).ToArray()));
        }
        catch (UnsupportedImportFileException ex)
        {
            return Validation("file", ex.Message);
        }
        catch (Exception)
        {
            return Validation("file", "This file could not be read. Confirm it is a valid CSV or Excel (.xlsx) file.");
        }
    }

    public static async Task<IResult> Commit(
        PolicyImportCommitApiRequest request,
        IPolicyImportService importService,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return Validation("request", "Import rows and a column mapping are required.");
        }

        try
        {
            var result = await importService.CommitAsync(
                new PolicyImportCommitRequest(request.Headers, request.ColumnMapping, request.Rows),
                cancellationToken);
            return Results.Ok(result);
        }
        catch (PolicyImportValidationException ex)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { [ex.Field] = [ex.Message] });
        }
    }

    private static bool IsMultipartFormData(string? contentType) =>
        contentType?.StartsWith("multipart/form-data", StringComparison.OrdinalIgnoreCase) == true;

    private static IResult Validation(string key, string message) =>
        Results.ValidationProblem(new Dictionary<string, string[]> { [key] = [message] });
}

public sealed record PolicyImportFieldOption(string Field, string Label, bool Required);

public sealed record PolicyImportParseResponse(
    IReadOnlyList<string> Headers,
    IReadOnlyList<IReadOnlyList<string>> Rows,
    bool Truncated,
    IReadOnlyDictionary<string, string> SuggestedMapping,
    IReadOnlyList<PolicyImportFieldOption> AvailableFields);

public sealed record PolicyImportCommitApiRequest(
    IReadOnlyList<string> Headers,
    IReadOnlyDictionary<string, string> ColumnMapping,
    IReadOnlyList<IReadOnlyList<string>> Rows);
