using System.Collections.Concurrent;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Fraud;

namespace InsuranceIntelligence.LocalTestDoubles;

public sealed class InMemoryFraudSignalDispositionRepository : IFraudSignalDispositionRepository
{
    private readonly ConcurrentDictionary<Guid, FraudSignalDisposition> dispositions = new();

    public Task SaveAsync(FraudSignalDisposition disposition, CancellationToken cancellationToken = default)
    {
        dispositions[disposition.SignalId] = disposition;
        return Task.CompletedTask;
    }

    public Task<FraudSignalDisposition?> GetAsync(Guid signalId, TenantScope scope, CancellationToken cancellationToken = default)
    {
        var found = dispositions.TryGetValue(signalId, out var disposition) && disposition.Scope.Matches(scope) ? disposition : null;
        return Task.FromResult(found);
    }

    public Task<IReadOnlyCollection<FraudSignalDisposition>> ListAsync(TenantScope scope, CancellationToken cancellationToken = default)
    {
        var matches = dispositions.Values.Where(d => d.Scope.Matches(scope)).ToArray();
        return Task.FromResult<IReadOnlyCollection<FraudSignalDisposition>>(matches);
    }
}
