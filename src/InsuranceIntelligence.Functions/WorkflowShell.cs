using InsuranceIntelligence.Infrastructure.Configuration;

namespace InsuranceIntelligence.Functions;

public sealed class WorkflowShell
{
    private readonly InfrastructureOptions options;

    public WorkflowShell()
        : this(new InfrastructureOptions())
    {
    }

    public WorkflowShell(InfrastructureOptions options)
    {
        this.options = options;
    }

    public string Name => "InsuranceIntelligence workflow runtime";

    public WorkflowRuntimeDescriptor Describe()
    {
        var queues = WorkflowQueueNames.From(options.ServiceBus);
        var processors = WorkflowProcessorDescriptor.From(queues);

        if (!options.QueueProvider.Equals("AzureServiceBus", StringComparison.OrdinalIgnoreCase))
        {
            return new WorkflowRuntimeDescriptor(
                "LocalTest",
                true,
                "Local workflow runtime is available only for isolated development and automated tests.",
                queues,
                Array.Empty<string>(),
                processors);
        }

        var missingSettings = MissingServiceBusSettings(options.ServiceBus, queues);

        return new WorkflowRuntimeDescriptor(
            "AzureServiceBus",
            missingSettings.Count == 0,
            missingSettings.Count == 0
                ? "Azure Service Bus workflow runtime is configured."
                : "Azure Service Bus workflow runtime is missing required configuration.",
            queues,
            missingSettings,
            processors);
    }

