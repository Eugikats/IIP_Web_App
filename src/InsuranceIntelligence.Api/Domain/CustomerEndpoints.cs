using InsuranceIntelligence.Api.Auth;
using InsuranceIntelligence.Api.Common;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Communications;
using InsuranceIntelligence.Domain.Customers;
using InsuranceIntelligence.Domain.Policies;
using Microsoft.AspNetCore.Mvc;

namespace InsuranceIntelligence.Api.Domain;

public static class CustomerEndpoints
{
    public static void MapCustomerEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/v1/customers")
            .WithTags("Customers")
            .RequireAuthorization()
            .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewCustomers));

        // NOTE: the customer list lives on CustomerProfileEndpoints.ListCustomers, registered in
        // Program.cs — that is the shape the portal consumes. A second `group.MapGet("/")` here
        // produced a route that also matched GET /api/v1/customers, so ASP.NET threw
        // AmbiguousMatchException and every request to the Customers page returned 500. Only the
        // unique customer-360 aggregate belongs in this group.

        group.MapGet("/{customerId}/360", async (
            string customerId,
            [FromServices] ICustomerRepository customerRepo,
            [FromServices] IPolicyRepository policyRepo,
            [FromServices] ICaseRepository caseRepo,
            [FromServices] ICommunicationRepository commsRepo,
            [FromServices] ITenantContext context,
            CancellationToken ct) =>
        {
            var scope = TenantScope.Create(context.TenantId.ToString(), context.WorkspaceId);

            var customer = await customerRepo.GetAsync(customerId, scope, ct);
            if (customer is null) return Results.NotFound();

            var policiesTask = policyRepo.ListByCustomerAsync(customerId, scope, ct);
            var casesTask = caseRepo.ListAsync(scope, ct); // In a real app we'd filter cases by customerId directly in the repo
            var commsTask = commsRepo.ListAsync(scope, ct); // In a real app we'd filter communications by customerId or cases

            await Task.WhenAll(policiesTask, casesTask, commsTask);

            // Client-side filtering for MVP
            var policies = await policiesTask;
            var cases = (await casesTask)
                .Where(c => c.CustomerName == customer.FullName || (!string.IsNullOrEmpty(c.CustomerEmail) && c.CustomerEmail == customer.Email))
                .ToList();
            var caseIds = cases.Select(c => c.CaseId).ToHashSet();
            var comms = (await commsTask).Where(c => caseIds.Contains(c.CaseId)).ToList();

            var customer360 = new Customer360(customer, policies, cases, comms);
            return Results.Ok(customer360);
        });
    }
}
