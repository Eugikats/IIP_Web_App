using InsuranceIntelligence.Domain.Cases;

namespace InsuranceIntelligence.Api.Cases;

public sealed record CreateCaseRequest(
    string? CustomerName,
    string? CustomerEmail,
    string? CustomerPhone,
    string? PolicyNumber,
    string? ProductLine,
    string? ClaimType,
    string? Channel,
    DateOnly? IncidentDate,
    string? IncidentDescription,
    MotorFnolDetails? MotorFnolDetails = null,
    MedicalFnolDetails? MedicalFnolDetails = null,
    decimal? ClaimedAmount = null);
