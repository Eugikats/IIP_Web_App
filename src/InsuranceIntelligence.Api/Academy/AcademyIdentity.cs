using InsuranceIntelligence.Api.Auth;
using InsuranceIntelligence.Api.Organisations;
using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Infrastructure.Configuration;

namespace InsuranceIntelligence.Api.Academy;

/// <summary>A person using the Academy. Identified by their sign-in alone; they need not belong to any organisation.</summary>
public sealed record AcademyLearner(string UserId, string Name, string? Email, bool HasOrganisation, bool IsOperator);

public static class AcademyIdentity
{
    /// <summary>
    /// The signed-in person, or null for a visitor. A system holding an API key is not a person. In local development the
    /// demo headers stand in for a sign-in, as they do everywhere else.
    /// </summary>
    public static AcademyLearner? Resolve(HttpContext http, InfrastructureOptions options, ITenantContextAccessor tenant, AcademyOperator academyOperator)
    {
        var development = options.AuthMode.Equals("Development", StringComparison.OrdinalIgnoreCase);
        if (!development && http.User.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        var userId = DemoAuthorization.GetUserId(http, fallback: string.Empty);
        if (string.IsNullOrWhiteSpace(userId) || InsuranceIntelligence.Domain.Organisations.ApiKeyRecord.IsMachineUser(userId))
        {
            return null;
        }

        var name = http.User.Identity?.IsAuthenticated == true
            ? FirstClaim(http, RuntimeClaimTypes.DisplayNames)
            : null;
        var email = OrganisationEndpoints.ResolveEmail(http, options);
        var hasOrganisation = tenant.HasCurrent;
        var isOperator = hasOrganisation
            && tenant.Current.Scope.Matches(academyOperator.Scope)
            && DemoAuthorization.Require(http, options, Permission.ManageTenantSettings) is null;

        return new AcademyLearner(userId, string.IsNullOrWhiteSpace(name) ? (email?.Split('@')[0] ?? "Learner") : name.Trim(), email, hasOrganisation, isOperator);
    }

    private static string? FirstClaim(HttpContext http, IEnumerable<string> types) =>
        types.Select(type => http.User.FindFirst(type)?.Value).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
}
