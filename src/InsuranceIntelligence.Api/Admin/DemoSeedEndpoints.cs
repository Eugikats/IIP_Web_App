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
        // The caller asked for this demo dataset, so assign its work to them, otherwise their own
        // "My Work" stays empty over the very data they just seeded.
        var result = await seedService.SeedAsync(scope, userId, userId, cancellationToken);

        return Results.Ok(new
        {
            legacyCasesPurged = purged,
            result.InsurerName,
            result.Products,
            result.Customers,
            result.Policies,
            result.Cases,
            result.Documents,
            result.Approvals,
            result.AuditEvents,
            heroCaseIds = result.HeroCaseIds
        });
    }

    /// <summary>
    /// Adds a few policyholders with an active policy and no claim, for demonstrating the claimant
    /// portal's first-claim journey. Same gates as <see cref="Run"/>: never on a protected runtime
    /// profile, and only for someone who can manage tenant settings. Scope comes from the signed-in
    /// member rather than the X-Tenant-Id header <see cref="Run"/> reads, which Entra auth rejects.
    /// </summary>
    public static async Task<IResult> SeedPolicyholders(
        DemoSeedService seedService,
        InfrastructureOptions options,
        HttpContext httpContext,
        IWebHostEnvironment environment,
        CancellationToken cancellationToken,
        int? count = null)
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

        if (!InsuranceIntelligence.Api.Workflow.WorkflowEndpoints.TryResolveScope(httpContext, out var scope, out _, out var validationError))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["tenantScope"] = [validationError] });
        }

        var policyholders = await seedService.SeedPolicyholdersWithoutClaimsAsync(scope, count ?? 3, cancellationToken);
        return Results.Ok(new { policyholders });
    }

    /// <summary>
    /// Creates, or refreshes, a policyholder profile for someone testing the WhatsApp assistant
    /// from their own phone: their name and number, a Motor and a Medical policy, and two past
    /// claims. The person's details arrive in the request, so no real phone number is ever written
    /// into source code or the shared demo dataset. Same Demo-only gate as every other seed route.
    /// </summary>
    public static async Task<IResult> SeedTestPolicyholder(
        TestPolicyholderRequest request,
        DemoSeedService seedService,
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

        if (!InsuranceIntelligence.Api.Workflow.WorkflowEndpoints.TryResolveScope(httpContext, out var scope, out _, out var validationError))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["tenantScope"] = [validationError] });
        }

        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.FullName))
        {
            errors["fullName"] = ["The policyholder's full name is required."];
        }

        if (InsuranceIntelligence.Domain.Customers.PhoneNumbers.Digits(request.Phone).Length < 9)
        {
            errors["phone"] = ["A phone number with the country code is required, for example +256 7XX XXXXXX."];
        }

        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var userId = DemoAuthorization.GetUserId(httpContext, fallback: "system:demo-seed");
        var result = await seedService.SeedTestPolicyholderAsync(
            scope, request.FullName, request.Phone, request.Email, "system:demo-seed", userId, cancellationToken);
        return Results.Ok(result);
    }

    /// <summary>
    /// Publishes the Kato field test's microinsurance products, indexes their wordings and key
    /// information summaries, and replaces the made-up approval references earlier seeds stamped on
    /// products with a plain demo marker. With a phone number, also loads the demo policyholder for
    /// it (the same thing "#demo" does from the demo phone). Idempotent; same Demo-only gate.
    /// </summary>
    public static async Task<IResult> SeedMicroinsurance(
        MicroinsuranceSeedRequest? request,
        MicroinsuranceDemoSeeder seeder,
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

        if (!InsuranceIntelligence.Api.Workflow.WorkflowEndpoints.TryResolveScope(httpContext, out var scope, out _, out var validationError))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["tenantScope"] = [validationError] });
        }

        var catalogue = await seeder.SeedCatalogueAsync(scope, cancellationToken);
        MicroinsuranceDemoSeeder.DemoPolicyholderResult? policyholder = null;
        if (!string.IsNullOrWhiteSpace(request?.Phone))
        {
            if (InsuranceIntelligence.Domain.Customers.PhoneNumbers.Digits(request.Phone).Length < 9)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["phone"] = ["A phone number with the country code is required, for example +256 7XX XXXXXX."]
                });
            }

            policyholder = await seeder.SeedDemoPolicyholderAsync(scope, request.Phone, cancellationToken);
        }

        return Results.Ok(new { catalogue, policyholder });
    }

    /// <summary>Loads an invented bank customer list (40 people) as prospects, for the bancassurance
    /// demonstration. Idempotent; same Demo-only gate as every other seed route.</summary>
    public static async Task<IResult> SeedBankCustomers(
        InsuranceIntelligence.Application.DemoSeeding.BankCustomerDemoSeeder seeder,
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

        if (!InsuranceIntelligence.Api.Workflow.WorkflowEndpoints.TryResolveScope(httpContext, out var scope, out _, out var validationError))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["tenantScope"] = [validationError] });
        }

        return Results.Ok(await seeder.SeedAsync(scope, cancellationToken));
    }
}

public sealed record MicroinsuranceSeedRequest(string? Phone);

public sealed record TestPolicyholderRequest(string FullName, string Phone, string? Email);
