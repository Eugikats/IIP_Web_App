using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Domain.Usage;

public enum UsageKind
{
    /// <summary>One customer message answered by the AI assistant (the costly step).</summary>
    AssistantTurn,

    /// <summary>One inbound customer message received on a WhatsApp number.</summary>
    InboundMessage,

    /// <summary>AI credits spent: every AI call is worth a number of credits from its real token use.
    /// Unlike the other kinds this one is added to after the call and is never refused by the counter;
    /// the check happens before the next call (<see cref="IUsageMeter.GetAsync"/> and the credit gate).</summary>
    AiCredits,
}

/// <summary>What a plan includes each calendar month. A limit is a ceiling, not a bill: past it the
/// costly work stops (the assistant falls back to its plain rule-based replies) and the owner is
/// told, so one organisation can never run up the platform's cost.</summary>
public sealed record UsagePlan(string PlanId, string DisplayName, int AssistantTurnsPerMonth, int InboundMessagesPerMonth, int AiCreditsPerMonth)
{
    public int LimitFor(UsageKind kind) => kind switch
    {
        UsageKind.AssistantTurn => AssistantTurnsPerMonth,
        UsageKind.InboundMessage => InboundMessagesPerMonth,
        UsageKind.AiCredits => AiCreditsPerMonth,
        _ => 0,
    };
}

public static class PlanCatalog
{
    public static readonly UsagePlan Trial = new("trial", "Trial", 300, 1_000, 2_000);
    public static readonly UsagePlan Standard = new("standard", "Standard", 10_000, 50_000, 25_000);
    public static readonly UsagePlan Unlimited = new("unlimited", "Unlimited", int.MaxValue, int.MaxValue, int.MaxValue);

    public static readonly IReadOnlyList<UsagePlan> All = [Trial, Standard, Unlimited];

    /// <summary>An unknown plan gets the smallest one: a typo must never become unlimited use.</summary>
    public static UsagePlan Find(string? planId) =>
        All.FirstOrDefault(p => string.Equals(p.PlanId, planId, StringComparison.OrdinalIgnoreCase)) ?? Trial;
}

public sealed record UsageDecision(bool Allowed, int Used, int Limit);

public sealed record UsageSnapshot(string Period, string PlanId, IReadOnlyDictionary<UsageKind, int> Used, IReadOnlyDictionary<UsageKind, int> Limits);

public interface IUsageMeter
{
    /// <summary>Counts one unit if the organisation's plan still has room, and says whether it did.
    /// A deployment with no organisations (the single-insurer Managed Application) is not metered.</summary>
    Task<UsageDecision> TryConsumeAsync(TenantScope scope, UsageKind kind, CancellationToken cancellationToken = default);

    Task<UsageSnapshot?> GetAsync(TenantScope scope, CancellationToken cancellationToken = default);

    /// <summary>Adds an amount that has already been spent (AI credits after a call). It is never refused:
    /// the work was done, and the next call is what the limit stops. Not metered for a deployment with
    /// no organisations.</summary>
    Task AddAsync(TenantScope scope, UsageKind kind, int amount, CancellationToken cancellationToken = default);
}
