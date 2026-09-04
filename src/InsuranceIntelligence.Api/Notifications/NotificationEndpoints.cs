using InsuranceIntelligence.Api.CommandCenter;
using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Application.Notifications;
using InsuranceIntelligence.Infrastructure.Configuration;

namespace InsuranceIntelligence.Api.Notifications;

/// <summary>
/// The tenant- and recipient-scoped in-app notification feed (WO-14). Every response is
/// implicitly scoped to the caller's own resolved TenantContext — there is no way to pass another
/// user's id, so this can never leak another user's notifications even within the same tenant (I5).
/// </summary>
public static class NotificationEndpoints
{
    public static async Task<IResult> GetFeed(
        int? limit,
        INotificationFeedService feedService,
        ITenantContextSetter tenantContextSetter,
        InfrastructureOptions options,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!CommandCenterQueryEndpoints.TrySetTenantContext(httpContext, tenantContextSetter, options, out var error)) return error;
        return Results.Ok(await feedService.GetFeedAsync(limit ?? 20, cancellationToken));
    }

    public static async Task<IResult> MarkRead(
        Guid notificationId,
        INotificationFeedService feedService,
        ITenantContextSetter tenantContextSetter,
        InfrastructureOptions options,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!CommandCenterQueryEndpoints.TrySetTenantContext(httpContext, tenantContextSetter, options, out var error)) return error;
        try
        {
            return Results.Ok(await feedService.MarkReadAsync(notificationId, cancellationToken));
        }
        catch (NotificationNotFoundException)
        {
            return Results.NotFound(new { message = "Notification was not found for the signed-in user." });
        }
    }

    public static async Task<IResult> MarkAllRead(
        INotificationFeedService feedService,
        ITenantContextSetter tenantContextSetter,
        InfrastructureOptions options,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!CommandCenterQueryEndpoints.TrySetTenantContext(httpContext, tenantContextSetter, options, out var error)) return error;
        var count = await feedService.MarkAllReadAsync(cancellationToken);
        return Results.Ok(new { markedRead = count });
    }
}
