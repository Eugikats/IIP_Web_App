using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Notifications;
using InsuranceIntelligence.Functions;
using InsuranceIntelligence.Infrastructure.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace InsuranceIntelligence.Api.Tests;

public sealed class FunctionsWorkflowRuntimeTests
{
    private static readonly string[] ForbiddenProductionTerms =
    [
        "mock",
        "simulated",
        "placeholder",
        "developmentauth",
        "fallbacktomock"
    ];

    [Fact]
    public void LocalTestDescriptor_DoesNotAdvertiseDeployedProviderBehavior()
    {
        var descriptor = new WorkflowShell(new InfrastructureOptions()).Describe();

        Assert.Equal("LocalTest", descriptor.Mode);
        Assert.True(descriptor.IsReady);
        Assert.DoesNotContain(ForbiddenProductionTerms, term => descriptor.Detail.Contains(term, StringComparison.OrdinalIgnoreCase));
        Assert.All(descriptor.Processors, processor =>
        {
            Assert.True(processor.RequiresAzureServiceBus);
            Assert.DoesNotContain(ForbiddenProductionTerms, term => processor.Purpose.Contains(term, StringComparison.OrdinalIgnoreCase));
        });
    }

    [Fact]
    public void AzureServiceBusDescriptor_RequiresNamespace()
    {
        var options = new InfrastructureOptions
        {
            QueueProvider = "AzureServiceBus"
        };

        var descriptor = new WorkflowShell(options).Describe();

        Assert.Equal("AzureServiceBus", descriptor.Mode);
        Assert.False(descriptor.IsReady);
        Assert.Contains("ServiceBus:FullyQualifiedNamespace", descriptor.MissingSettings);
    }

    [Fact]
    public void AzureServiceBusDescriptor_ReportsQueueContract()
    {
        var options = new InfrastructureOptions
        {
            QueueProvider = "AzureServiceBus",
            ServiceBus = new ServiceBusOptions
            {
                FullyQualifiedNamespace = "iip-dev.servicebus.windows.net",
                EmailIngestionQueue = "email-work",
                DocumentProcessingQueue = "document-work",
                KnowledgeIndexingQueue = "knowledge-work",
                OutboundCommunicationsQueue = "communications-work",
                ApprovalNotificationsQueue = "approval-work",
                AuditEventsQueue = "audit-work"
            }
        };

        var descriptor = new WorkflowShell(options).Describe();

        Assert.True(descriptor.IsReady);
        Assert.Empty(descriptor.MissingSettings);
        Assert.Equal("email-work", descriptor.Queues.EmailIngestion);
        Assert.Equal("document-work", descriptor.Queues.DocumentProcessing);
        Assert.Equal("knowledge-work", descriptor.Queues.KnowledgeIndexing);
        Assert.Equal("communications-work", descriptor.Queues.OutboundCommunications);
        Assert.Equal("approval-work", descriptor.Queues.ApprovalNotifications);
        Assert.Equal("audit-work", descriptor.Queues.AuditEvents);
        Assert.Contains(descriptor.Processors, processor => processor.Name == "Outbound communications" && processor.QueueName == "communications-work");
    }

