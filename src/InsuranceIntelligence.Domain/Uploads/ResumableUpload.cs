using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Domain.Uploads;

public enum UploadSessionState
{
    Open,
    Completed,
    Cancelled,
}

/// <summary>
/// A file being sent in pieces (build-loop item 23), so a dropped connection costs the piece in flight and not
/// the whole file. It belongs to one claim and one claimant, is limited to the file the claimant declared, and
/// is thrown away when it finishes or expires. The pieces themselves are held separately and deleted with it.
/// </summary>
public sealed record UploadSession
{
    public required Guid UploadId { get; init; }
    public required TenantScope Scope { get; init; }
    public required Guid CaseId { get; init; }

    /// <summary>Who started it (the claimant's verified contact); only they can add to it or finish it.</summary>
    public required string ActorId { get; init; }
    public required string FileName { get; init; }
    public required string ContentType { get; init; }
    public required long Size { get; init; }
    public required int ChunkSize { get; init; }
    public required int TotalChunks { get; init; }
    public string? DocumentTypeKey { get; init; }

    /// <summary>The browser's own identity for the file (name, size, last modified, slot) so choosing the same
    /// file again after a reload finds this session instead of starting over.</summary>
    public required string Fingerprint { get; init; }
    public UploadSessionState State { get; init; } = UploadSessionState.Open;
    public required DateTimeOffset CreatedAt { get; init; }
    public required DateTimeOffset ExpiresAt { get; init; }
}

public interface IUploadSessionRepository
{
    Task SaveAsync(UploadSession session, CancellationToken cancellationToken = default);

    Task<UploadSession?> GetAsync(TenantScope scope, Guid uploadId, CancellationToken cancellationToken = default);

    /// <summary>An open, unexpired session of the same claimant for the same file on the same claim, if there is one.</summary>
    Task<UploadSession?> FindOpenAsync(TenantScope scope, Guid caseId, string actorId, string fingerprint, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UploadSession>> ListForCaseAsync(TenantScope scope, Guid caseId, CancellationToken cancellationToken = default);
}

public interface IUploadPartStore
{
    Task SavePartAsync(TenantScope scope, Guid uploadId, int index, byte[] data, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<int>> ListIndexesAsync(TenantScope scope, Guid uploadId, CancellationToken cancellationToken = default);

    Task<byte[]?> ReadPartAsync(TenantScope scope, Guid uploadId, int index, CancellationToken cancellationToken = default);

    Task DeleteAllAsync(TenantScope scope, Guid uploadId, CancellationToken cancellationToken = default);
}

public static class ResumableUploadPolicy
{
    /// <summary>A quarter of a megabyte: small enough to finish on a poor connection in a few seconds, large
    /// enough that a 5 MB photo is about twenty requests.</summary>
    public const int ChunkSize = 256 * 1024;

    /// <summary>A file at or under this goes in one request; the extra round trips of a chunked upload are not worth it.</summary>
    public const long SingleRequestLimit = 256 * 1024;

    public static readonly TimeSpan SessionLifetime = TimeSpan.FromHours(24);

    public static int TotalChunks(long size) => size <= 0 ? 0 : (int)((size + ChunkSize - 1) / ChunkSize);

    /// <summary>How many bytes piece <paramref name="index"/> of a file of this size must have, or -1 when there is no such piece.</summary>
    public static int ExpectedLength(long size, int index)
    {
        var total = TotalChunks(size);
        if (index < 0 || index >= total)
        {
            return -1;
        }

        return index < total - 1 ? ChunkSize : (int)(size - (long)ChunkSize * (total - 1));
    }

    public static bool IsExpired(UploadSession session, DateTimeOffset now) => session.ExpiresAt <= now;
}
