using InsuranceIntelligence.Api.Auth;
using InsuranceIntelligence.Api.Workflow;
using InsuranceIntelligence.Application.Webhooks;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Integrations;
using InsuranceIntelligence.Domain.Webhooks;
using InsuranceIntelligence.Infrastructure.Configuration;

namespace InsuranceIntelligence.Api.Webhooks;

public sealed record CreateWebhookRequest(string? Url, IReadOnlyList<string>? Events);

/// <summary>Events out to the insurer's own systems: ERP, core insurance system, data warehouse.</summary>
public static class WebhookEndpoints
{
    public const int MaxSubscriptionsPerWorkspace = 10;

    public static IResult Catalogue() => Results.Ok(new
    {
        events = OutboundEventTypes.All,
        envelope = new { id = "event id", type = "one of events", occurredAt = "ISO 8601", organisationId = "string", workspaceId = "string", data = "see below" },
        data = new { claimId = "guid", claimReference = "CLM-XXXXXXXX", action = "audit action", previousStatus = "string or null", status = "string or null" },
        signing = new
        {
            headers = new[] { WebhookSignature.SignatureHeader, WebhookSignature.TimestampHeader, WebhookSignature.EventHeader, WebhookSignature.DeliveryHeader },
            algorithm = "HMAC-SHA256 over \"{timestamp}.{raw body}\" with your signing secret; sent as sha256={hex}. Reject calls older than 5 minutes.",
        },
        delivery = "At least once. Answer 2xx within 10 seconds. Failed calls are retried 7 times over about 36 hours; use the delivery id to ignore duplicates.",
        privacy = "Events carry identifiers and statuses only. Fetch anything else through the API.",
    });

    public static async Task<IResult> Create(
        CreateWebhookRequest request, IOutboundWebhookRepository repository, IConnectionSecretProtector protector,
        InfrastructureOptions options, HttpContext httpContext, CancellationToken cancellationToken)
    {
        if (DemoAuthorization.Require(httpContext, options, Permission.ConfigureIntegrations) is { } forbidden)
        {
            return forbidden;
        }

        if (!WorkflowEndpoints.TryResolveScope(httpContext, out var scope, out var userId, out var error))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["tenantScope"] = [error] });
        }

        if (!WebhookUrlPolicy.TryValidate(request.Url, out var uri, out var urlError))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["url"] = [urlError] });
        }

        var events = (request.Events is { Count: > 0 } ? request.Events : ["*"]).Select(e => e.Trim()).Distinct().ToArray();
        var unknown = events.FirstOrDefault(e => !OutboundEventTypes.IsKnown(e));
        if (unknown is not null)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["events"] = [$"Unknown event '{unknown}'. Choose from {string.Join(", ", OutboundEventTypes.All)}, or *."] });
        }

        if ((await repository.ListSubscriptionsAsync(scope, cancellationToken)).Count >= MaxSubscriptionsPerWorkspace)
        {
            return Results.Conflict(new { message = $"A workspace can have {MaxSubscriptionsPerWorkspace} webhooks. Remove one first." });
        }

        var secret = WebhookSignature.NewSecret();
        var subscription = new OutboundWebhookSubscription(
            Guid.NewGuid(), scope, uri!.ToString(), protector.Protect(secret), events, true, userId, DateTimeOffset.UtcNow);
        await repository.SaveSubscriptionAsync(subscription, cancellationToken);

        // The signing secret is shown here and nowhere else: only the receiver and IIP ever hold it.
        return Results.Created($"/api/v1/organisation/webhooks/{subscription.SubscriptionId}", new
        {
            subscription.SubscriptionId, subscription.Url, subscription.EventTypes, signingSecret = secret,
        });
    }

    public static async Task<IResult> List(
        IOutboundWebhookRepository repository, InfrastructureOptions options, HttpContext httpContext, CancellationToken cancellationToken)
    {
        if (DemoAuthorization.Require(httpContext, options, Permission.ConfigureIntegrations) is { } forbidden)
        {
            return forbidden;
        }

        if (!WorkflowEndpoints.TryResolveScope(httpContext, out var scope, out _, out var error))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["tenantScope"] = [error] });
        }

        var subscriptions = await repository.ListSubscriptionsAsync(scope, cancellationToken);
        return Results.Ok(subscriptions.Select(s => new
        {
            s.SubscriptionId, s.Url, s.EventTypes, s.IsActive, s.CreatedAt, s.LastDeliveredAt, s.ConsecutiveFailures,
        }));
    }

    public static async Task<IResult> Remove(
        Guid subscriptionId, IOutboundWebhookRepository repository, InfrastructureOptions options, HttpContext httpContext, CancellationToken cancellationToken)
    {
        if (DemoAuthorization.Require(httpContext, options, Permission.ConfigureIntegrations) is { } forbidden)
        {
            return forbidden;
        }

        if (!WorkflowEndpoints.TryResolveScope(httpContext, out var scope, out _, out var error))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["tenantScope"] = [error] });
        }

        await repository.DeleteSubscriptionAsync(scope, subscriptionId, cancellationToken);
        return Results.NoContent();
    }

    public static async Task<IResult> Test(
        Guid subscriptionId, IOutboundWebhookRepository repository, OutboundEventPublisher publisher,
        InfrastructureOptions options, HttpContext httpContext, CancellationToken cancellationToken)
    {
        if (DemoAuthorization.Require(httpContext, options, Permission.ConfigureIntegrations) is { } forbidden)
        {
            return forbidden;
        }

        if (!WorkflowEndpoints.TryResolveScope(httpContext, out var scope, out _, out var error))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["tenantScope"] = [error] });
        }

        var subscription = await repository.GetSubscriptionAsync(scope, subscriptionId, cancellationToken);
        if (subscription is null)
        {
            return Results.NotFound();
        }

        var delivery = await publisher.QueuePingAsync(subscription, cancellationToken);
        return Results.Accepted(value: new { delivery.DeliveryId, message = "A test event is queued and will be sent within a few seconds." });
    }

    public static async Task<IResult> Deliveries(
        Guid subscriptionId, IOutboundWebhookRepository repository, InfrastructureOptions options, HttpContext httpContext, CancellationToken cancellationToken)
    {
        if (DemoAuthorization.Require(httpContext, options, Permission.ConfigureIntegrations) is { } forbidden)
        {
            return forbidden;
        }

        if (!WorkflowEndpoints.TryResolveScope(httpContext, out var scope, out _, out var error))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["tenantScope"] = [error] });
        }

        var deliveries = await repository.ListDeliveriesAsync(scope, subscriptionId, 50, cancellationToken);
        return Results.Ok(deliveries.Select(d => new
        {
            d.DeliveryId, d.EventType, status = d.Status.ToString(), d.Attempts, d.CreatedAt, d.DeliveredAt, d.LastStatusCode, d.LastError, d.NextAttemptAt,
        }));
    }
}
