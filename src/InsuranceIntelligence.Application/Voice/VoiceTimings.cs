using System.Collections.Concurrent;

namespace InsuranceIntelligence.Application.Voice;

public sealed record VoiceTimingSummary(string Name, int Count, int P50Milliseconds, int P95Milliseconds, int BudgetMilliseconds);

/// <summary>
/// How long the parts of starting a call took on real phones, as numbers only: no words, no audio, no person.
/// Kept in memory per workspace (the last 500 of each), enough to check the start-up budgets.
/// </summary>
public interface IVoiceTimings
{
    /// <summary>Records the marks that are known, ignoring any that are not numbers in a sane range. Returns how many were kept.</summary>
    int Record(string tenantId, IReadOnlyDictionary<string, double> marks);

    IReadOnlyList<VoiceTimingSummary> Summarise(string tenantId);
}

public sealed class VoiceTimings : IVoiceTimings
{
    private const int Keep = 500;

    // The budgets in the plan: a call is ready in under 2 s from the tap, and the first words are heard in under 3.5 s.
    private static readonly IReadOnlyDictionary<string, int> Budgets = new Dictionary<string, int>
    {
        ["microphone_ready"] = 1500,
        ["offer_sent"] = 1500,
        ["answer_received"] = 2000,
        ["connected"] = 2000,
        ["first_audio"] = 3500,
    };

    private readonly ConcurrentDictionary<string, ConcurrentQueue<int>> samples = new();

    public int Record(string tenantId, IReadOnlyDictionary<string, double> marks)
    {
        var kept = 0;
        foreach (var (name, value) in marks)
        {
            if (!Budgets.ContainsKey(name) || double.IsNaN(value) || value < 0 || value > 120_000)
            {
                continue;
            }

            var queue = samples.GetOrAdd($"{tenantId}|{name}", _ => new ConcurrentQueue<int>());
            queue.Enqueue((int)Math.Round(value));
            while (queue.Count > Keep && queue.TryDequeue(out _))
            {
            }

            kept++;
        }

        return kept;
    }

    public IReadOnlyList<VoiceTimingSummary> Summarise(string tenantId) =>
        Budgets.Select(budget =>
        {
            var values = samples.TryGetValue($"{tenantId}|{budget.Key}", out var queue) ? queue.ToArray().OrderBy(v => v).ToArray() : [];
            return new VoiceTimingSummary(budget.Key, values.Length, Percentile(values, 0.5), Percentile(values, 0.95), budget.Value);
        }).ToList();

    private static int Percentile(int[] sorted, double p) =>
        sorted.Length == 0 ? 0 : sorted[Math.Min(sorted.Length - 1, (int)Math.Ceiling(p * sorted.Length) - 1 < 0 ? 0 : (int)Math.Ceiling(p * sorted.Length) - 1)];
}
