using InsuranceIntelligence.Api.Auth;
using InsuranceIntelligence.Api.Azure;
using InsuranceIntelligence.Api.Common;
using InsuranceIntelligence.Application.DemoSeeding;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Infrastructure.Configuration;
using InsuranceIntelligence.Infrastructure.DemoSeeding;

namespace InsuranceIntelligence.Api.Admin;

/// <summary>
/// Admin-triggered demo dataset seeding. Hard-gated to non-protected runtime profiles
/// (never reachable in Staging/Production/ManagedApplicationReviewer, mirroring the same
/// classification AzureProviderStartupValidator uses to decide the environment banner) so
/// this can never run against a deployment holding real customer data.
/// </summary>
public static class DemoSeedEndpoints
{
    // The two LocalTest/UAT artifacts confirmed for removal before the reseed.
    private static readonly Guid[] LegacyCaseIdsToPurge =
    [
        Guid.Parse("60ce6ebd-8362-4851-9ec5-a7602f53d3d2"),
        Guid.Parse("80bffbab-3459-40e4-bc06-da8e0c0753e4")
    ];

    public static async Task<IResult> Run(
        DemoSeedService seedService,
        IDemoDataPurgeService purgeService,
        InfrastructureOptions options,
        HttpContext httpContext,
        IWebHostEnvironment environment,
        CancellationToken cancellationToken)
    {
        var runtimeProfile = AzureProviderStartupValidator.ResolveRuntimeProfile(options, environment.EnvironmentName);
        if (AzureProviderStartupValidator.IsProtectedRuntime(runtimeProfile))
        {
            return Results.Problem(
                $"Demo data seeding is not available under the '{runtimeProfile}' runtime profile.",
                statusCode: StatusCodes.Status403Forbidden);
        }

        if (DemoAuthorization.Require(httpContext, options, Permission.ManageTenantSettings) is { } forbidden)
        {
            return forbidden;
        }

        if (!TenantScopeReader.TryGetFromHeaders(httpContext, out var scope, out var validationError))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["tenantScope"] = [validationError] });
        }

        var userId = DemoAuthorization.GetUserId(httpContext);

        var purged = await purgeService.PurgeCasesAsync(LegacyCaseIdsToPurge, scope, cancellationToken);
        // The caller asked for this demo dataset, so assign its work to them — otherwise their own
        // "My Work" stays empty over the very data they just seeded.
        var result = await seedService.SeedAsync(scope, userId, userId, cancellationToken);

        return Results.Ok(new
        {
            legacyCasesPurged = purged,
            result.InsurerName,
            result.Customers,
            result.Policies,
            result.Cases,
            result.Documents,
            result.Approvals,
            result.AuditEvents,
            heroCaseIds = result.HeroCaseIds
        });
    }
}
