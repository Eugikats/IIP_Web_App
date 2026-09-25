using System.Collections.Concurrent;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Translation;

namespace InsuranceIntelligence.LocalTestDoubles;

public sealed class InMemoryCustomerLanguagePreferenceRepository : ICustomerLanguagePreferenceRepository
{
    private readonly ConcurrentBag<CustomerLanguagePreference> preferences = new();

    public Task<CustomerLanguagePreference?> GetCurrentAsync(TenantScope scope, string contactWaId, CancellationToken cancellationToken = default)
    {
        var current = preferences
            .Where(preference => preference.Scope.Matches(scope) && preference.ContactWaId == contactWaId)
            .OrderByDescending(preference => preference.SetAt)
            .FirstOrDefault();
        return Task.FromResult(current);
    }

    public Task SaveAsync(CustomerLanguagePreference preference, CancellationToken cancellationToken = default)
    {
        preferences.Add(preference);
        return Task.CompletedTask;
    }
}
