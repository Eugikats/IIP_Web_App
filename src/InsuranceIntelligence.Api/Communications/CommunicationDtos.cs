namespace InsuranceIntelligence.Api.Communications;

public sealed record SendCommunicationRequest(
    Guid CaseId,
    string Recipient,
    string MessageBody,
    string SentByUserId,
    bool Approved,
    bool LowRisk);

public sealed record SendTeamsNotificationRequest(
    Guid? CaseId,
    string NotificationType,
    string Message,
    string RequestedByUserId);
