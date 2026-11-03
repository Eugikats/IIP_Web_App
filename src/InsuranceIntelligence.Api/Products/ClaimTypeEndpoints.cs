using InsuranceIntelligence.Api.ClientPortal;
using InsuranceIntelligence.Api.Common;
using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Application.Products;

namespace InsuranceIntelligence.Api.Products;

public sealed record SuggestClaimTypeRequest(string? ProductLine, string? Text);

/// <summary>
/// Claim types are configured per product (see <c>ProductEndpoints</c>); these routes are how a
/// person filing a claim, and the assistant, read them. Reads are scoped to the caller's own
/// tenant and workspace. The claimant route has no staff permission: the magic-link session
/// filter is its gate, and it can only ever list the claim types of its own tenant.
/// </summary>
public static class ClaimTypeEndpoints
{
    public static async Task<IResult> List(
        string? productLine,
        IClaimTypeService service,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!TenantScopeReader.TryGetFromHeadersOrQuery(httpContext, out var scope, out var error))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["tenantScope"] = [error] });
        }

        return Results.Ok(await service.ListForProductLineAsync(scope, productLine, cancellationToken));
    }

    public static async Task<IResult> Suggest(
        SuggestClaimTypeRequest request,
        IClaimTypeService service,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!TenantScopeReader.TryGetFromHeadersOrQuery(httpContext, out var scope, out var error))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["tenantScope"] = [error] });
        }

        return Results.Ok(await service.SuggestAsync(scope, request.ProductLine, request.Text, cancellationToken));
    }

    public static async Task<IResult> ImportExisting(
        Guid productId,
        IClaimTypeService service,
        CancellationToken cancellationToken)
    {
        try
        {
            return Results.Ok(await service.ImportExistingAsync(productId, cancellationToken));
        }
        catch (ProductValidationException ex)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { [ex.Field] = [ex.Message] });
        }
        catch (PermissionDeniedException)
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }
    }

    public static async Task<IResult> ListForClaimant(
        string? productLine,
        IClaimTypeService service,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!ClientPortalScopeReader.TryGetFromHeaders(httpContext, out var scope, out var error))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["tenantScope"] = [error] });
        }

        return Results.Ok(await service.ListForProductLineAsync(scope, productLine, cancellationToken));
    }
}
