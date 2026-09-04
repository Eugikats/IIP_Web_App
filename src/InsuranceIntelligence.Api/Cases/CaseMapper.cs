using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Api.Common;

namespace InsuranceIntelligence.Api.Cases;

public static class CaseMapper
{
    public static CaseResponse ToResponse(ClaimCase claimCase, IReadOnlyCollection<AuditEvent> auditEvents)
    {
        var lastEvent = auditEvents.OrderByDescending(x => x.Timestamp).FirstOrDefault();

        return new CaseResponse(
            claimCase.CaseId,
            claimCase.Scope.TenantId,
            claimCase.Scope.WorkspaceId,
            claimCase.CustomerName,
            claimCase.CustomerEmail,
            claimCase.CustomerPhone,
            claimCase.PolicyNumber,
            claimCase.ProductLine,
            claimCase.ClaimType,
            claimCase.Channel,
            claimCase.IncidentDate,
            claimCase.IncidentDescription,
            claimCase.MotorFnolDetails,
            claimCase.MedicalFnolDetails,
            claimCase.Status.ToString(),
            claimCase.CompletenessScore,
            claimCase.RiskLevel,
            claimCase.Priority.ToString(),
            claimCase.SlaDueAt,
            claimCase.AssignedUserId,
            claimCase.AssignedTeam,
            claimCase.CreatedByUserId,
            claimCase.CreatedAt,
            claimCase.UpdatedAt,
            claimCase.CorrelationId,
            new SafeAuditSummary(
                auditEvents.Count,
                lastEvent?.ActionName,
                lastEvent?.Timestamp,
                lastEvent?.CorrelationId),
            CaseActions.Allowed(claimCase),
            CaseActions.Blocked(claimCase));
    }
}
