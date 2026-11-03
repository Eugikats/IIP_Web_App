using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace InsuranceIntelligence.Infrastructure.Persistence;

/// <summary>One record per workspace holding its operating settings. A workspace that has never
/// saved any reads the defaults, which are the values that used to be constants in code.</summary>
public sealed class EfTenantSettingsRepository(InsuranceIntelligenceDbContext dbContext)
    : JsonRepositoryBase(dbContext), ITenantSettingsRepository
{
    public async Task<TenantSettings> GetAsync(TenantScope scope, CancellationToken cancellationToken = default)
    {
        var record = await Scoped(DbContext.TenantSettings.AsNoTracking(), scope).FirstOrDefaultAsync(cancellationToken);
        return record is null ? TenantSettings.Defaults(scope) : Deserialize<TenantSettings>(record.PayloadJson);
    }

    public Task SaveAsync(TenantSettings settings, CancellationToken cancellationToken = default) =>
        UpsertAsync(DbContext.TenantSettings, DeterministicId(settings.Scope), settings.Scope, Serialize(settings), cancellationToken: cancellationToken);

    private static Guid DeterministicId(TenantScope scope) =>
        new(System.Security.Cryptography.MD5.HashData(System.Text.Encoding.UTF8.GetBytes($"tenant-settings:{scope.TenantId}:{scope.WorkspaceId}")));
}
