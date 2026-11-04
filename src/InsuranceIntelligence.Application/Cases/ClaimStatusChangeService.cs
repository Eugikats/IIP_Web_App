using InsuranceIntelligence.Application.CommandCenter;
using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Approvals;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Application.Cases;

public sealed record StatusOptionDto(string Status, string Label, bool RequiresSupervisor, bool AllowedForYou);

public sealed record StatusOtherDto(string Status, string Label, string HowItIsReached);

public sealed record StatusOptionsDto(string CurrentStatus, string CurrentLabel, IReadOnlyList<StatusOptionDto> Options, IReadOnlyList<StatusOtherDto> Others);

public enum StatusChangeOutcome
{
    Done,
    NotFound,
    Invalid,
    NotAllowed,
    NeedsSupervisor,
    HumanRequired,
}

public sealed record StatusChangeResult(StatusChangeOutcome Outcome, string? Message = null, Guid? CaseId = null, string? Status = null, string? Label = null);

public interface IClaimStatusChangeService
{
    /// <summary>What the caller can do from the claim's current status, and where each other status comes from.</summary>
    Task<StatusOptionsDto?> OptionsAsync(TenantScope scope, Guid caseId, CancellationToken cancellationToken = default);

    /// <summary>The one audited "Change status" action: a defined move, a reason, a person with the permission.</summary>
    Task<StatusChangeResult> ChangeAsync(TenantScope scope, Guid caseId, string? toStatus, string? reason, string actorId, string correlationId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Changes a claim's status on an officer's say-so (build-loop item 20), but only along the moves listed
/// for it in <see cref="ClaimStatusTransitions"/>, with a reason written by the person, and never by an
/// automated actor. Moves that decide something (approval, escalation, settling or closing) stay with their
/// own actions and rules; taking a claim out of escalation or reopening a closed one needs the supervisor
/// authority that settling needs.
/// </summary>
public sealed class ClaimStatusChangeService(
    ICaseRepository caseRepository,
    IAuditRepository auditRepository,
    IPermissionService permissionService) : IClaimStatusChangeService
{
    public const int MaxReasonLength = 500;

    public async Task<StatusOptionsDto?> OptionsAsync(TenantScope scope, Guid caseId, CancellationToken cancellationToken = default)
    {
        var claim = await caseRepository.GetAsync(caseId, cancellationToken);
        if (claim is null || claim.Scope != scope)
        {
            return null;
        }

        var supervisor = await permissionService.HasPermissionAsync(Permission.ApproveSettlementWorkflowStep, cancellationToken);
        var manual = ClaimStatusTransitions.ManualFrom(claim.Status);
        var options = manual
            .Select(transition => new StatusOptionDto(
                transition.To.ToString(),
                ClaimStatusLabels.For(transition.To),
                transition.RequiresSupervisor,
                !transition.RequiresSupervisor || supervisor))
            .ToArray();
        var others = Enum.GetValues<CaseStatus>()
            .Where(status => status != claim.Status && manual.All(transition => transition.To != status))
            .Select(status => new StatusOtherDto(status.ToString(), ClaimStatusLabels.For(status), ClaimStatusTransitions.HowItIsReached(status)))
            .ToArray();
        return new StatusOptionsDto(claim.Status.ToString(), ClaimStatusLabels.For(claim.Status), options, others);
    }

    public async Task<StatusChangeResult> ChangeAsync(TenantScope scope, Guid caseId, string? toStatus, string? reason, string actorId, string correlationId, CancellationToken cancellationToken = default)
    {
        // A status change is a person's decision; an automated actor cannot make it.
        try
        {
            ApprovalDecisionGuard.EnsureHumanForConsequentialDecision(
                requiresHumanDecision: true, ApprovalDecisionGuard.ResolveActorType(actorId), "changeClaimStatus");
        }
        catch (DomainException exception)
        {
            return new StatusChangeResult(StatusChangeOutcome.HumanRequired, exception.Message);
        }

        var claim = await caseRepository.GetAsync(caseId, cancellationToken);
        if (claim is null || claim.Scope != scope)
        {
            return new StatusChangeResult(StatusChangeOutcome.NotFound, "Claim was not found.");
        }

        if (!Enum.TryParse<CaseStatus>(toStatus?.Trim(), ignoreCase: true, out var target) || !Enum.IsDefined(target))
        {
            return new StatusChangeResult(StatusChangeOutcome.Invalid, "Choose a status to move the claim to.");
        }

        var trimmed = reason?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return new StatusChangeResult(StatusChangeOutcome.Invalid, "Say in a sentence why you are changing the status.");
        }

        if (trimmed.Length > MaxReasonLength)
        {
            return new StatusChangeResult(StatusChangeOutcome.Invalid, $"The reason can be at most {MaxReasonLength} characters.");
        }

        if (target == claim.Status)
        {
            return new StatusChangeResult(StatusChangeOutcome.Invalid, $"The claim is already {ClaimStatusLabels.For(claim.Status).ToLowerInvariant()}.");
        }

        var transition = ClaimStatusTransitions.FindManual(claim.Status, target);
        if (transition is null)
        {
            return new StatusChangeResult(
                StatusChangeOutcome.NotAllowed,
                $"A claim that is {ClaimStatusLabels.For(claim.Status).ToLowerInvariant()} cannot be moved to {ClaimStatusLabels.For(target).ToLowerInvariant()} here. {ClaimStatusTransitions.HowItIsReached(target)}");
        }

        if (transition.RequiresSupervisor && !await permissionService.HasPermissionAsync(Permission.ApproveSettlementWorkflowStep, cancellationToken))
        {
            return new StatusChangeResult(
                StatusChangeOutcome.NeedsSupervisor,
                $"Moving a claim from {ClaimStatusLabels.For(claim.Status).ToLowerInvariant()} to {ClaimStatusLabels.For(target).ToLowerInvariant()} needs a supervisor.");
        }

        var before = claim.ToAuditSnapshot();
        var updated = claim with { Status = target, UpdatedAt = DateTimeOffset.UtcNow };
        await caseRepository.SaveAsync(updated, cancellationToken);
        await auditRepository.WriteAsync(AuditEvent.CaseStatusChanged(before, updated.ToAuditSnapshot(), actorId, correlationId, trimmed), cancellationToken);
        return new StatusChangeResult(StatusChangeOutcome.Done, null, caseId, target.ToString(), ClaimStatusLabels.For(target));
    }
}
