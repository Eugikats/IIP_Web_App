using InsuranceIntelligence.Domain.Approvals;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Documents;

namespace InsuranceIntelligence.Application.Claims;

/// <summary>
/// Application seam for the single governed claim path:
/// FNOL → evidence intake → assignment → review → approval → communication → evidence-pack export.
/// Claim type is a discriminator for evidence, queue, and designated-human escalation — not a parallel engine.
/// </summary>
public interface IClaimGovernedPathService
{
    ClaimRoutingDecision Route(
        string? claimType,
        MotorFnolDetails? motorFnolDetails = null,
        ClaimRoutingSignals? signals = null);

    ClaimRoutingDecision Route(ClaimCase claimCase, ClaimRoutingSignals? signals = null);

    DocumentChecklistRuleSet RequiredEvidence(ClaimCase claimCase);

    void EnsureHumanMayPerform(ClaimConsequentialAction action, DecisionActorType actorType);

    void EnsureMayAdvanceToReadyForExport(
        ClaimCase claimCase,
        DecisionActorType actorType,
        ClaimRoutingSignals? signals = null);

    void EnsureMayExportEvidencePack(
        ClaimCase claimCase,
        DecisionActorType actorType,
        ClaimRoutingSignals? signals = null);
}

public sealed class ClaimGovernedPathService : IClaimGovernedPathService
{
    public ClaimRoutingDecision Route(
        string? claimType,
        MotorFnolDetails? motorFnolDetails = null,
        ClaimRoutingSignals? signals = null) =>
        ClaimRoutingPolicy.Evaluate(claimType, motorFnolDetails, signals);

    public ClaimRoutingDecision Route(ClaimCase claimCase, ClaimRoutingSignals? signals = null) =>
        ClaimRoutingPolicy.Evaluate(claimCase, signals);

    public DocumentChecklistRuleSet RequiredEvidence(ClaimCase claimCase) =>
        DocumentChecklistRules.ForCase(claimCase);

    public void EnsureHumanMayPerform(ClaimConsequentialAction action, DecisionActorType actorType) =>
        ClaimAuthorityPolicy.EnsureActorMayPerform(action, actorType);

    public void EnsureMayAdvanceToReadyForExport(
        ClaimCase claimCase,
        DecisionActorType actorType,
        ClaimRoutingSignals? signals = null) =>
        ClaimAuthorityPolicy.EnsureMayAdvanceToReadyForExport(claimCase, actorType, signals);

    public void EnsureMayExportEvidencePack(
        ClaimCase claimCase,
        DecisionActorType actorType,
        ClaimRoutingSignals? signals = null) =>
        ClaimAuthorityPolicy.EnsureMayExportEvidencePack(claimCase, actorType, signals);
}
