using InsuranceIntelligence.Api.Workflow;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Customers;

namespace InsuranceIntelligence.Api.Customers;

/// <summary>
/// Staff surface for policyholder-initiated contact-detail change requests (build prompt outcome
/// L). Mirrors InsuranceIntelligence.Api.Contestability.ContestabilityEndpoints: list / acknowledge
/// / resolve. Resolving here never itself edits Customer.Email/Phone -- applying the change is a
/// separate, deliberate action a staff member takes with the existing customer-record editing
/// path, this only records that the request was reviewed and what was decided.
/// </summary>
public static class ContactDetailChangeEndpoints
{
    public static async Task<IResult> ListChangeRequests(
        IContactDetailChangeRequestRepository changeRequestRepository,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!WorkflowEndpoints.TryResolveScope(httpContext, out var scope, out _, out var validationError))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["tenantScope"] = [validationError]
            });
        }

        var requests = await changeRequestRepository.ListAsync(scope, cancellationToken);
        return Results.Ok(requests
            .OrderByDescending(request => request.RequestedAt)
            .Select(ToResponse)
            .ToArray());
    }

    public static async Task<IResult> AcknowledgeChangeRequest(
        Guid requestId,
        IContactDetailChangeRequestRepository changeRequestRepository,
        IAuditRepository auditRepository,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!WorkflowEndpoints.TryResolveScope(httpContext, out var scope, out _, out var validationError))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["tenantScope"] = [validationError]
            });
        }

        var existing = await changeRequestRepository.GetAsync(requestId, scope, cancellationToken);
        if (existing is null)
        {
            return Results.NotFound(new { message = "Contact-detail change request was not found for the requested tenant workspace." });
        }

        var actor = WorkflowEndpoints.GetActor(httpContext, "claims-officer");
        var correlationId = httpContext.TraceIdentifier;
        var acknowledged = existing.Acknowledge();
        await changeRequestRepository.SaveAsync(acknowledged, cancellationToken);
        if (acknowledged.Status != existing.Status)
        {
            await auditRepository.WriteAsync(
                AuditEvent.ContactDetailChangeAcknowledged(acknowledged, actor, correlationId),
                cancellationToken);
        }

        return Results.Ok(ToResponse(acknowledged));
    }

    public static async Task<IResult> ResolveChangeRequest(
        Guid requestId,
        StaffResolveContactDetailChangeBody body,
        IContactDetailChangeRequestRepository changeRequestRepository,
        IAuditRepository auditRepository,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!WorkflowEndpoints.TryResolveScope(httpContext, out var scope, out _, out var validationError))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["tenantScope"] = [validationError]
            });
        }

        var existing = await changeRequestRepository.GetAsync(requestId, scope, cancellationToken);
        if (existing is null)
        {
            return Results.NotFound(new { message = "Contact-detail change request was not found for the requested tenant workspace." });
        }

        var actor = WorkflowEndpoints.GetActor(httpContext, "claims-officer");
        var correlationId = httpContext.TraceIdentifier;
        var resolved = existing.Resolve(actor, body?.ResolutionNote);
        await changeRequestRepository.SaveAsync(resolved, cancellationToken);
        if (resolved.Status != existing.Status || resolved.ResolvedAt != existing.ResolvedAt)
        {
            await auditRepository.WriteAsync(
                AuditEvent.ContactDetailChangeResolved(resolved, actor, correlationId),
                cancellationToken);
        }

        return Results.Ok(ToResponse(resolved));
    }

    private static StaffContactDetailChangeResponse ToResponse(ContactDetailChangeRequest request) =>
        new(
            request.RequestId,
            request.PolicyNumber,
            request.CaseId,
            request.Status.ToString(),
            request.RequestedByUserId,
            request.RequestedEmail,
            request.RequestedPhone,
            request.Note,
            request.RequestedAt,
            request.ResolvedAt,
            request.ResolvedByUserId,
            request.ResolutionNote);
}

public sealed record StaffResolveContactDetailChangeBody(string? ResolutionNote);

public sealed record StaffContactDetailChangeResponse(
    Guid RequestId,
    string PolicyNumber,
    Guid? CaseId,
    string Status,
    string RequestedByUserId,
    string? RequestedEmail,
    string? RequestedPhone,
    string? Note,
    DateTimeOffset RequestedAt,
    DateTimeOffset? ResolvedAt,
    string? ResolvedByUserId,
    string? ResolutionNote);
