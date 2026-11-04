using InsuranceIntelligence.Api.Common;
using InsuranceIntelligence.Application.Compliance;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.ClientPortal;

namespace InsuranceIntelligence.Api.ClientPortal;

public sealed record ClaimantConsentRequest(string? NoticeVersion);

/// <summary>
/// The privacy notice and a claimant's consent to it (build-loop item 9). The claimant routes are
/// behind the magic-link session and act only on the signed-in claimant's own consent; the staff
/// route shows whether the person who filed a claim has agreed. Agreeing and withdrawing are
/// audited with the notice version, never the notice text or the claimant's contact.
/// </summary>
public static class ClaimantConsentEndpoints
{
    public const string ConsentRequiredCode = "consent_required";

    public static async Task<IResult> GetNotice(
        IClaimantConsentService service,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!ClientPortalScopeReader.TryGetFromHeaders(httpContext, out var scope, out var error))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["tenantScope"] = [error] });
        }

        return Results.Ok(await service.GetStatusAsync(scope, ClientPortalEndpoints.ResolveActor(httpContext), cancellationToken));
    }

    public static async Task<IResult> Grant(
        ClaimantConsentRequest request,
        IClaimantConsentService service,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!ClientPortalScopeReader.TryGetFromHeaders(httpContext, out var scope, out var error))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["tenantScope"] = [error] });
        }

        try
        {
            var session = Session(httpContext);
            return Results.Ok(await service.GrantAsync(
                scope,
                ClientPortalEndpoints.ResolveActor(httpContext),
                request.NoticeVersion ?? string.Empty,
                session?.CaseId,
                session?.SessionId.ToString(),
                httpContext.TraceIdentifier,
                cancellationToken));
        }
        catch (NoticeChangedException ex)
        {
            return Results.Conflict(new { code = "notice_changed", message = ex.Message });
        }
    }

    public static async Task<IResult> Withdraw(
        IClaimantConsentService service,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!ClientPortalScopeReader.TryGetFromHeaders(httpContext, out var scope, out var error))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["tenantScope"] = [error] });
        }

        var session = Session(httpContext);
        return Results.Ok(await service.WithdrawAsync(
            scope,
            ClientPortalEndpoints.ResolveActor(httpContext),
            session?.CaseId,
            session?.SessionId.ToString(),
            httpContext.TraceIdentifier,
            cancellationToken));
    }

    public static async Task<IResult> GetForCase(
        Guid caseId,
        ICaseRepository caseRepository,
        IClaimantConsentService service,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!TenantScopeReader.TryGetFromHeaders(httpContext, out var scope, out var error))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["tenantScope"] = [error] });
        }

        var claimCase = await caseRepository.GetAsync(caseId, cancellationToken);
        if (claimCase is null || claimCase.Scope != scope)
        {
            return Results.NotFound(new { message = "Claim was not found." });
        }

        return Results.Ok(await service.GetStatusForCaseAsync(claimCase, cancellationToken));
    }

    /// <summary>The response a document upload gets when the claimant has not agreed to the
    /// current notice.</summary>
    public static IResult ConsentRequired() =>
        Results.Json(
            new { code = ConsentRequiredCode, message = "Please read and agree to the privacy notice before sending documents." },
            statusCode: StatusCodes.Status403Forbidden);

    private static ClaimantPortalSession? Session(HttpContext httpContext) =>
        httpContext.Items.TryGetValue(ClaimantSessionEndpointFilter.SessionItemKey, out var stashed) ? stashed as ClaimantPortalSession : null;
}
