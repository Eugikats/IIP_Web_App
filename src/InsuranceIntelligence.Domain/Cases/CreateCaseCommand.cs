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
    /// <summary>True for a claim IIP created on its own from an identified WhatsApp/email
    /// sender, before any human has looked at it. Such a case starts no SLA clock (CaseIntakeService
    /// leaves SlaDueAt null) and is excluded from SLA/attention metrics for exactly that reason,
    /// see ClaimCase.RequiresConfirmation. A human confirming it is what starts the real SLA.</summary>
    bool RequiresConfirmation = false);
