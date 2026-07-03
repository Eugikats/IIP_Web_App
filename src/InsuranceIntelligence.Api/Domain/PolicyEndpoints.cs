using InsuranceIntelligence.Api.Common;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Policies;
using Microsoft.AspNetCore.Mvc;

namespace InsuranceIntelligence.Api.Domain;

public static class PolicyEndpoints
{
    public static void MapPolicyEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/v1/policies")
            .WithTags("Policies")
            .RequireAuthorization();

        group.MapGet("/", async ([FromServices] IPolicyRepository repository, [FromServices] ITenantContext context, CancellationToken ct) =>
        {
            var scope = TenantScope.Create(context.TenantId.ToString(), context.WorkspaceId);
            var policies = await repository.ListAsync(scope, ct);
            return Results.Ok(policies);
        });

        group.MapGet("/{policyId}/coverage", async (
            string policyId,
            [FromServices] ICoverageRepository coverageRepo,
            [FromServices] ITenantContext context,
            CancellationToken ct) =>
        {
            var scope = TenantScope.Create(context.TenantId.ToString(), context.WorkspaceId);
            var coverages = await coverageRepo.ListForPolicyAsync(policyId, scope, ct);
            return Results.Ok(coverages);
        });
    }
}
