using InsuranceIntelligence.Api.Common;
using InsuranceIntelligence.Api.Workflow;
using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Application.Fraud;
using InsuranceIntelligence.Domain.Fraud;

namespace InsuranceIntelligence.Api.Fraud;

// WO-19: a raised signal can now be worked to closure — triage, assign for investigation, record
// outcome, close — with a real actor and a real audit reason on every transition (I7). Every
// method delegates its permission check and workflow-state validation to FraudDispositionService;
// this layer only reads/validates the HTTP-shaped request.
public static class FraudDispositionEndpoints
{
    public static async Task<IResult> Triage(
        TriageFraudSignalRequest request,
        IFraudDispositionService service,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RuleId) || request.RelatedCaseIds is null || request.RelatedCaseIds.Count == 0)
        {
            return Validation("ruleId", "A rule ID and at least one related case are required to triage a signal.");
        }

        if (!TryReadAuditReason(httpContext, out var auditReason, out var reasonError))
        {
            return reasonError!;
        }

        try
        {
            var result = await service.TriageAsync(
                request.RuleId, request.RelatedCaseIds, auditReason, GetCorrelationId(httpContext), cancellationToken);
            return Results.Ok(result);
        }
        catch (PermissionDeniedException)
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }
        catch (FraudDispositionConflictException exception)
        {
            return Conflict(exception);
        }
    }

    public static async Task<IResult> Assign(
        Guid signalId,
        AssignFraudSignalRequest request,
        IFraudDispositionService service,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var assignedToUserId = string.IsNullOrWhiteSpace(request.AssignedToUserId)
            ? WorkflowEndpoints.GetActor(httpContext, null)
            : request.AssignedToUserId.Trim();
        if (string.IsNullOrWhiteSpace(assignedToUserId))
        {
            return Validation("assignedToUserId", "An assignee is required to assign a signal for investigation.");
        }

        if (!TryReadAuditReason(httpContext, out var auditReason, out var reasonError))
        {
            return reasonError!;
        }

        try
        {
            var result = await service.AssignAsync(signalId, assignedToUserId, auditReason, GetCorrelationId(httpContext), cancellationToken);
            return Results.Ok(result);
        }
        catch (PermissionDeniedException)
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }
        catch (FraudSignalNotFoundException)
        {
            return NotFound();
        }
        catch (FraudDispositionConflictException exception)
        {
            return Conflict(exception);
        }
    }

    public static async Task<IResult> RecordOutcome(
        Guid signalId,
        RecordFraudDispositionOutcomeRequest request,
        IFraudDispositionService service,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<FraudDispositionOutcome>(request.Outcome, ignoreCase: true, out var outcome))
        {
            return Validation("outcome", "A supported disposition outcome is required.");
        }

        if (!TryReadAuditReason(httpContext, out var auditReason, out var reasonError))
        {
            return reasonError!;
        }

        try
        {
            var result = await service.RecordOutcomeAsync(
                signalId, outcome, request.Notes, auditReason, GetCorrelationId(httpContext), cancellationToken);
            return Results.Ok(result);
        }
        catch (PermissionDeniedException)
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }
        catch (FraudSignalNotFoundException)
        {
            return NotFound();
        }
        catch (FraudDispositionConflictException exception)
        {
            return Conflict(exception);
        }
    }

    public static async Task<IResult> Close(
        Guid signalId,
        IFraudDispositionService service,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!TryReadAuditReason(httpContext, out var auditReason, out var reasonError))
        {
            return reasonError!;
        }

        try
        {
            var result = await service.CloseAsync(signalId, auditReason, GetCorrelationId(httpContext), cancellationToken);
            return Results.Ok(result);
        }
        catch (PermissionDeniedException)
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }
        catch (FraudSignalNotFoundException)
        {
            return NotFound();
        }
        catch (FraudDispositionConflictException exception)
        {
            return Conflict(exception);
        }
    }

    private static bool TryReadAuditReason(HttpContext httpContext, out string auditReason, out IResult? error)
    {
        var reason = AuditReasonReader.Read(httpContext);
        if (string.IsNullOrWhiteSpace(reason))
        {
            auditReason = string.Empty;
            error = Validation("auditReason", "An audit reason is required for every fraud signal disposition transition.");
            return false;
        }

        auditReason = reason;
        error = null;
        return true;
    }

    private static IResult NotFound() =>
        Results.NotFound(new { message = "Fraud signal disposition was not found for the requested tenant workspace." });

    private static IResult Conflict(FraudDispositionConflictException exception) =>
        Results.Conflict(new { message = exception.Message, blockedAction = exception.BlockedAction });

    private static IResult Validation(string key, string message) =>
        Results.ValidationProblem(new Dictionary<string, string[]> { [key] = [message] });

    private static string GetCorrelationId(HttpContext httpContext) =>
        httpContext.Request.Headers.TryGetValue("X-Correlation-ID", out var header) && !string.IsNullOrWhiteSpace(header)
            ? header.ToString()
            : httpContext.TraceIdentifier;
}
