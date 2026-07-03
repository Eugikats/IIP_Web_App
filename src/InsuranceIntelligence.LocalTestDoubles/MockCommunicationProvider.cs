using InsuranceIntelligence.Domain.Workflow;

namespace InsuranceIntelligence.LocalTestDoubles;

public sealed class MockCommunicationProvider : ICommunicationProvider
{
    public Task<CustomerUpdateDraft> SendAsync(
        CustomerUpdateDraft update,
        string sentByUserId,
        string? recipient = null,
        CancellationToken cancellationToken = default)
    {
        var sent = update with
        {
            Status = CustomerUpdateStatus.SimulatedSent,
            DeliveryStatus = "SimulatedSent",
            SentByUserId = sentByUserId,
            SentAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        return Task.FromResult(sent);
    }
}
