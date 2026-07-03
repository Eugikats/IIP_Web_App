using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Domain.Administration;

namespace InsuranceIntelligence.Application.Audit;

public sealed class AuditQueryService(
    ITenantContextAccessor tenantContextAccessor,
    IPermissionService permissionService,
    IAuditReadRepository repository) : IAuditQueryService
{
    private const int MaxPageSize = 200;
    private const string RedactedSummary = "Sensitive audit summary redacted.";

    private static readonly string[] UnsafeSummaryFragments =
    [
        "raw payload",
        "raw provider",
        "provider exception",
        "stack trace",
        "system prompt",
        "hidden prompt",
        "system message",
        "secret",
        "token",
        "connection string",
        "sas url"
    ];

    private static readonly string[] AllowedEventTypes =
    [
        AuditEventTypeValues.UserLogin,
        AuditEventTypeValues.DataViewed,
        AuditEventTypeValues.DataChanged,
        AuditEventTypeValues.DocumentUploaded,
        AuditEventTypeValues.AiExtractionGenerated,
        AuditEventTypeValues.HumanCorrectionMade,
        AuditEventTypeValues.ClaimDecisionMade,
        AuditEventTypeValues.CustomerCommunicationSent,
        AuditEventTypeValues.ConsentCaptured,
        AuditEventTypeValues.WorkflowChanged,
        AuditEventTypeValues.ReportExported
    ];

    public async Task<IReadOnlyList<AuditEventRowDto>> ListEventsAsync(
        AuditEventListQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        Validate(query);

        await permissionService.RequirePermissionAsync(Permission.ViewAuditLog, cancellationToken);

        var context = tenantContextAccessor.Current;
        var events = await repository.ListEventsAsync(
            context.TenantId,
            context.WorkspaceId,
            query,
            cancellationToken);

        return events
            .Select(auditEvent => new AuditEventRowDto(
                auditEvent.Id,
                NormalizeEventType(auditEvent.EventType),
                auditEvent.ActorUserId,
                auditEvent.TargetType,
                auditEvent.TargetId,
                SanitizeSummary(auditEvent.Summary),
                auditEvent.OccurredAt.ToString("O"),
                auditEvent.CorrelationId))
            .ToArray();
    }

    private static void Validate(AuditEventListQuery query)
    {
        if (query.Page < 1)
        {
            throw new AuditQueryValidationException("page", "Page must be greater than zero.");
        }

        if (query.PageSize < 1 || query.PageSize > MaxPageSize)
        {
            throw new AuditQueryValidationException("pageSize", $"Page size must be between 1 and {MaxPageSize}.");
        }

        if (query.FromUtc is not null && query.ToUtc is not null && query.FromUtc > query.ToUtc)
        {
            throw new AuditQueryValidationException("fromUtc", "From UTC must be before To UTC.");
        }
    }

    private static string NormalizeEventType(string eventType) =>
        AllowedEventTypes.Contains(eventType, StringComparer.Ordinal)
            ? eventType
            : AuditEventTypeValues.DataChanged;

    private static string SanitizeSummary(string summary) =>
        UnsafeSummaryFragments.Any(fragment => summary.Contains(fragment, StringComparison.OrdinalIgnoreCase))
            ? RedactedSummary
            : summary;
}
