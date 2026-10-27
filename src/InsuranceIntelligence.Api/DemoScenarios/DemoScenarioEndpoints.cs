using InsuranceIntelligence.Api.Azure;
using InsuranceIntelligence.Api.Organisations;
using InsuranceIntelligence.Application.DemoScenarios;
using InsuranceIntelligence.Application.Organisations;
using InsuranceIntelligence.Infrastructure.Configuration;

namespace InsuranceIntelligence.Api.DemoScenarios;

/// <summary>
/// The demonstration worlds (Mavuno Bank, Nile Life, Nile General) and the switch between them. Available only where
/// demonstration data is allowed, never under a protected runtime profile, and only to a signed-in person, who becomes
/// the administrator of the organisation that holds each world. Setting one up uses the signed-in identity alone, like
/// signing up, because the person may not yet belong to the organisation they are about to create.
/// </summary>
public static class DemoScenarioEndpoints
{
    public static async Task<IResult> List(
        DemoScenarioService scenarios,
        InfrastructureOptions options,
        IWebHostEnvironment environment,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (NotAvailable(options, environment) is { } closed)
        {
            return closed;
        }

        if (OrganisationEndpoints.SignedInUser(httpContext, options) is not { } userId)
        {
            return Results.Unauthorized();
        }

        var states = await scenarios.ListAsync(userId, cancellationToken);
        return Results.Ok(states.Select(Describe));
    }

    public static async Task<IResult> Activate(
        string scenarioId,
        DemoScenarioService scenarios,
        InfrastructureOptions options,
        IWebHostEnvironment environment,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (NotAvailable(options, environment) is { } closed)
        {
            return closed;
        }

        if (OrganisationEndpoints.SignedInUser(httpContext, options) is not { } userId)
        {
            return Results.Unauthorized();
        }

        try
        {
            var state = await scenarios.ActivateAsync(userId, scenarioId, httpContext.TraceIdentifier, cancellationToken);
            return Results.Ok(Describe(state));
        }
        catch (OrganisationException exception)
        {
            var status = exception.Code switch
            {
                "not_found" => StatusCodes.Status404NotFound,
                "forbidden" => StatusCodes.Status403Forbidden,
                "conflict" => StatusCodes.Status409Conflict,
                _ => StatusCodes.Status400BadRequest,
            };
            return Results.Problem(exception.Message, statusCode: status);
        }
    }

    private static IResult? NotAvailable(InfrastructureOptions options, IWebHostEnvironment environment)
    {
        var runtimeProfile = AzureProviderStartupValidator.ResolveRuntimeProfile(options, environment.EnvironmentName);
        return AzureProviderStartupValidator.IsProtectedRuntime(runtimeProfile)
            ? Results.Problem(
                $"Demonstrations are not available under the '{runtimeProfile}' runtime profile.",
                statusCode: StatusCodes.Status403Forbidden)
            : null;
    }

    private static object Describe(DemoScenarioState state) => new
    {
        id = state.Scenario.Id,
        title = state.Scenario.Title,
        summary = state.Scenario.Summary,
        emphasis = state.Scenario.Emphasis,
        brandName = state.Scenario.BrandName,
        isBank = state.Scenario.IsBank,
        lineFocus = state.Scenario.LineFocus.ToString(),
        partnerInsurers = state.Scenario.PartnerInsurers,
        ready = state.Ready,
        organisationId = state.OrganisationId,
        workspaceId = state.WorkspaceId,
    };
}
