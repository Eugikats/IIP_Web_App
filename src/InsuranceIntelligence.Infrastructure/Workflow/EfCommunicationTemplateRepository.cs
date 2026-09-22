using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Workflow;
using InsuranceIntelligence.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InsuranceIntelligence.Infrastructure.Workflow;

/// <summary>Append-only, exactly like EfAgentBehaviourProfileRepository and EfProductRepository:
/// every SaveAsync call inserts a new row keyed by a fresh VersionId, never overwrites a previous
/// one, so a template's edit history stays visible.</summary>
public sealed class EfCommunicationTemplateRepository(InsuranceIntelligenceDbContext dbContext)
    : JsonRepositoryBase(dbContext), ICommunicationTemplateRepository
{
    public async Task<CommunicationTemplate?> GetCurrentAsync(TenantScope scope, string templateKey, CancellationToken cancellationToken = default)
    {
        var versions = await ListVersionsAsync(scope, templateKey, cancellationToken);
        return versions.FirstOrDefault();
    }

    public async Task<IReadOnlyCollection<CommunicationTemplate>> ListVersionsAsync(TenantScope scope, string templateKey, CancellationToken cancellationToken = default)
    {
        var records = await Scoped(DbContext.CommunicationTemplates.AsNoTracking(), scope)
            .Where(record => record.TemplateKey == templateKey)
            .ToArrayAsync(cancellationToken);
        return records
            .Select(record => Deserialize<CommunicationTemplate>(record.PayloadJson))
            .OrderByDescending(template => template.Version)
            .ToArray();
    }

    public async Task<IReadOnlyCollection<CommunicationTemplate>> ListCurrentForAllTemplatesAsync(TenantScope scope, CancellationToken cancellationToken = default)
    {
        var records = await Scoped(DbContext.CommunicationTemplates.AsNoTracking(), scope).ToArrayAsync(cancellationToken);
        return records
            .Select(record => Deserialize<CommunicationTemplate>(record.PayloadJson))
            .GroupBy(template => template.TemplateKey, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(template => template.Version).First())
            .ToArray();
    }

    public Task SaveAsync(CommunicationTemplate version, CancellationToken cancellationToken = default) =>
        UpsertAsync(
            DbContext.CommunicationTemplates,
            version.VersionId,
            version.Scope,
            Serialize(version),
            record =>
            {
                record.TemplateKey = version.TemplateKey;
                record.Version = version.Version;
            },
            cancellationToken);
}
