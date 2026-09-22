namespace InsuranceIntelligence.Domain.Policies;

/// <summary>Whether a policy is currently in force, from its own cover dates. Deterministic
/// (AGENTS.md non-negotiable 2): a policyholder asks "is my cover active right now?" and this is
/// answered from the two dates already on the record, never a model call.</summary>
public enum PolicyStandingState
{
    Active,
    Expired,
    NotYetStarted,
    Unknown,
}

public static class PolicyStanding
{
    public static PolicyStandingState Resolve(DateOnly? effectiveFrom, DateOnly? effectiveTo, DateOnly today)
    {
        if (effectiveFrom is null && effectiveTo is null)
        {
            return PolicyStandingState.Unknown;
        }

        if (effectiveFrom is { } from && today < from)
        {
            return PolicyStandingState.NotYetStarted;
        }

        if (effectiveTo is { } to && today > to)
        {
            return PolicyStandingState.Expired;
        }

        return PolicyStandingState.Active;
    }
}
