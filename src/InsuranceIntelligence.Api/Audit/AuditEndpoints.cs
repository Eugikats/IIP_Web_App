using InsuranceIntelligence.Application.Audit;
using InsuranceIntelligence.Application.Core;

namespace InsuranceIntelligence.Api.Audit;

public static class AuditEndpoints
{
    public static async Task<IResult> ListEvents(
        IAuditQueryService queryService,
        CancellationToken cancellationToken,
        string? eventType = null,
        string? actorUserId = null,
        string? targetType = null,
        string? targetId = null,
        DateTimeOffset? fromUtc = null,
        DateTimeOffset? toUtc = null,
        int page = 1,
        int pageSize = 100)
    {
        var query = new AuditEventListQuery(
            eventType,
            actorUserId,
            targetType,
            targetId,
            fromUtc,
            toUtc,
            page,
            pageSize);

        try
        {
            var events = await queryService.ListEventsAsync(query, cancellationToken);
            return Results.Ok(events);
        }
        catch (AuditQueryValidationException ex)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [ex.Field] = [ex.Message]
            });
        }
        catch (PermissionDeniedException)
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }
    }
}
