using System.Security.Cryptography;
using System.Text;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Uploads;
using Microsoft.EntityFrameworkCore;

namespace InsuranceIntelligence.Infrastructure.Persistence;

/// <summary>Upload sessions (build-loop item 23). Every read is limited to the caller's workspace.</summary>
public sealed class EfUploadSessionRepository(InsuranceIntelligenceDbContext dbContext)
    : JsonRepositoryBase(dbContext), IUploadSessionRepository
{
    public Task SaveAsync(UploadSession session, CancellationToken cancellationToken = default) =>
        UpsertAsync(
            DbContext.UploadSessions,
            session.UploadId,
            session.Scope,
            Serialize(session),
            record =>
            {
                record.CaseId = session.CaseId;
                record.Fingerprint = Key(session.Fingerprint);
            },
            cancellationToken);

    public async Task<UploadSession?> GetAsync(TenantScope scope, Guid uploadId, CancellationToken cancellationToken = default)
    {
        var record = await Scoped(DbContext.UploadSessions.AsNoTracking(), scope).FirstOrDefaultAsync(item => item.Id == uploadId, cancellationToken);
        return record is null ? null : Deserialize<UploadSession>(record.PayloadJson);
    }

    public async Task<UploadSession?> FindOpenAsync(TenantScope scope, Guid caseId, string actorId, string fingerprint, CancellationToken cancellationToken = default)
    {
        var key = Key(fingerprint);
        var records = await Scoped(DbContext.UploadSessions.AsNoTracking(), scope)
            .Where(item => item.CaseId == caseId && item.Fingerprint == key)
            .ToListAsync(cancellationToken);
        return records
            .Select(record => Deserialize<UploadSession>(record.PayloadJson))
            .Where(session => session.State == UploadSessionState.Open && session.ActorId == actorId && session.ExpiresAt > DateTimeOffset.UtcNow)
            .OrderByDescending(session => session.CreatedAt)
            .FirstOrDefault();
    }

    public async Task<IReadOnlyList<UploadSession>> ListForCaseAsync(TenantScope scope, Guid caseId, CancellationToken cancellationToken = default)
    {
        var records = await Scoped(DbContext.UploadSessions.AsNoTracking(), scope).Where(item => item.CaseId == caseId).ToListAsync(cancellationToken);
        return records.Select(record => Deserialize<UploadSession>(record.PayloadJson)).ToArray();
    }

    private static string Key(string fingerprint) => fingerprint.Length > 200 ? fingerprint[..200] : fingerprint;
}

/// <summary>The pieces of files being uploaded, held until the file is whole and then deleted.</summary>
public sealed class EfUploadPartStore(InsuranceIntelligenceDbContext dbContext)
    : JsonRepositoryBase(dbContext), IUploadPartStore
{
    private static Guid PartId(Guid uploadId, int index) =>
        new(MD5.HashData(Encoding.UTF8.GetBytes($"{uploadId:N}:{index}")));

    public Task SavePartAsync(TenantScope scope, Guid uploadId, int index, byte[] data, CancellationToken cancellationToken = default) =>
        UpsertAsync(
            DbContext.UploadParts,
            PartId(uploadId, index),
            scope,
            string.Empty,
            record =>
            {
                record.UploadId = uploadId;
                record.PartIndex = index;
                record.Data = data;
            },
            cancellationToken);

    public async Task<IReadOnlyList<int>> ListIndexesAsync(TenantScope scope, Guid uploadId, CancellationToken cancellationToken = default) =>
        await Scoped(DbContext.UploadParts.AsNoTracking(), scope)
            .Where(item => item.UploadId == uploadId)
            .Select(item => item.PartIndex)
            .OrderBy(index => index)
            .ToListAsync(cancellationToken);

    public async Task<byte[]?> ReadPartAsync(TenantScope scope, Guid uploadId, int index, CancellationToken cancellationToken = default) =>
        await Scoped(DbContext.UploadParts.AsNoTracking(), scope)
            .Where(item => item.UploadId == uploadId && item.PartIndex == index)
            .Select(item => item.Data)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task DeleteAllAsync(TenantScope scope, Guid uploadId, CancellationToken cancellationToken = default)
    {
        var parts = await Scoped(DbContext.UploadParts, scope).Where(item => item.UploadId == uploadId).ToListAsync(cancellationToken);
        if (parts.Count == 0)
        {
            return;
        }

        DbContext.UploadParts.RemoveRange(parts);
        await DbContext.SaveChangesAsync(cancellationToken);
    }
}