    [Theory]
    [InlineData("Staging")]
    [InlineData("Production")]
    [InlineData("ManagedApplicationReviewer")]
    public void ProtectedRuntimeValidation_RejectsNonServiceBusWorkflowRuntime(string environmentName)
    {
        var errors = WorkflowShell.ValidateProtectedRuntime(new InfrastructureOptions(), environmentName);

        Assert.Contains(errors, error => error.Contains("INSURANCEINTELLIGENCE_QUEUE_PROVIDER=AzureServiceBus", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("Staging")]
    [InlineData("Production")]
    [InlineData("ManagedApplicationReviewer")]
    public void ProtectedRuntimeValidation_AcceptsConfiguredServiceBusWorkflowRuntime(string environmentName)
    {
        var errors = WorkflowShell.ValidateProtectedRuntime(ProtectedOptions(), environmentName);

        Assert.Empty(errors);
    }

    [Theory]
    [InlineData("Staging")]
    [InlineData("Production")]
    [InlineData("ManagedApplicationReviewer")]
    public void ProtectedRuntimeValidation_RejectsMockProviderModes(string environmentName)
    {
        var errors = WorkflowShell.ValidateProtectedRuntime(
            ProtectedOptions() with { NotificationProvider = "Mock" },
            environmentName);

        Assert.Contains(errors, error => error.Contains("INSURANCEINTELLIGENCE_NOTIFICATION_PROVIDER=Mock", StringComparison.Ordinal));
    }

    [Fact]
    public void TeamsNotificationDispatch_UsesConfiguredQueueAndIdentityConnection()
    {
        Assert.Equal("%ServiceBus:ApprovalNotificationsQueue%", TeamsNotificationDispatchFunction.ApprovalNotificationsQueueSetting);
        Assert.Equal("ServiceBusConnection", TeamsNotificationDispatchFunction.ServiceBusConnectionSetting);
    }

    [Fact]
    public async Task TeamsNotificationDispatch_InvalidEnvelopeFailsForRetryAndDeadLetter()
    {
        var function = new TeamsNotificationDispatchFunction(
            new StubTeamsNotificationProvider("delivered_webhook"),
            new RecordingTeamsNotificationRepository(),
            NullLogger<TeamsNotificationDispatchFunction>.Instance);

        await Assert.ThrowsAsync<InvalidDataException>(() => function.Run("{}", CancellationToken.None));
    }

    [Fact]
    public async Task TeamsNotificationDispatch_UnconfirmedDeliveryFailsForRetryAndDeadLetter()
    {
        var repository = new RecordingTeamsNotificationRepository();
        var function = new TeamsNotificationDispatchFunction(
            new StubTeamsNotificationProvider("ConfigurationRequired"),
            repository,
            NullLogger<TeamsNotificationDispatchFunction>.Instance);
        var message = """
            {
              "QueueName": "approval-notifications",
              "MessageType": "TeamsNotification.Dispatch",
              "TenantId": "tenant-1",
              "WorkspaceId": "workspace-1",
              "CorrelationId": "correlation-1",
              "Payload": {
                "NotificationId": "11111111-1111-1111-1111-111111111111",
                "NotificationType": "ApprovalRequired",
                "PayloadJson": "{}"
              }
            }
            """;

        await Assert.ThrowsAsync<InvalidOperationException>(() => function.Run(message, CancellationToken.None));
        Assert.Equal(TeamsNotificationDeliveryStatus.Failed, repository.Saved?.DeliveryStatus);
        Assert.Equal("Delivery failed. Review correlated provider telemetry.", repository.Saved?.ErrorMessage);
    }

    [Fact]
    public async Task TeamsNotificationDispatch_ConfirmedDeliveryCompletes()
    {
        var repository = new RecordingTeamsNotificationRepository();
        var function = new TeamsNotificationDispatchFunction(
            new StubTeamsNotificationProvider("delivered_webhook"),
            repository,
            NullLogger<TeamsNotificationDispatchFunction>.Instance);
        var message = """
            {
              "QueueName": "approval-notifications",
              "MessageType": "TeamsNotification.Dispatch",
              "TenantId": "tenant-1",
              "WorkspaceId": "workspace-1",
              "CorrelationId": "correlation-1",
              "Payload": {
                "NotificationId": "22222222-2222-2222-2222-222222222222",
                "CaseId": "3fd88a70-04dc-4d55-b60c-1c00db4118b1",
                "NotificationType": "SlaAtRisk",
                "PayloadJson": "{}"
              }
            }
            """;

        await function.Run(message, CancellationToken.None);

        Assert.Equal(Guid.Parse("22222222-2222-2222-2222-222222222222"), repository.Saved?.Id);
        Assert.Equal(TeamsNotificationDeliveryStatus.Delivered, repository.Saved?.DeliveryStatus);
        Assert.NotNull(repository.Saved?.DeliveredAt);
        Assert.Null(repository.Saved?.ErrorMessage);
    }

    [Fact]
    public void GraphTeamsStub_IsRejectedAsUnimplemented()
    {
        var errors = InsuranceIntelligence.Api.Azure.AzureProviderStartupValidator.Validate(
            new InfrastructureOptions { NotificationProvider = "GraphTeams" });

        Assert.Contains(errors, error => error.Contains("not implemented", StringComparison.OrdinalIgnoreCase));
    }

    private static InfrastructureOptions ProtectedOptions() =>
        new()
        {
            AuthMode = "Entra",
            StorageProvider = "SqlServer",
            BlobProvider = "AzureBlob",
            DocumentProvider = "NotConfigured",
            RetrievalProvider = "AzureAiSearch",
            AiProvider = "NotConfigured",
            QueueProvider = "AzureServiceBus",
            EmailProvider = "Disabled",
            CommunicationProvider = "Disabled",
            NotificationProvider = "Disabled",
            SmsProvider = "Disabled",
            ChannelProvider = "Disabled",
            ProvisionerProvider = "Disabled",
            ServiceBus = new ServiceBusOptions
            {
                FullyQualifiedNamespace = "iip-prod.servicebus.windows.net"
            }
        };

    private sealed class RecordingTeamsNotificationRepository : ITeamsNotificationRepository
    {
        public TeamsNotification? Saved { get; private set; }

        public Task SaveAsync(TeamsNotification notification, CancellationToken cancellationToken = default)
        {
            Saved = notification;
            return Task.CompletedTask;
        }

        public Task<IEnumerable<TeamsNotification>> ListAsync(TenantScope scope, CancellationToken cancellationToken = default) =>
            Task.FromResult<IEnumerable<TeamsNotification>>(
                Saved is not null && Saved.Scope.Matches(scope) ? [Saved] : []);
    }

    private sealed class StubTeamsNotificationProvider(string status) : ITeamsNotificationProvider
    {
        public Task<string> SendAsync(TeamsNotification notification, CancellationToken cancellationToken = default) =>
            Task.FromResult(status);
    }
}
