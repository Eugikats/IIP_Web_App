using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Integrations;
using InsuranceIntelligence.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InsuranceIntelligence.Infrastructure.WhatsApp;

public sealed class EfWhatsAppChannelConfigurationRepository(InsuranceIntelligenceDbContext dbContext)
    : JsonRepositoryBase(dbContext), IWhatsAppChannelConfigurationRepository
{
    public async Task<WhatsAppChannelConfiguration?> GetAsync(TenantScope scope, CancellationToken cancellationToken = default)
    {
        var id = WhatsAppChannelConfiguration.StorageKey(scope);
        var record = await Scoped(DbContext.WhatsAppChannelConfigurations.AsNoTracking(), scope)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        return record is null ? null : Deserialize<WhatsAppChannelConfiguration>(record.PayloadJson);
    }

    public Task SaveAsync(WhatsAppChannelConfiguration configuration, CancellationToken cancellationToken = default) =>
        UpsertAsync(
            DbContext.WhatsAppChannelConfigurations,
            WhatsAppChannelConfiguration.StorageKey(configuration.Scope),
            configuration.Scope,
            Serialize(configuration),
            cancellationToken: cancellationToken);

    public async Task DeleteAsync(TenantScope scope, CancellationToken cancellationToken = default)
    {
        var id = WhatsAppChannelConfiguration.StorageKey(scope);
        var record = await Scoped(DbContext.WhatsAppChannelConfigurations, scope)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (record is not null)
        {
            DbContext.WhatsAppChannelConfigurations.Remove(record);
            await DbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
