using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Domain.Cases;

public sealed record CreateCaseCommand(
    TenantScope Scope,
    string CustomerName,
    string? CustomerEmail,
    string? CustomerPhone,
    string PolicyNumber,
    string ProductLine,
    string ClaimType,
    string Channel,
    DateOnly IncidentDate,
    string IncidentDescription,
    string CreatedByUserId,
    string CorrelationId,
    MotorFnolDetails? MotorFnolDetails = null,
    MedicalFnolDetails? MedicalFnolDetails = null,
    /// <summary>The amount being claimed, when the submitter provides one. Feeds
    /// PolicyRiskSignalEvaluator's advisory check against the policy's sum insured.</summary>
    decimal? ClaimedAmount = null,
    /// <summary>True for a claim IIP created on its own from an identified WhatsApp/email
    /// sender, before any human has looked at it. Such a case starts no SLA clock (CaseIntakeService
    /// leaves SlaDueAt null) and is excluded from SLA/attention metrics for exactly that reason,
    /// see ClaimCase.RequiresConfirmation. A human confirming it is what starts the real SLA.</summary>
    bool RequiresConfirmation = false,
    string? OriginalIncidentText = null,
    string? OriginalIncidentLanguage = null,
    /// <summary>True for a person filling in a form (staff or claimant): when the claim's product
    /// has configured claim types, the claim type must be one of them, otherwise intake throws
    /// ClaimTypeNotConfiguredException. Automatic channels (email, WhatsApp) leave this false and
    /// get a best-guess type that staff confirm.</summary>
    bool RequireConfiguredClaimType = false,
    /// <summary>Answers to the product's claim fields, keyed by field key. Validated against the
    /// definitions for the claim's product and claim type.</summary>
    IReadOnlyDictionary<string, string>? FieldValues = null,
    /// <summary>True when the claimant filled the form themselves: only customer-visible fields
    /// are accepted.</summary>
    bool FieldsFromCustomer = false);
