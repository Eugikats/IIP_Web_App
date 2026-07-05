using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Application.Audit;
using InsuranceIntelligence.Application.Channels;
using InsuranceIntelligence.Application.Claims;
using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Application.Customers;
using InsuranceIntelligence.Application.SalesLeads;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Communications;
using InsuranceIntelligence.Domain.Documents;
using InsuranceIntelligence.Domain.Integrations;
using InsuranceIntelligence.Domain.Knowledge;
using InsuranceIntelligence.Domain.Notifications;
using InsuranceIntelligence.Domain.Workflow;
using InsuranceIntelligence.Infrastructure.Configuration;
using InsuranceIntelligence.Infrastructure.Persistence;
using InsuranceIntelligence.Infrastructure.Simulated;
using InsuranceIntelligence.Infrastructure.Graph;
using InsuranceIntelligence.Infrastructure.Notifications;
using InsuranceIntelligence.Infrastructure.Providers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace InsuranceIntelligence.Infrastructure.DependencyInjection;

public static class InfrastructureRegistration
{
    public static IServiceCollection AddInsuranceIntelligenceInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var options = BindOptions(configuration);
        services.AddSingleton(options);

        if (options.StorageProvider.Equals("EfSqlite", StringComparison.OrdinalIgnoreCase)
            || options.StorageProvider.Equals("SQLite", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("SQLite storage is not supported by deployable runtime. Use InMemory only for isolated local/test execution or SqlServer for deployed environments.");
        }

        if (options.StorageProvider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(options.SqlServerConnectionString))
            {
                throw new InvalidOperationException("SqlServer storage requires INSURANCEINTELLIGENCE_SQLSERVER_CONNECTION_STRING or ConnectionStrings:InsuranceIntelligenceSql.");
            }

            services.AddDbContext<InsuranceIntelligenceDbContext>(db =>
                db.UseSqlServer(options.SqlServerConnectionString));
            AddEfRepositories(services);
        }
        else if (options.StorageProvider.Equals("InMemory", StringComparison.OrdinalIgnoreCase))
        {
            AddSupportRepositories(services);
        }
        else
        {
            throw new InvalidOperationException($"Unsupported storage provider '{options.StorageProvider}'. Use InMemory for isolated local/test execution or SqlServer for deployed environments.");
        }

