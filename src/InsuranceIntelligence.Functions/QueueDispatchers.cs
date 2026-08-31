using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Azure.Identity;
using Azure.Messaging.ServiceBus;
using InsuranceIntelligence.Domain.Queue;
using InsuranceIntelligence.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;

namespace InsuranceIntelligence.Functions;

public sealed class FunctionsMockMessageQueueDispatcher(ILogger<FunctionsMockMessageQueueDispatcher> logger) : IMessageQueueDispatcher
{
    public Task DispatchAsync(QueueMessageEnvelope envelope, CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Mock queue dispatch accepted. Queue={QueueName} MessageType={MessageType} Tenant={TenantId} Workspace={WorkspaceId}",
            envelope.QueueName,
            envelope.MessageType,
            envelope.TenantId,
            envelope.WorkspaceId);
        return Task.CompletedTask;
    }
}

public sealed class FunctionsNotConfiguredMessageQueueDispatcher : IMessageQueueDispatcher
{
    public Task DispatchAsync(QueueMessageEnvelope envelope, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("Message queue provider is not configured.");
}

public sealed class FunctionsAzureServiceBusQueueDispatcher(
    InfrastructureOptions options,
    ILogger<FunctionsAzureServiceBusQueueDispatcher> logger) : IMessageQueueDispatcher
{
    public async Task DispatchAsync(QueueMessageEnvelope envelope, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(options.ServiceBus.FullyQualifiedNamespace))
        {
            throw new InvalidOperationException("Azure Service Bus is selected but ServiceBus:FullyQualifiedNamespace is not configured.");
        }

        await using var client = new ServiceBusClient(options.ServiceBus.FullyQualifiedNamespace, new DefaultAzureCredential());
        var sender = client.CreateSender(envelope.QueueName);
        var message = BuildMessage(envelope);

        logger.LogInformation(
            "Dispatching Azure Service Bus message. Queue={QueueName} MessageType={MessageType} CorrelationId={CorrelationId}",
            envelope.QueueName,
            envelope.MessageType,
            envelope.CorrelationId);
        await sender.SendMessageAsync(message, cancellationToken);
    }

    public static ServiceBusMessage BuildMessage(QueueMessageEnvelope envelope)
    {
        var body = JsonSerializer.Serialize(new
        {
            envelope.MessageType,
            envelope.TenantId,
            envelope.WorkspaceId,
            envelope.CorrelationId,
            envelope.Payload
        });
        var idempotencyKey = BuildIdempotencyKey(envelope);
        var messageId = BuildMessageId(idempotencyKey);
        var message = new ServiceBusMessage(body)
        {
            ContentType = "application/json",
            CorrelationId = envelope.CorrelationId,
            MessageId = messageId,
            Subject = envelope.MessageType
        };
        message.ApplicationProperties["tenantId"] = envelope.TenantId;
        message.ApplicationProperties["workspaceId"] = envelope.WorkspaceId;
        message.ApplicationProperties["idempotencyKey"] = idempotencyKey;
        return message;
    }

    private static string BuildIdempotencyKey(QueueMessageEnvelope envelope) =>
        string.Join(
            ":",
            Sanitize(envelope.QueueName),
            Sanitize(envelope.MessageType),
            Sanitize(envelope.TenantId),
            Sanitize(envelope.WorkspaceId),
            Sanitize(envelope.CorrelationId));

    private static string BuildMessageId(string idempotencyKey) =>
        idempotencyKey.Length <= 128
            ? idempotencyKey
            : $"sha256:{Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(idempotencyKey))).ToLowerInvariant()}";

    private static string Sanitize(string value) =>
        string.IsNullOrWhiteSpace(value)
            ? "missing"
            : value.Trim().Replace(":", "_", StringComparison.Ordinal);
}
