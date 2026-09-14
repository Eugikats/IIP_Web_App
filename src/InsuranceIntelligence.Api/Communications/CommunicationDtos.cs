namespace InsuranceIntelligence.Api.Communications;

// WO-16: Approved/LowRisk/MessageBody/CaseId are deliberately NOT client-supplied fields here,
// this request only names which approved CustomerUpdateDraft to send. The endpoint looks that
// draft up and derives all of those from the server-persisted record, exactly like
// ConfiguredCustomerUpdateCommunicationProvider already does for the customer-updates/{id}/send
// path. A client-trusted Approved flag on a send endpoint would let anyone holding
// SendCustomerUpdate permission dispatch an unapproved message by simply claiming approved:true,
// the human gate must be enforced server-side (I4), never accepted from the caller.
public sealed record SendCommunicationRequest(
    Guid CustomerUpdateId,
    string Recipient,
    string SentByUserId);

public sealed record SendTeamsNotificationRequest(
    Guid? CaseId,
    string NotificationType,
    string Message,
    string RequestedByUserId);
