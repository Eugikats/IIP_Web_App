using InsuranceIntelligence.Api.Common;
using InsuranceIntelligence.Api.Workflow;
using InsuranceIntelligence.Application.CustomerAssistant;

namespace InsuranceIntelligence.Api.CustomerAssistant;

public static class CustomerFollowUpEndpoints
{
    public static async Task<IResult> List(
        ICustomerFollowUpManagementService service,
        CancellationToken cancellationToken)
    {
        try
        {
            return Results.Ok(await service.ListAsync(cancellationToken));
        }
        catch (InsuranceIntelligence.Application.Core.PermissionDeniedException)
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }
    }

    public static async Task<IResult> Cancel(
        Guid taskId,
        ICustomerFollowUpManagementService service,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var reason = AuditReasonReader.Read(httpContext);
        try
        {
            var result = await service.CancelAsync(taskId, reason ?? string.Empty,
                WorkflowEndpoints.GetCorrelationId(httpContext), cancellationToken);
            return result is null
                ? Results.NotFound(new { message = "Follow-up task was not found for the current tenant workspace." })
                : Results.Ok(result);
        }
        catch (InsuranceIntelligence.Application.Core.PermissionDeniedException)
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }
        catch (CustomerFollowUpManagementValidationException exception)
        {
            return WorkflowEndpoints.Validation(exception.Field, exception.Message);
        }
        catch (CustomerFollowUpManagementConflictException exception)
        {
            return Results.Conflict(new { message = exception.Message });
        }
    }
}
