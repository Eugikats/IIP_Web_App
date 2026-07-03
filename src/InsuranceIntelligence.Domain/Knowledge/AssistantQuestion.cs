using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Domain.Knowledge;

public sealed record AssistantQuestion(
    TenantScope Scope,
    Guid? CaseId,
    string Question,
    string? ProductLine,
    string Audience,
    string UserId,
    string CorrelationId);
