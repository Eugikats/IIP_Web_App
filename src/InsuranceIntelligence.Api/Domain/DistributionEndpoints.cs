using InsuranceIntelligence.Api.Common;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Distribution;
using Microsoft.AspNetCore.Mvc;

namespace InsuranceIntelligence.Api.Domain;

public static class DistributionEndpoints
{
    public static void MapDistributionEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/v1/distribution")
            .WithTags("Distribution")
            .RequireAuthorization();

        group.MapGet("/branches", async ([FromServices] IBranchRepository repository, [FromServices] ITenantContext context, CancellationToken ct) =>
        {
            var scope = TenantScope.Create(context.TenantId.ToString(), context.WorkspaceId);
            var branches = await repository.ListAsync(scope, ct);
            return Results.Ok(branches);
        });

        group.MapGet("/producers", async ([FromServices] IProducerRepository repository, [FromServices] ITenantContext context, CancellationToken ct) =>
        {
            var scope = TenantScope.Create(context.TenantId.ToString(), context.WorkspaceId);
            var producers = await repository.ListAsync(scope, ct);
            return Results.Ok(producers);
        });
    }
}
