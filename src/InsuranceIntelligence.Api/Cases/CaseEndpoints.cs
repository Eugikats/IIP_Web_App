using InsuranceIntelligence.Api.Auth;
using InsuranceIntelligence.Api.Common;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Documents;
using InsuranceIntelligence.Domain.Workflow;

namespace InsuranceIntelligence.Api.Cases;

public static class CaseEndpoints
{
    public static async Task<IResult> CreateCase(
        CreateCaseRequest request,
        CaseIntakeService service,
        IAuditRepository auditRepository,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!TenantScopeReader.TryGetFromHeadersOrQuery(httpContext, out var scope, out var validationError))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["tenantScope"] = [validationError]
            });
        }

        var actor = DemoAuthorization.GetUserId(httpContext, fallback: string.Empty);
        if (string.IsNullOrWhiteSpace(actor))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["userId"] = ["Authenticated user context is required to create a case."]
            });
        }

        try
        {
            var correlationId = GetCorrelationId(httpContext);
            var command = new CreateCaseCommand(
                scope,
                request.CustomerName ?? string.Empty,
                request.CustomerEmail,
                request.CustomerPhone,
                request.PolicyNumber ?? string.Empty,
                request.ProductLine ?? string.Empty,
                request.ClaimType ?? string.Empty,
                request.Channel ?? string.Empty,
                request.IncidentDate ?? default,
                request.IncidentDescription ?? string.Empty,
                actor,
                correlationId);

            var claimCase = await service.CreateCaseAsync(command, cancellationToken);
            var auditEvents = await auditRepository.ListForCaseAsync(claimCase.CaseId, cancellationToken);
            var response = CaseMapper.ToResponse(claimCase, auditEvents);

            return Results.Created($"/api/v1/cases/{claimCase.CaseId}", response);
        }
        catch (ArgumentException ex)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [ex.ParamName ?? "request"] = [ex.Message]
            });
        }
    }

    public static async Task<IResult> GetCase(
        Guid caseId,
        CaseIntakeService service,
        IAuditRepository auditRepository,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!TenantScopeReader.TryGetFromHeadersOrQuery(httpContext, out var scope, out var validationError))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["tenantScope"] = [validationError]
            });
        }

        var claimCase = await service.GetCaseAsync(caseId, scope, cancellationToken);
        if (claimCase is null)
        {
            return Results.NotFound(new { message = "Case was not found for the requested tenant workspace." });
        }

        if (!ClaimAccessGuard.CanViewCase(httpContext, claimCase))
        {
            return Results.NotFound(new { message = "Case was not found for the requested tenant workspace." });
        }

        var auditEvents = await auditRepository.ListForCaseAsync(caseId, cancellationToken);
        return Results.Ok(CaseMapper.ToResponse(claimCase, auditEvents));
    }

    public static async Task<IResult> ListCases(
        ICaseRepository caseRepository,
        ICaseDocumentRepository documentRepository,
        IDocumentChecklistProvider checklistProvider,
        IAuditRepository auditRepository,
        ISlaCalculator slaCalculator,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!TenantScopeReader.TryGetFromHeadersOrQuery(httpContext, out var scope, out var validationError))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["tenantScope"] = [validationError]
            });
        }

        var cases = ClaimAccessGuard.FilterVisibleCases(
            httpContext,
            await caseRepository.ListAsync(scope, cancellationToken));
        var responses = new List<ClaimInboxItemResponse>();

        foreach (var claimCase in cases)
        {
            var documents = await documentRepository.ListForCaseAsync(claimCase.CaseId, scope, cancellationToken);
            var checklist = await checklistProvider.BuildChecklistAsync(claimCase, documents, cancellationToken);
            var auditEvents = await auditRepository.ListForCaseAsync(claimCase.CaseId, cancellationToken);
            var lastEvent = auditEvents.OrderByDescending(auditEvent => auditEvent.Timestamp).FirstOrDefault();
            var documentStatus = documents.Count == 0
                ? "New"
                : checklist.MissingRequiredDocuments.Count > 0
                    ? "Missing Documents"
                    : "Ready For Review";
            var slaStatus = slaCalculator.Calculate(claimCase, DateTimeOffset.UtcNow);

            responses.Add(new ClaimInboxItemResponse(
                claimCase.CaseId,
                claimCase.CustomerName,
                claimCase.PolicyNumber,
                claimCase.ProductLine,
                claimCase.ClaimType,
                claimCase.Channel,
                claimCase.Status.ToString(),
                claimCase.CompletenessScore,
                documentStatus,
                claimCase.RiskLevel,
                claimCase.Priority.ToString(),
                slaStatus.ToString(),
                claimCase.AssignedUserId,
                claimCase.AssignedTeam,
                claimCase.UpdatedAt,
                new SafeAuditSummary(
                    auditEvents.Count,
                    lastEvent?.ActionName,
                    lastEvent?.Timestamp,
                    lastEvent?.CorrelationId),
                CaseActions.Allowed(claimCase),
                CaseActions.Blocked(claimCase)));
        }

        return Results.Ok(responses);
    }

    private static string GetCorrelationId(HttpContext httpContext)
    {
        if (httpContext.Request.Headers.TryGetValue("X-Correlation-ID", out var header)
            && !string.IsNullOrWhiteSpace(header))
        {
            return header.ToString();
        }

        return httpContext.TraceIdentifier;
    }

}
