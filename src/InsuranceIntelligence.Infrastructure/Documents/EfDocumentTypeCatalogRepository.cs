using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Documents;
using InsuranceIntelligence.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InsuranceIntelligence.Infrastructure.Documents;

/// <summary>Append-only, same pattern as EfCommunicationTemplateRepository: every SaveAsync inserts
/// a new version row keyed by a fresh VersionId, so the full history for a key is reconstructable.</summary>
public sealed class EfDocumentTypeCatalogRepository(InsuranceIntelligenceDbContext dbContext)
    : JsonRepositoryBase(dbContext), IDocumentTypeCatalogRepository
{
    public async Task<DocumentTypeDefinition?> GetCurrentAsync(TenantScope scope, string key, CancellationToken cancellationToken = default)
    {
        var versions = await ListVersionsAsync(scope, key, cancellationToken);
        return versions.FirstOrDefault();
    }

    public async Task<IReadOnlyCollection<DocumentTypeDefinition>> ListCurrentAsync(TenantScope scope, CancellationToken cancellationToken = default)
    {
        var records = await Scoped(DbContext.DocumentTypeDefinitions.AsNoTracking(), scope).ToArrayAsync(cancellationToken);
        return records
            .Select(record => Deserialize<DocumentTypeDefinition>(record.PayloadJson))
            .GroupBy(definition => definition.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(definition => definition.Version).First())
            .ToArray();
    }

    public async Task<IReadOnlyCollection<DocumentTypeDefinition>> ListVersionsAsync(TenantScope scope, string key, CancellationToken cancellationToken = default)
    {
        var records = await Scoped(DbContext.DocumentTypeDefinitions.AsNoTracking(), scope)
            .Where(record => record.Key == key)
            .ToArrayAsync(cancellationToken);
        return records
            .Select(record => Deserialize<DocumentTypeDefinition>(record.PayloadJson))
            .OrderByDescending(definition => definition.Version)
            .ToArray();
    }

    public Task SaveAsync(DocumentTypeDefinition version, CancellationToken cancellationToken = default) =>
        UpsertAsync(
            DbContext.DocumentTypeDefinitions,
            version.VersionId,
            version.Scope,
            Serialize(version),
            record =>
            {
                record.Key = version.Key;
                record.Version = version.Version;
            },
            cancellationToken);
}
