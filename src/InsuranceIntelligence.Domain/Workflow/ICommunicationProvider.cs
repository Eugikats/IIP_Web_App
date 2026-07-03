namespace InsuranceIntelligence.Domain.Workflow;

public interface ICommunicationProvider
{
    Task<CustomerUpdateDraft> SendAsync(
        CustomerUpdateDraft update,
        string sentByUserId,
        string? recipient = null,
        CancellationToken cancellationToken = default);
}
