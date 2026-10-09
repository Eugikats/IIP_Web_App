using System.Text.Json;
using InsuranceIntelligence.Application.Billing;
using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Organisations;
using InsuranceIntelligence.Domain.Usage;
using InsuranceIntelligence.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace InsuranceIntelligence.Infrastructure.Billing;

public sealed class EfAiUsageRepository(InsuranceIntelligenceDbContext dbContext) : JsonRepositoryBase(dbContext), IAiUsageRepository
{
    public Task SaveAsync(AiUsageEntry entry, CancellationToken cancellationToken = default) =>
        UpsertAsync(
            DbContext.AiUsageRecords,
            entry.UsageId,
            entry.Scope,
            Serialize(entry),
            record => record.OccurredAt = entry.OccurredAt,
            cancellationToken);

    public async Task<IReadOnlyList<AiUsageEntry>> ListAsync(TenantScope scope, DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default)
    {
        var records = await Scoped(DbContext.AiUsageRecords.AsNoTracking(), scope)
            .Where(record => record.OccurredAt >= from && record.OccurredAt < to)
            .ToListAsync(cancellationToken);
        return records.Select(record => Deserialize<AiUsageEntry>(record.PayloadJson)).ToList();
    }
}

/// <summary>Where an organisation's credits stand: used so far this month against its plan. An organisation
/// within 90 percent is "near the limit"; a deployment with no organisations is not metered.</summary>
public sealed class AiCreditGate(IUsageMeter usageMeter, IOrganisationRepository organisations) : IAiCreditGate
{
    public async Task<CreditStatus> StatusAsync(TenantScope scope, CancellationToken cancellationToken = default)
    {
        var organisation = await organisations.GetAsync(scope.TenantId, cancellationToken);
        if (organisation is null)
        {
            return new CreditStatus(false, string.Empty, "No plan", 0, int.MaxValue, false, false);
        }

        var plan = PlanCatalog.Find(organisation.PlanId);
        var snapshot = await usageMeter.GetAsync(scope, cancellationToken);
        var used = snapshot?.Used.GetValueOrDefault(UsageKind.AiCredits) ?? 0;
        var included = plan.AiCreditsPerMonth;
        var unlimited = included == int.MaxValue;
        return new CreditStatus(!unlimited, plan.PlanId, plan.DisplayName, used, included, !unlimited && used >= included, !unlimited && used >= included * 0.9);
    }
}

public sealed class AiUsageRecorder(
    IAiUsageRepository repository,
    IUsageMeter usageMeter,
    IAiCreditGate gate,
    AiCreditPricing pricing,
    IAiUsageContext context,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<AiUsageRecorder> logger) : IAiUsageRecorder
{
    public async Task RecordAsync(string provider, string? model, int? promptTokens, int? completionTokens, string? feature = null, CancellationToken cancellationToken = default)
    {
        var scope = ResolveScope();
        if (scope is null)
        {
            // Without a workspace there is nobody to count it against. Say so rather than lose it silently.
            logger.LogWarning("An AI call was made with no workspace in scope, so it was not counted. Feature={Feature}", feature ?? context.Feature);
            return;
        }

        var (cost, credits) = pricing.Calculate(model, promptTokens, completionTokens);
        var userId = context.UserId ?? (tenantContextAccessor.HasCurrent ? tenantContextAccessor.Current.UserId : null);
        try
        {
            await repository.SaveAsync(new AiUsageEntry(
                Guid.NewGuid(), scope, userId, feature ?? context.Feature, provider, model, promptTokens, completionTokens, cost, credits, DateTimeOffset.UtcNow), cancellationToken);
            await usageMeter.AddAsync(scope, UsageKind.AiCredits, credits, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // A failure to count must never break the customer's answer that was already produced.
            logger.LogError(exception, "An AI call could not be recorded. Feature={Feature}", context.Feature);
        }
    }

    public async Task EnsureCreditsAsync(CancellationToken cancellationToken = default)
    {
        var scope = ResolveScope();
        if (scope is null)
        {
            return;
        }

        if ((await gate.StatusAsync(scope, cancellationToken)).Exhausted)
        {
            throw new AiCreditsExhaustedException();
        }
    }

    private TenantScope? ResolveScope() =>
        context.Scope ?? (tenantContextAccessor.HasCurrent ? tenantContextAccessor.Current.Scope : null);
}
