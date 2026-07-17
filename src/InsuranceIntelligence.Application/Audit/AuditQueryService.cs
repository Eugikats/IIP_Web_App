using System.Text.RegularExpressions;
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
    private static readonly Regex EmailPattern = new(
        @"(?<![A-Za-z0-9._%+-])[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}(?![A-Za-z0-9._%+-])",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase,
        TimeSpan.FromMilliseconds(100));

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
                MinimizeIdentifier(auditEvent.ActorUserId),
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
            : EmailPattern.Replace(summary, MaskEmail);

    private static string? MinimizeIdentifier(string? identifier) =>
        string.IsNullOrWhiteSpace(identifier)
            ? identifier
            : EmailPattern.Replace(identifier, MaskEmail);

    private static string MaskEmail(Match match)
    {
        var email = match.Value;
        var atIndex = email.IndexOf('@', StringComparison.Ordinal);
        if (atIndex <= 0)
        {
            return "[email]";
        }

        var local = email[..atIndex];
        var domain = email[(atIndex + 1)..];
        var first = char.ToLowerInvariant(local[0]);
        return $"{first}***@{domain.ToLowerInvariant()}";
    }
}
