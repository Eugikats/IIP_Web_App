using InsuranceIntelligence.Api.Auth;
using InsuranceIntelligence.Api.Common;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Compliance;
using InsuranceIntelligence.Domain.Documents;
using InsuranceIntelligence.Domain.Knowledge;
using InsuranceIntelligence.Domain.Workflow;
using InsuranceIntelligence.Infrastructure.Configuration;

namespace InsuranceIntelligence.Api.Compliance;

public static class ComplianceEndpoints
{
    public static async Task<IResult> ListAuditEvents(
        IAuditRepository auditRepository,
        InfrastructureOptions options,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (DemoAuthorization.Require(httpContext, options, Permission.ViewAuditLog) is { } forbidden)
        {
            return forbidden;
        }

        if (!TenantScopeReader.TryGetFromHeadersOrQuery(httpContext, out var scope, out var validationError))
        {
            return Validation("tenantScope", validationError);
        }

        var events = await auditRepository.ListAsync(scope, cancellationToken);
        var filtered = events.AsEnumerable();
        if (httpContext.Request.Query.TryGetValue("caseId", out var caseIdValue)
            && Guid.TryParse(caseIdValue, out var caseId))
        {
            filtered = filtered.Where(auditEvent => auditEvent.CaseId == caseId);
        }

        if (httpContext.Request.Query.TryGetValue("eventType", out var eventType)
            && !string.IsNullOrWhiteSpace(eventType))
        {
            filtered = filtered.Where(auditEvent => auditEvent.ActionName.Contains(eventType.ToString(), StringComparison.OrdinalIgnoreCase));
        }

        if (httpContext.Request.Query.TryGetValue("actor", out var actor)
            && !string.IsNullOrWhiteSpace(actor))
        {
            filtered = filtered.Where(auditEvent => string.Equals(auditEvent.UserId, actor.ToString(), StringComparison.OrdinalIgnoreCase));
        }

        return Results.Ok(filtered.ToArray());
    }

    public static async Task<IResult> GetAuditPack(
        Guid caseId,
        ICaseRepository caseRepository,
        IAuditRepository auditRepository,
        ICaseDocumentRepository documentRepository,
        IAgentActionLogRepository actionLogRepository,
        IApprovalRepository approvalRepository,
        ICustomerUpdateRepository updateRepository,
        InfrastructureOptions options,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (DemoAuthorization.Require(httpContext, options, Permission.ViewAuditLog) is { } forbidden)
        {
            return forbidden;
        }

        if (!TenantScopeReader.TryGetFromHeadersOrQuery(httpContext, out var scope, out var validationError))
        {
            return Validation("tenantScope", validationError);
        }

        var claimCase = await caseRepository.GetAsync(caseId, cancellationToken);
        if (claimCase is null || !claimCase.Scope.Matches(scope))
        {
            return Results.NotFound(new { message = "Case was not found for the requested tenant workspace." });
        }

        var pack = AuditPack.Build(
            claimCase,
            await auditRepository.ListForCaseAsync(caseId, cancellationToken),
            await documentRepository.ListForCaseAsync(caseId, scope, cancellationToken),
            await actionLogRepository.ListForCaseAsync(caseId, scope, cancellationToken),
            await approvalRepository.ListForCaseAsync(caseId, scope, cancellationToken),
            await updateRepository.ListForCaseAsync(caseId, scope, cancellationToken));

        return Results.Ok(pack);
    }

    private static IResult Validation(string key, string message) =>
        Results.ValidationProblem(new Dictionary<string, string[]> { [key] = [message] });
}
