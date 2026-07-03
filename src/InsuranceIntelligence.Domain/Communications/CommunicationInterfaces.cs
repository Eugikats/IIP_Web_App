using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Workflow;

namespace InsuranceIntelligence.Domain.Communications;

public interface ICommunicationRepository
{
    Task SaveAsync(Communication communication, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<Communication>> ListAsync(TenantScope scope, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<Communication>> ListForCaseAsync(Guid caseId, TenantScope scope, CancellationToken cancellationToken = default);
}

public interface ICommunicationSendingProvider
{
    Task<CommunicationSendResult> SendEmailAsync(CommunicationSendRequest request, CancellationToken cancellationToken = default);

    Task<CommunicationSendResult> SendSmsAsync(CommunicationSendRequest request, CancellationToken cancellationToken = default);
}

public interface ISmsProvider
{
    Task<CommunicationSendResult> SendAsync(CommunicationSendRequest request, CancellationToken cancellationToken = default);
}

public sealed record CommunicationSendRequest(
    TenantScope Scope,
    Guid CaseId,
    CommunicationChannel Channel,
    string Recipient,
    string MessageBody,
    string SentByUserId,
    bool Approved,
    bool LowRisk,
    string CorrelationId);

public sealed record CommunicationSendResult(string DeliveryStatus, string ProviderMessageId, string? SafeError);
