using System.Collections.Concurrent;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Integrations;

namespace InsuranceIntelligence.LocalTestDoubles;

public sealed class InMemoryWhatsAppChannelConfigurationRepository : IWhatsAppChannelConfigurationRepository
{
    private readonly ConcurrentDictionary<Guid, WhatsAppChannelConfiguration> configurations = new();

    public Task<WhatsAppChannelConfiguration?> GetAsync(TenantScope scope, CancellationToken cancellationToken = default)
    {
        configurations.TryGetValue(WhatsAppChannelConfiguration.StorageKey(scope), out var configuration);
        return Task.FromResult(configuration);
    }

    public Task SaveAsync(WhatsAppChannelConfiguration configuration, CancellationToken cancellationToken = default)
    {
        configurations[WhatsAppChannelConfiguration.StorageKey(configuration.Scope)] = configuration;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<WhatsAppChannelConfiguration>> ListActiveAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<WhatsAppChannelConfiguration>>(configurations.Values.Where(item => item.IsActive).ToArray());

    public Task DeleteAsync(TenantScope scope, CancellationToken cancellationToken = default)
    {
        configurations.TryRemove(WhatsAppChannelConfiguration.StorageKey(scope), out _);
        return Task.CompletedTask;
    }
}
