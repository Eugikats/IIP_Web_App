using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Infrastructure.Configuration;

namespace InsuranceIntelligence.Api.Academy;

public static class AcademyMeEndpoint
{
    /// <summary>
    /// Who is using the Academy. Open to everyone: a visitor is told they are signed out, a signed-in person is told their
    /// name, whether they also belong to an organisation and whether they run the Academy.
    /// </summary>
    public static IResult Get(HttpContext http, InfrastructureOptions options, ITenantContextAccessor tenant, AcademyOperator academyOperator)
    {
        http.Response.Headers.CacheControl = "no-store";
        var learner = AcademyIdentity.Resolve(http, options, tenant, academyOperator);
        return learner is null
            ? Results.Ok(new { signedIn = false })
            : Results.Ok(new { signedIn = true, userId = learner.UserId, name = learner.Name, email = learner.Email, hasOrganisation = learner.HasOrganisation, isOperator = learner.IsOperator });
    }
}
