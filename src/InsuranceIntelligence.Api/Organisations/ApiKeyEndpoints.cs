using InsuranceIntelligence.Api.Auth;
using InsuranceIntelligence.Api.Workflow;
using InsuranceIntelligence.Application.Organisations;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Infrastructure.Configuration;

namespace InsuranceIntelligence.Api.Organisations;

public sealed record CreateApiKeyRequest(string? Name, int? ExpiresInDays);

public static class ApiKeyEndpoints
{
    public static Task<IResult> Create(
        CreateApiKeyRequest request, ApiKeyService service, InfrastructureOptions options, HttpContext httpContext, CancellationToken cancellationToken) =>
        Admin(httpContext, options, async (organisationId, userId) =>
        {
            var created = await service.CreateAsync(organisationId, userId, request.Name ?? string.Empty, request.ExpiresInDays, httpContext.TraceIdentifier, cancellationToken);
            // The key is shown here and nowhere else. Only a hash of it is kept.
            return Results.Created($"/api/v1/organisation/api-keys/{created.KeyId}", created);
        });

    public static Task<IResult> List(
        ApiKeyService service, InfrastructureOptions options, HttpContext httpContext, CancellationToken cancellationToken) =>
        Admin(httpContext, options, async (organisationId, _) => Results.Ok(await service.ListAsync(organisationId, cancellationToken)));

    public static Task<IResult> Revoke(
        Guid keyId, ApiKeyService service, InfrastructureOptions options, HttpContext httpContext, CancellationToken cancellationToken) =>
        Admin(httpContext, options, async (organisationId, userId) =>
        {
            await service.RevokeAsync(organisationId, userId, keyId, httpContext.TraceIdentifier, cancellationToken);
            return Results.NoContent();
        });

    private static async Task<IResult> Admin(
        HttpContext httpContext, InfrastructureOptions options, Func<string, string, Task<IResult>> action)
    {
        if (DemoAuthorization.Require(httpContext, options, Permission.ManageOrganisation) is { } forbidden)
        {
            return forbidden;
        }

        if (!WorkflowEndpoints.TryResolveScope(httpContext, out var scope, out var userId, out _) || string.IsNullOrWhiteSpace(userId))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        try
        {
            return await action(scope.TenantId, userId);
        }
        catch (OrganisationException exception)
        {
            var status = exception.Code switch
            {
                "not_found" => StatusCodes.Status404NotFound,
                "conflict" => StatusCodes.Status409Conflict,
                _ => StatusCodes.Status400BadRequest,
            };
            return Results.Problem(exception.Message, statusCode: status);
        }
    }
}
