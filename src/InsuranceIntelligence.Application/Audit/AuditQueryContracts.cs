using InsuranceIntelligence.Application.Core;

namespace InsuranceIntelligence.Application.Audit;

public interface IAuditQueryService
{
    Task<IReadOnlyList<AuditEventRowDto>> ListEventsAsync(
        AuditEventListQuery query,
        CancellationToken cancellationToken = default);
}

public interface IAuditReadRepository
{
    Task<IReadOnlyList<AuditEventReadModel>> ListEventsAsync(
        string tenantId,
        string workspaceId,
        AuditEventListQuery query,
        CancellationToken cancellationToken = default);
}

public sealed record AuditEventListQuery(
    string? EventType = null,
    string? ActorUserId = null,
    string? TargetType = null,
    string? TargetId = null,
    DateTimeOffset? FromUtc = null,
    DateTimeOffset? ToUtc = null,
    int Page = 1,
    int PageSize = 100);

public sealed record AuditEventReadModel(
    string Id,
    string EventType,
    string? ActorUserId,
    string? TargetType,
    string? TargetId,
    string Summary,
    DateTimeOffset OccurredAt,
    string? CorrelationId);

public sealed record AuditEventRowDto(
    string Id,
    string EventType,
    string? ActorUserId,
    string? TargetType,
    string? TargetId,
    string Summary,
    string OccurredAt,
    string? CorrelationId);

public static class AuditEventTypeValues
{
    public const string UserLogin = "user_login";
    public const string DataViewed = "data_viewed";
    public const string DataChanged = "data_changed";
    public const string DocumentUploaded = "document_uploaded";
    public const string AiExtractionGenerated = "ai_extraction_generated";
    public const string HumanCorrectionMade = "human_correction_made";
    public const string ClaimDecisionMade = "claim_decision_made";
    public const string CustomerCommunicationSent = "customer_communication_sent";
    public const string ConsentCaptured = "consent_captured";
    public const string WorkflowChanged = "workflow_changed";
    public const string ReportExported = "report_exported";
}

public sealed class AuditQueryValidationException(string field, string message) : InvalidOperationException(message)
{
    public string Field { get; } = field;
}
