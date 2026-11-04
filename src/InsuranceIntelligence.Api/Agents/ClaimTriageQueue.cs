using System.Text.Json;
using InsuranceIntelligence.Api.Jobs;
using InsuranceIntelligence.Application.Agents;
using InsuranceIntelligence.Application.ClaimsSummary;
using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Jobs;

namespace InsuranceIntelligence.Api.Agents;

/// <summary>
/// Hands a claim to the claims agent for review without making the person who triggered it wait.
///
/// It used to be an in-process queue, so a claim queued in the seconds before an API restart was
/// never reviewed. It is now a durable job (build-loop item 17): the request writes the job to the
/// database and returns, a worker runs the review, and a review still waiting when the API stops is
/// still there when it starts. Asking again while one is waiting does not queue a second: they
/// coalesce, so a claim that receives five documents in a minute is reviewed once, after the last.
/// </summary>
public interface IClaimTriageQueue
{
    Task EnqueueAsync(TenantScope scope, Guid caseId, string correlationId, string trigger, CancellationToken cancellationToken = default);
}

public sealed class DurableClaimTriageQueue(IBackgroundJobStore store, BackgroundPipelineOptions options) : IClaimTriageQueue
{
    public async Task EnqueueAsync(TenantScope scope, Guid caseId, string correlationId, string trigger, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        await store.EnqueueAsync(
            new BackgroundJob
            {
                JobId = Guid.NewGuid(),
                Scope = scope,
                Queue = BackgroundQueues.ClaimReview,
                Type = BackgroundJobTypes.ClaimReview,
                Key = caseId.ToString("N"),
                PayloadJson = JsonSerializer.Serialize(new { caseId, trigger }),
                Status = BackgroundJobStatus.Pending,
                MaxAttempts = Math.Min(options.MaxAttempts, 3),
                NotBefore = options.Inline ? now : now + TimeSpan.FromSeconds(options.ReviewSettleSeconds),
                CreatedAt = now,
                UpdatedAt = now,
                CorrelationId = correlationId,
                CaseId = caseId,
            },
            coalesceWithRunning: false,
            cancellationToken: cancellationToken);
    }
}

/// <summary>
/// Reviews a claim as the claims agent (build-loop item 17): summary of the claim, its documents and
/// policy, prepared for a person to read. It runs as a system actor with the narrowest role that can
/// read a claim and never decides anything. If the agent is switched off for the workspace the job
/// succeeds without doing anything, so a switched-off agent is not an error.
/// </summary>
public sealed class ClaimReviewJobHandler : IBackgroundJobHandler
{
    /// <summary>Who the claims agent acts as when no person asked for the review. Recorded on the
    /// review, its agent run and its audit events, so the claim timeline says the agent did it.</summary>
    public const string SystemActorId = "system:claim-triage";

    private const string AgentKey = "claims-summary";
    private static readonly TimeSpan RunTimeout = TimeSpan.FromMinutes(2);

    public string Queue => BackgroundQueues.ClaimReview;

    public string Type => BackgroundJobTypes.ClaimReview;

    public async Task HandleAsync(BackgroundJob job, IServiceProvider services, CancellationToken cancellationToken)
    {
        if (job.CaseId is not { } caseId)
        {
            throw new JobFailure("The job does not say which claim to review.", retry: false);
        }

        // Agent tools check permissions through the tenant context, which a background task does not
        // have. Set one explicitly, as the platform itself, with Claims Supervisor: the narrowest role
        // that can read a claim, its documents and its policy, which is all this review needs.
        services.GetRequiredService<ITenantContextSetter>().SetCurrent(new TenantContext(
            job.Scope.TenantId, job.Scope.WorkspaceId, SystemActorId, [PlatformRole.ClaimsSupervisor.ToString()]));

        var gate = await services.GetRequiredService<IAgentGate>().CheckAsync(job.Scope, AgentKey, cancellationToken);
        if (!gate.Enabled)
        {
            return;
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(RunTimeout);
        await services.GetRequiredService<IClaimsSummaryService>().SummarizeAsync(caseId, job.Scope, SystemActorId, job.CorrelationId, timeout.Token);

        // Then the review score and recommendation, which read the summary's model notes (item 18).
        // The score and recommendation are rules and are always kept; only the written explanation
        // depends on the model, and a model failure leaves it out rather than failing the review.
        await services.GetRequiredService<InsuranceIntelligence.Application.Fraud.IClaimAssessmentService>()
            .PrepareAsync(job.Scope, caseId, SystemActorId, job.CorrelationId, timeout.Token);
    }
}
