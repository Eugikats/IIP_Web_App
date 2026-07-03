using InsuranceIntelligence.Infrastructure.Configuration;

namespace InsuranceIntelligence.Api.Azure;

public static class AzureProviderStartupValidator
{
    private static readonly (string SettingName, Func<InfrastructureOptions, string> Selector)[] ProductionProviderModes =
    [
        ("INSURANCEINTELLIGENCE_BLOB_PROVIDER", options => options.BlobProvider),
        ("INSURANCEINTELLIGENCE_DOCUMENT_PROVIDER", options => options.DocumentProvider),
        ("INSURANCEINTELLIGENCE_RETRIEVAL_PROVIDER", options => options.RetrievalProvider),
        ("INSURANCEINTELLIGENCE_AI_PROVIDER", options => options.AiProvider),
        ("INSURANCEINTELLIGENCE_QUEUE_PROVIDER", options => options.QueueProvider),
        ("INSURANCEINTELLIGENCE_EMAIL_PROVIDER", options => options.EmailProvider),
        ("INSURANCEINTELLIGENCE_COMMUNICATION_PROVIDER", options => options.CommunicationProvider),
        ("INSURANCEINTELLIGENCE_NOTIFICATION_PROVIDER", options => options.NotificationProvider)
    ];

    public static IReadOnlyCollection<string> Validate(InfrastructureOptions options)
    {
        var errors = new List<string>();

        if (options.BlobProvider.Equals("AzureBlob", StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrWhiteSpace(options.AzureBlob.Endpoint)
            && string.IsNullOrWhiteSpace(options.AzureBlob.AccountName)
            && string.IsNullOrWhiteSpace(options.AzureBlob.ConnectionString))
        {
            errors.Add("AzureBlob mode requires AzureBlob:Endpoint, AzureBlob:AccountName, or AzureBlob:ConnectionString.");
        }

        if (options.DocumentProvider.Equals("AzureDocumentIntelligence", StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrWhiteSpace(options.DocumentIntelligence.Endpoint))
        {
            errors.Add("AzureDocumentIntelligence mode requires DocumentIntelligence:Endpoint.");
        }

        if (options.RetrievalProvider.Equals("AzureAiSearch", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(options.AzureAiSearch.Endpoint))
            {
                errors.Add("AzureAiSearch mode requires AzureAiSearch:Endpoint.");
            }

            if (string.IsNullOrWhiteSpace(options.AzureAiSearch.IndexName))
            {
                errors.Add("AzureAiSearch mode requires AzureAiSearch:IndexName.");
            }
        }

        if (options.AiProvider.Equals("AzureOpenAI", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(options.AzureOpenAI.Endpoint))
            {
                errors.Add("AzureOpenAI mode requires AzureOpenAI:Endpoint.");
            }

            if (string.IsNullOrWhiteSpace(options.AzureOpenAI.ChatDeployment))
            {
                errors.Add("AzureOpenAI mode requires AzureOpenAI:ChatDeployment.");
            }

            if (string.IsNullOrWhiteSpace(options.AzureOpenAI.EmbeddingDeployment))
            {
                errors.Add("AzureOpenAI mode requires AzureOpenAI:EmbeddingDeployment.");
            }
        }

        if (options.QueueProvider.Equals("AzureServiceBus", StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrWhiteSpace(options.ServiceBus.FullyQualifiedNamespace))
        {
            errors.Add("AzureServiceBus mode requires ServiceBus:FullyQualifiedNamespace.");
        }

        if (options.EmailProvider.Equals("Graph", StringComparison.OrdinalIgnoreCase)
            && !HasConfiguredGraphMailboxAccess(options))
        {
            errors.Add("Graph email mode requires GraphMail tenant, client, credential settings, and GraphMail:MailboxAccessScoped=true after mailbox access has been restricted to approved insurer mailboxes.");
        }

        if (options.CommunicationProvider.Equals("GraphEmail", StringComparison.OrdinalIgnoreCase)
            && (!HasConfiguredGraphMailboxAccess(options) || string.IsNullOrWhiteSpace(options.GraphMail.DefaultMailboxAddress)))
        {
            errors.Add("GraphEmail communication mode requires GraphMail client credential settings, GraphMail:MailboxAccessScoped=true, and GraphMail:DefaultMailboxAddress.");
        }

        return errors;
    }

    public static IReadOnlyCollection<string> ValidateProduction(InfrastructureOptions options, string environmentName)
    {
        var runtimeProfile = ResolveRuntimeProfile(options, environmentName);
        if (!IsProtectedRuntime(runtimeProfile))
        {
            return Array.Empty<string>();
        }

        var errors = new List<string>();

        if (options.AuthMode.Equals("Development", StringComparison.OrdinalIgnoreCase))
        {
            errors.Add($"{runtimeProfile} requires INSURANCEINTELLIGENCE_AUTH_MODE to use an external identity mode such as Entra.");
        }

        if (IsForbiddenProvider(options.StorageProvider, includeSqlite: true))
        {
            errors.Add($"{runtimeProfile} requires Azure SQL durable storage; INSURANCEINTELLIGENCE_STORAGE_PROVIDER cannot be {options.StorageProvider}.");
        }
        else if (options.StorageProvider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrWhiteSpace(options.SqlServerConnectionString))
        {
            errors.Add($"{runtimeProfile} requires Azure SQL durable storage; INSURANCEINTELLIGENCE_SQLSERVER_CONNECTION_STRING or ConnectionStrings:InsuranceIntelligenceSql must be configured.");
        }

        if (options.SeedDemoData)
        {
            errors.Add($"{runtimeProfile} cannot start with INSURANCEINTELLIGENCE_SEED_DEMO_DATA=true.");
        }

        if (options.AllowProductionMockProviders)
        {
            errors.Add($"{runtimeProfile} cannot start with INSURANCEINTELLIGENCE_ALLOW_PRODUCTION_MOCKS=true.");
        }

        foreach (var (settingName, selector) in ProductionProviderModes)
        {
            var value = selector(options);
            if (IsForbiddenProvider(value, includeSqlite: false))
            {
                errors.Add($"{runtimeProfile} requires a real configured provider for {settingName}; {value} is not allowed.");
            }
        }

        return errors;
    }

    public static string ResolveRuntimeProfile(InfrastructureOptions options, string environmentName)
    {
        if (!string.IsNullOrWhiteSpace(options.RuntimeProfile))
        {
            return options.RuntimeProfile;
        }

        if (environmentName.Equals("Development", StringComparison.OrdinalIgnoreCase))
        {
            return "Development";
        }

        if (environmentName.Equals("Staging", StringComparison.OrdinalIgnoreCase)
            || environmentName.Equals("Production", StringComparison.OrdinalIgnoreCase)
            || environmentName.Equals("ManagedApplicationReviewer", StringComparison.OrdinalIgnoreCase)
            || environmentName.Equals("LocalTest", StringComparison.OrdinalIgnoreCase))
        {
            return environmentName;
        }

        return environmentName.Equals("Test", StringComparison.OrdinalIgnoreCase)
            ? "LocalTest"
            : environmentName;
    }

    private static bool IsProtectedRuntime(string environmentName) =>
        environmentName.Equals("Staging", StringComparison.OrdinalIgnoreCase)
        || environmentName.Equals("Production", StringComparison.OrdinalIgnoreCase)
        || environmentName.Equals("ManagedApplicationReviewer", StringComparison.OrdinalIgnoreCase);

    private static bool IsForbiddenProvider(string value, bool includeSqlite) =>
        value.Equals("Mock", StringComparison.OrdinalIgnoreCase)
        || value.Equals("Simulated", StringComparison.OrdinalIgnoreCase)
        || value.Equals("Placeholder", StringComparison.OrdinalIgnoreCase)
        || value.Equals("InMemory", StringComparison.OrdinalIgnoreCase)
        || value.Equals("DevelopmentAuth", StringComparison.OrdinalIgnoreCase)
        || value.Equals("Demo", StringComparison.OrdinalIgnoreCase)
        || value.Equals("SeededDemoBusinessData", StringComparison.OrdinalIgnoreCase)
        || value.Equals("FallbackToMock", StringComparison.OrdinalIgnoreCase)
        || (includeSqlite && (value.Equals("SQLite", StringComparison.OrdinalIgnoreCase)
            || value.Equals("EfSqlite", StringComparison.OrdinalIgnoreCase)));

    private static bool HasConfiguredGraphMailboxAccess(InfrastructureOptions options) =>
        HasGraphClientCredential(options)
        && options.GraphMail.MailboxAccessScoped;

    private static bool HasGraphClientCredential(InfrastructureOptions options) =>
        !options.GraphMail.UseManagedIdentity
        && !string.IsNullOrWhiteSpace(options.GraphMail.TenantId)
        && !string.IsNullOrWhiteSpace(options.GraphMail.ClientId)
        && !string.IsNullOrWhiteSpace(options.GraphMail.ClientSecret);

    public static void ThrowIfInvalid(InfrastructureOptions options)
    {
        var errors = Validate(options);
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                "Live Azure provider mode is selected but required configuration is missing. "
                + string.Join(' ', errors)
                + " Mock provider modes do not require Azure credentials.");
        }
    }

    public static void ThrowIfProductionInvalid(InfrastructureOptions options, string environmentName)
    {
        var errors = ValidateProduction(options, environmentName);
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                "Production startup configuration is unsafe. "
                + string.Join(' ', errors)
                + " Test doubles are allowed only in LocalTest or isolated Development execution.");
        }
    }
}
