using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Workflow;

namespace InsuranceIntelligence.Domain.Communications;

public sealed record Communication
{
    public required Guid CommunicationId { get; init; }
    public required TenantScope Scope { get; init; }
    public required Guid CaseId { get; init; }
    public string? CustomerId { get; init; }
    public required CommunicationChannel Channel { get; init; }
    public required string Direction { get; init; }
    public required string MessageBody { get; init; }
    public string? TemplateId { get; init; }
    public required string SentBy { get; init; }
    public required string DeliveryStatus { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required string CorrelationId { get; init; }

    /// <summary>The sending provider's own message id (e.g. Africa's Talking's messageId or
    /// EgoSMS's MsgFollowUpUniqueCode), the correlation key a delivery-status webhook uses to
    /// find this communication again and update DeliveryStatus from "Sent" to a real outcome.
    /// Null for channels/providers that don't return one, or for inbound messages.</summary>
    public string? ProviderMessageId { get; init; }
}
