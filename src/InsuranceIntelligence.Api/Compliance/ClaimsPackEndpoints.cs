using InsuranceIntelligence.Api.Workflow;
using InsuranceIntelligence.Application.Compliance;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Cases;

namespace InsuranceIntelligence.Api.Compliance;

/// <summary>
/// Downloads the claims pack for one claim (build-loop item 24): a zip with a summary PDF, the customer's
/// documents renamed after what they are, the evidence exactly as received, and a manifest of SHA-256 hashes.
/// Needs the permission to export evidence; the claim must be in the caller's workspace; every download is
/// audited with what it held. The JSON audit pack export is a separate route and is unchanged.
/// </summary>
public static class ClaimsPackEndpoints
{
    public static async Task<IResult> Download(
        Guid caseId,
        IClaimsPackBuilder builder,
        IAuditRepository auditRepository,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!WorkflowEndpoints.TryResolveScope(httpContext, out var scope, out var userId, out var error))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["tenantScope"] = [error] });
        }

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["userId"] = ["Authenticated user context is required to download a claims pack."] });
        }

        var pack = await builder.BuildAsync(scope, caseId, userId, cancellationToken);
        if (pack is null)
        {
            return Results.NotFound(new { message = "Claim was not found." });
        }

        await auditRepository.WriteAsync(AuditEvent.ClaimsPackExported(scope, caseId, userId, pack.Manifest.Files.Count, pack.Content.Length, httpContext.TraceIdentifier), cancellationToken);
        httpContext.Response.Headers["X-Pack-Files"] = pack.Manifest.Files.Count.ToString();
        return Results.File(pack.Content, "application/zip", pack.FileName);
    }
}
