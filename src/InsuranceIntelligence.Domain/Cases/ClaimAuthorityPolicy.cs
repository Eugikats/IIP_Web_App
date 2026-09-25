using InsuranceIntelligence.Domain.Approvals;
using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Domain.Cases;

/// <summary>
/// Platform authority ceiling for the governed claim path.
/// AI/system/integration actors never finalize consequential insurance decisions.
/// Designated-human escalation triggers cannot be cleared by automation.
/// </summary>
public static class ClaimAuthorityPolicy
{
    public static void EnsureActorMayPerform(
        ClaimConsequentialAction action,
        DecisionActorType actorType)
    {
        if (actorType != DecisionActorType.Human)
        {
            throw new DomainException(
                $"Action '{action}' requires a designated human actor and cannot be finalized by '{actorType}'.");
        }
    }

    public static void EnsureDesignatedHumanMayClearEscalation(
        ClaimRoutingDecision routing,
        DecisionActorType actorType)
    {
        if (!routing.RequiresDesignatedHumanEscalation)
        {
            return;
        }

        EnsureActorMayPerform(ClaimConsequentialAction.ApproveClaim, actorType);
    }

    public static void EnsureMayAdvanceToReadyForExport(
        ClaimCase claimCase,
        DecisionActorType actorType,
        ClaimRoutingSignals? signals = null)
    {
        EnsureActorMayPerform(ClaimConsequentialAction.ApproveClaim, actorType);

        var routing = ClaimRoutingPolicy.Evaluate(claimCase, signals);
        if (!routing.RequiresDesignatedHumanBeforeExport)
        {
            return;
        }

        // Exception routes must pass through designated-human escalation ownership
        // (Escalated status or designated escalation team) before export readiness.
        var onDesignatedPath = claimCase.Status == CaseStatus.Escalated
            || IsDesignatedEscalationTeam(claimCase.AssignedTeam);

        if (!onDesignatedPath)
        {
            throw new DomainException(
                "This claim type requires designated-human escalation before approval can advance the case to ReadyForExport. "
                + routing.OperatorExplanation);
        }
    }

    public static void EnsureMayExportEvidencePack(
        ClaimCase claimCase,
        DecisionActorType actorType,
        ClaimRoutingSignals? signals = null)
    {
        EnsureActorMayPerform(ClaimConsequentialAction.EvidencePackExport, actorType);

        if (claimCase.Status is CaseStatus.Settled or CaseStatus.Closed)
        {
            throw new DomainException("Settled or closed cases cannot export a new evidence pack from the active governed path.");
        }

        if (claimCase.Status != CaseStatus.ReadyForExport)
        {
            throw new DomainException(
                "This claim can be exported once a person has approved it and it is marked Ready for export.");
        }

        // Exception-route clearance is enforced when advancing to ReadyForExport.
        // Export itself remains human-gated, tenant-scoped, and audited by the caller.
        _ = ClaimRoutingPolicy.Evaluate(claimCase, signals);
    }

    public static bool IsDesignatedEscalationTeam(string? team) =>
        string.Equals(team, ClaimRoutingPolicy.DesignatedInjuryEscalationTeam, StringComparison.OrdinalIgnoreCase)
        || string.Equals(team, ClaimRoutingPolicy.DesignatedFraudEscalationTeam, StringComparison.OrdinalIgnoreCase)
        || string.Equals(team, ClaimRoutingPolicy.TotalLossDeskTeam, StringComparison.OrdinalIgnoreCase)
        || string.Equals(team, ClaimRoutingPolicy.ClaimsSupervisionTeam, StringComparison.OrdinalIgnoreCase);

    public static ClaimConsequentialAction? TryMapApprovalType(string? approvalType)
    {
        if (string.IsNullOrWhiteSpace(approvalType))
        {
            return null;
        }

        var normalized = approvalType.Trim().ToLowerInvariant();
        if (normalized.Contains("repudiat", StringComparison.Ordinal) || normalized.Contains("deny", StringComparison.Ordinal))
        {
            return ClaimConsequentialAction.Repudiate;
        }

        if (normalized.Contains("settlement", StringComparison.Ordinal))
        {
            return ClaimConsequentialAction.ApproveSettlement;
        }

        if (normalized.Contains("payment", StringComparison.Ordinal) || normalized.Contains("payout", StringComparison.Ordinal))
        {
            return ClaimConsequentialAction.ApprovePayment;
        }

        if (normalized.Contains("reject", StringComparison.Ordinal))
        {
            return ClaimConsequentialAction.RejectClaim;
        }

        if (normalized.Contains("approve", StringComparison.Ordinal) && normalized.Contains("claim", StringComparison.Ordinal))
        {
            return ClaimConsequentialAction.ApproveClaim;
        }

        return null;
    }
}
