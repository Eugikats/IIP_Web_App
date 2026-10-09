using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Organisations;
using InsuranceIntelligence.Domain.Usage;
using Microsoft.EntityFrameworkCore;

namespace InsuranceIntelligence.Infrastructure.Persistence;

public sealed class UsageCounterEntity
{
    public Guid Id { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public string Period { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public int Count { get; set; }
}

public sealed class EfUsageMeter(InsuranceIntelligenceDbContext db, IOrganisationRepository organisations) : IUsageMeter
{
    private const int Attempts = 4;

    public async Task<UsageDecision> TryConsumeAsync(TenantScope scope, UsageKind kind, CancellationToken cancellationToken = default)
    {
        var organisation = await organisations.GetAsync(scope.TenantId, cancellationToken);
        if (organisation is null)
        {
            return new UsageDecision(true, 0, int.MaxValue); // not a SaaS organisation: not metered
        }

        var limit = PlanCatalog.Find(organisation.PlanId).LimitFor(kind);
        var period = CurrentPeriod();
        for (var attempt = 0; attempt < Attempts; attempt++)
        {
            var counter = await db.UsageCounters.FirstOrDefaultAsync(
                c => c.TenantId == scope.TenantId && c.Period == period && c.Kind == kind.ToString(), cancellationToken);
            if (counter is null)
            {
                counter = new UsageCounterEntity { Id = Guid.NewGuid(), TenantId = scope.TenantId, Period = period, Kind = kind.ToString(), Count = 0 };
                db.UsageCounters.Add(counter);
            }

            if (counter.Count >= limit)
            {
                return new UsageDecision(false, counter.Count, limit);
            }

            counter.Count++;
            try
            {
                await db.SaveChangesAsync(cancellationToken);
                return new UsageDecision(true, counter.Count, limit);
            }
            catch (DbUpdateException)
            {
                // Another request moved the counter or created it first: read again and retry.
                db.ChangeTracker.Clear();
            }
        }

        // Could not settle the count: fail closed on the costly work rather than meter nothing.
        return new UsageDecision(false, limit, limit);
    }

    public async Task AddAsync(TenantScope scope, UsageKind kind, int amount, CancellationToken cancellationToken = default)
    {
        if (amount <= 0)
        {
            return;
        }

        var period = CurrentPeriod();
        for (var attempt = 0; attempt < Attempts; attempt++)
        {
            var counter = await db.UsageCounters.FirstOrDefaultAsync(
                c => c.TenantId == scope.TenantId && c.Period == period && c.Kind == kind.ToString(), cancellationToken);
            if (counter is null)
            {
                counter = new UsageCounterEntity { Id = Guid.NewGuid(), TenantId = scope.TenantId, Period = period, Kind = kind.ToString(), Count = 0 };
                db.UsageCounters.Add(counter);
            }

            counter.Count = (int)Math.Min((long)counter.Count + amount, int.MaxValue);
            try
            {
                await db.SaveChangesAsync(cancellationToken);
                return;
            }
            catch (DbUpdateException)
            {
                db.ChangeTracker.Clear();
            }
        }
    }

    public async Task<UsageSnapshot?> GetAsync(TenantScope scope, CancellationToken cancellationToken = default)
    {
        var organisation = await organisations.GetAsync(scope.TenantId, cancellationToken);
        if (organisation is null)
        {
            return null;
        }

        var period = CurrentPeriod();
        var plan = PlanCatalog.Find(organisation.PlanId);
        var rows = await db.UsageCounters.AsNoTracking()
            .Where(c => c.TenantId == scope.TenantId && c.Period == period)
            .ToListAsync(cancellationToken);
        var used = Enum.GetValues<UsageKind>().ToDictionary(k => k, k => rows.FirstOrDefault(r => r.Kind == k.ToString())?.Count ?? 0);
        var limits = Enum.GetValues<UsageKind>().ToDictionary(k => k, plan.LimitFor);
        return new UsageSnapshot(period, plan.PlanId, used, limits);
    }

    private static string CurrentPeriod() => DateTimeOffset.UtcNow.ToString("yyyy-MM");
}
