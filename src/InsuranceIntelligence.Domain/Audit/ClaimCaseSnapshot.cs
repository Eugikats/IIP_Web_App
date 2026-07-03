using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Domain.Audit;

public sealed record ClaimCaseSnapshot(
    Guid CaseId,
    TenantScope Scope,
    string Status);
