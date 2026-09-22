using InsuranceIntelligence.Api.Auth;
using InsuranceIntelligence.Api.Workflow;
using InsuranceIntelligence.Application.Cases;
using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Infrastructure.Configuration;

namespace InsuranceIntelligence.Api.Cases;

/// <summary>Manual/administrative surface for follow-through (build prompt outcome G): the same
/// ICaseFollowThroughService the scheduled CaseFollowThroughTimerFunction calls, exposed here so
/// an officer or the demo run does not have to wait for the clock, and the manual and scheduled
/// paths are provably the same code, mirroring RetentionEndpoints.EnforceRetention.</summary>
public static class CaseFollowThroughEndpoints
{
    public static async Task<IResult> Run(
        ICaseFollowThroughService followThroughService,
        InfrastructureOptions options,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (DemoAuthorization.Require(httpContext, options, Permission.ManageTenantSettings) is { } forbidden)
        {
            return forbidden;
        }

        if (!WorkflowEndpoints.TryResolveScope(httpContext, out var scope, out _, out var validationError))
        {
            return WorkflowEndpoints.Validation("tenantScope", validationError);
        }

        var correlationId = WorkflowEndpoints.GetCorrelationId(httpContext);
        var result = await followThroughService.RunAsync(scope, correlationId, cancellationToken);

        return Results.Ok(new CaseFollowThroughRunResponse(result.MissingDocumentRemindersSent, result.SlaWarningsSent));
    }
}

public sealed record CaseFollowThroughRunResponse(int MissingDocumentRemindersSent, int SlaWarningsSent);
