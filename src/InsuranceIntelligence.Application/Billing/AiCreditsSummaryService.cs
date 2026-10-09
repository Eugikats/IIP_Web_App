using System.Globalization;
using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Domain.Administration;

namespace InsuranceIntelligence.Application.Billing;

public sealed record CreditsByFeature(string Feature, string Label, int Credits, int Calls);

public sealed record CreditsByDay(string Day, int Credits);

public sealed record CreditsSummary(
    bool Metered,
    string Period,
    string PlanName,
    int Included,
    int Used,
    int Remaining,
    int PercentUsed,
    string State,
    int Calls,
    IReadOnlyList<CreditsByFeature> ByFeature,
    IReadOnlyList<CreditsByDay> ByDay,
    decimal? CostUsd,
    bool CanAddCredits);

public interface IAiCreditsSummaryService
{
    Task<CreditsSummary> GetAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// What the credit meter shows: credits used this month against the plan, where they went by feature and by day.
/// Anyone who can use the workspace sees the meter; the real dollar cost, and the action to add credits, are for
/// people who can see billing.
/// </summary>
public sealed class AiCreditsSummaryService(
    ITenantContextAccessor tenantContextAccessor,
    IPermissionService permissionService,
    IAiCreditGate gate,
    IAiUsageRepository repository) : IAiCreditsSummaryService
{
    public async Task<CreditsSummary> GetAsync(CancellationToken cancellationToken = default)
    {
        var context = tenantContextAccessor.Current;
        var now = DateTimeOffset.UtcNow;
        var monthStart = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var status = await gate.StatusAsync(context.Scope, cancellationToken);
        var entries = await repository.ListAsync(context.Scope, monthStart, monthStart.AddMonths(1), cancellationToken);
        var canSeeBilling = await permissionService.HasPermissionAsync(Permission.ViewBilling, cancellationToken);

        var byFeature = entries
            .GroupBy(entry => entry.Feature)
            .Select(group => new CreditsByFeature(group.Key, LabelFor(group.Key), group.Sum(e => e.Credits), group.Count()))
            .OrderByDescending(item => item.Credits)
            .ToList();
        var byDay = Enumerable.Range(1, now.Day)
            .Select(day => new CreditsByDay(
                new DateTime(now.Year, now.Month, day).ToString("d MMM", CultureInfo.InvariantCulture),
                entries.Where(e => e.OccurredAt.Day == day).Sum(e => e.Credits)))
            .ToList();

        // The counter is the number that gates work; the records explain it. They agree except for a call
        // that could not be recorded, so the larger of the two is shown and never less than was counted.
        var recorded = entries.Sum(entry => entry.Credits);
        var used = Math.Max(status.Used, recorded);
        var included = status.Metered ? status.Included : 0;
        var percent = status.Metered && included > 0 ? (int)Math.Min(100, Math.Round(100.0 * used / included)) : 0;
        var state = !status.Metered ? "not_metered" : status.Exhausted ? "exhausted" : status.NearLimit ? "near_limit" : "ok";

        return new CreditsSummary(
            status.Metered,
            monthStart.ToString("yyyy-MM", CultureInfo.InvariantCulture),
            status.PlanName,
            included,
            used,
            status.Metered ? Math.Max(0, included - used) : 0,
            percent,
            state,
            entries.Count,
            byFeature,
            byDay,
            canSeeBilling ? Math.Round(entries.Sum(entry => entry.CostUsd), 4) : null,
            canSeeBilling);
    }

    public static string LabelFor(string feature) => feature switch
    {
        "kato" => "Kato on WhatsApp",
        "command_center" => "Command Center",
        "policy_answers" => "Policy answers",
        "customer_updates" => "Customer updates",
        "other" => "Other",
        var other => string.Join(' ', other.Split('_', StringSplitOptions.RemoveEmptyEntries)) is { Length: > 0 } words
            ? char.ToUpperInvariant(words[0]) + words[1..]
            : "Other",
    };
}
