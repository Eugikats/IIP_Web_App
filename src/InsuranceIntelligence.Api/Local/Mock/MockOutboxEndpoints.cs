using InsuranceIntelligence.Api.Common;

namespace InsuranceIntelligence.Api.Local.Mock;

/// <summary>
/// Developer-runtime-only view of what the Mock communication adapters "sent". Mapped only when
/// <c>developmentDiagnosticsEnabled</c> is true (Development or LocalTest), never in a deployed
/// profile. Reads and clears are scoped to the caller's own tenant and workspace.
/// </summary>
public static class MockOutboxEndpoints
{
    public static IResult List(MockOutbox? outbox, HttpContext httpContext)
    {
        if (outbox is null)
        {
            return Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "Not configured", detail: "The Mock communication provider is not active.");
        }

        if (!TenantScopeReader.TryGetFromHeadersOrQuery(httpContext, out var scope, out var error))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["tenantScope"] = [error] });
        }

        return Results.Ok(outbox.ListFor(scope.TenantId, scope.WorkspaceId));
    }

    public static IResult Clear(MockOutbox? outbox, HttpContext httpContext)
    {
        if (outbox is null)
        {
            return Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "Not configured", detail: "The Mock communication provider is not active.");
        }

        if (!TenantScopeReader.TryGetFromHeadersOrQuery(httpContext, out var scope, out var error))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["tenantScope"] = [error] });
        }

        outbox.Clear(scope.TenantId, scope.WorkspaceId);
        return Results.NoContent();
    }
}
