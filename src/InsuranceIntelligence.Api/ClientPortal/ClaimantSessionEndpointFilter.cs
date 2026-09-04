using InsuranceIntelligence.Domain.ClientPortal;
using InsuranceIntelligence.Infrastructure.Configuration;

namespace InsuranceIntelligence.Api.ClientPortal;

/// <summary>
/// Verifies the X-Client-Portal-Session header against a stored <see cref="ClaimantPortalSession"/>
/// before a claimant-facing route runs, and — on success — stashes the session's verified contact
/// value in HttpContext.Items for the handler to use instead of trusting client-supplied identity
/// headers.
///
/// Outside Development, a missing or invalid session token fails the request closed (401) here,
/// before the handler runs. In Development, a missing session is allowed through so the existing
/// demo-header-based local testing flow keeps working — ClientPortalEndpoints itself falls back to
/// header trust only in that same case, so the two must agree.
/// </summary>
public sealed class ClaimantSessionEndpointFilter : IEndpointFilter
{
    public const string ContactValueItemKey = "iip.claimant-contact-value";
    public const string SessionItemKey = "iip.claimant-session";
    public const string SessionHeaderName = "X-Client-Portal-Session";

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var httpContext = context.HttpContext;
        var options = httpContext.RequestServices.GetService<InfrastructureOptions>();
        var isDevelopment = options is null || options.AuthMode.Equals("Development", StringComparison.OrdinalIgnoreCase);

        if (!httpContext.Request.Headers.TryGetValue(SessionHeaderName, out var rawSessionToken)
            || string.IsNullOrWhiteSpace(rawSessionToken))
        {
            return isDevelopment ? await next(context) : Results.Unauthorized();
        }

        var sessionRepository = httpContext.RequestServices.GetService<IClaimantPortalSessionRepository>();
        if (sessionRepository is null || !ClientPortalScopeReader.TryGetFromHeaders(httpContext, out var scope, out _))
        {
            return isDevelopment ? await next(context) : Results.Unauthorized();
        }

        var tokenHash = ClaimantTokenGenerator.Hash(rawSessionToken.ToString());
        var session = await sessionRepository.GetByTokenHashAsync(tokenHash, scope, httpContext.RequestAborted);
        if (session is null || !session.IsValidAt(DateTimeOffset.UtcNow))
        {
            return Results.Unauthorized();
        }

        if (context.HttpContext.Request.RouteValues.TryGetValue("caseId", out var routeCaseId)
            && Guid.TryParse(routeCaseId?.ToString(), out var caseId)
            && caseId != session.CaseId)
        {
            return Results.Unauthorized();
        }

        httpContext.Items[ContactValueItemKey] = session.ContactValue;
        httpContext.Items[SessionItemKey] = session;
        return await next(context);
    }
}
