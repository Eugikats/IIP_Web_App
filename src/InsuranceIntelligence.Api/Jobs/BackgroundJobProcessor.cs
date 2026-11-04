using InsuranceIntelligence.Api.Local;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Jobs;

namespace InsuranceIntelligence.Api.Jobs;

/// <summary>How the background pipeline runs. Read from configuration once; tests replace it.</summary>
public sealed record BackgroundPipelineOptions
{
    /// <summary>"Background" (the default): an upload returns as soon as the file is stored and a
    /// worker does the rest. "Inline": the upload does the work before returning, which is what the
    /// fast deterministic tests use.</summary>
    public bool Inline { get; init; }

    public bool WorkerEnabled { get; init; } = true;
    public int Workers { get; init; } = 2;
    public int MaxAttempts { get; init; } = 4;

    /// <summary>Seconds to wait before attempt 2, 3, 4 and later.</summary>
    public IReadOnlyList<int> BackoffSeconds { get; init; } = [5, 30, 120];

    public int PollMilliseconds { get; init; } = 500;

    /// <summary>How long a claim review waits before it starts, so documents that arrive together are
    /// reviewed together.</summary>
    public int ReviewSettleSeconds { get; init; } = 2;
    public int LeaseSeconds { get; init; } = 180;

    public TimeSpan BackoffAfter(int attemptsMade) =>
        TimeSpan.FromSeconds(BackoffSeconds.Count == 0 ? 0 : BackoffSeconds[Math.Min(Math.Max(attemptsMade - 1, 0), BackoffSeconds.Count - 1)]);

    public static BackgroundPipelineOptions From(IConfiguration configuration)
    {
        var backoff = configuration["INSURANCEINTELLIGENCE_PIPELINE_BACKOFF_SECONDS"];
        return new BackgroundPipelineOptions
        {
            Inline = string.Equals(configuration["INSURANCEINTELLIGENCE_DOCUMENT_PIPELINE"], "Inline", StringComparison.OrdinalIgnoreCase),
            WorkerEnabled = !string.Equals(configuration["INSURANCEINTELLIGENCE_PIPELINE_WORKER"], "Off", StringComparison.OrdinalIgnoreCase),
            Workers = int.TryParse(configuration["INSURANCEINTELLIGENCE_PIPELINE_WORKERS"], out var workers) ? Math.Clamp(workers, 1, 16) : 2,
            MaxAttempts = int.TryParse(configuration["INSURANCEINTELLIGENCE_PIPELINE_MAX_ATTEMPTS"], out var attempts) ? Math.Clamp(attempts, 1, 10) : 4,
            BackoffSeconds = string.IsNullOrWhiteSpace(backoff)
                ? [5, 30, 120]
                : backoff.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(value => int.TryParse(value, out var seconds) ? Math.Clamp(seconds, 0, 3600) : 5).ToArray(),
            PollMilliseconds = int.TryParse(configuration["INSURANCEINTELLIGENCE_PIPELINE_POLL_MS"], out var poll) ? Math.Clamp(poll, 50, 10_000) : 500,
        };
    }
}

/// <summary>Thrown by a handler to say the step failed, with a sentence safe to show an administrator.
/// <see cref="Retry"/> false means trying again cannot help (a service is not configured).</summary>
public sealed class JobFailure(string safeMessage, bool retry = true) : Exception(safeMessage)
{
    public bool Retry { get; } = retry;
}

/// <summary>Does one kind of job. Runs inside a fresh dependency scope in the job's own workspace.</summary>
public interface IBackgroundJobHandler
{
    string Queue { get; }

    string Type { get; }

    Task HandleAsync(BackgroundJob job, IServiceProvider services, CancellationToken cancellationToken);
}

public interface IBackgroundJobProcessor
{
    /// <summary>Takes the next due job from the queues and runs it to an outcome: done, waiting to be
    /// retried after a back-off, or dead-lettered. Returns whether there was a job.</summary>
    Task<bool> ProcessNextAsync(IReadOnlyCollection<string> queues, string owner, CancellationToken cancellationToken, TenantScope? onlyScope = null);
}

