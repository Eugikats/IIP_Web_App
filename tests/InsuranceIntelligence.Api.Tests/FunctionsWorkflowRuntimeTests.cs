using InsuranceIntelligence.Functions;
using InsuranceIntelligence.Infrastructure.Configuration;

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
        var options = new InfrastructureOptions
        {
            QueueProvider = "AzureServiceBus",
            ServiceBus = new ServiceBusOptions
            {
                FullyQualifiedNamespace = "iip-prod.servicebus.windows.net"
            }
        };

        var errors = WorkflowShell.ValidateProtectedRuntime(options, environmentName);

        Assert.Empty(errors);
    }
}
