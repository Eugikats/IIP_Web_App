using InsuranceIntelligence.Api.Auth;
using InsuranceIntelligence.Api.Workflow;
using InsuranceIntelligence.Application.BankEvents;
using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Infrastructure.Configuration;

namespace InsuranceIntelligence.Api.BankEvents;

/// <summary>
/// Bank events: see them, record one, or load a file of them. For people who manage sales leads. The audit trail records
/// that an event was recorded or a file loaded, by id and name; the phone number and details stay out of it.
/// </summary>
public static class EventEndpoints
{
    public static IResult Types() => Results.Ok(BankEventCatalog.All.Select(type => new
    {
        code = type.Code,
        label = type.Label,
        reason = type.Reason,
        suggestedOffer = type.SuggestedOffer,
        suggestedTiming = type.SuggestedTiming,
        isTimeTrigger = type.IsTimeTrigger,
    }));

    public static async Task<IResult> List(
        string? state, string? type, int? take, EventService events, InfrastructureOptions options, HttpContext httpContext, CancellationToken cancellationToken)
    {
        if (Resolve(httpContext, options, out var scope, out _) is { } failed)
        {
            return failed;
        }

        EventProcessingState? parsed = Enum.TryParse<EventProcessingState>(state, ignoreCase: true, out var value) ? value : null;
        var items = await events.ListAsync(scope, parsed, type, take ?? 200, cancellationToken);
        return Results.Ok(items.Select(Describe));
    }

    public static async Task<IResult> Record(
        EventRequest request, EventService events, IAuditWriter audit, InfrastructureOptions options, HttpContext httpContext, CancellationToken cancellationToken)
    {
        if (Resolve(httpContext, options, out var scope, out var userId) is { } failed)
        {
            return failed;
        }

        try
        {
            var result = await events.RecordAsync(
                scope,
                new EventInput(request.EventType, request.Phone, request.OccurredAt, request.ExternalId, request.Amount, request.TermMonths, request.ProductCode, request.Branch),
                EventSource.Recorded, null, userId, cancellationToken);
            if (result.Created)
            {
                await audit.WriteAsync(
                    AuditEvent.WorkspaceSettingChanged(scope, userId, "event", "recorded", result.Event.EventId, $"A {result.Event.EventType} event was recorded.", httpContext.TraceIdentifier),
                    cancellationToken);
            }

            return Results.Ok(new { created = result.Created, @event = Describe(result.Event) });
        }
        catch (EventException problem)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["event"] = [problem.Message] });
        }
    }

    public static async Task<IResult> Import(
        EventImportRequest request, EventService events, IAuditWriter audit, InfrastructureOptions options, HttpContext httpContext, CancellationToken cancellationToken)
    {
        if (Resolve(httpContext, options, out var scope, out var userId) is { } failed)
        {
            return failed;
        }

        var name = string.IsNullOrWhiteSpace(request.FileName) ? "uploaded file" : request.FileName.Trim();
        try
        {
            var result = await events.ImportCsvAsync(scope, name, request.Csv ?? string.Empty, userId, cancellationToken);
            await audit.WriteAsync(
                AuditEvent.WorkspaceSettingChanged(scope, userId, "event", "imported", name, $"A file of events was loaded: {result.Created} new, {result.Duplicates} already known, {result.Rejected.Count} refused.", httpContext.TraceIdentifier),
                cancellationToken);
            return Results.Ok(result);
        }
        catch (EventException problem)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["file"] = [problem.Message] });
        }
    }

    private static IResult? Resolve(HttpContext httpContext, InfrastructureOptions options, out InsuranceIntelligence.Domain.Common.TenantScope scope, out string userId)
    {
        scope = default!;
        userId = string.Empty;
        if (DemoAuthorization.Require(httpContext, options, Permission.ManageSalesLeads) is { } forbidden)
        {
            return forbidden;
        }

        if (!WorkflowEndpoints.TryResolveScope(httpContext, out scope, out userId, out var scopeError))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["tenantScope"] = [scopeError] });
        }

        return string.IsNullOrWhiteSpace(userId)
            ? Results.ValidationProblem(new Dictionary<string, string[]> { ["userId"] = ["A signed-in user is required."] })
            : null;
    }

    private static object Describe(WorkspaceEvent item)
    {
        var type = BankEventCatalog.Find(item.EventType);
        return new
        {
            item.EventId,
            item.EventType,
            label = type?.Label ?? item.EventType,
            phone = MaskPhone(item.Phone),
            item.OccurredAt,
            item.ReceivedAt,
            source = item.Source.ToString(),
            item.SourceReference,
            details = item.Details,
            state = item.State.ToString(),
            item.ProcessedAt,
        };
    }

    /// <summary>The list shows enough of a number to tell people apart, not the whole number.</summary>
    internal static string MaskPhone(string phone) => phone.Length <= 6 ? phone : $"+{phone[..3]} {new string('*', Math.Max(phone.Length - 6, 1))} {phone[^3..]}";
}

public sealed record EventRequest(
    string? EventType, string? Phone, DateTimeOffset? OccurredAt, string? ExternalId, decimal? Amount, int? TermMonths, string? ProductCode, string? Branch);

public sealed record EventImportRequest(string? FileName, string? Csv);
