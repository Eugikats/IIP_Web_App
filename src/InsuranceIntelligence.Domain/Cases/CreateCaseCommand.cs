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
    MotorFnolDetails? MotorFnolDetails = null);
