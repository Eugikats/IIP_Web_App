using System.Reflection;
using System.Text.Json;
using InsuranceIntelligence.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace InsuranceIntelligence.Infrastructure.Persistence;

/// <summary>
/// Everything one organisation owns, found from the data model itself: every table that carries a
/// tenant id. Export hands the organisation its data when it leaves; Purge removes it for good.
/// Working from the model rather than a list means a table added later is covered automatically.
/// Stored credentials (WhatsApp tokens, provider secrets) are never exported: they are the
/// organisation's secrets with other companies, not records the organisation asked to take with it.
/// </summary>
public sealed class TenantDataService(InsuranceIntelligenceDbContext db)
{
    private static readonly string[] NeverExported =
    [
        "WhatsAppChannelConfigurations", "IntegrationConnections", "OrganisationInvitations",
    ];

    public async Task<string> ExportAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        var tables = new SortedDictionary<string, object>(StringComparer.Ordinal);
        foreach (var (table, rows) in await TenantRowsAsync(tenantId, cancellationToken))
        {
            if (NeverExported.Contains(table, StringComparer.Ordinal))
            {
                continue;
            }

            tables[table] = rows.Select(Describe).ToArray();
        }

        return JsonSerializer.Serialize(
            new { organisationId = tenantId, exportedAt = DateTimeOffset.UtcNow, tables },
            new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>Deletes every row the organisation owns, in every table. Irreversible.</summary>
    public async Task<int> PurgeAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        var removed = 0;
        foreach (var (_, rows) in await TenantRowsAsync(tenantId, cancellationToken))
        {
            db.RemoveRange(rows);
            removed += rows.Count;
        }

        // Invitations hold people's email addresses and are keyed by organisation, not tenant.
        var invitations = await db.OrganisationInvitations.Where(i => i.OrganisationId == tenantId).ToListAsync(cancellationToken);
        db.OrganisationInvitations.RemoveRange(invitations);
        removed += invitations.Count;

        await db.SaveChangesAsync(cancellationToken);
        return removed;
    }

    private async Task<List<(string Table, List<object> Rows)>> TenantRowsAsync(string tenantId, CancellationToken cancellationToken)
    {
        var result = new List<(string, List<object>)>();
        foreach (var entityType in db.Model.GetEntityTypes())
        {
            var clr = entityType.ClrType;
            var tenantProperty = clr.GetProperty("TenantId", BindingFlags.Public | BindingFlags.Instance);
            if (tenantProperty is null || tenantProperty.PropertyType != typeof(string))
            {
                continue;
            }

            var table = entityType.GetTableName() ?? clr.Name;
            var method = typeof(TenantDataService).GetMethod(nameof(LoadAsync), BindingFlags.NonPublic | BindingFlags.Instance)!
                .MakeGenericMethod(clr);
            var rows = await (Task<List<object>>)method.Invoke(this, [tenantId, cancellationToken])!;
            if (rows.Count > 0)
            {
                result.Add((table, rows));
            }
        }

        return result;
    }

    private async Task<List<object>> LoadAsync<T>(string tenantId, CancellationToken cancellationToken) where T : class
    {
        var property = typeof(T).GetProperty("TenantId")!;
        var all = await db.Set<T>().ToListAsync(cancellationToken);
        return all.Where(row => string.Equals((string?)property.GetValue(row), tenantId, StringComparison.Ordinal)).Cast<object>().ToList();
    }

    private static object Describe(object row)
    {
        // The JSON-stored tables keep the real record in PayloadJson; hand that back as itself.
        var payload = row.GetType().GetProperty("PayloadJson")?.GetValue(row) as string;
        if (payload is not null)
        {
            using var document = JsonDocument.Parse(payload);
            return document.RootElement.Clone();
        }

        return row;
    }
}
