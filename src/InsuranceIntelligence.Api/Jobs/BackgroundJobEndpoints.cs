using InsuranceIntelligence.Api.Workflow;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Jobs;

namespace InsuranceIntelligence.Api.Jobs;

public sealed record BackgroundJobResponse(
    Guid JobId,
    string Queue,
    string Type,
    string What,
    string Status,
    int Attempts,
    int MaxAttempts,
    DateTimeOffset? NextAttemptAt,
    string? LastError,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt,
    Guid? CaseId,
    string? ClaimReference);

public sealed record BackgroundWorkResponse(
    int Pending,
    int Running,
    int Succeeded,
    int DeadLettered,
    IReadOnlyList<BackgroundJobResponse> Jobs);

/// <summary>
/// Visibility of the durable background work (build-loop item 17): what is waiting, running, done
/// and stuck, with plain reasons, and a way for an administrator to send a dead-lettered job back.
/// Everything is limited to the caller's own workspace. Job records carry identifiers and a short
/// safe description of a failure, never document content.
/// </summary>
public static class BackgroundJobEndpoints
{
    public static async Task<IResult> List(
        string? status,
        Guid? caseId,
        IBackgroundJobStore store,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!WorkflowEndpoints.TryResolveScope(httpContext, out var scope, out _, out var error))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["tenantScope"] = [error] });
        }

        BackgroundJobStatus? wanted = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<BackgroundJobStatus>(status, ignoreCase: true, out var parsed))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["status"] = ["Use Pending, Running, Succeeded or DeadLettered."] });
            }

            wanted = parsed;
        }

        return Results.Ok(await Build(store, scope, wanted, caseId, cancellationToken));
    }

    /// <summary>The same view for one claim, for the staff who work it.</summary>
    public static async Task<IResult> ListForCase(
        Guid caseId,
        ICaseRepository caseRepository,
        IBackgroundJobStore store,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!WorkflowEndpoints.TryResolveScope(httpContext, out var scope, out _, out var error))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["tenantScope"] = [error] });
        }

        var claim = await caseRepository.GetAsync(caseId, cancellationToken);
        if (claim is null || claim.Scope != scope)
        {
            return Results.NotFound(new { message = "Claim was not found." });
        }

        return Results.Ok(await Build(store, scope, null, caseId, cancellationToken));
    }

    public static async Task<IResult> Retry(
        Guid jobId,
        IBackgroundJobStore store,
        IAuditRepository auditRepository,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!WorkflowEndpoints.TryResolveScope(httpContext, out var scope, out var userId, out var error))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["tenantScope"] = [error] });
        }

        var existing = await store.GetAsync(scope, jobId, cancellationToken);
        if (existing is null)
        {
            return Results.NotFound(new { message = "That job was not found." });
        }

        var requeued = await store.RequeueAsync(scope, jobId, cancellationToken);
        if (requeued is null)
        {
            return Results.Conflict(new { message = "Only a job that has given up can be sent back." });
        }

        await auditRepository.WriteAsync(
            AuditEvent.BackgroundJobRequeued(scope, userId, requeued.JobId, requeued.Type, requeued.CaseId, httpContext.TraceIdentifier),
            cancellationToken);
        return Results.Ok(ToResponse(requeued));
    }

    private static async Task<BackgroundWorkResponse> Build(IBackgroundJobStore store, InsuranceIntelligence.Domain.Common.TenantScope scope, BackgroundJobStatus? status, Guid? caseId, CancellationToken cancellationToken)
    {
        var counts = await store.CountAsync(scope, cancellationToken);
        int Count(BackgroundJobStatus key) => counts.TryGetValue(key, out var value) ? value : 0;
        var jobs = await store.ListAsync(scope, status, caseId, 100, cancellationToken);
        return new BackgroundWorkResponse(
            Count(BackgroundJobStatus.Pending),
            Count(BackgroundJobStatus.Running),
            Count(BackgroundJobStatus.Succeeded),
            Count(BackgroundJobStatus.DeadLettered),
            jobs.Select(ToResponse).ToArray());
    }

    private static BackgroundJobResponse ToResponse(BackgroundJob job) =>
        new(
            job.JobId,
            job.Queue,
            job.Type,
            job.Type switch
            {
                BackgroundJobTypes.DocumentProcess => "Reading an uploaded document",
                BackgroundJobTypes.ClaimReview => "Reviewing a claim",
                BackgroundJobTypes.ClaimsPackSend => "Emailing a claims pack",
                _ => job.Type,
            },
            job.Status.ToString(),
            job.Attempts,
            job.MaxAttempts,
            job.Status == BackgroundJobStatus.Pending && job.Attempts > 0 ? job.NotBefore : null,
            job.LastError,
            job.CreatedAt,
            job.CompletedAt,
            job.CaseId,
            job.CaseId is { } id ? ClaimReference.For(id) : null);
}
