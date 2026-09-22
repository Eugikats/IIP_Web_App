using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Products;
using InsuranceIntelligence.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InsuranceIntelligence.Infrastructure.Products;

/// <summary>
/// Append-only, exactly like EfAgentBehaviourProfileRepository: every SaveAsync call inserts a new
/// row keyed by a fresh VersionId, never overwrites a previous one. That is what lets a claim keep
/// the exact product version in force when it was filed even after the product moves on.
/// </summary>
public sealed class EfProductRepository(InsuranceIntelligenceDbContext dbContext)
    : JsonRepositoryBase(dbContext), IProductRepository
{
    public async Task<Product?> GetCurrentAsync(TenantScope scope, Guid productId, CancellationToken cancellationToken = default)
    {
        var versions = await ListVersionsAsync(scope, productId, cancellationToken);
        return versions.FirstOrDefault();
    }

    public async Task<Product?> GetVersionAsync(TenantScope scope, Guid productId, int version, CancellationToken cancellationToken = default)
    {
        var versions = await ListVersionsAsync(scope, productId, cancellationToken);
        return versions.FirstOrDefault(product => product.Version == version);
    }

    public async Task<IReadOnlyCollection<Product>> ListVersionsAsync(TenantScope scope, Guid productId, CancellationToken cancellationToken = default)
    {
        var records = await Scoped(DbContext.ProductVersions.AsNoTracking(), scope)
            .Where(record => record.ProductId == productId)
            .ToArrayAsync(cancellationToken);
        return records
            .Select(record => Deserialize<Product>(record.PayloadJson))
            .OrderByDescending(product => product.Version)
            .ToArray();
    }

    public async Task<IReadOnlyCollection<Product>> ListCurrentForAllProductsAsync(TenantScope scope, CancellationToken cancellationToken = default)
    {
        var records = await Scoped(DbContext.ProductVersions.AsNoTracking(), scope).ToArrayAsync(cancellationToken);
        return records
            .Select(record => Deserialize<Product>(record.PayloadJson))
            .GroupBy(product => product.ProductId)
            .Select(group => group.OrderByDescending(product => product.Version).First())
            .ToArray();
    }

    public Task SaveAsync(Product version, CancellationToken cancellationToken = default) =>
        UpsertAsync(
            DbContext.ProductVersions,
            version.VersionId,
            version.Scope,
            Serialize(version),
            record =>
            {
                record.ProductId = version.ProductId;
                record.Version = version.Version;
            },
            cancellationToken);
}
