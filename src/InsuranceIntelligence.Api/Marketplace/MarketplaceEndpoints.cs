using InsuranceIntelligence.Api.Auth;
using InsuranceIntelligence.Application.Marketplace;
using InsuranceIntelligence.Application.Organisations;
using InsuranceIntelligence.Infrastructure.Configuration;

namespace InsuranceIntelligence.Api.Marketplace;

public sealed record PurchaseTokenRequest(string? Token);
public sealed record ActivatePurchaseRequest(string? Token, string? OrganisationName);

/// <summary>Microsoft's webhook body. Only the subscription id is used, and only as a reason to ask
/// Microsoft what is true now; nothing else in it is believed.</summary>
public sealed record MarketplaceWebhookEvent(string? SubscriptionId, string? Action);

public static class MarketplaceEndpoints
{
    /// <summary>The landing page calls this with the token from the URL Microsoft sent the buyer to.</summary>
    public static Task<IResult> Resolve(
        PurchaseTokenRequest request, MarketplaceFulfillmentService service, InfrastructureOptions options, HttpContext httpContext, CancellationToken cancellationToken) =>
        Run(async () => SignedIn(httpContext, options)
            ? Results.Ok(await service.ResolveAsync(request.Token ?? string.Empty, cancellationToken))
            : Results.Unauthorized());

    public static Task<IResult> Activate(
        ActivatePurchaseRequest request, MarketplaceFulfillmentService service, InfrastructureOptions options, HttpContext httpContext, CancellationToken cancellationToken) =>
        Run(async () =>
        {
            if (!SignedIn(httpContext, options))
            {
                return Results.Unauthorized();
            }

            var userId = DemoAuthorization.GetUserId(httpContext, fallback: string.Empty);
            var organisation = await service.ActivateAsync(
                userId, request.OrganisationName ?? string.Empty, request.Token ?? string.Empty, httpContext.TraceIdentifier, cancellationToken);
            return Results.Ok(new { organisation.OrganisationId, organisation.Name, organisation.PlanId, workspaceId = organisation.DefaultWorkspaceId });
        });

    /// <summary>Open to Microsoft by design. A forged call can only make us ask Microsoft again.</summary>
    public static async Task<IResult> Webhook(
        MarketplaceWebhookEvent body, MarketplaceFulfillmentService service, HttpContext httpContext, CancellationToken cancellationToken)
    {
        try
        {
            await service.SyncAsync(body.SubscriptionId ?? string.Empty, httpContext.TraceIdentifier, cancellationToken);
            return Results.Ok();
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            // Microsoft retries on a server error, which is what is wanted when it could not be reached.
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }
    }

    private static bool SignedIn(HttpContext httpContext, InfrastructureOptions options) =>
        options.AuthMode.Equals("Development", StringComparison.OrdinalIgnoreCase)
            ? !string.IsNullOrWhiteSpace(DemoAuthorization.GetUserId(httpContext, fallback: string.Empty))
            : httpContext.User.Identity?.IsAuthenticated == true;

    private static async Task<IResult> Run(Func<Task<IResult>> action)
    {
        try
        {
            return await action();
        }
        catch (OrganisationException exception)
        {
            var status = exception.Code switch
            {
                "not_found" => StatusCodes.Status404NotFound,
                "forbidden" => StatusCodes.Status403Forbidden,
                "conflict" => StatusCodes.Status409Conflict,
                "not_configured" => StatusCodes.Status503ServiceUnavailable,
                "upstream" => StatusCodes.Status502BadGateway,
                _ => StatusCodes.Status400BadRequest,
            };
            return Results.Problem(exception.Message, statusCode: status);
        }
    }
}
