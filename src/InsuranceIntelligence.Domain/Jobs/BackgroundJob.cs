using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Domain.Jobs;

public enum BackgroundJobStatus
{
    /// <summary>Waiting for its turn, or for the time it was told to wait until after a failure.</summary>
    Pending,

    /// <summary>Held by a worker. If the worker dies the hold runs out and another takes it over.</summary>
    Running,

    Succeeded,

    /// <summary>Failed every allowed attempt. Kept, visible and retryable by a person; never retried
    /// silently again.</summary>
    DeadLettered,
}

/// <summary>
/// One piece of work that must not be lost (build-loop item 17): process an uploaded document,
/// review a claim. It lives in the database, in the workspace it belongs to, so it survives a
/// restart and is never seen by another workspace. The payload carries identifiers only, never a
/// document's content or a customer's words.
/// </summary>
public sealed record BackgroundJob
{
    public required Guid JobId { get; init; }
    public required TenantScope Scope { get; init; }

    /// <summary>Which line of work: "document-processing" or "claim-review".</summary>
    public required string Queue { get; init; }

    /// <summary>What to do, for example "document.process" or "claim.review".</summary>
    public required string Type { get; init; }

    /// <summary>What the work is about (a document id, a claim id). At most one unfinished job exists
    /// for the same workspace, queue, type and key, so asking twice does the work once.</summary>
    public required string Key { get; init; }

    public string PayloadJson { get; init; } = "{}";
    public required BackgroundJobStatus Status { get; init; }
    public int Attempts { get; init; }
    public int MaxAttempts { get; init; } = 4;
    public required DateTimeOffset NotBefore { get; init; }
    public string? LeaseOwner { get; init; }
    public DateTimeOffset? LeasedUntil { get; init; }

    /// <summary>A short, safe description of the last failure (an error class and a plain sentence),
    /// never a stack trace, a provider message or content.</summary>
    public string? LastError { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }
    public required DateTimeOffset UpdatedAt { get; init; }
    public DateTimeOffset? CompletedAt { get; init; }
    public required string CorrelationId { get; init; }
    public Guid? CaseId { get; init; }
    public Guid? DocumentId { get; init; }

    /// <summary>Changes every time the job is taken or saved, so two workers can never both hold it.</summary>
    public Guid LeaseToken { get; init; }

    public bool IsFinished => Status is BackgroundJobStatus.Succeeded or BackgroundJobStatus.DeadLettered;
}

public static class BackgroundQueues
{
    public const string DocumentProcessing = "document-processing";
    public const string ClaimReview = "claim-review";
}

public static class BackgroundJobTypes
{
    public const string DocumentProcess = "document.process";
    public const string ClaimReview = "claim.review";
    public const string ClaimsPackSend = "claims.pack.send";
}

public interface IBackgroundJobStore
{
    /// <summary>Adds the job, unless an unfinished job with the same workspace, queue, type and key
    /// already exists, in which case that one is returned and nothing is added. With
    /// <paramref name="coalesceWithRunning"/> false, only a job still waiting counts: a new job is
    /// added behind one that is already running, so work that arrives while it runs is not missed.</summary>
    Task<BackgroundJob> EnqueueAsync(BackgroundJob job, bool coalesceWithRunning = true, CancellationToken cancellationToken = default);

    /// <summary>Takes the next job that is due, in any workspace, for a worker: pending jobs whose time
    /// has come and running jobs whose hold has run out. Null when nothing is due or another worker
    /// got there first.</summary>
    /// <param name="onlyScope">Limits the search to one workspace. Workers leave it null; tests use it
    /// so they only ever take their own jobs.</param>
    Task<BackgroundJob?> LeaseNextAsync(IReadOnlyCollection<string> queues, string owner, TimeSpan lease, DateTimeOffset now, TenantScope? onlyScope = null, CancellationToken cancellationToken = default);

    /// <summary>Records the outcome. Returns false when the job is no longer held by the caller (its
    /// hold ran out and another worker took it), in which case nothing is written.</summary>
    Task<bool> SaveHeldAsync(BackgroundJob job, CancellationToken cancellationToken = default);

    Task<BackgroundJob?> GetAsync(TenantScope scope, Guid jobId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BackgroundJob>> ListAsync(TenantScope scope, BackgroundJobStatus? status, Guid? caseId, int take, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<BackgroundJobStatus, int>> CountAsync(TenantScope scope, CancellationToken cancellationToken = default);

    /// <summary>A person sends a dead-lettered job back to the queue with fresh attempts.</summary>
    Task<BackgroundJob?> RequeueAsync(TenantScope scope, Guid jobId, CancellationToken cancellationToken = default);
}
