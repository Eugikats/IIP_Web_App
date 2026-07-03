using System.Text.Json;
using InsuranceIntelligence.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace InsuranceIntelligence.Infrastructure.Persistence;

public abstract class JsonRepositoryBase(InsuranceIntelligenceDbContext dbContext)
{
    protected InsuranceIntelligenceDbContext DbContext { get; } = dbContext;

    protected static string Serialize<T>(T value) => JsonSerializer.Serialize(value, JsonSerializerSettings.Options);

    protected static T Deserialize<T>(string json) =>
        JsonSerializer.Deserialize<T>(json, JsonSerializerSettings.Options)
        ?? throw new InvalidOperationException($"Could not deserialize {typeof(T).Name}.");

    protected static void ApplyScope(PersistedScopedJson record, TenantScope scope)
    {
        record.TenantId = scope.TenantId;
        record.WorkspaceId = scope.WorkspaceId;
        record.UpdatedAt = DateTimeOffset.UtcNow;
    }

    protected static IQueryable<TRecord> Scoped<TRecord>(IQueryable<TRecord> query, TenantScope scope)
        where TRecord : PersistedScopedJson =>
        query.Where(record => record.TenantId == scope.TenantId && record.WorkspaceId == scope.WorkspaceId);

    protected async Task UpsertAsync<TRecord>(
        DbSet<TRecord> set,
        Guid id,
        TenantScope scope,
        string payloadJson,
        Action<TRecord>? configure = null,
        CancellationToken cancellationToken = default)
        where TRecord : PersistedScopedJson, new()
    {
        var record = await set.FindAsync([id], cancellationToken);
        if (record is null)
        {
            record = new TRecord { Id = id };
            set.Add(record);
        }

        ApplyScope(record, scope);
        record.PayloadJson = payloadJson;
        configure?.Invoke(record);
        await DbContext.SaveChangesAsync(cancellationToken);
    }
}
