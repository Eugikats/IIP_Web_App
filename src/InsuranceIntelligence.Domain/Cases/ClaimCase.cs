using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Workflow;

namespace InsuranceIntelligence.Domain.Cases;

public sealed record ClaimCase
{
    public required Guid CaseId { get; init; }
    public required TenantScope Scope { get; init; }
    public required string CustomerName { get; init; }
    public string? CustomerEmail { get; init; }
    public string? CustomerPhone { get; init; }
    public required string PolicyNumber { get; init; }
    public required string ProductLine { get; init; }

    /// <summary>The configured Product, and the exact Published version that was in force, resolved
    /// by matching ProductLine to a published product's name at intake (outcome A/C). Null when no
    /// matching product has been configured yet, ProductLine remains the source of truth either way.
    /// Never re-resolved after intake (see CaseIntakeService), even if the product is later edited
    /// or republished, that is the entire point: a claim already filed keeps the process and
    /// document matrix version that was in force when it was filed.</summary>
    public Guid? ProductId { get; init; }
    public int? ProductVersion { get; init; }

    /// <summary>The amount the customer is claiming, when known at intake. Null is honest absence,
    /// not zero, most intake paths do not collect this yet.</summary>
    public decimal? ClaimedAmount { get; init; }

    /// <summary>Deterministic, rule-based advisory signals from real policy fields (policy in force
    /// on the loss date, claimed amount against the sum insured), computed once at intake by
    /// PolicyRiskSignalEvaluator. Advisory only, never a decision; empty when no matching policy
    /// was found to check against.</summary>
    public IReadOnlyList<string> AdvisoryRiskSignals { get; init; } = [];
    public required string ClaimType { get; init; }
    public required string Channel { get; init; }
    public required DateOnly IncidentDate { get; init; }
    public required string IncidentDescription { get; init; }
    public MotorFnolDetails? MotorFnolDetails { get; init; }
    public MedicalFnolDetails? MedicalFnolDetails { get; init; }
    public required CaseStatus Status { get; init; }
    public required decimal CompletenessScore { get; init; }
    public string RiskLevel { get; init; } = "Low";
    public CasePriority Priority { get; init; } = CasePriority.Normal;
    public DateTimeOffset? SlaDueAt { get; init; }

    /// <summary>True for a claim IIP created on its own from an identified WhatsApp/email sender
    /// (see RuleBasedWhatsAppToCaseMatcher / RuleBasedEmailToCaseMatcher's identified-no-case
    /// path), a human has not yet looked at it. SlaDueAt stays null until a human confirms it via
    /// CaseEndpoints.ConfirmCase, which is also what excludes it from SLA/attention metrics
    /// (BasicSlaCalculator treats a null SlaDueAt as never at risk or breached) without a
    /// dedicated CaseStatus value or a fourth duplicated metrics carve-out.</summary>
    public bool RequiresConfirmation { get; init; }
    public string? AssignedUserId { get; init; }
    public string? AssignedTeam { get; init; }
    public required string CreatedByUserId { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required DateTimeOffset UpdatedAt { get; init; }
    public required string CorrelationId { get; init; }

    public ClaimCaseSnapshot ToAuditSnapshot() =>
        new(CaseId, Scope, Status.ToString());
}
