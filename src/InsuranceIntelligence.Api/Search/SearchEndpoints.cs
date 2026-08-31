using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Application.Search;

namespace InsuranceIntelligence.Api.Search;

public static class SearchEndpoints
{
    public static async Task<IResult> Search(
        string? q,
        int? limit,
        IGlobalSearchService searchService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        httpContext.Response.Headers.CacheControl = "no-store";
        httpContext.Response.Headers.Pragma = "no-cache";
        var query = q?.Trim() ?? string.Empty;
        if (query.Length is < 2 or > 100)
        {
            return Validation("q", "Search query must contain between 2 and 100 characters.");
        }

        var resultLimit = limit ?? 20;
        if (resultLimit is < 1 or > 50)
        {
            return Validation("limit", "Search limit must be between 1 and 50.");
        }

        try
        {
            return Results.Ok(await searchService.SearchAsync(query, resultLimit, cancellationToken));
        }
        catch (PermissionDeniedException)
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }
        catch (InvalidOperationException)
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }
    }

    private static IResult Validation(string key, string message) =>
        Results.ValidationProblem(new Dictionary<string, string[]> { [key] = [message] });
}
