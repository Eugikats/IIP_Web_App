using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Domain.Billing;

/// <summary>
/// Usage is counted by calendar month in UTC. The same key is stored on every metered record so a month's
/// total is an indexed lookup, and so plans and allowances (item 32) can speak of "this month".
/// </summary>
public static class UsagePeriod
{
    public static string KeyFor(DateTimeOffset moment) => moment.UtcDateTime.ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture);

    public static bool IsValid(string? key) =>
        key is { Length: 7 } && key[4] == '-' && int.TryParse(key.AsSpan(0, 4), out var year) && year is >= 2000 and <= 2999
        && int.TryParse(key.AsSpan(5, 2), out var month) && month is >= 1 and <= 12;
}

/// <summary>What the model was used for in a period. Tokens are exact when the provider reported them; a call without a reported count adds to <see cref="Calls"/> only.</summary>
public sealed record AiUsageTotals(int Calls, long PromptTokens, long CompletionTokens, long CostMicroUsd, int UnpricedCalls)
{
    public static readonly AiUsageTotals None = new(0, 0, 0, 0, 0);

    public long TotalTokens => PromptTokens + CompletionTokens;

    /// <summary>The estimated cost in US dollars, or null when nothing in the period could be priced.</summary>
    public decimal? CostUsd => Calls > 0 && UnpricedCalls >= Calls ? null : CostMicroUsd / 1_000_000m;
}

/// <summary>What was stored in a workspace: files received in the period and everything held now.</summary>
public sealed record DocumentUsageTotals(int DocumentsInPeriod, int DocumentsHeld, long BytesHeld)
{
    public static readonly DocumentUsageTotals None = new(0, 0, 0);
}

/// <summary>
/// Reads the metered totals for one workspace. Implementations answer from the platform's own records (never from a
/// provider's invoice) and only ever for the scope they are given.
/// </summary>
public interface IWorkspaceUsageReader
{
    Task<AiUsageTotals> GetAiUsageAsync(TenantScope scope, string periodKey, CancellationToken cancellationToken = default);

    Task<DocumentUsageTotals> GetDocumentUsageAsync(TenantScope scope, string periodKey, CancellationToken cancellationToken = default);
}
