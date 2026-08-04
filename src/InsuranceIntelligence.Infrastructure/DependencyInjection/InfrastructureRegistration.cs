using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Application.Audit;
using InsuranceIntelligence.Application.Channels;
using InsuranceIntelligence.Application.Claims;
using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Application.Customers;
using InsuranceIntelligence.Application.SalesLeads;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Communications;
using InsuranceIntelligence.Domain.Contestability;
using InsuranceIntelligence.Domain.Documents;
using InsuranceIntelligence.Domain.Integrations;
using InsuranceIntelligence.Domain.Knowledge;
using InsuranceIntelligence.Domain.Notifications;
using InsuranceIntelligence.Domain.Workflow;
using InsuranceIntelligence.Infrastructure.Communications;
using InsuranceIntelligence.Infrastructure.Configuration;
using InsuranceIntelligence.Infrastructure.Persistence;
using InsuranceIntelligence.Infrastructure.Email;
using InsuranceIntelligence.Infrastructure.Graph;
using InsuranceIntelligence.Infrastructure.Notifications;
using InsuranceIntelligence.Infrastructure.Providers;
using InsuranceIntelligence.Infrastructure.Telemetry;
using InsuranceIntelligence.Infrastructure.WhatsApp;
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
        services.AddSingleton<IOperationalTelemetry, LoggingOperationalTelemetry>();

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
        else
        {
            throw new InvalidOperationException($"Unsupported storage provider '{options.StorageProvider}'. Deployable runtime requires SqlServer; use explicit LocalTest projects for test doubles.");
        }

        services.AddSingleton<IEmailIngestionProvider, DisabledEmailIngestionProvider>();
        services.AddHttpClient<IGraphMailClient, MicrosoftGraphMailClient>();
        services.AddSingleton<IGraphMailProvider, GraphMailProvider>();
        if (options.EmailProvider.Equals("Graph", StringComparison.OrdinalIgnoreCase))
        {
            services.AddScoped<IEmailAttachmentProcessor, GraphEmailAttachmentProcessor>();
        }
        else
        {
            services.AddSingleton<IEmailAttachmentProcessor, DisabledEmailAttachmentProcessor>();
        }
        services.AddScoped<IEmailToCaseMatcher, RuleBasedEmailToCaseMatcher>();
        if (options.CommunicationProvider.Equals("GraphEmail", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<ICommunicationSendingProvider, GraphCommunicationSendingProvider>();
        }
        else
        {
            services.AddSingleton<ICommunicationSendingProvider, DisabledCommunicationSendingProvider>();
        }
        if (options.SmsProvider.Equals("AfricasTalking", StringComparison.OrdinalIgnoreCase))
        {
            services.AddHttpClient<ISmsProvider, AfricasTalkingSmsProvider>();
        }
        else
        {
            services.AddSingleton<ISmsProvider, DisabledSmsProvider>();
        }

        if (options.ChannelProvider.Equals("WhatsAppCloudApi", StringComparison.OrdinalIgnoreCase))
        {
            services.AddHttpClient<IWhatsAppInboundProvider, WhatsAppCloudApiInboundProvider>();
            services.AddScoped<IWhatsAppAttachmentProcessor, WhatsAppAttachmentProcessor>();
        }
        else
        {
            services.AddSingleton<IWhatsAppInboundProvider, DisabledWhatsAppInboundProvider>();
            services.AddSingleton<IWhatsAppAttachmentProcessor, DisabledWhatsAppAttachmentProcessor>();
        }

        services.AddScoped<IWhatsAppToCaseMatcher, RuleBasedWhatsAppToCaseMatcher>();
        services.AddScoped<WhatsAppInboundDocumentIngestionService>();

        if (options.NotificationProvider.Equals("IncomingWebhook", StringComparison.OrdinalIgnoreCase))
        {
            services.Configure<InsuranceIntelligence.Infrastructure.Notifications.IncomingWebhookTeamsProviderOptions>(
                configuration.GetSection("TeamsWebhook"));
            services.AddHttpClient<ITeamsNotificationProvider, InsuranceIntelligence.Infrastructure.Notifications.IncomingWebhookTeamsProvider>();
        }
        else
        {
            services.AddSingleton<ITeamsNotificationProvider, DisabledTeamsNotificationProvider>();
        }
        services.AddSingleton<IWebhookSubscriptionProvider, DisabledWebhookSubscriptionProvider>();

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
        services.AddScoped<IClaimReviewRequestRepository, EfClaimReviewRequestRepository>();
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
        services.AddScoped<EfSalesLeadRepository>();
        services.AddScoped<ISalesLeadReadRepository>(sp => sp.GetRequiredService<EfSalesLeadRepository>());
        services.AddScoped<ISalesLeadWriteRepository>(sp => sp.GetRequiredService<EfSalesLeadRepository>());
        services.AddScoped<InsuranceIntelligence.Application.Tenants.IWorkspaceCatalogRepository, EfWorkspaceCatalogRepository>();
        services.AddScoped<InsuranceIntelligence.Application.Onboarding.ITenantOnboardingRepository, EfTenantOnboardingRepository>();
        services.AddScoped<InsuranceIntelligence.Application.Onboarding.ITenantMembershipWriter, EfTenantMembershipWriter>();
        services.AddScoped<InsuranceIntelligence.Application.Agents.IAgentStateRepository, EfAgentStateRepository>();
        services.AddScoped<InsuranceIntelligence.Application.AgentWorkflows.IAgentWorkflowDefinitionRepository, EfAgentWorkflowDefinitionRepository>();
        services.AddScoped<InsuranceIntelligence.Application.AgentWorkflows.IAgentWorkflowRunRepository, EfAgentWorkflowRunRepository>();
        services.AddScoped<InsuranceIntelligence.Application.AgentWorkflows.IAgentWorkItemRepository, EfAgentWorkItemRepository>();
        services.AddScoped<InsuranceIntelligence.Application.Documents.IDocumentReadRepository, EfDocumentReadRepository>();
        // W1.3: use the SQL-aware health check (wraps AzureProviderHealthCheck and adds a real
        // Azure SQL connectivity probe). The other three Core ports (ITenantContextAccessor,
        // IPermissionService, IAuditWriter) are already correctly implemented via the hexagonal
        // service + EF-repository pattern above (PermissionService/AuditWriter delegate to the
        // registered EfPermissionReadRepository/EfAuditEventRepository), so no *Ef service
        // duplicates are registered for them.
        services.AddScoped<IProviderHealthCheck, ProviderHealthCheckEf>();
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
            AzureRegion = configuration["INSURANCEINTELLIGENCE_AZURE_REGION"],
            DataResidencyCountry = configuration["INSURANCEINTELLIGENCE_DATA_RESIDENCY_COUNTRY"],
            DataResidencyGeography = configuration["INSURANCEINTELLIGENCE_DATA_RESIDENCY_GEOGRAPHY"],
            DataResidencyMode = configuration["INSURANCEINTELLIGENCE_DATA_RESIDENCY_MODE"] ?? "regional",
            DataResidencyAllowedAzureRegions = configuration["INSURANCEINTELLIGENCE_DATA_RESIDENCY_ALLOWED_AZURE_REGIONS"],
            DataResidencyApprovalReference = configuration["INSURANCEINTELLIGENCE_DATA_RESIDENCY_APPROVAL_REFERENCE"],
            AiProvider = configuration["INSURANCEINTELLIGENCE_AI_PROVIDER"] ?? "Mock",
            DocumentProvider = configuration["INSURANCEINTELLIGENCE_DOCUMENT_PROVIDER"] ?? "Mock",
            RetrievalProvider = configuration["INSURANCEINTELLIGENCE_RETRIEVAL_PROVIDER"] ?? "Mock",
            QueueProvider = configuration["INSURANCEINTELLIGENCE_QUEUE_PROVIDER"] ?? "Mock",
            EmailProvider = configuration["INSURANCEINTELLIGENCE_EMAIL_PROVIDER"] ?? "Mock",
            CommunicationProvider = configuration["INSURANCEINTELLIGENCE_COMMUNICATION_PROVIDER"] ?? "Mock",
            SmsProvider = configuration["INSURANCEINTELLIGENCE_SMS_PROVIDER"] ?? "Mock",
            NotificationProvider = configuration["INSURANCEINTELLIGENCE_NOTIFICATION_PROVIDER"] ?? "Mock",
            ChannelProvider = configuration["INSURANCEINTELLIGENCE_CHANNEL_PROVIDER"] ?? "Mock",
            ProvisionerProvider = configuration["INSURANCEINTELLIGENCE_PROVISIONER_PROVIDER"] ?? "Mock",
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
                DeploymentType = configuration["AzureOpenAI:DeploymentType"],
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
                AgentWorkflowWorkerUserId = configuration["ServiceBus:AgentWorkflowWorkerUserId"] ?? "system:agent-workflow-worker",
                EmailIngestionQueue = configuration["ServiceBus:EmailIngestionQueue"] ?? "email-ingestion",
                DocumentProcessingQueue = configuration["ServiceBus:DocumentProcessingQueue"] ?? "document-processing",
                KnowledgeIndexingQueue = configuration["ServiceBus:KnowledgeIndexingQueue"] ?? "knowledge-indexing",
                OutboundCommunicationsQueue = configuration["ServiceBus:OutboundCommunicationsQueue"] ?? "outbound-communications",
                ApprovalNotificationsQueue = configuration["ServiceBus:ApprovalNotificationsQueue"] ?? "approval-notifications",
                AuditEventsQueue = configuration["ServiceBus:AuditEventsQueue"] ?? "audit-events",
                AgentWorkflowQueue = configuration["ServiceBus:AgentWorkflowQueue"] ?? "agent-workflow"
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
            AfricasTalking = new AfricasTalkingOptions
            {
                Username = configuration["AfricasTalking:Username"],
                ApiKey = configuration["AfricasTalking:ApiKey"],
                SenderId = configuration["AfricasTalking:SenderId"],
                BaseUrl = configuration["AfricasTalking:BaseUrl"] ?? "https://api.africastalking.com",
                RequestTimeoutSeconds = int.TryParse(configuration["AfricasTalking:RequestTimeoutSeconds"], out var atTimeout) ? atTimeout : 15,
                AutoSendTransactionalSmsEnabled = bool.TryParse(configuration["AfricasTalking:AutoSendTransactionalSmsEnabled"], out var autoSms) && autoSms,
            },
            WhatsApp = new WhatsAppOptions
            {
                PhoneNumberId = configuration["WhatsApp:PhoneNumberId"],
                AccessToken = configuration["WhatsApp:AccessToken"],
                AppSecret = configuration["WhatsApp:AppSecret"],
                VerifyToken = configuration["WhatsApp:VerifyToken"],
                TenantId = configuration["WhatsApp:TenantId"] ?? configuration["INSURANCEINTELLIGENCE_TENANT_ID"],
                WorkspaceId = configuration["WhatsApp:WorkspaceId"] ?? configuration["INSURANCEINTELLIGENCE_WORKSPACE_ID"],
                IntegrationUserId = configuration["WhatsApp:IntegrationUserId"],
                GraphBaseUrl = configuration["WhatsApp:GraphBaseUrl"] ?? "https://graph.facebook.com/v21.0",
                RequestTimeoutSeconds = int.TryParse(configuration["WhatsApp:RequestTimeoutSeconds"], out var waTimeout) ? waTimeout : 20,
            },
            ProviderHealth = new ProviderHealthOptions
            {
                TimeoutSeconds = int.TryParse(configuration["ProviderHealth:TimeoutSeconds"], out var healthTimeout) ? healthTimeout : 5
            }
        };
}