    public static IReadOnlyCollection<string> ValidateProtectedRuntime(InfrastructureOptions options, string environmentName)
    {
        if (!IsProtectedRuntime(environmentName))
        {
            return Array.Empty<string>();
        }

        var errors = new List<string>();
        if (options.AuthMode.Equals("Development", StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("Protected workflow runtime requires external identity; Development auth is not allowed.");
        }

        var providerModes = new[]
        {
            ("INSURANCEINTELLIGENCE_STORAGE_PROVIDER", options.StorageProvider),
            ("INSURANCEINTELLIGENCE_BLOB_PROVIDER", options.BlobProvider),
            ("INSURANCEINTELLIGENCE_DOCUMENT_PROVIDER", options.DocumentProvider),
            ("INSURANCEINTELLIGENCE_RETRIEVAL_PROVIDER", options.RetrievalProvider),
            ("INSURANCEINTELLIGENCE_AI_PROVIDER", options.AiProvider),
            ("INSURANCEINTELLIGENCE_QUEUE_PROVIDER", options.QueueProvider),
            ("INSURANCEINTELLIGENCE_EMAIL_PROVIDER", options.EmailProvider),
            ("INSURANCEINTELLIGENCE_COMMUNICATION_PROVIDER", options.CommunicationProvider),
            ("INSURANCEINTELLIGENCE_NOTIFICATION_PROVIDER", options.NotificationProvider)
        };
        foreach (var (settingName, providerMode) in providerModes)
        {
            if (IsForbiddenProvider(providerMode, settingName == "INSURANCEINTELLIGENCE_STORAGE_PROVIDER"))
            {
                errors.Add($"Protected workflow runtime does not allow {settingName}={providerMode}.");
            }
        }

        if (!options.QueueProvider.Equals("AzureServiceBus", StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("Protected workflow runtime requires INSURANCEINTELLIGENCE_QUEUE_PROVIDER=AzureServiceBus.");
        }

        var descriptor = new WorkflowShell(options).Describe();
        foreach (var missingSetting in descriptor.MissingSettings)
        {
            errors.Add($"Protected workflow runtime is missing {missingSetting}.");
        }

        return errors;
    }

    private static IReadOnlyCollection<string> MissingServiceBusSettings(ServiceBusOptions serviceBus, WorkflowQueueNames queues)
    {
        var missingSettings = new List<string>();

        if (string.IsNullOrWhiteSpace(serviceBus.FullyQualifiedNamespace))
        {
            missingSettings.Add("ServiceBus:FullyQualifiedNamespace");
        }

        foreach (var queue in queues.Settings())
        {
            if (string.IsNullOrWhiteSpace(queue.QueueName))
            {
                missingSettings.Add(queue.Setting);
            }
        }

        return missingSettings;
    }

    private static bool IsForbiddenProvider(string value, bool includeSqlite) =>
        value.Equals("Mock", StringComparison.OrdinalIgnoreCase)
        || value.Equals("Simulated", StringComparison.OrdinalIgnoreCase)
        || value.Equals("Placeholder", StringComparison.OrdinalIgnoreCase)
        || value.Equals("InMemory", StringComparison.OrdinalIgnoreCase)
        || value.Equals("DevelopmentAuth", StringComparison.OrdinalIgnoreCase)
        || value.Equals("Demo", StringComparison.OrdinalIgnoreCase)
        || value.Equals("SeededDemoBusinessData", StringComparison.OrdinalIgnoreCase)
        || value.Equals("FallbackToMock", StringComparison.OrdinalIgnoreCase)
        || (includeSqlite && value.Equals("SQLite", StringComparison.OrdinalIgnoreCase));

    private static bool IsProtectedRuntime(string environmentName) =>
        environmentName.Equals("Staging", StringComparison.OrdinalIgnoreCase)
        || environmentName.Equals("Production", StringComparison.OrdinalIgnoreCase)
        || environmentName.Equals("ManagedApplicationReviewer", StringComparison.OrdinalIgnoreCase);
}

public sealed record WorkflowRuntimeDescriptor(
    string Mode,
    bool IsReady,
    string Detail,
    WorkflowQueueNames Queues,
    IReadOnlyCollection<string> MissingSettings,
    IReadOnlyCollection<WorkflowProcessorDescriptor> Processors);

public sealed record WorkflowQueueNames(
    string EmailIngestion,
    string DocumentProcessing,
    string KnowledgeIndexing,
    string OutboundCommunications,
    string ApprovalNotifications,
    string AuditEvents,
    string AgentWorkflow)
{
    public static WorkflowQueueNames From(ServiceBusOptions serviceBus) =>
        new(
            serviceBus.EmailIngestionQueue,
            serviceBus.DocumentProcessingQueue,
            serviceBus.KnowledgeIndexingQueue,
            serviceBus.OutboundCommunicationsQueue,
            serviceBus.ApprovalNotificationsQueue,
            serviceBus.AuditEventsQueue,
            serviceBus.AgentWorkflowQueue);

    public IEnumerable<(string Setting, string QueueName)> Settings()
    {
        yield return ("ServiceBus:EmailIngestionQueue", EmailIngestion);
        yield return ("ServiceBus:DocumentProcessingQueue", DocumentProcessing);
        yield return ("ServiceBus:KnowledgeIndexingQueue", KnowledgeIndexing);
        yield return ("ServiceBus:OutboundCommunicationsQueue", OutboundCommunications);
        yield return ("ServiceBus:ApprovalNotificationsQueue", ApprovalNotifications);
        yield return ("ServiceBus:AuditEventsQueue", AuditEvents);
        yield return ("ServiceBus:AgentWorkflowQueue", AgentWorkflow);
    }
}

public sealed record WorkflowProcessorDescriptor(
    string Name,
    string QueueName,
    string Trigger,
    string Purpose,
    bool RequiresAzureServiceBus)
{
    public static IReadOnlyCollection<WorkflowProcessorDescriptor> From(WorkflowQueueNames queues) =>
        new[]
        {
            new WorkflowProcessorDescriptor(
                "Email ingestion",
                queues.EmailIngestion,
                "Service Bus queue",
                "Create or link claim work from approved Microsoft Graph mailbox messages.",
                true),
            new WorkflowProcessorDescriptor(
                "Document processing",
                queues.DocumentProcessing,
                "Service Bus queue",
                "Process uploaded claim documents through configured document intelligence providers.",
                true),
            new WorkflowProcessorDescriptor(
                "Knowledge indexing",
                queues.KnowledgeIndexing,
                "Service Bus queue",
                "Index approved tenant knowledge into the configured retrieval provider.",
                true),
            new WorkflowProcessorDescriptor(
                "Outbound communications",
                queues.OutboundCommunications,
                "Service Bus queue",
                "Dispatch approved customer communications through configured channels.",
                true),
            new WorkflowProcessorDescriptor(
                "Approval notifications",
                queues.ApprovalNotifications,
                "Service Bus queue",
                "Notify human reviewers about approvals, escalations, and SLA risk.",
                true),
            new WorkflowProcessorDescriptor(
                "Audit events",
                queues.AuditEvents,
                "Service Bus queue",
                "Persist workflow evidence, action metadata, and audit export events.",
                true),
            new WorkflowProcessorDescriptor(
                "Agent workflow",
                queues.AgentWorkflow,
                "Service Bus queue",
                "Execute bounded agent and tool nodes from durable tenant-scoped workflow runs.",
                true)
        };
}