        if (options.EmailProvider.Equals("Mock", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IEmailIngestionProvider, MockEmailIngestionProvider>();
        }
        else
        {
            services.AddSingleton<IEmailIngestionProvider, DisabledEmailIngestionProvider>();
        }
        services.AddHttpClient<IGraphMailClient, MicrosoftGraphMailClient>();
        services.AddSingleton<IGraphMailProvider, GraphMailProvider>();
        if (options.EmailProvider.Equals("Graph", StringComparison.OrdinalIgnoreCase))
        {
            services.AddScoped<IEmailAttachmentProcessor, GraphEmailAttachmentProcessor>();
        }
        else
        {
            services.AddSingleton<IEmailAttachmentProcessor, MockEmailAttachmentProcessor>();
        }
        services.AddScoped<IEmailToCaseMatcher, BasicEmailToCaseMatcher>();
        if (options.CommunicationProvider.Equals("GraphEmail", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<ICommunicationSendingProvider, GraphCommunicationSendingProvider>();
        }
        else if (options.CommunicationProvider.Equals("Mock", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<ICommunicationSendingProvider, MockCommunicationSendingProvider>();
        }
        else
        {
            services.AddSingleton<ICommunicationSendingProvider, DisabledCommunicationSendingProvider>();
        }
        if (options.CommunicationProvider.Equals("Mock", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<ISmsProvider, MockSmsProvider>();
        }
        else
        {
            services.AddSingleton<ISmsProvider, DisabledSmsProvider>();
        }

        if (options.NotificationProvider.Equals("Mock", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<ITeamsNotificationProvider, InsuranceIntelligence.Infrastructure.Notifications.MockTeamsNotificationProvider>();
        }
        else if (options.NotificationProvider.Equals("IncomingWebhook", StringComparison.OrdinalIgnoreCase))
        {
            services.Configure<InsuranceIntelligence.Infrastructure.Notifications.IncomingWebhookTeamsProviderOptions>(
                configuration.GetSection("TeamsWebhook"));
            services.AddHttpClient<ITeamsNotificationProvider, InsuranceIntelligence.Infrastructure.Notifications.IncomingWebhookTeamsProvider>();
        }
        else
        {
            services.AddSingleton<ITeamsNotificationProvider, DisabledTeamsNotificationProvider>();
        }
        if (options.EmailProvider.Equals("Mock", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IWebhookSubscriptionProvider, MockWebhookSubscriptionProvider>();
        }
        else
        {
            services.AddSingleton<IWebhookSubscriptionProvider, DisabledWebhookSubscriptionProvider>();
        }

        return services;
    }

    public static async Task InitializeInsuranceIntelligenceInfrastructureAsync(
        this IServiceProvider serviceProvider,
        IHostEnvironment environment,
        CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var options = scope.ServiceProvider.GetRequiredService<InfrastructureOptions>();
        if (options.StorageProvider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase)
            && (environment.IsDevelopment() || options.AuthMode.Equals("Development", StringComparison.OrdinalIgnoreCase)))
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<InsuranceIntelligenceDbContext>();
            await dbContext.Database.EnsureCreatedAsync(cancellationToken);
        }

        if (options.SeedDemoData)
        {
            await DemoDataSeeder.SeedAsync(scope.ServiceProvider, cancellationToken);
        }
    }

    private static void AddEfRepositories(IServiceCollection services)
    {
        services.AddScoped<ICaseRepository, EfCaseRepository>();
        services.AddScoped<IAuditRepository, EfAuditRepository>();
        services.AddScoped<ICaseDocumentRepository, EfCaseDocumentRepository>();
        services.AddScoped<IKnowledgeDocumentRepository, EfKnowledgeDocumentRepository>();
        services.AddScoped<IAgentActionLogRepository, EfAgentActionLogRepository>();
        services.AddScoped<IWorkflowTaskRepository, EfWorkflowTaskRepository>();
        services.AddScoped<IApprovalRepository, EfApprovalRepository>();
        services.AddScoped<ICustomerUpdateRepository, EfCustomerUpdateRepository>();
        services.AddScoped<ICommunicationRepository, EfCommunicationRepository>();
        services.AddScoped<ITenantConfigurationRepository, EfTenantConfigurationRepository>();
        services.AddScoped<IUserAccountRepository, EfUserAccountRepository>();
        services.AddScoped<IMailboxConnectionRepository, EfMailboxConnectionRepository>();
        services.AddScoped<IInboundEmailRepository, EfInboundEmailRepository>();
        services.AddScoped<ITeamsNotificationRepository, EfTeamsNotificationRepository>();
        services.AddScoped<InsuranceIntelligence.Domain.Customers.ICustomerRepository, EfCustomerRepository>();
        services.AddScoped<InsuranceIntelligence.Domain.Policies.IPolicyRepository, EfPolicyRepository>();
        services.AddScoped<InsuranceIntelligence.Domain.Policies.ICoverageRepository, EfCoverageRepository>();
        services.AddScoped<InsuranceIntelligence.Domain.Distribution.IBranchRepository, EfBranchRepository>();
        services.AddScoped<InsuranceIntelligence.Domain.Distribution.IProducerRepository, EfProducerRepository>();
        services.AddScoped<IPermissionReadRepository, EfPermissionReadRepository>();
        services.AddScoped<IAuditEventRepository, EfAuditEventRepository>();
        services.AddScoped<IClaimsReadRepository, EfClaimsReadRepository>();
        // W2.5: read-side adapters backing the Phase 2 query services. Channels and Sales Leads
        // have no ingested data source yet, so their adapters return empty/null honestly.
        services.AddScoped<IAuditReadRepository, EfAuditReadRepository>();
        services.AddScoped<ICustomerReadRepository, EfCustomerReadRepository>();
        services.AddScoped<IChannelReadRepository, EfChannelReadRepository>();
        services.AddScoped<ISalesLeadReadRepository, EfSalesLeadReadRepository>();
        services.AddScoped<InsuranceIntelligence.Application.Agents.IAgentStateRepository, EfAgentStateRepository>();
        // W1.3: use the SQL-aware health check (wraps AzureProviderHealthCheck and adds a real
        // Azure SQL connectivity probe). The other three Core ports (ITenantContextAccessor,
        // IPermissionService, IAuditWriter) are already correctly implemented via the hexagonal
        // service + EF-repository pattern above (PermissionService/AuditWriter delegate to the
        // registered EfPermissionReadRepository/EfAuditEventRepository), so no *Ef service
        // duplicates are registered for them.
        services.AddScoped<IProviderHealthCheck, ProviderHealthCheckEf>();
    }

    private static void AddSupportRepositories(IServiceCollection services)
    {
        services.AddSingleton<ITenantConfigurationRepository, InMemoryTenantConfigurationRepository>();
        services.AddSingleton<IUserAccountRepository, InMemoryUserAccountRepository>();
        services.AddSingleton<ICommunicationRepository, InMemoryCommunicationRepository>();
        services.AddSingleton<IMailboxConnectionRepository, InMemoryMailboxConnectionRepository>();
        services.AddSingleton<IInboundEmailRepository, InMemoryInboundEmailRepository>();
        services.AddSingleton<ITeamsNotificationRepository, InMemoryTeamsNotificationRepository>();
        services.AddSingleton<InsuranceIntelligence.Domain.Customers.ICustomerRepository, InMemoryCustomerRepository>();
        services.AddSingleton<InsuranceIntelligence.Domain.Policies.IPolicyRepository, InMemoryPolicyRepository>();
        services.AddSingleton<InsuranceIntelligence.Domain.Policies.ICoverageRepository, InMemoryCoverageRepository>();
        services.AddSingleton<InsuranceIntelligence.Domain.Distribution.IBranchRepository, InMemoryBranchRepository>();
        services.AddSingleton<InsuranceIntelligence.Domain.Distribution.IProducerRepository, InMemoryProducerRepository>();
    }

    public static InfrastructureOptions BindOptions(IConfiguration configuration) =>
        new()
        {
            StorageProvider = configuration["INSURANCEINTELLIGENCE_STORAGE_PROVIDER"] ?? "InMemory",
            BlobProvider = configuration["INSURANCEINTELLIGENCE_BLOB_PROVIDER"] ?? "Mock",
            AuthMode = configuration["INSURANCEINTELLIGENCE_AUTH_MODE"] ?? "Development",
            RuntimeProfile = configuration["INSURANCEINTELLIGENCE_RUNTIME_PROFILE"],
            TenantId = configuration["INSURANCEINTELLIGENCE_TENANT_ID"],
            WorkspaceId = configuration["INSURANCEINTELLIGENCE_WORKSPACE_ID"],
            AllowProductionMockProviders = bool.TryParse(configuration["INSURANCEINTELLIGENCE_ALLOW_PRODUCTION_MOCKS"], out var allowProductionMocks) && allowProductionMocks,
            SeedDemoData = bool.TryParse(configuration["INSURANCEINTELLIGENCE_SEED_DEMO_DATA"], out var seed) && seed,
            SqlServerConnectionString = configuration["INSURANCEINTELLIGENCE_SQLSERVER_CONNECTION_STRING"]
                ?? configuration.GetConnectionString("InsuranceIntelligenceSql"),
            EntraTenantId = configuration["INSURANCEINTELLIGENCE_ENTRA_TENANT_ID"],
            EntraClientId = configuration["INSURANCEINTELLIGENCE_ENTRA_CLIENT_ID"],
            EntraAudience = configuration["INSURANCEINTELLIGENCE_ENTRA_AUDIENCE"],
            EntraIssuer = configuration["INSURANCEINTELLIGENCE_ENTRA_ISSUER"],
            AiProvider = configuration["INSURANCEINTELLIGENCE_AI_PROVIDER"] ?? "Mock",
            DocumentProvider = configuration["INSURANCEINTELLIGENCE_DOCUMENT_PROVIDER"] ?? "Mock",
            RetrievalProvider = configuration["INSURANCEINTELLIGENCE_RETRIEVAL_PROVIDER"] ?? "Mock",
            QueueProvider = configuration["INSURANCEINTELLIGENCE_QUEUE_PROVIDER"] ?? "Mock",
            EmailProvider = configuration["INSURANCEINTELLIGENCE_EMAIL_PROVIDER"] ?? "Mock",
            CommunicationProvider = configuration["INSURANCEINTELLIGENCE_COMMUNICATION_PROVIDER"] ?? "Mock",
            NotificationProvider = configuration["INSURANCEINTELLIGENCE_NOTIFICATION_PROVIDER"] ?? "Mock",
            AzureBlob = new AzureBlobOptions
            {
                AccountName = configuration["AzureBlob:AccountName"],
                Endpoint = configuration["AzureBlob:Endpoint"],
                ConnectionString = configuration["AzureBlob:ConnectionString"],
                DocumentContainer = configuration["AzureBlob:DocumentContainer"] ?? "raw-documents",
                KnowledgeContainer = configuration["AzureBlob:KnowledgeContainer"] ?? "knowledge-documents",
                EvidenceContainer = configuration["AzureBlob:EvidenceContainer"] ?? "audit-exports",
                UseManagedIdentity = !bool.TryParse(configuration["AzureBlob:UseManagedIdentity"], out var blobManagedIdentity) || blobManagedIdentity
            },
            DocumentIntelligence = new DocumentIntelligenceOptions
            {
                Endpoint = configuration["DocumentIntelligence:Endpoint"],
                ModelMode = configuration["DocumentIntelligence:ModelMode"] ?? "Prebuilt",
                DefaultModelId = configuration["DocumentIntelligence:DefaultModelId"] ?? "prebuilt-document",
                UseManagedIdentity = !bool.TryParse(configuration["DocumentIntelligence:UseManagedIdentity"], out var documentManagedIdentity) || documentManagedIdentity
            },
            AzureAiSearch = new AzureAiSearchOptions
            {
                Endpoint = configuration["AzureAiSearch:Endpoint"],
                IndexName = configuration["AzureAiSearch:IndexName"],
                UseManagedIdentity = !bool.TryParse(configuration["AzureAiSearch:UseManagedIdentity"], out var searchManagedIdentity) || searchManagedIdentity,
                SemanticConfigurationName = configuration["AzureAiSearch:SemanticConfigurationName"],
                VectorSearchEnabled = bool.TryParse(configuration["AzureAiSearch:VectorSearchEnabled"], out var vectorSearch) && vectorSearch
            },
            AzureOpenAI = new AzureOpenAiOptions
            {
                Endpoint = configuration["AzureOpenAI:Endpoint"],
                ChatDeployment = configuration["AzureOpenAI:ChatDeployment"],
                EmbeddingDeployment = configuration["AzureOpenAI:EmbeddingDeployment"],
                PolicyAnswerPromptTemplatePath = configuration["AzureOpenAI:PolicyAnswerPromptTemplatePath"],
                ApiVersion = configuration["AzureOpenAI:ApiVersion"] ?? "2024-10-21",
                UseManagedIdentity = !bool.TryParse(configuration["AzureOpenAI:UseManagedIdentity"], out var openAiManagedIdentity) || openAiManagedIdentity,
                MaxTokens = int.TryParse(configuration["AzureOpenAI:MaxTokens"], out var maxTokens) ? maxTokens : 800,
                Temperature = decimal.TryParse(configuration["AzureOpenAI:Temperature"], out var temperature) ? temperature : 0.25m,
                RequestTimeoutSeconds = int.TryParse(configuration["AzureOpenAI:RequestTimeoutSeconds"], out var requestTimeout) ? requestTimeout : 30
            },
            ServiceBus = new ServiceBusOptions
            {
                FullyQualifiedNamespace = configuration["ServiceBus:FullyQualifiedNamespace"],
                UseManagedIdentity = !bool.TryParse(configuration["ServiceBus:UseManagedIdentity"], out var serviceBusManagedIdentity) || serviceBusManagedIdentity,
                EmailIngestionQueue = configuration["ServiceBus:EmailIngestionQueue"] ?? "email-ingestion",
                DocumentProcessingQueue = configuration["ServiceBus:DocumentProcessingQueue"] ?? "document-processing",
                KnowledgeIndexingQueue = configuration["ServiceBus:KnowledgeIndexingQueue"] ?? "knowledge-indexing",
                OutboundCommunicationsQueue = configuration["ServiceBus:OutboundCommunicationsQueue"] ?? "outbound-communications",
                ApprovalNotificationsQueue = configuration["ServiceBus:ApprovalNotificationsQueue"] ?? "approval-notifications",
                AuditEventsQueue = configuration["ServiceBus:AuditEventsQueue"] ?? "audit-events"
            },
            GraphMail = new GraphMailOptions
            {
                TenantId = configuration["GraphMail:TenantId"] ?? configuration["INSURANCEINTELLIGENCE_ENTRA_TENANT_ID"],
                ClientId = configuration["GraphMail:ClientId"] ?? configuration["INSURANCEINTELLIGENCE_ENTRA_CLIENT_ID"],
                ClientSecret = configuration["GraphMail:ClientSecret"],
                DefaultMailboxAddress = configuration["GraphMail:DefaultMailboxAddress"],
                GraphBaseUrl = configuration["GraphMail:GraphBaseUrl"] ?? "https://graph.microsoft.com/v1.0",
                UseManagedIdentity = bool.TryParse(configuration["GraphMail:UseManagedIdentity"], out var graphManagedIdentity) && graphManagedIdentity,
                MailboxAccessScoped = bool.TryParse(configuration["GraphMail:MailboxAccessScoped"], out var mailboxAccessScoped) && mailboxAccessScoped,
                ProcessUnreadOnly = !bool.TryParse(configuration["GraphMail:ProcessUnreadOnly"], out var unreadOnly) || unreadOnly,
                MoveProcessedMessages = !bool.TryParse(configuration["GraphMail:MoveProcessedMessages"], out var moveProcessed) || moveProcessed,
                ProcessedFolderName = configuration["GraphMail:ProcessedFolderName"] ?? "InsuranceIntelligence Processed",
                MaxMessagesPerPull = int.TryParse(configuration["GraphMail:MaxMessagesPerPull"], out var maxGraphMessages) ? maxGraphMessages : 10
            },
            ProviderHealth = new ProviderHealthOptions
            {
                TimeoutSeconds = int.TryParse(configuration["ProviderHealth:TimeoutSeconds"], out var healthTimeout) ? healthTimeout : 5
            }
        };
}
