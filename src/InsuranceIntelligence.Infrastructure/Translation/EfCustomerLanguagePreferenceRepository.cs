using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Translation;
using InsuranceIntelligence.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InsuranceIntelligence.Infrastructure.Translation;

/// <summary>Append-only, the same pattern as EfConsentRepository: every SaveAsync inserts a new
/// row keyed by a fresh PreferenceId, so a language change is a new fact recorded, not an old one
/// overwritten. ContactWaId is the indexed column.</summary>
public sealed class EfCustomerLanguagePreferenceRepository(InsuranceIntelligenceDbContext dbContext)
    : JsonRepositoryBase(dbContext), ICustomerLanguagePreferenceRepository
{
    public async Task<CustomerLanguagePreference?> GetCurrentAsync(TenantScope scope, string contactWaId, CancellationToken cancellationToken = default)
    {
        var records = await Scoped(DbContext.CustomerLanguagePreferences.AsNoTracking(), scope)
            .Where(record => record.ContactWaId == contactWaId)
            .ToArrayAsync(cancellationToken);

        return records
            .Select(record => Deserialize<CustomerLanguagePreference>(record.PayloadJson))
            .OrderByDescending(preference => preference.SetAt)
            .FirstOrDefault();
    }

    public Task SaveAsync(CustomerLanguagePreference preference, CancellationToken cancellationToken = default) =>
        UpsertAsync(
            DbContext.CustomerLanguagePreferences,
            preference.PreferenceId,
            preference.Scope,
            Serialize(preference),
            record => record.ContactWaId = preference.ContactWaId,
            cancellationToken);
}
