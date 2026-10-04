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
        else if (!string.Equals(record.TenantId, scope.TenantId, StringComparison.Ordinal)
            || !string.Equals(record.WorkspaceId, scope.WorkspaceId, StringComparison.Ordinal))
        {
            // The id already belongs to another organisation or workspace. Writing here would have
            // quietly moved their record to the caller; refuse, and say nothing about whose it is.
            throw new TenantIsolationException();
        }

        ApplyScope(record, scope);
        record.PayloadJson = payloadJson;
        configure?.Invoke(record);
        await DbContext.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>A write was refused because the record id belongs to another organisation or workspace.</summary>
public sealed class TenantIsolationException()
    : InvalidOperationException("That record cannot be written from this organisation.");
