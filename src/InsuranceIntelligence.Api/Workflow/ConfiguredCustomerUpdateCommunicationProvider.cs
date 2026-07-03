using InsuranceIntelligence.Domain.Communications;
using InsuranceIntelligence.Domain.Workflow;

namespace InsuranceIntelligence.Api.Workflow;

public sealed class ConfiguredCustomerUpdateCommunicationProvider(
    ICommunicationSendingProvider communicationSendingProvider) : ICommunicationProvider
{
    public async Task<CustomerUpdateDraft> SendAsync(
        CustomerUpdateDraft update,
        string sentByUserId,
        string? recipient = null,
        CancellationToken cancellationToken = default)
    {
        if ((update.Channel is CommunicationChannel.Email or CommunicationChannel.SMS)
            && string.IsNullOrWhiteSpace(recipient))
        {
            return update with
            {
                DeliveryStatus = "ConfigurationRequired",
                SentByUserId = sentByUserId,
                UpdatedAt = DateTimeOffset.UtcNow
            };
        }

        var result = update.Channel switch
        {
            CommunicationChannel.Email => await communicationSendingProvider.SendEmailAsync(ToRequest(update, sentByUserId, recipient), cancellationToken),
            CommunicationChannel.SMS => await communicationSendingProvider.SendSmsAsync(ToRequest(update, sentByUserId, recipient), cancellationToken),
            _ => new CommunicationSendResult(
                "ConfigurationRequired",
                string.Empty,
                $"{update.Channel} customer-update sending is not configured.")
        };

        if (result.DeliveryStatus is "ApprovalRequired" or "ConfigurationRequired" or "Failed")
        {
            return update with
            {
                DeliveryStatus = result.DeliveryStatus,
                SentByUserId = sentByUserId,
                UpdatedAt = DateTimeOffset.UtcNow
            };
        }

        return update with
        {
            Status = CustomerUpdateStatus.Sent,
            DeliveryStatus = result.DeliveryStatus,
            SentByUserId = sentByUserId,
            SentAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    private static CommunicationSendRequest ToRequest(CustomerUpdateDraft update, string sentByUserId, string? recipient) =>
        new(
            update.Scope,
            update.CaseId,
            update.Channel,
            Recipient: recipient ?? string.Empty,
            update.MessageBody,
            sentByUserId,
            Approved: update.Status == CustomerUpdateStatus.Approved,
            LowRisk: !update.RequiresApproval,
            update.CorrelationId);
}
