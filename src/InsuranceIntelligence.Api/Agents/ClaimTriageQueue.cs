using System.Threading.Channels;
using InsuranceIntelligence.Application.Agents;
using InsuranceIntelligence.Application.ClaimsSummary;
using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Api.Agents;

public sealed record ClaimTriageRequest(TenantScope Scope, Guid CaseId, string CorrelationId, string Trigger);

/// <summary>
/// Hands a newly created claim to the claims agent without making the person who submitted it wait.
///
/// Before this, the claims agent only ran when someone opened a claim and pressed "Generate
/// summary", so a claim submitted by a customer or a branch officer sat with no review until a human
/// happened to ask for one. Every intake path (staff entry, the claimant portal, email) now enqueues
/// the claim here the moment it exists, and the review is waiting on the claim page when the handler
/// opens it.
///
/// In-process rather than Service Bus on purpose: the model call is the slow part, not the hand-off,
/// and an in-process queue has no message contract to version and no second deployment to keep in
/// step. The cost is that a claim queued in the seconds before an API restart is not reviewed; the
/// claim page still offers "Generate summary", so nothing is ever blocked on this.
/// </summary>
public interface IClaimTriageQueue
{
    void Enqueue(TenantScope scope, Guid caseId, string correlationId, string trigger);
}

public sealed class ClaimTriageQueue : IClaimTriageQueue
{
    // Bounded, so a burst of imported claims cannot grow memory without limit. Dropping the oldest
    // request under that pressure is safe: an unreviewed claim can still be reviewed on demand.
    private readonly Channel<ClaimTriageRequest> channel = Channel.CreateBounded<ClaimTriageRequest>(
        new BoundedChannelOptions(500) { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true });

    public ChannelReader<ClaimTriageRequest> Reader => channel.Reader;

    public void Enqueue(TenantScope scope, Guid caseId, string correlationId, string trigger) =>
        channel.Writer.TryWrite(new ClaimTriageRequest(scope, caseId, correlationId, trigger));
}

public sealed class ClaimTriageWorker(
    ClaimTriageQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<ClaimTriageWorker> logger) : BackgroundService
{
    /// <summary>Who the claims agent acts as when no person asked for the review. Recorded on the
    /// review, its agent run and its audit events, so the claim timeline says the agent did it.</summary>
    public const string SystemActorId = "system:claim-triage";

    private const string AgentKey = "claims-summary";
    private static readonly TimeSpan RunTimeout = TimeSpan.FromMinutes(2);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var request in queue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await RunAsync(request, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                // A review that fails must never take the worker down with it; the next claim still
                // gets reviewed, and this one can be reviewed on demand from the claim page.
                logger.LogWarning(exception,
                    "Automatic claim review failed. CaseId={CaseId} Trigger={Trigger} CorrelationId={CorrelationId}",
                    request.CaseId, request.Trigger, request.CorrelationId);
            }
        }
    }

    private async Task RunAsync(ClaimTriageRequest request, CancellationToken stoppingToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var services = scope.ServiceProvider;

        // Agent tools check permissions through the tenant context, which a background task does not
        // have. Set one explicitly, as the platform itself, with Claims Supervisor: the narrowest role
        // that can read a claim, its documents and its policy, which is all this review needs.
        services.GetRequiredService<ITenantContextSetter>().SetCurrent(new TenantContext(
            request.Scope.TenantId,
            request.Scope.WorkspaceId,
            SystemActorId,
            [PlatformRole.ClaimsSupervisor.ToString()]));

        var gate = await services.GetRequiredService<IAgentGate>().CheckAsync(request.Scope, AgentKey, stoppingToken);
        if (!gate.Enabled)
        {
            logger.LogInformation(
                "Automatic claim review skipped: the claims agent is switched off for this workspace. CaseId={CaseId}",
                request.CaseId);
            return;
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        timeout.CancelAfter(RunTimeout);

        await services.GetRequiredService<IClaimsSummaryService>().SummarizeAsync(
            request.CaseId,
            request.Scope,
            SystemActorId,
            request.CorrelationId,
            timeout.Token);

        logger.LogInformation(
            "Automatic claim review prepared. CaseId={CaseId} Trigger={Trigger} CorrelationId={CorrelationId}",
            request.CaseId, request.Trigger, request.CorrelationId);
    }
}
