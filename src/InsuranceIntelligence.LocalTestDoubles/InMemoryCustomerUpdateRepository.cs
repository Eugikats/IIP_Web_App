using System.Collections.Concurrent;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Workflow;

namespace InsuranceIntelligence.LocalTestDoubles;

public sealed class InMemoryCustomerUpdateRepository : ICustomerUpdateRepository
{
    private readonly ConcurrentDictionary<Guid, CustomerUpdateDraft> updates = new();

    public Task SaveAsync(CustomerUpdateDraft update, CancellationToken cancellationToken = default)
    {
        updates[update.UpdateId] = update;
        return Task.CompletedTask;
    }

    public Task<CustomerUpdateDraft?> GetAsync(Guid updateId, TenantScope scope, CancellationToken cancellationToken = default)
    {
        updates.TryGetValue(updateId, out var update);
        return Task.FromResult(update is not null && update.Scope.Matches(scope) ? update : null);
    }

    public Task<IReadOnlyCollection<CustomerUpdateDraft>> ListAsync(TenantScope scope, CancellationToken cancellationToken = default)
    {
        var matches = updates.Values
            .Where(update => update.Scope.Matches(scope))
            .OrderByDescending(update => update.CreatedAt)
            .ToArray();

        return Task.FromResult<IReadOnlyCollection<CustomerUpdateDraft>>(matches);
    }

    public Task<IReadOnlyCollection<CustomerUpdateDraft>> ListForCaseAsync(Guid caseId, TenantScope scope, CancellationToken cancellationToken = default)
    {
        var matches = updates.Values
            .Where(update => update.CaseId == caseId && update.Scope.Matches(scope))
            .OrderByDescending(update => update.CreatedAt)
            .ToArray();

        return Task.FromResult<IReadOnlyCollection<CustomerUpdateDraft>>(matches);
    }
}
