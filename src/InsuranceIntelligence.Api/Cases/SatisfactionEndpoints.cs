using InsuranceIntelligence.Api.Workflow;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Workflow;

namespace InsuranceIntelligence.Api.Cases;

/// <summary>Outcome O: "open a service recovery task on a poor answer." The rating is officer
/// observed (see ClaimSatisfaction.cs), never inferred automatically from an inbound reply.</summary>
public static class SatisfactionEndpoints
{
    public static async Task<IResult> RecordResponse(
        Guid caseId,
        RecordSatisfactionResponseRequest request,
        ICaseRepository caseRepository,
        ISatisfactionResponseRepository responseRepository,
        IWorkflowTaskRepository taskRepository,
        IAuditRepository auditRepository,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!WorkflowEndpoints.TryResolveScope(httpContext, out var scope, out _, out var validationError))
        {
            return WorkflowEndpoints.Validation("tenantScope", validationError);
        }

        var claimCase = await WorkflowEndpoints.GetScopedCaseAsync(caseRepository, caseId, scope, httpContext, cancellationToken);
        if (claimCase is null)
        {
            return WorkflowEndpoints.NotFoundCase();
        }

        if (!Enum.TryParse<SatisfactionRating>(request.Rating, ignoreCase: true, out var rating))
        {
            return WorkflowEndpoints.Validation("rating", "Rating must be 'Good', 'Neutral' or 'Poor'.");
        }

        var actor = WorkflowEndpoints.GetActor(httpContext, null);
        if (string.IsNullOrWhiteSpace(actor))
        {
            return WorkflowEndpoints.Validation("userId", "Authenticated user context is required to record a satisfaction answer.");
        }

        var correlationId = WorkflowEndpoints.GetCorrelationId(httpContext);
        var now = DateTimeOffset.UtcNow;
        var response = new SatisfactionResponse
        {
            ResponseId = Guid.NewGuid(),
            Scope = scope,
            CaseId = caseId,
            Rating = rating,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
            RecordedByUserId = actor,
            RecordedAt = now,
            CorrelationId = correlationId
        };

        await responseRepository.SaveAsync(response, cancellationToken);
        await auditRepository.WriteAsync(AuditEvent.SatisfactionResponseRecorded(scope, caseId, actor, rating.ToString(), correlationId), cancellationToken);

        Guid? openedTaskId = null;
        if (rating == SatisfactionRating.Poor)
        {
            var task = new WorkflowTask
            {
                TaskId = Guid.NewGuid(),
                Scope = scope,
                CaseId = caseId,
                TaskType = WorkflowTaskType.ServiceRecoveryFollowUp,
                Status = WorkflowTaskStatus.Open,
                Title = $"Service recovery: {claimCase.CustomerName}",
                Summary = $"Poor satisfaction answer recorded for claim {claimCase.PolicyNumber}. Follow up with the customer.",
                AssignedTeam = "Claims Supervision",
                CreatedByUserId = "system",
                CreatedAt = now,
                UpdatedAt = now,
                CorrelationId = correlationId
            };
            await taskRepository.SaveAsync(task, cancellationToken);
            await auditRepository.WriteAsync(AuditEvent.ServiceRecoveryTaskOpened(scope, caseId, actor, task.TaskId, correlationId), cancellationToken);
            openedTaskId = task.TaskId;
        }

        return Results.Ok(new SatisfactionResponseRecordedResponse(response.ResponseId, response.Rating.ToString(), openedTaskId));
    }

    public static async Task<IResult> ListResponses(
        Guid caseId,
        ICaseRepository caseRepository,
        ISatisfactionResponseRepository responseRepository,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!WorkflowEndpoints.TryResolveScope(httpContext, out var scope, out _, out var validationError))
        {
            return WorkflowEndpoints.Validation("tenantScope", validationError);
        }

        var claimCase = await WorkflowEndpoints.GetScopedCaseAsync(caseRepository, caseId, scope, httpContext, cancellationToken);
        if (claimCase is null)
        {
            return WorkflowEndpoints.NotFoundCase();
        }

        var responses = await responseRepository.ListForCaseAsync(caseId, scope, cancellationToken);
        return Results.Ok(responses.Select(response => new SatisfactionResponseDto(
            response.ResponseId,
            response.Rating.ToString(),
            response.Notes,
            response.RecordedByUserId,
            response.RecordedAt)).ToArray());
    }
}

public sealed record RecordSatisfactionResponseRequest(string Rating, string? Notes);

public sealed record SatisfactionResponseRecordedResponse(Guid ResponseId, string Rating, Guid? ServiceRecoveryTaskId);

public sealed record SatisfactionResponseDto(Guid ResponseId, string Rating, string? Notes, string RecordedByUserId, DateTimeOffset RecordedAt);
