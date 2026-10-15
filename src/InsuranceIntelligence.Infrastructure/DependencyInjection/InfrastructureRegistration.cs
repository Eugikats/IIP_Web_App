using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Application.Audit;
using InsuranceIntelligence.Application.Channels;
using InsuranceIntelligence.Application.Claims;
using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Application.Customers;
using InsuranceIntelligence.Application.CommandCenter;
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
using Microsoft.Extensions.DependencyInjection.Extensions;
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
                db.UseSqlServer(options.SqlServerConnectionString, SqlServerResilience.Configure));
            AddEfRepositories(services);
        }
        else if (options.StorageProvider.Equals("InMemory", StringComparison.OrdinalIgnoreCase)
            && IsLocalOnlyRuntime(options, environment))
        {
            // Isolated local/test execution only, exactly as the SQLite guard above directs.
            // Data lives for the lifetime of the process and is never durable. This branch is
            // unreachable in every deployed profile twice over: the runtime-profile check here,
            // and AzureProviderStartupValidator.ValidateProduction, which independently refuses
            // to boot Staging/Production/ManagedApplicationReviewer/Demo on non-SqlServer
            // storage. It exists so the team can run the platform with no Azure credentials.
            services.AddDbContext<InsuranceIntelligenceDbContext>(db =>
                db.UseInMemoryDatabase("insuranceintelligence-localtest"));
            AddEfRepositories(services);
        }
        else
        {
            throw new InvalidOperationException($"Unsupported storage provider '{options.StorageProvider}'. Deployable runtime requires SqlServer; use explicit LocalTest projects for test doubles.");
        }

        services.AddHttpClient<IGraphMailClient, MicrosoftGraphMailClient>();
        services.AddSingleton<IGraphMailProvider, GraphMailProvider>();
        if (options.EmailProvider.Equals("Graph", StringComparison.OrdinalIgnoreCase))
        {
            // TestIngestion pulls live mail through IGraphMailProvider in Graph mode, this type is
            // registered to satisfy DI but is never actually invoked there.
            services.AddSingleton<IEmailIngestionProvider, DisabledEmailIngestionProvider>();
            services.AddScoped<IEmailAttachmentProcessor, GraphEmailAttachmentProcessor>();
        }
        else
        {
            services.AddSingleton<IEmailIngestionProvider, InsuranceIntelligence.Infrastructure.Email.LocalEmailIngestionProvider>();
            services.AddSingleton<IEmailAttachmentProcessor, InsuranceIntelligence.Infrastructure.Email.LocalEmailAttachmentProcessor>();
        }
        services.AddScoped<IEmailToCaseMatcher, RuleBasedEmailToCaseMatcher>();
        // Connection model (docs/01-strategy-requirements-roadmap/IIP_HACKATHON_READINESS_BLUEPRINT.md
        // §3): tenant-configured provider credentials, validated against the real provider before
        // persisting, encrypted at rest, write-only from the browser once saved, generalized from
        // the WhatsApp settings screen to every connection type. Scoped: resolves through
        // IIntegrationConnectionRepository, which is DbContext-backed.
        services.AddScoped<InsuranceIntelligence.Domain.Integrations.IIntegrationConnectionRepository, InsuranceIntelligence.Infrastructure.Connections.EfIntegrationConnectionRepository>();
        services.AddScoped<InsuranceIntelligence.Domain.Integrations.IUserFileConnectionRepository, InsuranceIntelligence.Infrastructure.Connections.EfUserFileConnectionRepository>();
        services.AddScoped<InsuranceIntelligence.Domain.Security.IUserSecurityRepository, InsuranceIntelligence.Infrastructure.Security.EfUserSecurityRepository>();
        services.AddScoped<InsuranceIntelligence.Domain.Security.IMfaPolicyRepository, InsuranceIntelligence.Infrastructure.Security.EfMfaPolicyRepository>();
        services.AddScoped<InsuranceIntelligence.Domain.Compliance.ILegalHoldRepository, InsuranceIntelligence.Infrastructure.Compliance.EfLegalHoldRepository>();
        services.AddScoped<InsuranceIntelligence.Domain.Academy.IAcademyRecordStore, InsuranceIntelligence.Infrastructure.Academy.EfAcademyRecordStore>();
        services.AddScoped<InsuranceIntelligence.Domain.Integrations.IDataConnectorRepository, InsuranceIntelligence.Infrastructure.Connectors.EfDataConnectorRepository>();
        services.AddScoped<InsuranceIntelligence.Domain.Integrations.IConnectorRunRepository, InsuranceIntelligence.Infrastructure.Connectors.EfConnectorRunRepository>();
        services.AddScoped<InsuranceIntelligence.Domain.Integrations.IConnectorItemRepository, InsuranceIntelligence.Infrastructure.Connectors.EfConnectorItemRepository>();
        services.AddScoped<InsuranceIntelligence.Domain.Integrations.IWhatsAppWebhookReceiptRepository, InsuranceIntelligence.Infrastructure.WhatsApp.EfWhatsAppWebhookReceiptRepository>();
        services.AddSingleton(InsuranceIntelligence.Application.Billing.AiCreditPricing.Default);
        services.AddScoped<InsuranceIntelligence.Application.Billing.IAiUsageContext, InsuranceIntelligence.Application.Billing.AiUsageContext>();
        services.AddScoped<InsuranceIntelligence.Application.Billing.IAiUsageRepository, InsuranceIntelligence.Infrastructure.Billing.EfAiUsageRepository>();
        services.AddScoped<InsuranceIntelligence.Application.Billing.IAiCreditGate, InsuranceIntelligence.Infrastructure.Billing.AiCreditGate>();
        services.AddScoped<InsuranceIntelligence.Application.Billing.IAiUsageRecorder, InsuranceIntelligence.Infrastructure.Billing.AiUsageRecorder>();
        services.AddScoped<InsuranceIntelligence.Domain.Agents.IAgentBehaviourProfileRepository, InsuranceIntelligence.Infrastructure.Agents.EfAgentBehaviourProfileRepository>();
        services.AddScoped<InsuranceIntelligence.Domain.Products.IProductRepository, InsuranceIntelligence.Infrastructure.Products.EfProductRepository>();
        services.AddScoped<InsuranceIntelligence.Domain.Workflow.ICommunicationTemplateRepository, InsuranceIntelligence.Infrastructure.Workflow.EfCommunicationTemplateRepository>();
        services.AddScoped<InsuranceIntelligence.Domain.Documents.IDocumentTypeCatalogRepository, InsuranceIntelligence.Infrastructure.Documents.EfDocumentTypeCatalogRepository>();
        services.AddScoped<InsuranceIntelligence.Domain.Documents.IDocumentLabelledExampleRepository, InsuranceIntelligence.Infrastructure.Documents.EfDocumentLabelledExampleRepository>();
        services.AddSingleton<InsuranceIntelligence.Domain.Integrations.IConnectionSecretProtector, InsuranceIntelligence.Infrastructure.Connections.DataProtectionConnectionSecretProtector>();
        services.AddSingleton<ISmsProviderFactory, SmsProviderFactory>();
        services.AddSingleton<InsuranceIntelligence.Domain.Integrations.IConnectionValidator, EgoSmsConnectionValidator>();
        services.AddSingleton<InsuranceIntelligence.Domain.Integrations.IConnectionValidator, AfricasTalkingConnectionValidator>();
        services.AddSingleton<IEmailSendingProviderFactory, EmailSendingProviderFactory>();
        services.AddSingleton<InsuranceIntelligence.Domain.Integrations.IConnectionValidator, SmtpConnectionValidator>();

        // ISmsProvider/IEmailSendingProvider are always the tenant-aware resolvers now, the
        // three-step order (tenant Connection, then env var fallback, then Not configured).
        // SmsProvider/AfricasTalking and CommunicationProvider/GraphMail options below still govern
        // step 2, the fallback, exactly as before. Scoped because they depend on the scoped
        // connection repository.
        services.AddScoped<ISmsProvider, TenantAwareSmsProvider>();
        services.AddScoped<IEmailSendingProvider, TenantAwareEmailSendingProvider>();

        // Policy lookup port (blueprint §14 Phase 7, WO 7.1/7.2), same tenant-aware resolution
        // shape as SMS/email above, except the fallback (this platform's own policy storage) is
        // always a meaningful answer, so there is no third "not configured" state.
        services.AddSingleton<InsuranceIntelligence.Domain.Integrations.IConnectionValidator, InsuranceIntelligence.Infrastructure.Policies.GenericRestPolicyConnectionValidator>();
        services.AddScoped<InsuranceIntelligence.Infrastructure.Policies.InternalPolicyLookupProvider>();
        services.AddScoped<InsuranceIntelligence.Domain.Policies.IPolicyLookupProvider, InsuranceIntelligence.Infrastructure.Policies.TenantAwarePolicyLookupProvider>();

        if (options.CommunicationProvider.Equals("GraphEmail", StringComparison.OrdinalIgnoreCase))
        {
            services.AddScoped<ICommunicationSendingProvider, GraphCommunicationSendingProvider>();
        }
        else
        {
            services.AddScoped<ICommunicationSendingProvider, DisabledCommunicationSendingProvider>();
        }

        // Registered unconditionally, the settings screen (WhatsAppConfigurationEndpoints) must be
        // able to capture, validate, and store WhatsApp credentials even before an operator has
        // also switched INSURANCEINTELLIGENCE_CHANNEL_PROVIDER to WhatsAppCloudApi at the
        // deployment level.
        services.AddScoped<InsuranceIntelligence.Domain.Integrations.IWhatsAppChannelConfigurationRepository, EfWhatsAppChannelConfigurationRepository>();
        services.AddSingleton<InsuranceIntelligence.Domain.Integrations.IWhatsAppCredentialProtector, DataProtectionWhatsAppCredentialProtector>();
        services.AddSingleton<IWhatsAppRuntimeSettingsProvider, WhatsAppRuntimeSettingsProvider>();
        services.AddScoped<InsuranceIntelligence.Domain.Common.IWorkspaceDirectory, InsuranceIntelligence.Infrastructure.Persistence.EfWorkspaceDirectory>();
        services.AddScoped<InsuranceIntelligence.Domain.Organisations.IOrganisationRepository, InsuranceIntelligence.Infrastructure.Persistence.EfOrganisationRepository>();
        services.AddScoped<InsuranceIntelligence.Domain.Webhooks.IOutboundWebhookRepository, InsuranceIntelligence.Infrastructure.Webhooks.EfOutboundWebhookRepository>();
        services.AddScoped<InsuranceIntelligence.Application.Webhooks.OutboundEventPublisher>();
        services.AddSingleton<InsuranceIntelligence.Application.Webhooks.IWebhookHttpSender, InsuranceIntelligence.Infrastructure.Webhooks.SafeWebhookHttpSender>();
        services.AddScoped<InsuranceIntelligence.Application.Webhooks.OutboundWebhookDispatcher>();
        services.AddScoped<InsuranceIntelligence.Domain.Organisations.IApiKeyRepository, InsuranceIntelligence.Infrastructure.Persistence.EfApiKeyRepository>();
        services.AddScoped<InsuranceIntelligence.Application.Organisations.ApiKeyService>();
        services.AddScoped<InsuranceIntelligence.Application.Organisations.OrganisationService>();
        services.AddScoped<InsuranceIntelligence.Application.Organisations.OrganisationOnboardingService>();
        services.AddScoped<InsuranceIntelligence.Domain.Usage.IUsageMeter, InsuranceIntelligence.Infrastructure.Persistence.EfUsageMeter>();
        services.AddScoped<InsuranceIntelligence.Infrastructure.Persistence.TenantDataService>();

        // Microsoft Marketplace. The real client is used only when the deployment says it is a
        // registered publisher (INSURANCEINTELLIGENCE_MARKETPLACE_ENABLED); otherwise every
        // Marketplace route reports it is not connected rather than pretending.
        services.AddSingleton(InsuranceIntelligence.Application.Marketplace.MarketplacePlanMap.Parse(configuration["INSURANCEINTELLIGENCE_MARKETPLACE_PLAN_MAP"]));
        if (bool.TryParse(configuration["INSURANCEINTELLIGENCE_MARKETPLACE_ENABLED"], out var marketplaceEnabled) && marketplaceEnabled)
        {
            services.AddHttpClient<InsuranceIntelligence.Domain.Marketplace.IMarketplaceFulfillmentClient, InsuranceIntelligence.Infrastructure.Marketplace.MicrosoftMarketplaceFulfillmentClient>()
                .ConfigureHttpClient(client => client.Timeout = TimeSpan.FromSeconds(30));
            services.AddSingleton<Azure.Core.TokenCredential>(_ => new Azure.Identity.DefaultAzureCredential());
        }
        else
        {
            services.AddSingleton<InsuranceIntelligence.Domain.Marketplace.IMarketplaceFulfillmentClient, InsuranceIntelligence.Infrastructure.Marketplace.NotConfiguredMarketplaceFulfillmentClient>();
        }

        services.AddScoped<InsuranceIntelligence.Application.Marketplace.MarketplaceFulfillmentService>();
        services.AddHttpClient<InsuranceIntelligence.Domain.Integrations.IWhatsAppGraphValidationClient, MetaGraphWhatsAppValidationClient>();

        // The real inbound/outbound providers are registered unconditionally now (blueprint §3.3,
        // §4.4) rather than gated on INSURANCEINTELLIGENCE_CHANNEL_PROVIDER, this was previously
        // the whole reason a validated WhatsApp connection still needed a redeploy to go live.
        // It is safe to drop the gate because WhatsAppCloudApiInboundProvider/OutboundProvider
        // already fail closed on their own: IsConfigured is exactly
        // `settingsProvider.Current is not null`, and WhatsAppRuntimeSettingsProvider already
        // implements the same three-step resolution as TenantAwareSmsProvider, a saved, active
        // tenant connection first, then INSURANCEINTELLIGENCE_WHATSAPP_* environment configuration,
        // then null (honestly "not configured", identical to what DisabledWhatsAppInboundProvider
        // reported). ChannelProvider itself is untouched and still governs the separate
        // AzureProviderStartupValidator fail-closed check for Staging/Production.
        services.AddHttpClient<IWhatsAppInboundProvider, WhatsAppCloudApiInboundProvider>();
        // Everything that leaves for a customer passes the content guard and the per-number rate limit first.
        services.AddHttpClient<WhatsAppCloudApiOutboundProvider>();
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<OutboundRateLimiter>();
        services.AddTransient<IWhatsAppOutboundProvider>(provider => new GuardedWhatsAppOutboundProvider(
            provider.GetRequiredService<WhatsAppCloudApiOutboundProvider>(),
            provider.GetRequiredService<OutboundRateLimiter>(),
            provider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<GuardedWhatsAppOutboundProvider>>()));
        services.AddScoped<IWhatsAppAttachmentProcessor, WhatsAppAttachmentProcessor>();

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
        await Task.CompletedTask;
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
        services.AddScoped<InsuranceIntelligence.Domain.Customers.IContactDetailChangeRequestRepository, EfContactDetailChangeRequestRepository>();
        services.AddScoped<ICommunicationRepository, EfCommunicationRepository>();
        services.AddScoped<ITenantConfigurationRepository, EfTenantConfigurationRepository>();
        services.AddScoped<InsuranceIntelligence.Domain.Academy.IAcademyPricingRepository, EfAcademyPricingRepository>();
        services.AddScoped<IUserAccountRepository, EfUserAccountRepository>();
        services.AddScoped<IMailboxConnectionRepository, EfMailboxConnectionRepository>();
        services.AddScoped<IInboundEmailRepository, EfInboundEmailRepository>();
        services.AddScoped<ITeamsNotificationRepository, EfTeamsNotificationRepository>();
        services.AddScoped<INotificationRepository, EfNotificationRepository>();
        services.AddScoped<InsuranceIntelligence.Domain.Fraud.IFraudSignalDispositionRepository, InsuranceIntelligence.Infrastructure.Fraud.EfFraudSignalDispositionRepository>();
        services.AddScoped<InsuranceIntelligence.Domain.Documents.IDocumentFieldValidationRuleRepository, InsuranceIntelligence.Infrastructure.Documents.EfDocumentFieldValidationRuleRepository>();
        services.AddScoped<InsuranceIntelligence.Domain.ClientPortal.IClaimantMagicLinkTokenRepository, InsuranceIntelligence.Infrastructure.ClientPortal.EfClaimantMagicLinkTokenRepository>();
        services.AddScoped<InsuranceIntelligence.Domain.ClientPortal.IClaimantPortalSessionRepository, InsuranceIntelligence.Infrastructure.ClientPortal.EfClaimantPortalSessionRepository>();
        services.AddScoped<InsuranceIntelligence.Domain.CaseReview.ICaseReviewRepository, EfCaseReviewRepository>();
        services.AddScoped<InsuranceIntelligence.Domain.Customers.ICustomerRepository, EfCustomerRepository>();
        services.AddScoped<InsuranceIntelligence.Domain.Policies.IPolicyRepository, EfPolicyRepository>();
        services.AddScoped<InsuranceIntelligence.Domain.Policies.ICoverageRepository, EfCoverageRepository>();
        services.AddScoped<InsuranceIntelligence.Domain.Distribution.IBranchRepository, EfBranchRepository>();
        services.AddScoped<InsuranceIntelligence.Domain.Distribution.IProducerRepository, EfProducerRepository>();
        // Single-tenant deployments never read the organisations table on the sign-in path, so the code can be
        // deployed before its migration is applied.
        services.AddScoped<IPermissionReadRepository>(provider => new EfPermissionReadRepository(
            provider.GetRequiredService<InsuranceIntelligenceDbContext>(),
            checkOrganisationStatus: provider.GetRequiredService<InsuranceIntelligence.Infrastructure.Configuration.InfrastructureOptions>().MultiTenant));
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
        services.AddScoped<InsuranceIntelligence.Domain.Agents.IAgentRunRepository, EfAgentRunRepository>();
        services.AddScoped<InsuranceIntelligence.Application.AgentWorkflows.IAgentWorkflowDefinitionRepository, EfAgentWorkflowDefinitionRepository>();
        services.AddScoped<InsuranceIntelligence.Application.AgentWorkflows.IAgentWorkflowRunRepository, EfAgentWorkflowRunRepository>();
        services.AddScoped<InsuranceIntelligence.Application.AgentWorkflows.IAgentWorkItemRepository, EfAgentWorkItemRepository>();
        services.AddScoped<InsuranceIntelligence.Application.Documents.IDocumentReadRepository, EfDocumentReadRepository>();
        services.AddScoped<ICommandCenterConversationRepository, EfCommandCenterConversationRepository>();
        services.AddScoped<IWhatsAppMessageRepository, EfWhatsAppMessageRepository>();
        services.AddScoped<InsuranceIntelligence.Domain.CustomerAssistant.ICustomerConversationRepository, InsuranceIntelligence.Infrastructure.CustomerAssistant.EfCustomerConversationRepository>();
        services.AddScoped<InsuranceIntelligence.Application.SalesLeads.IConversationActivityReader, InsuranceIntelligence.Infrastructure.CustomerAssistant.EfConversationActivityReader>();
        services.AddScoped<InsuranceIntelligence.Domain.Compliance.IConsentRepository, InsuranceIntelligence.Infrastructure.Compliance.EfConsentRepository>();
        services.AddScoped<InsuranceIntelligence.Domain.Translation.ICustomerLanguagePreferenceRepository, InsuranceIntelligence.Infrastructure.Translation.EfCustomerLanguagePreferenceRepository>();
        services.AddScoped<InsuranceIntelligence.Domain.Retention.IRetentionReminderRepository, InsuranceIntelligence.Infrastructure.Retention.EfRetentionReminderRepository>();
        services.AddScoped<InsuranceIntelligence.Domain.Cases.ISatisfactionResponseRepository, InsuranceIntelligence.Infrastructure.Cases.EfSatisfactionResponseRepository>();
        services.AddScoped<InsuranceIntelligence.Domain.Compliance.IRetentionPolicyRepository, InsuranceIntelligence.Infrastructure.Compliance.EfRetentionPolicyRepository>();
        services.AddScoped<InsuranceIntelligence.Application.Compliance.IRetentionEnforcementService, InsuranceIntelligence.Application.Compliance.RetentionEnforcementService>();
        // Shared by the MailboxIngestion timer and the admin test-ingestion endpoint, so the
        // scheduled and manual collection paths are provably the same code rather than two that
        // can drift -- the same rationale RetentionEnforcementService is registered under.
        services.AddScoped<InsuranceIntelligence.Application.Integrations.IMailboxIngestionService, InsuranceIntelligence.Application.Integrations.MailboxIngestionService>();
        services.AddScoped<InsuranceIntelligence.Application.Compliance.IDataSubjectRightsService, InsuranceIntelligence.Application.Compliance.DataSubjectRightsService>();
        services.AddSingleton<InsuranceIntelligence.Domain.CustomerAssistant.ICustomerIntentClassifier, InsuranceIntelligence.Domain.CustomerAssistant.KeywordCustomerIntentClassifier>();
        services.AddScoped<InsuranceIntelligence.Infrastructure.DemoSeeding.IDemoDataPurgeService, InsuranceIntelligence.Infrastructure.DemoSeeding.EfDemoDataPurgeService>();
        services.AddScoped<InsuranceIntelligence.Application.DemoSeeding.DemoSeedService>();
        services.AddScoped<InsuranceIntelligence.Application.DemoSeeding.BankCustomerDemoSeeder>();
        services.AddScoped<InsuranceIntelligence.Application.DemoSeeding.DemoConversationSeeder>();
        // W1.3: use the SQL-aware health check (wraps AzureProviderHealthCheck and adds a real
        // Azure SQL connectivity probe). The other three Core ports (ITenantContextAccessor,
        // IPermissionService, IAuditWriter) are already correctly implemented via the hexagonal
        // service + EF-repository pattern above (PermissionService/AuditWriter delegate to the
        // registered EfPermissionReadRepository/EfAuditEventRepository), so no *Ef service
        // duplicates are registered for them.
        services.AddScoped<IProviderHealthCheck, ProviderHealthCheckEf>();
    }


    /// <summary>
    /// True only for developer-machine execution (LocalTest / Development / Test), where
    /// non-durable in-memory storage is permitted. Deny-by-default: any profile that is not
    /// explicitly one of these, including anything unrecognised, is treated as deployed and
    /// refused, so a typo or a new profile name can never silently unlock in-memory storage
    /// in an environment that may hold real data.
    /// </summary>
    private static bool IsLocalOnlyRuntime(InfrastructureOptions options, IHostEnvironment environment)
    {
        var profile = string.IsNullOrWhiteSpace(options.RuntimeProfile)
            ? environment.EnvironmentName
            : options.RuntimeProfile;

        return profile.Equals("LocalTest", StringComparison.OrdinalIgnoreCase)
            || profile.Equals("Development", StringComparison.OrdinalIgnoreCase)
            || profile.Equals("Test", StringComparison.OrdinalIgnoreCase);
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
            ExternalIdAuthority = configuration["INSURANCEINTELLIGENCE_EXTERNAL_ID_AUTHORITY"],
            ExternalIdClientId = configuration["INSURANCEINTELLIGENCE_EXTERNAL_ID_CLIENT_ID"],
            ExternalIdAudience = configuration["INSURANCEINTELLIGENCE_EXTERNAL_ID_AUDIENCE"],
            DataProtectionBlobUri = configuration["INSURANCEINTELLIGENCE_DATAPROTECTION_BLOB_URI"],
            DataProtectionKeyUri = configuration["INSURANCEINTELLIGENCE_DATAPROTECTION_KEY_URI"],
            ClientPortalBaseUrl = configuration["INSURANCEINTELLIGENCE_CLIENT_PORTAL_BASE_URL"],
            SmsWebhookSharedSecret = configuration["INSURANCEINTELLIGENCE_SMS_WEBHOOK_SHARED_SECRET"],
            ApiPublicBaseUrl = configuration["INSURANCEINTELLIGENCE_API_PUBLIC_BASE_URL"],
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
            // Absent means Disabled, not Mock: translation is optional, and a deployed profile
            // refuses Mock, so defaulting to it would stop an unconfigured deployment starting.
            TranslationProvider = configuration["INSURANCEINTELLIGENCE_TRANSLATION_PROVIDER"] ?? "Disabled",
            Translation = new TranslationOptions
            {
                Endpoint = configuration["Translation:Endpoint"],
                ApiKey = configuration["Translation:ApiKey"],
                RequestTimeoutSeconds = int.TryParse(configuration["Translation:RequestTimeoutSeconds"], out var trTimeout) && trTimeout > 0 ? trTimeout : 60,
                EnabledLanguages = configuration["Translation:EnabledLanguages"] ?? string.Empty,
                AllowUnreviewedTranslations = bool.TryParse(configuration["Translation:AllowUnreviewedTranslations"], out var trUnreviewed) && trUnreviewed,
            },
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
            MultiTenant = bool.TryParse(configuration["INSURANCEINTELLIGENCE_MULTI_TENANT"], out var multiTenant) && multiTenant,
            WhatsApp = new WhatsAppOptions
            {
                PhoneNumberId = configuration["WhatsApp:PhoneNumberId"],
                AccessToken = configuration["WhatsApp:AccessToken"],
                AppSecret = configuration["WhatsApp:AppSecret"],
                VerifyToken = configuration["WhatsApp:VerifyToken"],
                TenantId = configuration["WhatsApp:TenantId"] ?? configuration["INSURANCEINTELLIGENCE_TENANT_ID"],
                WorkspaceId = configuration["WhatsApp:WorkspaceId"] ?? configuration["INSURANCEINTELLIGENCE_WORKSPACE_ID"],
                IntegrationUserId = configuration["WhatsApp:IntegrationUserId"],
                GraphBaseUrl = configuration["WhatsApp:GraphBaseUrl"] ?? "https://graph.facebook.com/v24.0",
                RequestTimeoutSeconds = int.TryParse(configuration["WhatsApp:RequestTimeoutSeconds"], out var waTimeout) ? waTimeout : 20,
                AssistantAutoReplyEnabled = bool.TryParse(configuration["WhatsApp:AssistantAutoReplyEnabled"], out var autoReply) && autoReply,
            },
            ProviderHealth = new ProviderHealthOptions
            {
                TimeoutSeconds = int.TryParse(configuration["ProviderHealth:TimeoutSeconds"], out var healthTimeout) ? healthTimeout : 5
            }
        };
}
