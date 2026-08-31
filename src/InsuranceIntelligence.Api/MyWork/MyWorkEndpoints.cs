using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Application.MyWork;

namespace InsuranceIntelligence.Api.MyWork;

public static class MyWorkEndpoints
{
    public static async Task<IResult> List(
        string? ownership,
        string? sourceType,
        string? state,
        int? limit,
        IMyWorkService service,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        httpContext.Response.Headers.CacheControl = "no-store";
        var errors = new Dictionary<string, string[]>();
        if (!TryParse(ownership, MyWorkOwnership.Mine, out MyWorkOwnership parsedOwnership))
            errors["ownership"] = ["Ownership must be mine, unassigned, or all."];
        if (!TryParseNullable(sourceType, out MyWorkSourceType? parsedType))
            errors["sourceType"] = ["Source type is not recognized."];
        if (!TryParseNullable(state, out MyWorkState? parsedState))
            errors["state"] = ["State must be ready, inProgress, escalated, completed, or closed."];
        var parsedLimit = limit ?? 50;
        if (parsedLimit is < 1 or > 100)
            errors["limit"] = ["Limit must be between 1 and 100."];
        if (errors.Count > 0) return Results.ValidationProblem(errors);

        try
        {
            return Results.Ok(await service.ListAsync(
                new MyWorkQuery(parsedOwnership, parsedType, parsedState, parsedLimit), cancellationToken));
        }
        catch (PermissionDeniedException)
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }
    }

    private static bool TryParse<T>(string? value, T defaultValue, out T parsed) where T : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            parsed = defaultValue;
            return true;
        }
        return Enum.TryParse(value, true, out parsed) && Enum.IsDefined(parsed);
    }

    private static bool TryParseNullable<T>(string? value, out T? parsed) where T : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            parsed = null;
            return true;
        }
        if (Enum.TryParse<T>(value, true, out var result) && Enum.IsDefined(result))
        {
            parsed = result;
            return true;
        }
        parsed = null;
        return false;
    }
}
