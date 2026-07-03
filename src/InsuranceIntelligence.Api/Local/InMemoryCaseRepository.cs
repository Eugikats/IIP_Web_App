using System.Collections.Concurrent;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Api.Local;

public sealed class InMemoryCaseRepository : ICaseRepository
{
    private readonly ConcurrentDictionary<Guid, ClaimCase> cases = new();

    public Task SaveAsync(ClaimCase claimCase, CancellationToken cancellationToken = default)
    {
        cases[claimCase.CaseId] = claimCase;
        return Task.CompletedTask;
    }

    public Task<ClaimCase?> GetAsync(Guid caseId, CancellationToken cancellationToken = default)
    {
        cases.TryGetValue(caseId, out var claimCase);
        return Task.FromResult(claimCase);
    }

    public Task<IReadOnlyCollection<ClaimCase>> ListAsync(TenantScope scope, CancellationToken cancellationToken = default)
    {
        var matches = cases.Values
            .Where(claimCase => claimCase.Scope.Matches(scope))
            .OrderByDescending(claimCase => claimCase.CreatedAt)
            .ToArray();

        return Task.FromResult<IReadOnlyCollection<ClaimCase>>(matches);
    }
}
