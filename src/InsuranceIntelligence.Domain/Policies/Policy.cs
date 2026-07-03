namespace InsuranceIntelligence.Domain.Policies;

public sealed record Policy(
    string PolicyId,
    string TenantId,
    string WorkspaceId,
    string PolicyNumber,
    string ProductLine,
    string? CoverType,
    string? CustomerId,
    string? BranchId,
    DateOnly? EffectiveFrom,
    DateOnly? EffectiveTo);
