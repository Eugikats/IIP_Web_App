using InsuranceIntelligence.Application.Cases;
using InsuranceIntelligence.Domain.Cases;

namespace InsuranceIntelligence.Api.Cases;

/// <summary>
/// Tells the customer about a status change from the endpoints that make one (build-loop item 21). It runs
/// after the change and its audit event are saved and never lets a messaging problem undo or fail them: the
/// notifier records what happened, and a message that could not go out is kept for a person.
/// </summary>
internal static class ClaimStatusNotifications
{
    public static async Task NotifyAsync(HttpContext httpContext, ClaimCase before, ClaimCase after, string correlationId, CancellationToken cancellationToken)
    {
        if (before.Status == after.Status)
        {
            return;
        }

        // A host without request services (a direct call in a unit test) has nothing to notify through.
        var notifier = httpContext.RequestServices?.GetService<IClaimStatusNotifier>();
        if (notifier is null)
        {
            return;
        }

        try
        {
            await notifier.NotifyAsync(before, after, correlationId, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            httpContext.RequestServices?.GetService<ILoggerFactory>()?
                .CreateLogger("ClaimStatusNotifications")
                .LogWarning(exception, "The customer could not be told about claim {CaseId} moving to {Status}.", after.CaseId, after.Status);
        }
    }
}
