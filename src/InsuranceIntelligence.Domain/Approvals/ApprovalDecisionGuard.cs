using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Domain.Approvals;

/// <summary>
/// Platform ceiling: consequential insurance Decide / Send / Execute actions
/// require an accountable human actor. Tenant policy may narrow capability but
/// cannot bypass this guard.
/// </summary>
public static class ApprovalDecisionGuard
{
    public static void EnsureHumanForConsequentialDecision(
        bool requiresHumanDecision,
        DecisionActorType actorType,
        string requestedActionType)
    {
        if (!requiresHumanDecision)
        {
            return;
        }

        if (actorType != DecisionActorType.Human)
        {
            throw new DomainException(
                $"Action '{requestedActionType}' requires a human actor and cannot be finalized by '{actorType}'.");
        }
    }

    /// <summary>
    /// Maps identity user ids and audit actor kinds onto the decision ceiling enum.
    /// Prefixed ids (<c>agent:</c>, <c>system:</c>, <c>integration:</c>) are non-human.
    /// </summary>
    public static DecisionActorType ResolveActorType(string? userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return DecisionActorType.System;
        }

        if (userId.StartsWith("agent:", StringComparison.OrdinalIgnoreCase))
        {
            return DecisionActorType.Agent;
        }

        if (userId.StartsWith("system:", StringComparison.OrdinalIgnoreCase))
        {
            return DecisionActorType.System;
        }

        if (userId.StartsWith("integration:", StringComparison.OrdinalIgnoreCase))
        {
            return DecisionActorType.Integration;
        }

        return DecisionActorType.Human;
    }
}
