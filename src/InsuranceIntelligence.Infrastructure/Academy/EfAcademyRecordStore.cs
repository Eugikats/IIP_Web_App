using System.Security.Cryptography;
using System.Text;
using InsuranceIntelligence.Domain.Academy;
using InsuranceIntelligence.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace InsuranceIntelligence.Infrastructure.Academy;

/// <summary>
/// The Academy's records, in one table of its own. Nothing here is scoped to an insurer's workspace: learners are people,
/// not organisations. Reads treat a table that does not exist yet as empty, so a release that ships ahead of its
/// migration shows empty learner data instead of failing; writes do not, because a lost write must be seen.
/// </summary>
public sealed class EfAcademyRecordStore(InsuranceIntelligenceDbContext dbContext) : IAcademyRecordStore
{
    private static Guid IdOf(string kind, string owner, string key) =>
        new(MD5.HashData(Encoding.UTF8.GetBytes($"academy:{kind}:{owner}:{key}")));

    private static AcademyRecord ToRecord(AcademyRecordEntity entity) => new(entity.Kind, entity.Owner, entity.Key, entity.PayloadJson, entity.UpdatedAt);

    public async Task<AcademyRecord?> GetAsync(string kind, string owner, string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var id = IdOf(kind, owner, key);
            var entity = await dbContext.AcademyRecords.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
            return entity is null ? null : ToRecord(entity);
        }
        catch (SqlException failure) when (failure.Number == 208)
        {
            return null;
        }
    }

    public async Task SaveAsync(string kind, string owner, string key, string json, CancellationToken cancellationToken = default)
    {
        var id = IdOf(kind, owner, key);
        var entity = await dbContext.AcademyRecords.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (entity is null)
        {
            dbContext.AcademyRecords.Add(new AcademyRecordEntity { Id = id, Kind = kind, Owner = owner, Key = key, PayloadJson = json, UpdatedAt = DateTimeOffset.UtcNow });
        }
        else
        {
            entity.PayloadJson = json;
            entity.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AcademyRecord>> ListByOwnerAsync(string kind, string owner, CancellationToken cancellationToken = default)
    {
        try
        {
            var rows = await dbContext.AcademyRecords.AsNoTracking().Where(item => item.Kind == kind && item.Owner == owner).ToListAsync(cancellationToken);
            return rows.Select(ToRecord).ToArray();
        }
        catch (SqlException failure) when (failure.Number == 208)
        {
            return [];
        }
    }

    public async Task<IReadOnlyList<AcademyRecord>> ListByKindAsync(string kind, int take, CancellationToken cancellationToken = default)
    {
        try
        {
            var rows = await dbContext.AcademyRecords.AsNoTracking()
                .Where(item => item.Kind == kind)
                .OrderByDescending(item => item.UpdatedAt)
                .Take(Math.Clamp(take, 1, 2000))
                .ToListAsync(cancellationToken);
            return rows.Select(ToRecord).ToArray();
        }
        catch (SqlException failure) when (failure.Number == 208)
        {
            return [];
        }
    }

    public async Task DeleteAsync(string kind, string owner, string key, CancellationToken cancellationToken = default)
    {
        var id = IdOf(kind, owner, key);
        var entity = await dbContext.AcademyRecords.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (entity is not null)
        {
            dbContext.AcademyRecords.Remove(entity);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<int> CountAsync(string kind, CancellationToken cancellationToken = default)
    {
        try
        {
            return await dbContext.AcademyRecords.AsNoTracking().CountAsync(item => item.Kind == kind, cancellationToken);
        }
        catch (SqlException failure) when (failure.Number == 208)
        {
            return 0;
        }
    }
}
