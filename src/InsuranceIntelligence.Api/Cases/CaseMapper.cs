using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Documents;
using InsuranceIntelligence.Api.Common;
using InsuranceIntelligence.Api.Local;

namespace InsuranceIntelligence.Api.Cases;

public static class CaseMapper
{
    public static CaseResponse ToResponse(ClaimCase claimCase, IReadOnlyCollection<AuditEvent> auditEvents, DocumentCompletenessChecklist? checklist = null)
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
            claimCase.RequiresConfirmation,
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
            CaseActions.Blocked(claimCase),
            checklist is null ? null : new RequiredDocumentsResponse(
                checklist.RequiredDocuments.Select(StaticDocumentChecklistProvider.LabelFor).ToArray(),
                checklist.OptionalDocuments.Select(StaticDocumentChecklistProvider.LabelFor).ToArray(),
                checklist.MissingRequiredDocuments.Select(missing => missing.Label).ToArray()));
    }
}
