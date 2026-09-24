using InsuranceIntelligence.Api.Workflow;
using InsuranceIntelligence.Application.Agents;
using InsuranceIntelligence.Application.CustomerSummary;
using Microsoft.Extensions.Caching.Memory;

namespace InsuranceIntelligence.Api.Customer;

public sealed class CustomerSummaryLogMarker;

/// <summary>
/// POST /api/v1/customers/{customerId}/summary: the Customer Summary Agent's briefing for the
/// Customer 360 page.
///
/// The model call takes several seconds, and a staff member moving between a customer and their
/// claims would otherwise pay that every time the page opened. A prepared briefing is therefore
/// kept for 30 minutes per workspace and customer, and the response always says when it was
/// prepared so nobody mistakes an older briefing for a fresh one. ?refresh=true prepares a new one.
/// </summary>
public static class CustomerSummaryEndpoints
{
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromMinutes(30);

    public sealed record CustomerSummaryResponse(
        string CustomerId,
        string Summary,
        IReadOnlyList<CustomerSummarySource> Sources,
        DateTimeOffset GeneratedAt,
        bool FromCache);

    public static async Task<IResult> Summarize(
        string customerId,
        bool? refresh,
        ICustomerSummaryService summaryService,
        IAgentGate agentGate,
        IMemoryCache cache,
        HttpContext httpContext,
        ILogger<CustomerSummaryLogMarker> logger,
        CancellationToken cancellationToken)
    {
        if (!WorkflowEndpoints.TryResolveScope(httpContext, out var scope, out _, out var validationError))
        {
            return WorkflowEndpoints.Validation("tenantScope", validationError);
        }

        if (string.IsNullOrWhiteSpace(customerId))
        {
            return WorkflowEndpoints.Validation("customerId", "A customer is required.");
        }

        var gate = await agentGate.CheckAsync(CustomerSummaryService.AgentKey, cancellationToken);
        if (!gate.Enabled)
        {
            return Results.Json(
                new { status = "AgentDisabled", agentKey = CustomerSummaryService.AgentKey, message = gate.RefusalMessage },
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        var cacheKey = $"customer-summary:{scope.TenantId}:{scope.WorkspaceId}:{customerId.Trim()}";
        if (refresh is not true && cache.TryGetValue(cacheKey, out CustomerSummaryResult? cached) && cached is not null)
        {
            return Results.Ok(ToResponse(cached, fromCache: true));
        }

        var actor = WorkflowEndpoints.GetActor(httpContext, null);
        var correlationId = WorkflowEndpoints.GetCorrelationId(httpContext);

        CustomerSummaryResult? result;
        try
        {
            result = await summaryService.SummarizeAsync(customerId.Trim(), scope, actor, correlationId, cancellationToken);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(exception, "Customer summary could not be prepared. CorrelationId={CorrelationId}", correlationId);
            return Results.Json(
                new
                {
                    status = "Unavailable",
                    message = "The customer summary could not be prepared just now. The records on this page are complete; try again in a moment.",
                    correlationId
                },
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        if (result is null)
        {
            return Results.NotFound(new { message = "Customer profile was not found for the current tenant workspace." });
        }

        cache.Set(cacheKey, result, CacheLifetime);
        return Results.Ok(ToResponse(result, fromCache: false));
    }

    private static CustomerSummaryResponse ToResponse(CustomerSummaryResult result, bool fromCache) =>
        new(result.CustomerId, result.Summary, result.Sources, result.GeneratedAt, fromCache);
}
