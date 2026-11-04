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

    /// <summary>When CaseFollowThroughService last prompted the customer for missing documents,
    /// null if it never has. Read back on the next run so a reminder only repeats after the
    /// configured wait, never every time the timer fires (outcome G).</summary>
    public DateTimeOffset? LastMissingDocumentReminderAt { get; init; }
    public required string ClaimType { get; init; }

    /// <summary>The key of the configured claim type this claim was resolved to (build-loop item
    /// 3), or null for a claim whose product has no configured claim types, or whose text did not
    /// match one. ClaimType stays the human-readable text either way.</summary>
    public string? ClaimTypeKey { get; init; }
    public required string Channel { get; init; }
    public required DateOnly IncidentDate { get; init; }
    public required string IncidentDescription { get; init; }

    /// <summary>What the customer actually wrote, when IncidentDescription is a translation of it.
    /// A machine translation must never be the only record of a claim, so the customer's own words
    /// are kept beside it and shown to the person handling the claim.</summary>
    public string? OriginalIncidentText { get; init; }

    /// <summary>ISO code of the language OriginalIncidentText is in, for example "lg".</summary>
    public string? OriginalIncidentLanguage { get; init; }

    /// <summary>The answers to the product's claim fields, keyed by field key (build-loop item 5).
    /// Read through <see cref="InsuranceIntelligence.Domain.Products.ClaimFieldValues.For"/>, which
    /// also reads the original Motor/Medical FNOL records for claims filed before fields existed.</summary>
    public IReadOnlyDictionary<string, string>? FieldValues { get; init; }

    /// <summary>Where each answer came from and who confirmed it (build-loop item 15): the customer,
    /// an officer, or a document an officer accepted it from. A field with no entry was put there by
    /// a person before this was recorded, and is treated the same way: confirmed.</summary>
    public IReadOnlyDictionary<string, InsuranceIntelligence.Domain.Documents.FieldProvenance>? FieldProvenance { get; init; }
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
