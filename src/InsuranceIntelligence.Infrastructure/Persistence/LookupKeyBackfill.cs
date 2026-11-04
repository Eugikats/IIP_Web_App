using InsuranceIntelligence.Domain.Cases;
using Microsoft.EntityFrameworkCore;

namespace InsuranceIntelligence.Infrastructure.Persistence;

/// <summary>
/// Fills the lookup columns on rows saved before they existed (build-loop item 35), in small batches, across every workspace. It is safe to
/// run again and safe to run while the platform is in use: a row it has not reached yet is still found, by the lookups' fallback.
/// </summary>
public sealed class LookupKeyBackfill(InsuranceIntelligenceDbContext dbContext)
{
    private const int Batch = 200;

    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        var total = 0;
        while (true)
        {
            var done = await CasesAsync(cancellationToken) + await CustomersAsync(cancellationToken) + await PoliciesAsync(cancellationToken);
            total += done;
            if (done == 0)
            {
                return total;
            }
        }
    }

    private async Task<int> CasesAsync(CancellationToken cancellationToken)
    {
        var rows = await dbContext.Cases.Where(record => record.LookupVersion == 0).Take(Batch).ToListAsync(cancellationToken);
        foreach (var row in rows)
        {
            CaseLookup.Fill(row, System.Text.Json.JsonSerializer.Deserialize<ClaimCase>(row.PayloadJson, JsonSerializerSettings.Options)!);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return rows.Count;
    }

    private async Task<int> CustomersAsync(CancellationToken cancellationToken)
    {
        var rows = await dbContext.Customers.Where(record => record.LookupVersion == 0).Take(Batch).ToListAsync(cancellationToken);
        foreach (var row in rows)
        {
            var customer = System.Text.Json.JsonSerializer.Deserialize<InsuranceIntelligence.Domain.Customers.Customer>(row.PayloadJson, JsonSerializerSettings.Options)!;
            row.PhoneKey = InsuranceIntelligence.Domain.Common.LookupKeys.PhoneKey(customer.Phone);
            row.EmailKey = InsuranceIntelligence.Domain.Common.LookupKeys.EmailKey(customer.Email);
            row.LookupVersion = CaseLookup.Version;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return rows.Count;
    }

    private async Task<int> PoliciesAsync(CancellationToken cancellationToken)
    {
        var rows = await dbContext.Policies.Where(record => record.LookupVersion == 0).Take(Batch).ToListAsync(cancellationToken);
        foreach (var row in rows)
        {
            var policy = System.Text.Json.JsonSerializer.Deserialize<InsuranceIntelligence.Domain.Policies.Policy>(row.PayloadJson, JsonSerializerSettings.Options)!;
            row.PolicyKey = InsuranceIntelligence.Domain.Common.LookupKeys.PolicyKey(policy.PolicyNumber);
            row.CustomerKey = InsuranceIntelligence.Domain.Common.LookupKeys.TextKey(policy.CustomerId, 80);
            row.LookupVersion = CaseLookup.Version;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return rows.Count;
    }
}
