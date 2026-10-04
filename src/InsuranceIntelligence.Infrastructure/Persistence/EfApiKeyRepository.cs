using InsuranceIntelligence.Domain.Organisations;
using Microsoft.EntityFrameworkCore;

namespace InsuranceIntelligence.Infrastructure.Persistence;

public sealed class ApiKeyEntity
{
    public Guid Id { get; set; }
    public string OrganisationId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Prefix { get; set; } = string.Empty;
    public string KeyHash { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string CreatedByUserId { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public DateTimeOffset? LastUsedAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
}

public sealed class EfApiKeyRepository(InsuranceIntelligenceDbContext db) : IApiKeyRepository
{
    public async Task AddAsync(ApiKeyRecord key, CancellationToken cancellationToken = default)
    {
        db.ApiKeys.Add(new ApiKeyEntity
        {
            Id = key.KeyId, OrganisationId = key.OrganisationId, Name = key.Name, Prefix = key.Prefix, KeyHash = key.KeyHash,
            UserId = key.UserId, CreatedByUserId = key.CreatedByUserId, CreatedAt = key.CreatedAt, ExpiresAt = key.ExpiresAt,
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(ApiKeyRecord key, CancellationToken cancellationToken = default)
    {
        var entity = await db.ApiKeys.FirstAsync(k => k.Id == key.KeyId, cancellationToken);
        entity.LastUsedAt = key.LastUsedAt;
        entity.RevokedAt = key.RevokedAt;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<ApiKeyRecord?> GetByHashAsync(string keyHash, CancellationToken cancellationToken = default)
    {
        var entity = await db.ApiKeys.AsNoTracking().FirstOrDefaultAsync(k => k.KeyHash == keyHash, cancellationToken);
        return entity is null ? null : ToDomain(entity);
    }

    public async Task<ApiKeyRecord?> GetAsync(string organisationId, Guid keyId, CancellationToken cancellationToken = default)
    {
        var entity = await db.ApiKeys.AsNoTracking().FirstOrDefaultAsync(k => k.Id == keyId && k.OrganisationId == organisationId, cancellationToken);
        return entity is null ? null : ToDomain(entity);
    }

    public async Task<IReadOnlyList<ApiKeyRecord>> ListAsync(string organisationId, CancellationToken cancellationToken = default)
    {
        var entities = await db.ApiKeys.AsNoTracking().Where(k => k.OrganisationId == organisationId).ToListAsync(cancellationToken);
        return entities.Select(ToDomain).ToArray();
    }

    private static ApiKeyRecord ToDomain(ApiKeyEntity e) => new(
        e.Id, e.OrganisationId, e.Name, e.Prefix, e.KeyHash, e.UserId, e.CreatedByUserId, e.CreatedAt, e.ExpiresAt, e.LastUsedAt, e.RevokedAt);
}
