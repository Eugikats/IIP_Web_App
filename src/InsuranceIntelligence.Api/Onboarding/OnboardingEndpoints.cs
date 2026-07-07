using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Application.Onboarding;
using InsuranceIntelligence.Infrastructure.Configuration;

namespace InsuranceIntelligence.Api.Onboarding;

/// <summary>
/// Supplies the non-secret Entra settings needed to build the admin-consent redirect. The client id
/// comes from the app registration; the redirect URI is the onboarding callback. If either is
/// missing the consent URL is reported as Not configured (never fabricated).
/// </summary>
public sealed class OnboardingConfiguration(InfrastructureOptions options, IConfiguration configuration) : IOnboardingConfiguration
{
    public string? EntraClientId => options.EntraClientId;
    public string? AdminConsentRedirectUri =>
        configuration["Onboarding:AdminConsentRedirectUri"] ?? configuration["Onboarding__AdminConsentRedirectUri"];
}

public static class OnboardingEndpoints
{
    public static async Task<IResult> Create(
        CreateTenantOnboardingRequest request,
        ITenantOnboardingService service,
        CancellationToken cancellationToken)
    {
        try
        {
            var created = await service.CreatePendingAsync(request, cancellationToken);
            return Results.Created($"/api/v1/onboarding/{created.OnboardingId}", created);
        }
        catch (TenantOnboardingValidationException ex)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { [ex.Field] = [ex.Message] });
        }
        catch (PermissionDeniedException)
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }
    }

    public static async Task<IResult> List(ITenantOnboardingService service, CancellationToken cancellationToken)
    {
        try
        {
            return Results.Ok(await service.ListAsync(cancellationToken));
        }
        catch (PermissionDeniedException)
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }
    }

    public static async Task<IResult> Get(string onboardingId, ITenantOnboardingService service, CancellationToken cancellationToken)
    {
        try
        {
            var record = await service.GetAsync(onboardingId, cancellationToken);
            return record is null
                ? Results.NotFound(new { message = "Onboarding record not found." })
                : Results.Ok(record);
        }
        catch (PermissionDeniedException)
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }
    }

    public static async Task<IResult> GetConsentUrl(string onboardingId, ITenantOnboardingService service, CancellationToken cancellationToken)
    {
        try
        {
            return Results.Ok(await service.GetAdminConsentUrlAsync(onboardingId, cancellationToken));
        }
        catch (TenantOnboardingValidationException ex)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { [ex.Field] = [ex.Message] });
        }
        catch (PermissionDeniedException)
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }
    }

    /// <summary>
    /// Microsoft admin-consent redirect target. Authorized by the opaque state value, not a
    /// permission — the insurer admin returns here from Microsoft with admin_consent/tenant/state.
    /// </summary>
    public static async Task<IResult> ConsentCallback(
        HttpContext httpContext,
        ITenantOnboardingService service,
        CancellationToken cancellationToken)
    {
        var query = httpContext.Request.Query;
        var request = new CompleteConsentRequest(
            State: query["state"].ToString(),
            Tenant: query["tenant"].ToString(),
            AdminConsent: bool.TryParse(query["admin_consent"], out var consent) && consent,
            AdminUserId: query["admin_user_id"].ToString());
        try
        {
            var result = await service.CompleteConsentAsync(request, cancellationToken);
            return Results.Ok(result);
        }
        catch (TenantOnboardingValidationException ex)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { [ex.Field] = [ex.Message] });
        }
    }
}
