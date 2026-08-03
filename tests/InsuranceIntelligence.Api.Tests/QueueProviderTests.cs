using InsuranceIntelligence.Api.Azure;
using InsuranceIntelligence.Domain.Queue;

namespace InsuranceIntelligence.Api.Tests;

public sealed class QueueProviderTests
{
    [Fact]
    public void AzureServiceBusMessage_UsesDeterministicIdempotencyKey()
    {
        var envelope = new QueueMessageEnvelope(
            QueueName: "approval-notifications",
            MessageType: "approval.created",
            TenantId: "tenant-a",
            WorkspaceId: "workspace-a",
            CorrelationId: "corr-001",
            Payload: new { ApprovalId = "approval-1" });

        var first = AzureServiceBusQueueDispatcher.BuildMessage(envelope);
        var second = AzureServiceBusQueueDispatcher.BuildMessage(envelope);

        Assert.Equal(first.MessageId, second.MessageId);
        Assert.Equal("approval-notifications:approval.created:tenant-a:workspace-a:corr-001", first.MessageId);
        Assert.Equal(first.MessageId, first.ApplicationProperties["idempotencyKey"]);
        Assert.Equal("tenant-a", first.ApplicationProperties["tenantId"]);
        Assert.Equal("workspace-a", first.ApplicationProperties["workspaceId"]);
        Assert.Equal("corr-001", first.CorrelationId);
        Assert.Equal("approval.created", first.Subject);
        Assert.Equal("application/json", first.ContentType);
    }

    [Fact]
    public void AzureServiceBusMessage_SanitizesColonSeparatedIdempotencyKeyParts()
    {
        var envelope = new QueueMessageEnvelope(
            QueueName: "approval:notifications",
            MessageType: "approval:created",
            TenantId: "tenant:a",
            WorkspaceId: "workspace:a",
            CorrelationId: "corr:001",
            Payload: new { ApprovalId = "approval-1" });

        var message = AzureServiceBusQueueDispatcher.BuildMessage(envelope);

        Assert.Equal("approval_notifications:approval_created:tenant_a:workspace_a:corr_001", message.MessageId);
    }

    [Fact]
    public void BuildMessage_HashesLongMessageIdAndPreservesLogicalIdempotencyKey()
    {
        var envelope = new QueueMessageEnvelope(
            QueueName: $"approval-notifications-{new string('q', 80)}",
            MessageType: $"approval.created.{new string('m', 80)}",
            TenantId: $"tenant-{new string('t', 80)}",
            WorkspaceId: $"workspace-{new string('w', 80)}",
            CorrelationId: Guid.NewGuid().ToString(),
            Payload: new { ApprovalId = "approval-1" });

        var first = AzureServiceBusQueueDispatcher.BuildMessage(envelope);
        var second = AzureServiceBusQueueDispatcher.BuildMessage(envelope);
        var logicalKey = Assert.IsType<string>(first.ApplicationProperties["idempotencyKey"]);

        Assert.StartsWith("sha256:", first.MessageId, StringComparison.Ordinal);
        Assert.True(first.MessageId.Length <= 128);
        Assert.Equal(first.MessageId, second.MessageId);
        Assert.True(logicalKey.Length > 128);
        Assert.NotEqual(logicalKey, first.MessageId);
        Assert.Equal(logicalKey, second.ApplicationProperties["idempotencyKey"]);
    }
}