/// <summary>
/// Runs durable jobs (build-loop item 17). Each job gets its own scope and a time limit; success is
/// recorded; a failure waits a back-off and tries again until the attempts run out, then the job is
/// dead-lettered where an administrator can see why and send it back. A worker that dies mid-job
/// simply lets the hold run out and the job is taken again. Failure text is reduced to a safe
/// sentence: a provider's own message, a stack trace or any content never reaches the job record.
/// </summary>
public sealed class BackgroundJobProcessor(
    IServiceScopeFactory scopeFactory,
    IEnumerable<IBackgroundJobHandler> handlers,
    BackgroundPipelineOptions options,
    ILogger<BackgroundJobProcessor> logger) : IBackgroundJobProcessor
{
    public static readonly TimeSpan JobTimeLimit = TimeSpan.FromMinutes(4);
    private readonly IReadOnlyList<IBackgroundJobHandler> handlers = handlers.ToArray();

    public async Task<bool> ProcessNextAsync(IReadOnlyCollection<string> queues, string owner, CancellationToken cancellationToken, TenantScope? onlyScope = null)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IBackgroundJobStore>();
        var job = await store.LeaseNextAsync(queues, owner, TimeSpan.FromSeconds(options.LeaseSeconds), DateTimeOffset.UtcNow, onlyScope, cancellationToken);
        if (job is null)
        {
            return false;
        }

        var handler = handlers.FirstOrDefault(candidate => candidate.Queue == job.Queue && candidate.Type == job.Type);
        string? error = null;
        var retry = true;
        if (handler is null)
        {
            error = "No worker knows how to do this kind of work.";
            retry = false;
        }
        else
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(JobTimeLimit);
            try
            {
                await handler.HandleAsync(job, scope.ServiceProvider, timeout.Token);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Shutting down: leave the hold to run out so another instance takes the job.
                return true;
            }
            catch (Exception exception)
            {
                (error, retry) = Describe(exception);
                logger.LogWarning(
                    "Background job failed. Queue={Queue} Type={Type} JobId={JobId} Attempt={Attempt} Reason={Reason}",
                    job.Queue, job.Type, job.JobId, job.Attempts, exception.GetType().Name);
            }
        }

        var now = DateTimeOffset.UtcNow;
        BackgroundJob outcome;
        if (error is null)
        {
            outcome = job with { Status = BackgroundJobStatus.Succeeded, LastError = null, CompletedAt = now, LeaseOwner = null, LeasedUntil = null };
        }
        else if (!retry || job.Attempts >= job.MaxAttempts)
        {
            outcome = job with { Status = BackgroundJobStatus.DeadLettered, LastError = error, CompletedAt = now, LeaseOwner = null, LeasedUntil = null };
        }
        else
        {
            outcome = job with { Status = BackgroundJobStatus.Pending, LastError = error, NotBefore = now + options.BackoffAfter(job.Attempts), LeaseOwner = null, LeasedUntil = null };
        }

        if (!await store.SaveHeldAsync(outcome, CancellationToken.None))
        {
            logger.LogInformation("A background job's hold had run out before it finished, so another worker has it. JobId={JobId}", job.JobId);
        }

        return true;
    }

    /// <summary>A sentence for an administrator and whether trying again could help.</summary>
    internal static (string Message, bool Retry) Describe(Exception exception) => exception switch
    {
        JobFailure failure => (failure.Message, failure.Retry),
        ProviderNotConfiguredException => ("A service this step needs is not configured for this workspace.", false),
        OperationCanceledException or TimeoutException => ("The step took too long.", true),
        HttpRequestException => ("A service this step uses could not be reached.", true),
        _ => ($"The step failed ({exception.GetType().Name}).", true),
    };
}
