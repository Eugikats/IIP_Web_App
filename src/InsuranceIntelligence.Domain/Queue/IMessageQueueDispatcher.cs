namespace InsuranceIntelligence.Domain.Queue;

public interface IMessageQueueDispatcher
{
    Task DispatchAsync(QueueMessageEnvelope envelope, CancellationToken cancellationToken = default);
}

public interface IMessageQueueConsumer
{
    Task StartAsync(CancellationToken cancellationToken = default);
}

public sealed record QueueMessageEnvelope(
    string QueueName,
    string MessageType,
    string TenantId,
    string WorkspaceId,
    string CorrelationId,
    object Payload);
