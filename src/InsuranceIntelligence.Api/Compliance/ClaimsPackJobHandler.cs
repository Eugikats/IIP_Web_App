using System.Text.Json;
using InsuranceIntelligence.Api.Jobs;
using InsuranceIntelligence.Application.Compliance;
using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Jobs;

namespace InsuranceIntelligence.Api.Compliance;

/// <summary>
/// Emails a claim's pack in the background when the workspace's chosen trigger happened (build-loop item 25). It is a
/// durable job: it is retried if the mail connection fails, and gives up visibly (Admin, Background work) with a plain
/// reason rather than losing the send. It runs as the platform itself and records that.
/// </summary>
public sealed class ClaimsPackJobHandler : IBackgroundJobHandler
{
    public const string SystemActorId = "system:claims-pack";

    public string Queue => BackgroundQueues.ClaimReview;

    public string Type => BackgroundJobTypes.ClaimsPackSend;

    public async Task HandleAsync(BackgroundJob job, IServiceProvider services, CancellationToken cancellationToken)
    {
        using var payload = JsonDocument.Parse(job.PayloadJson);
        if (job.CaseId is not { } caseId
            || !payload.RootElement.TryGetProperty("reasonKey", out var reasonKey)
            || !payload.RootElement.TryGetProperty("reason", out var reason))
        {
            throw new JobFailure("The job does not say which claim or why.", retry: false);
        }

        services.GetRequiredService<ITenantContextSetter>().SetCurrent(new TenantContext(
            job.Scope.TenantId, job.Scope.WorkspaceId, SystemActorId, [PlatformRole.ClaimsSupervisor.ToString()]));
        var result = await services.GetRequiredService<IClaimsPackEmailService>().SendAsync(
            job.Scope, caseId, reasonKey.GetString()!, reason.GetString()!, SystemActorId, job.CorrelationId, cancellationToken);

        if (result.Outcome is "Sent" or "AlreadySent" or "NotFound" or "NoRecipients")
        {
            return;
        }

        throw new JobFailure("The claims pack could not be emailed to everyone. Check the mail connection.");
    }
}

/// <summary>Where a shared pack link points: the customer portal's address, or the local portal in development.</summary>
public sealed class ConfiguredPortalBaseUrl(InsuranceIntelligence.Infrastructure.Configuration.InfrastructureOptions options) : IPortalBaseUrl
{
    public string? BaseUrl =>
        !string.IsNullOrWhiteSpace(options.ClientPortalBaseUrl) ? options.ClientPortalBaseUrl.TrimEnd('/')
        : options.AuthMode.Equals("Development", StringComparison.OrdinalIgnoreCase) ? "http://localhost:3000"
        : null;
}

/// <summary>Tells the pack trigger about a change from the places that make one (a request or a background job).</summary>
internal static class ClaimsPackTriggers
{
    public static async Task OnCompletenessAsync(IServiceProvider? services, InsuranceIntelligence.Domain.Cases.ClaimCase claim, string correlationId, CancellationToken cancellationToken)
    {
        var trigger = services?.GetService<IClaimsPackTrigger>();
        if (trigger is null || claim.CompletenessScore < 100m)
        {
            return;
        }

        try
        {
            await trigger.OnCompletenessAsync(claim, correlationId, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            services!.GetService<ILoggerFactory>()?.CreateLogger("ClaimsPackTriggers").LogWarning(exception, "The claims pack trigger failed for claim {CaseId}.", claim.CaseId);
        }
    }
}
