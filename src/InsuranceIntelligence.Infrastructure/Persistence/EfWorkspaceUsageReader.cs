using InsuranceIntelligence.Domain.Billing;
using InsuranceIntelligence.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace InsuranceIntelligence.Infrastructure.Persistence;

/// <summary>
/// Totals a workspace's usage from the platform's own records with indexed lookups on the month key. Every query is
/// filtered to the one scope it is given. Rows stored before metering have no month or size and are not counted.
/// </summary>
public sealed class EfWorkspaceUsageReader(InsuranceIntelligenceDbContext dbContext) : JsonRepositoryBase(dbContext), IWorkspaceUsageReader
{
    public async Task<AiUsageTotals> GetAiUsageAsync(TenantScope scope, string periodKey, CancellationToken cancellationToken = default)
    {
        if (!UsagePeriod.IsValid(periodKey))
        {
            return AiUsageTotals.None;
        }

        // A model call is a metering record, or an assistant answer that reported its tokens. Other actions are not calls.
        var rows = Scoped(DbContext.AgentActionLogs.AsNoTracking(), scope)
            .Where(record => record.PeriodKey == periodKey
                && (record.RecordKind == InsuranceIntelligence.Domain.Knowledge.ActionLogKinds.ModelCall || record.PromptTokens != null || record.CompletionTokens != null));
        var calls = await rows.CountAsync(cancellationToken);
        if (calls == 0)
        {
            return AiUsageTotals.None;
        }

        var prompt = await rows.SumAsync(record => (long)(record.PromptTokens ?? 0), cancellationToken);
        var completion = await rows.SumAsync(record => (long)(record.CompletionTokens ?? 0), cancellationToken);
        var cost = await rows.SumAsync(record => record.CostMicroUsd ?? 0L, cancellationToken);
        var unpriced = await rows.CountAsync(record => record.CostMicroUsd == null, cancellationToken);
        return new AiUsageTotals(calls, prompt, completion, cost, unpriced);
    }

    public async Task<DocumentUsageTotals> GetDocumentUsageAsync(TenantScope scope, string periodKey, CancellationToken cancellationToken = default)
    {
        var all = Scoped(DbContext.CaseDocuments.AsNoTracking(), scope);
        var held = await all.CountAsync(cancellationToken);
        if (held == 0)
        {
            return DocumentUsageTotals.None;
        }

        var bytes = await all.SumAsync(record => record.SizeBytes, cancellationToken);
        var inPeriod = UsagePeriod.IsValid(periodKey) ? await all.CountAsync(record => record.PeriodKey == periodKey, cancellationToken) : 0;
        return new DocumentUsageTotals(inPeriod, held, bytes);
    }
}
