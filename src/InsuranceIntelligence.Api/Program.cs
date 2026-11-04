using InsuranceIntelligence.Api.Auth;
using InsuranceIntelligence.Api.Common;
using InsuranceIntelligence.Api.Azure;
using InsuranceIntelligence.Api.Admin;
using InsuranceIntelligence.Api.Assistant;
using InsuranceIntelligence.Api.Audit;
using InsuranceIntelligence.Api.Cases;
using InsuranceIntelligence.Api.Channels;
using InsuranceIntelligence.Api.Claims;
using InsuranceIntelligence.Api.ClientPortal;
using InsuranceIntelligence.Api.Customer;
using InsuranceIntelligence.Api.SalesLeads;
using InsuranceIntelligence.Application.Ai;
using InsuranceIntelligence.Application.Audit;
using InsuranceIntelligence.Application.Channels;
using InsuranceIntelligence.Application.Claims;
using InsuranceIntelligence.Application.CommandCenter;
using InsuranceIntelligence.Application.Compliance;
using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Application.Customers;
using InsuranceIntelligence.Application.SalesLeads;
using InsuranceIntelligence.Application.Search;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Api.CommandCenter;
using InsuranceIntelligence.Api.Communications;
using InsuranceIntelligence.Api.Compliance;
using InsuranceIntelligence.Api.Contestability;
using InsuranceIntelligence.Api.Domain;
using InsuranceIntelligence.Api.Documents;
using InsuranceIntelligence.Api.Health;
using InsuranceIntelligence.Api.Integrations;
using InsuranceIntelligence.Api.Knowledge;
using InsuranceIntelligence.Api.Local;
using InsuranceIntelligence.Api.Reports;
using InsuranceIntelligence.Api.Search;
using InsuranceIntelligence.Api.Workflow;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Documents;
using InsuranceIntelligence.Domain.Knowledge;
using InsuranceIntelligence.Domain.Queue;
using InsuranceIntelligence.Domain.Storage;
using InsuranceIntelligence.Domain.Workflow;
using InsuranceIntelligence.Domain.Notifications;
using InsuranceIntelligence.Infrastructure.Notifications;
using InsuranceIntelligence.Infrastructure.DependencyInjection;
using InsuranceIntelligence.Infrastructure.Configuration;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging.ApplicationInsights;
using Microsoft.Identity.Web;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddApplicationInsightsTelemetry();
builder.Logging.AddFilter<ApplicationInsightsLoggerProvider>("InsuranceIntelligence", LogLevel.Information);

// Gate 1 (G1.6): load deployment secrets from Azure Key Vault when configured. The vault URI is a
// non-secret pointer (Azure:KeyVaultUri / Azure__KeyVaultUri); secrets are read via managed identity
// (DefaultAzureCredential), no secrets in config files or env. Only activates when a URI is set, so
// local/dev is unaffected. Key Vault secret names map to config keys ("--" for nested sections).
var keyVaultUri = builder.Configuration["Azure:KeyVaultUri"] ?? builder.Configuration["Azure__KeyVaultUri"];
if (!string.IsNullOrWhiteSpace(keyVaultUri))
{
    builder.Configuration.AddAzureKeyVault(
        new Uri(keyVaultUri),
        new Azure.Identity.DefaultAzureCredential());
}

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        var configuredOrigin = builder.Configuration["Portal:CorsOrigin"];
        var allowedOrigins = string.IsNullOrWhiteSpace(configuredOrigin)
            ? new[] { "http://localhost:3000", "http://127.0.0.1:3000" }
            : configuredOrigin.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        policy.AllowAnyHeader()
            .AllowAnyMethod()
            // A browser on another origin can only read these response headers if they are exposed: the
            // name a download was given (claims pack, item 24) and how many files it holds.
            .WithExposedHeaders("Content-Disposition", "X-Pack-Files")
            .WithOrigins(allowedOrigins)
            .AllowCredentials();
    });
});

var infrastructureOptions = InfrastructureRegistration.BindOptions(builder.Configuration);
var runtimeProfile = AzureProviderStartupValidator.ResolveRuntimeProfile(
    infrastructureOptions, builder.Environment.EnvironmentName);
AzureProviderStartupValidator.ThrowIfInvalid(infrastructureOptions);
AzureProviderStartupValidator.ThrowIfProductionInvalid(infrastructureOptions, runtimeProfile);

// Development diagnostics endpoints (debug notification trigger, email test-ingestion)
// are mapped ONLY in local Development/LocalTest. They must never exist as a deployed
// surface in Demo/Staging/Production/Reviewer, per the Gate 0 secure-baseline rule.
var developmentDiagnosticsEnabled = builder.Environment.IsDevelopment()
    && (runtimeProfile.Equals("Development", StringComparison.OrdinalIgnoreCase)
        || runtimeProfile.Equals("LocalTest", StringComparison.OrdinalIgnoreCase));

builder.Services.AddAuthorization();
// Handles ProviderNotConfiguredException with a structured, capability-aware response instead
// of the generic 500 below; must be registered before AddProblemDetails' fallback so it runs
// first (ASP.NET Core tries handlers in registration order).
builder.Services.AddExceptionHandler<InsuranceIntelligence.Api.Local.ProviderNotConfiguredExceptionHandler>();
builder.Services.AddProblemDetails();

// OpenAPI/Swagger. The generated document is the machine-checkable API contract map
// (see docs/api/IIP_API_Contracts_Master.md, ADR-0004). The interactive Swagger UI and
// the JSON document are exposed ONLY in Development/LocalTest (see developmentDiagnosticsEnabled
// below) so no schema/endpoint enumeration surface is published in deployed environments.
// Backs IWhatsAppCredentialProtector (and any future at-rest secret encryption), real, reversible
// encryption keyed by a key ring, not a hand-rolled cipher. Default persistence is the local
// filesystem, which on a single-instance Azure App Service is %HOME%\ASP.NET\DataProtection-Keys,
// durable across app restarts on this deployment's current single-instance B1 plan. Scaling to
// multiple instances would need PersistKeysToAzureBlobStorage + ProtectKeysWithAzureKeyVault so
// every instance shares one key ring; noted here rather than built speculatively.
builder.Services.AddDataProtection().SetApplicationName("InsuranceIntelligence");

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(swagger =>
{
    swagger.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "IIP Operations API",
        Version = "v1",
        Description = "Insurance Intelligence Platform (IIP), canonical /api/v1 contract. "
            + "Human-reviewed insurance intelligence; AI prepares, humans decide.",
    });
    // Minimal-API route params without a type constraint (e.g. {customerId}) can collide
    // on the same normalized path; pick the first deterministically so the document always
    // generates. Enrichment (operationIds, permission metadata) is layered on in the
    // hand-written contract map (docs/api/IIP_API_Contracts_Master.md).
    swagger.ResolveConflictingActions(descriptions => descriptions.First());
});
if (infrastructureOptions.AuthMode.Equals("Entra", StringComparison.OrdinalIgnoreCase))
{
    // Multitenant Entra authentication for IIP's single app registration configured for
    // "Accounts in any organizational directory". Per the reconciled backend-wiring spec:
    // - EntraTenantId is IIP's publisher/home tenant, NOT a customer insurer tenant, and must
    //   NOT restrict the JWT issuer.
    // - Issuer validation is delegated to Microsoft.Identity.Web's multitenant-aware path
    //   (TenantId = "organizations"); do NOT hardcode a single ValidIssuer.
    RequireSetting(infrastructureOptions.EntraTenantId, "INSURANCEINTELLIGENCE_ENTRA_TENANT_ID");
    var clientId = RequireSetting(infrastructureOptions.EntraClientId, "INSURANCEINTELLIGENCE_ENTRA_CLIENT_ID");
    var audience = string.IsNullOrWhiteSpace(infrastructureOptions.EntraAudience)
        ? $"api://{clientId}"
        : infrastructureOptions.EntraAudience;
    var validAudiences = new[] { audience, clientId }
        .Where(value => !string.IsNullOrWhiteSpace(value))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddMicrosoftIdentityWebApi(
            jwt =>
            {
                jwt.Authority = "https://login.microsoftonline.com/organizations/v2.0";
                jwt.Audience = audience;
                jwt.RequireHttpsMetadata = true;

                jwt.TokenValidationParameters ??= new TokenValidationParameters();
                jwt.TokenValidationParameters.ValidateIssuer = true;
                jwt.TokenValidationParameters.ValidateAudience = true;
                jwt.TokenValidationParameters.ValidAudiences = validAudiences;
                jwt.TokenValidationParameters.ValidateLifetime = true;
                jwt.TokenValidationParameters.ValidateIssuerSigningKey = true;
                jwt.TokenValidationParameters.IssuerValidator = (issuer, _, _) =>
                {
                    if (!Uri.TryCreate(issuer, UriKind.Absolute, out var issuerUri)
                        || !string.Equals(issuerUri.Host, "login.microsoftonline.com", StringComparison.OrdinalIgnoreCase))
                    {
                        throw new SecurityTokenInvalidIssuerException($"Unsupported token issuer '{issuer}'.");
                    }

                    var segments = issuerUri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
                    if (segments.Length >= 2
                        && Guid.TryParse(segments[0], out _)
                        && string.Equals(segments[1], "v2.0", StringComparison.OrdinalIgnoreCase))
                    {
                        return issuer;
                    }

                    throw new SecurityTokenInvalidIssuerException($"Unsupported token issuer '{issuer}'.");
                };
                jwt.TokenValidationParameters.ClockSkew = TimeSpan.FromMinutes(2);
                jwt.TokenValidationParameters.NameClaimType = "preferred_username";
                jwt.TokenValidationParameters.RoleClaimType = "roles";
                // Do NOT set ValidIssuer/ValidIssuers: multitenant issuer validation is dynamic.
            },
            identity =>
            {
                identity.Instance = "https://login.microsoftonline.com/";
                identity.TenantId = "organizations";
                identity.ClientId = clientId;
                // Audience is validated on the JwtBearer options above; MicrosoftIdentityOptions
                // (Microsoft.Identity.Web 4.x) exposes no Audience property.
            });
}
else
{
    builder.Services.AddAuthentication();
}

// In-memory storage is permitted only for developer-machine execution, so the team can run
// the platform with no Azure account. Deny-by-default on the runtime profile: anything that
// is not explicitly a local profile, including an unrecognised value, is refused. This is
// one of three independent blocks; AddInsuranceIntelligenceInfrastructure applies the same
// profile test, and AzureProviderStartupValidator.ValidateProduction separately refuses to
// boot Staging/Production/ManagedApplicationReviewer/Demo on non-SqlServer storage.
var storageProvider = infrastructureOptions.StorageProvider;
if (storageProvider.Equals("InMemory", StringComparison.OrdinalIgnoreCase)
    && !IsLocalDeveloperRuntime(runtimeProfile))
{
    throw new InvalidOperationException(
        $"InsuranceIntelligence.Api cannot run with INSURANCEINTELLIGENCE_STORAGE_PROVIDER=InMemory under the "
        + $"'{runtimeProfile}' runtime profile. In-memory storage is limited to LocalTest/Development execution; "
        + "deployed profiles require Azure SQL.");
}

static bool IsLocalDeveloperRuntime(string profile) =>
    profile.Equals("LocalTest", StringComparison.OrdinalIgnoreCase)
    || profile.Equals("Development", StringComparison.OrdinalIgnoreCase)
    || profile.Equals("Test", StringComparison.OrdinalIgnoreCase);

builder.Services.AddInsuranceIntelligenceInfrastructure(builder.Configuration, builder.Environment);

if (infrastructureOptions.BlobProvider.Equals("AzureBlob", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddSingleton<IDocumentStorageProvider, AzureBlobDocumentStorageProvider>();
    builder.Services.AddSingleton<IKnowledgeStorageProvider, AzureBlobKnowledgeStorageProvider>();
    builder.Services.AddSingleton<IEvidencePackStorageProvider, AzureBlobEvidencePackStorageProvider>();
}
else if (infrastructureOptions.BlobProvider.Equals("Mock", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddSingleton<IDocumentStorageProvider, LocalMockDocumentStorageProvider>();
    builder.Services.AddSingleton<IKnowledgeStorageProvider, LocalMockKnowledgeStorageProvider>();
    if (InsuranceIntelligence.Api.Local.Mock.MockProviderPolicy.IsLocalDeveloperRuntime(runtimeProfile))
    {
        builder.Services.AddSingleton<IEvidencePackStorageProvider, InsuranceIntelligence.Api.Local.Mock.LocalMockEvidencePackStorageProvider>();
    }
    else
    {
        builder.Services.AddSingleton<IEvidencePackStorageProvider, NotConfiguredEvidencePackStorageProvider>();
    }
}
else
{
    builder.Services.AddSingleton<IDocumentStorageProvider, NotConfiguredDocumentStorageProvider>();
    builder.Services.AddSingleton<IKnowledgeStorageProvider, NotConfiguredKnowledgeStorageProvider>();
    builder.Services.AddSingleton<IEvidencePackStorageProvider, NotConfiguredEvidencePackStorageProvider>();
}

if (infrastructureOptions.DocumentProvider.Equals("AzureDocumentIntelligence", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddSingleton<IDocumentProcessingProvider, AzureDocumentIntelligenceProvider>();
}
else if (InsuranceIntelligence.Api.Local.Mock.MockProviderPolicy.IsActive(infrastructureOptions.DocumentProvider, runtimeProfile))
{
    builder.Services.AddSingleton<IDocumentProcessingProvider, InsuranceIntelligence.Api.Local.Mock.MockDocumentProcessingProvider>();
}
else
{
    builder.Services.AddSingleton<IDocumentProcessingProvider, NotConfiguredDocumentProcessingProvider>();
}

// ITranslationProvider is always the tenant-aware resolver now, the same three-step order as
// ISmsProvider/IEmailSendingProvider: a tenant's own dashboard-configured connection (e.g. Sunbird
// AI) first, then the deployment's environment-configured engine below, then not configured.
// TranslationProvider/Translation options still govern step 2, the fallback, exactly as before.
// Mock is Development only (AzureProviderStartupValidator refuses it under every deployed profile);
// AfriSlm is the llama.cpp container that hosts the model file. Anything else, including an absent
// setting, fails closed to English rather than guessing. Scoped because it depends on the scoped
// connection repository, mirroring TenantAwareSmsProvider.
builder.Services.AddSingleton<InsuranceIntelligence.Application.Translation.ITranslationProviderFactory, InsuranceIntelligence.Api.Local.TranslationProviderFactory>();
builder.Services.AddSingleton<InsuranceIntelligence.Domain.Integrations.IConnectionValidator, InsuranceIntelligence.Infrastructure.Translation.SunbirdConnectionValidator>();
builder.Services.AddScoped<InsuranceIntelligence.Infrastructure.Translation.TenantAwareTranslationProvider>();
builder.Services.AddScoped<InsuranceIntelligence.Application.Translation.ITranslationProvider>(sp => sp.GetRequiredService<InsuranceIntelligence.Infrastructure.Translation.TenantAwareTranslationProvider>());
builder.Services.AddScoped<InsuranceIntelligence.Application.Translation.ITranslationEngineHealth>(sp => sp.GetRequiredService<InsuranceIntelligence.Infrastructure.Translation.TenantAwareTranslationProvider>());

builder.Services.AddSingleton<InsuranceIntelligence.Application.CustomerAssistant.IAssistantLanguageSettings>(sp =>
    new InsuranceIntelligence.Api.Local.ConfiguredAssistantLanguageSettings(
        sp.GetRequiredService<InsuranceIntelligence.Domain.Administration.ITenantConfigurationRepository>(), infrastructureOptions.Translation));

builder.Services.AddSingleton<InsuranceIntelligence.Application.Translation.ITranslationCache, InsuranceIntelligence.Infrastructure.Translation.InMemoryTranslationCache>();
builder.Services.AddScoped<InsuranceIntelligence.Application.Translation.ITranslationService, InsuranceIntelligence.Application.Translation.TranslationService>();
builder.Services.AddSingleton<InsuranceIntelligence.Application.Translation.ILanguageDetector, InsuranceIntelligence.Application.Translation.WordListLanguageDetector>();
builder.Services.AddScoped<InsuranceIntelligence.Application.Translation.ICustomerLanguageResolutionService, InsuranceIntelligence.Application.Translation.CustomerLanguageResolutionService>();

builder.Services.AddSingleton<ICompletenessScoringService, CompletenessScoringService>();
// Scoped, not Singleton: StaticDocumentChecklistProvider now optionally depends on
// IProductRepository (outcome E's product-document-matrix integration), which is itself Scoped
// (it resolves through the per-request DbContext); a Singleton cannot consume a Scoped service.
builder.Services.AddScoped<IDocumentChecklistProvider, StaticDocumentChecklistProvider>();
builder.Services.AddSingleton<ISourceCitationFormatter, SourceCitationFormatter>();
if (infrastructureOptions.RetrievalProvider.Equals("AzureAiSearch", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddSingleton<IKnowledgeIndexProvider, AzureAiSearchKnowledgeIndexProvider>();
    builder.Services.AddSingleton<IRetrievalProvider, AzureAiSearchRetrievalProvider>();
}
else if (InsuranceIntelligence.Api.Local.Mock.MockProviderPolicy.IsActive(infrastructureOptions.RetrievalProvider, runtimeProfile))
{
    builder.Services.AddSingleton<IKnowledgeIndexProvider, InsuranceIntelligence.Api.Local.Mock.MockKnowledgeIndexProvider>();
    builder.Services.AddSingleton<IRetrievalProvider, InsuranceIntelligence.Api.Local.Mock.MockRetrievalProvider>();
}
else
{
    builder.Services.AddSingleton<IKnowledgeIndexProvider, NotConfiguredKnowledgeIndexProvider>();
    builder.Services.AddSingleton<IRetrievalProvider, NotConfiguredRetrievalProvider>();
}

if (infrastructureOptions.AiProvider.Equals("AzureOpenAI", StringComparison.OrdinalIgnoreCase))
{
    // Scoped, not Singleton: both now depend on the scoped IAgentBehaviourProfileRepository
    // (blueprint §6.3) to compose the tenant's configured system prompt on every call. Embeddings
    // carry no per-call prompt, so that one stays Singleton.
    builder.Services.AddScoped<IAiAssistantProvider, AzureOpenAiAssistantProvider>();
    builder.Services.AddSingleton<IEmbeddingProvider, AzureOpenAiEmbeddingProvider>();
    builder.Services.AddScoped<IChatCompletionProvider, AzureOpenAiChatCompletionProvider>();
    builder.Services.AddScoped<IToolCallingChatProvider, AzureOpenAiToolCallingChatProvider>();
    builder.Services.AddScoped<IConversationInsightsService, ConversationInsightsService>();
}
else if (InsuranceIntelligence.Api.Local.Mock.MockProviderPolicy.IsActive(infrastructureOptions.AiProvider, runtimeProfile))
{
    // Deterministic Mock adapters, developer runtimes only (MockProviderPolicy). The conversation
    // insights agent is not registered: it needs a model that returns structured JSON, which a
    // scripted mock cannot honestly provide.
    builder.Services.AddSingleton<IAiAssistantProvider, InsuranceIntelligence.Api.Local.Mock.MockAiAssistantProvider>();
    builder.Services.AddSingleton<IEmbeddingProvider, InsuranceIntelligence.Api.Local.Mock.MockEmbeddingProvider>();
    builder.Services.AddSingleton<IChatCompletionProvider, InsuranceIntelligence.Api.Local.Mock.MockChatCompletionProvider>();
    builder.Services.AddSingleton<IToolCallingChatProvider, InsuranceIntelligence.Api.Local.Mock.MockToolCallingChatProvider>();
}
else
{
    builder.Services.AddSingleton<IAiAssistantProvider, NotConfiguredAiAssistantProvider>();
    builder.Services.AddSingleton<IChatCompletionProvider, NotConfiguredChatCompletionProvider>();
}

if (infrastructureOptions.QueueProvider.Equals("AzureServiceBus", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddSingleton<IMessageQueueDispatcher, AzureServiceBusQueueDispatcher>();
}
else if (infrastructureOptions.QueueProvider.Equals("Mock", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddSingleton<IMessageQueueDispatcher, MockMessageQueueDispatcher>();
}
else
{
    builder.Services.AddSingleton<IMessageQueueDispatcher, NotConfiguredMessageQueueDispatcher>();
}

builder.Services.AddSingleton<IWorkflowRuleProvider, StaticWorkflowRuleProvider>();
if (infrastructureOptions.AiProvider.Equals("AzureOpenAI", StringComparison.OrdinalIgnoreCase))
{
    // Scoped, not Singleton, see the AzureOpenAiAssistantProvider registration above; same reason.
    builder.Services.AddScoped<ICustomerUpdateDraftingProvider, AzureOpenAiCustomerUpdateDraftingProvider>();
}
else if (InsuranceIntelligence.Api.Local.Mock.MockProviderPolicy.IsActive(infrastructureOptions.AiProvider, runtimeProfile))
{
    builder.Services.AddSingleton<ICustomerUpdateDraftingProvider, InsuranceIntelligence.Api.Local.Mock.MockCustomerUpdateDraftingProvider>();
}
else
{
    builder.Services.AddSingleton<ICustomerUpdateDraftingProvider, NotConfiguredCustomerUpdateDraftingProvider>();
}
if (infrastructureOptions.CommunicationProvider.Equals("GraphEmail", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddScoped<ICommunicationProvider, ConfiguredCustomerUpdateCommunicationProvider>();
    builder.Services.AddScoped<InsuranceIntelligence.Domain.Integrations.IWhatsAppSenderResolver, ConfiguredWhatsAppSenderResolver>();
}
else if (InsuranceIntelligence.Api.Local.Mock.MockProviderPolicy.IsActive(infrastructureOptions.CommunicationProvider, runtimeProfile))
{
    // Mock sending records into an in-memory outbox (readable at /api/v1/dev/mock-outbox) and
    // replaces the Disabled sender the infrastructure registered. Nothing leaves the process.
    builder.Services.AddSingleton<InsuranceIntelligence.Api.Local.Mock.MockOutbox>();
    builder.Services.AddScoped<InsuranceIntelligence.Domain.Communications.ICommunicationSendingProvider, InsuranceIntelligence.Api.Local.Mock.MockCommunicationSendingProvider>();
    builder.Services.AddScoped<ICommunicationProvider, ConfiguredCustomerUpdateCommunicationProvider>();
    builder.Services.AddScoped<InsuranceIntelligence.Domain.Integrations.IWhatsAppSenderResolver, MockWhatsAppSenderResolver>();
}
else
{
    builder.Services.AddSingleton<ICommunicationProvider, NotConfiguredCommunicationProvider>();
    builder.Services.AddScoped<InsuranceIntelligence.Domain.Integrations.IWhatsAppSenderResolver, ConfiguredWhatsAppSenderResolver>();
}
if (InsuranceIntelligence.Api.Local.Mock.MockProviderPolicy.IsActive(infrastructureOptions.EmailProvider, runtimeProfile))
{
    // A pretend IMAP host for a developer runtime: mailboxes in memory, filled through /api/v1/dev/mock-mailbox/deliver.
    builder.Services.AddSingleton<InsuranceIntelligence.Api.Local.Mock.MockMailStore>();
    builder.Services.AddSingleton<InsuranceIntelligence.Domain.Integrations.IImapMailClient, InsuranceIntelligence.Api.Local.Mock.MockImapMailClient>();
}
builder.Services.AddSingleton<ISlaCalculator, BasicSlaCalculator>();

if (infrastructureOptions.NotificationProvider.Equals("IncomingWebhook", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.Configure<IncomingWebhookTeamsProviderOptions>(builder.Configuration.GetSection("TeamsWebhook"));
    builder.Services.AddHttpClient<ITeamsNotificationProvider, IncomingWebhookTeamsProvider>();
}
else
{
    builder.Services.AddSingleton<ITeamsNotificationProvider, InsuranceIntelligence.Infrastructure.Graph.DisabledTeamsNotificationProvider>();
}

builder.Services.AddScoped<ITeamsNotificationQueue, DefaultTeamsNotificationQueue>();
builder.Services.AddScoped<CaseIntakeService>();
builder.Services.AddScoped<InsuranceIntelligence.Application.Claims.IClaimGovernedPathService, InsuranceIntelligence.Application.Claims.ClaimGovernedPathService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<TenantContextAccessor>();
builder.Services.AddScoped<ITenantContextAccessor>(serviceProvider => serviceProvider.GetRequiredService<TenantContextAccessor>());
builder.Services.AddScoped<ITenantContextSetter>(serviceProvider => serviceProvider.GetRequiredService<TenantContextAccessor>());
builder.Services.AddScoped<IPermissionService, PermissionService>();
builder.Services.AddScoped<IGlobalSearchService, GlobalSearchService>();
builder.Services.AddScoped<IAuditWriter, AuditWriter>();
builder.Services.AddScoped<IClaimsQueryService, ClaimsQueryService>();
builder.Services.AddSingleton<IAiOutputValidator, DeterministicAiOutputValidator>();
builder.Services.AddSingleton<IIntentClassifier, DeterministicThenAiIntentClassifier>();
builder.Services.AddSingleton<IProviderRequirementResolver, ProviderRequirementResolver>();
builder.Services.AddScoped<INotificationDraftService, CustomerUpdateNotificationDraftService>();
builder.Services.AddScoped<ICommandCenterQueryService, CommandCenterQueryService>();
builder.Services.AddScoped<ICommandCenterConversationService, CommandCenterConversationService>();
builder.Services.AddScoped<IConversationGroundingService, ConversationGroundingService>();
// W2.5: Phase 2 core-workspace query services.
builder.Services.AddScoped<IChannelQueryService, ChannelQueryService>();
builder.Services.AddScoped<ICustomerQueryService, CustomerQueryService>();
builder.Services.AddScoped<InsuranceIntelligence.Application.Policies.IPolicyImportService, InsuranceIntelligence.Application.Policies.PolicyImportService>();
builder.Services.AddScoped<InsuranceIntelligence.Application.Customers.ICustomerConsentService, InsuranceIntelligence.Application.Customers.CustomerConsentService>();
builder.Services.AddScoped<InsuranceIntelligence.Application.Retention.IRetentionWatchService, InsuranceIntelligence.Application.Retention.RetentionWatchService>();
builder.Services.AddScoped<ISalesLeadQueryService, SalesLeadQueryService>();
builder.Services.AddScoped<InsuranceIntelligence.Application.CustomerAssistant.IConversationControlService, InsuranceIntelligence.Application.CustomerAssistant.ConversationControlService>();
builder.Services.AddScoped<ISalesLeadCommandService, SalesLeadCommandService>();
builder.Services.AddScoped<IProspectTriageService, ProspectTriageService>();
builder.Services.AddScoped<InsuranceIntelligence.Application.Academy.IAcademyPricingService, InsuranceIntelligence.Application.Academy.AcademyPricingService>();
// A message that cannot reach a real phone may be recorded as a clearly labelled demonstration send, but
// only where the runtime profile is not protected. In Staging and Production it is blocked, never faked.
builder.Services.AddSingleton(new OutreachOptions(!AzureProviderStartupValidator.IsProtectedRuntime(runtimeProfile)));
builder.Services.AddScoped<IOutreachService, OutreachService>();
builder.Services.AddScoped<IAcquisitionReportService, AcquisitionReportService>();
builder.Services.AddScoped<InsuranceIntelligence.Application.Brief.IBriefService, InsuranceIntelligence.Application.Brief.BriefService>();
builder.Services.AddScoped<InsuranceIntelligence.Application.Products.IProductQueryService, InsuranceIntelligence.Application.Products.ProductQueryService>();
builder.Services.AddScoped<InsuranceIntelligence.Application.Products.IProductCommandService, InsuranceIntelligence.Application.Products.ProductCommandService>();
builder.Services.AddScoped<InsuranceIntelligence.Application.Products.IClaimTypeService, InsuranceIntelligence.Application.Products.ClaimTypeService>();
builder.Services.AddScoped<InsuranceIntelligence.Application.Products.IWordingDraftService, InsuranceIntelligence.Application.Products.WordingDraftService>();
builder.Services.AddScoped<InsuranceIntelligence.Application.Compliance.IClaimantConsentService, InsuranceIntelligence.Application.Compliance.ClaimantConsentService>();
builder.Services.AddSingleton<InsuranceIntelligence.Domain.Documents.IImageDerivativeProvider, InsuranceIntelligence.Api.Documents.MagickImageDerivativeProvider>();
builder.Services.AddSingleton<InsuranceIntelligence.Domain.Documents.IDocumentPdfBuilder, InsuranceIntelligence.Api.Documents.MagickDocumentPdfBuilder>();
builder.Services.AddScoped<InsuranceIntelligence.Application.Documents.ISlotPdfService, InsuranceIntelligence.Application.Documents.SlotPdfService>();
// Recognising documents (build-loop item 14): a vision model against the workspace's whole document
// catalogue. Local runs use the deterministic Mock; where no model is configured every file honestly
// goes to a person instead of being guessed at.
if (infrastructureOptions.AiProvider.Equals("AzureOpenAI", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddScoped<InsuranceIntelligence.Domain.Documents.IDocumentRecognizer, InsuranceIntelligence.Api.Azure.AzureOpenAiVisionDocumentRecognizer>();
}
else if (InsuranceIntelligence.Api.Local.Mock.MockProviderPolicy.IsActive(infrastructureOptions.AiProvider, runtimeProfile))
{
    builder.Services.AddSingleton<InsuranceIntelligence.Domain.Documents.IDocumentRecognizer, InsuranceIntelligence.Api.Local.Mock.MockDocumentRecognizer>();
}
else
{
    builder.Services.AddSingleton<InsuranceIntelligence.Domain.Documents.IDocumentRecognizer, InsuranceIntelligence.Api.Azure.NotConfiguredDocumentRecognizer>();
}

builder.Services.AddScoped<InsuranceIntelligence.Application.Documents.IDocumentRecognitionService, InsuranceIntelligence.Application.Documents.DocumentRecognitionService>();
builder.Services.AddScoped<InsuranceIntelligence.Application.Documents.IFieldProposalService, InsuranceIntelligence.Application.Documents.FieldProposalService>();
builder.Services.AddSingleton<InsuranceIntelligence.Domain.Documents.IImageForensicsReader, InsuranceIntelligence.Api.Documents.MagickImageForensicsReader>();
builder.Services.AddScoped<InsuranceIntelligence.Application.Documents.IAuthenticityService, InsuranceIntelligence.Application.Documents.AuthenticityService>();
builder.Services.AddScoped<InsuranceIntelligence.Application.Fraud.IClaimAssessmentService, InsuranceIntelligence.Application.Fraud.ClaimAssessmentService>();
builder.Services.AddScoped<InsuranceIntelligence.Application.Cases.IClaimantStatusPresenter, InsuranceIntelligence.Application.Cases.ClaimantStatusPresenter>();
builder.Services.AddScoped<InsuranceIntelligence.Application.Cases.IClaimStatusChangeService, InsuranceIntelligence.Application.Cases.ClaimStatusChangeService>();
builder.Services.AddScoped<InsuranceIntelligence.Application.Cases.IClaimStatusNotifier, InsuranceIntelligence.Application.Cases.ClaimStatusNotifier>();
builder.Services.AddScoped<InsuranceIntelligence.Application.Compliance.IClaimsPackBuilder, InsuranceIntelligence.Application.Compliance.ClaimsPackBuilder>();
builder.Services.AddScoped<InsuranceIntelligence.Application.Customers.ICustomerMemoryService, InsuranceIntelligence.Application.Customers.CustomerMemoryService>();
builder.Services.AddScoped<InsuranceIntelligence.Application.Compliance.IClaimsPackEmailService, InsuranceIntelligence.Application.Compliance.ClaimsPackEmailService>();
builder.Services.AddScoped<InsuranceIntelligence.Application.Compliance.IClaimsPackTrigger, InsuranceIntelligence.Application.Compliance.ClaimsPackTrigger>();
builder.Services.AddSingleton<InsuranceIntelligence.Application.Compliance.IPortalBaseUrl, InsuranceIntelligence.Api.Compliance.ConfiguredPortalBaseUrl>();
builder.Services.AddSingleton<InsuranceIntelligence.Api.Jobs.IBackgroundJobHandler, InsuranceIntelligence.Api.Compliance.ClaimsPackJobHandler>();
builder.Services.AddScoped<InsuranceIntelligence.Application.Products.IClaimFieldService, InsuranceIntelligence.Application.Products.ClaimFieldService>();
builder.Services.AddScoped<InsuranceIntelligence.Application.Tenants.IWorkspaceCatalogService, InsuranceIntelligence.Application.Tenants.WorkspaceCatalogService>();
builder.Services.AddScoped<InsuranceIntelligence.Application.Onboarding.IOnboardingConfiguration, InsuranceIntelligence.Api.Onboarding.OnboardingConfiguration>();
builder.Services.AddScoped<InsuranceIntelligence.Application.Onboarding.ITenantOnboardingService, InsuranceIntelligence.Application.Onboarding.TenantOnboardingService>();
builder.Services.AddScoped<IAuditQueryService, AuditQueryService>();
builder.Services.AddSingleton<IComplianceResidencySettings>(_ => new ComplianceResidencySettings(
    infrastructureOptions.DataResidencyCountry,
    infrastructureOptions.DataResidencyGeography,
    infrastructureOptions.AzureRegion,
    infrastructureOptions.DataResidencyMode,
    infrastructureOptions.AzureOpenAI.DeploymentType,
    infrastructureOptions.DataResidencyAllowedAzureRegions,
    infrastructureOptions.DataResidencyApprovalReference,
    infrastructureOptions.AiProvider));
builder.Services.AddScoped<IComplianceQueryService, ComplianceQueryService>();
// Initiative LP: rule-based advisory domains + admin domains (Fraud/Underwriting/Agents/Billing).
builder.Services.AddScoped<InsuranceIntelligence.Application.Fraud.IFraudQueryService, InsuranceIntelligence.Application.Fraud.FraudQueryService>();
builder.Services.AddScoped<InsuranceIntelligence.Application.Fraud.IFraudDispositionService, InsuranceIntelligence.Application.Fraud.FraudDispositionService>();
builder.Services.AddScoped<InsuranceIntelligence.Application.Underwriting.IUnderwritingQueryService, InsuranceIntelligence.Application.Underwriting.UnderwritingQueryService>();
builder.Services.AddScoped<InsuranceIntelligence.Application.Underwriting.IUnderwritingSubmissionSource, InsuranceIntelligence.Api.Underwriting.NotConfiguredUnderwritingSubmissionSource>();
builder.Services.AddScoped<InsuranceIntelligence.Application.Agents.IAgentRegistryService, InsuranceIntelligence.Application.Agents.AgentRegistryService>();
builder.Services.AddScoped<InsuranceIntelligence.Application.Agents.IAgentGate, InsuranceIntelligence.Application.Agents.AgentGate>();
builder.Services.AddScoped<InsuranceIntelligence.Application.Agents.IAgentRunRecorder, InsuranceIntelligence.Application.Agents.AgentRunRecorder>();

// WO-08: read-tier agent tool registry. Each tool is registered as IAgentTool so
// AgentToolRegistry (itself the sole IAgentToolRegistry) can resolve the full set by name.
builder.Services.AddScoped<InsuranceIntelligence.Application.Agents.Tools.ClaimSnapshotLoader>();
builder.Services.AddScoped<InsuranceIntelligence.Application.Agents.Tools.IAgentTool, InsuranceIntelligence.Application.Agents.Tools.ClaimsAttentionReadTool>();
builder.Services.AddScoped<InsuranceIntelligence.Application.Agents.Tools.IAgentTool, InsuranceIntelligence.Application.Agents.Tools.ClaimsSlaReadTool>();
builder.Services.AddScoped<InsuranceIntelligence.Application.Agents.Tools.IAgentTool, InsuranceIntelligence.Application.Agents.Tools.DocumentsGapsReadTool>();
builder.Services.AddScoped<InsuranceIntelligence.Application.Agents.Tools.IAgentTool, InsuranceIntelligence.Application.Agents.Tools.ApprovalsQueueReadTool>();
builder.Services.AddScoped<InsuranceIntelligence.Application.Agents.Tools.IAgentTool, InsuranceIntelligence.Application.Agents.Tools.KnowledgeSearchTool>();
builder.Services.AddScoped<InsuranceIntelligence.Application.Agents.Tools.IAgentTool, InsuranceIntelligence.Application.Agents.Tools.AuditReadTool>();
builder.Services.AddScoped<InsuranceIntelligence.Application.Agents.Tools.IAgentTool, InsuranceIntelligence.Application.Agents.Tools.ProviderHealthReadTool>();
builder.Services.AddScoped<InsuranceIntelligence.Application.Agents.Tools.IAgentTool, InsuranceIntelligence.Application.Agents.Tools.MetricsReadTool>();
// WO-10: single-entity read tools added for the claims-summary agent, its first real consumer.
builder.Services.AddScoped<InsuranceIntelligence.Application.Agents.Tools.IAgentTool, InsuranceIntelligence.Application.Agents.Tools.ClaimReadTool>();
builder.Services.AddScoped<InsuranceIntelligence.Application.Agents.Tools.IAgentTool, InsuranceIntelligence.Application.Agents.Tools.DocumentReadTool>();
builder.Services.AddScoped<InsuranceIntelligence.Application.Agents.Tools.IAgentTool, InsuranceIntelligence.Application.Agents.Tools.PolicyReadTool>();
builder.Services.AddScoped<InsuranceIntelligence.Application.Agents.Tools.IAgentToolRegistry, InsuranceIntelligence.Application.Agents.Tools.AgentToolRegistry>();
builder.Services.AddScoped<InsuranceIntelligence.Application.ClaimsSummary.IClaimsSummaryService, InsuranceIntelligence.Application.ClaimsSummary.ClaimsSummaryService>();
// Customer 360 briefing, chat instructions, and the automatic review every new claim receives.
builder.Services.AddMemoryCache();
builder.Services.AddScoped<InsuranceIntelligence.Application.CustomerSummary.ICustomerSummaryService, InsuranceIntelligence.Application.CustomerSummary.CustomerSummaryService>();
builder.Services.AddScoped<ICommandCenterActionService, CommandCenterActionService>();
// Durable background work (build-loop item 17): document processing and claim review are jobs in the
// database, run by a worker, retried with a back-off and dead-lettered where a person can see them.
builder.Services.AddSingleton(InsuranceIntelligence.Api.Jobs.BackgroundPipelineOptions.From(builder.Configuration));
builder.Services.AddScoped<InsuranceIntelligence.Api.Agents.IClaimTriageQueue, InsuranceIntelligence.Api.Agents.DurableClaimTriageQueue>();
builder.Services.AddScoped<InsuranceIntelligence.Api.Documents.DocumentProcessor>();
builder.Services.AddScoped<InsuranceIntelligence.Api.Documents.IDocumentPipeline, InsuranceIntelligence.Api.Documents.DocumentPipeline>();
builder.Services.AddSingleton<InsuranceIntelligence.Api.Jobs.IBackgroundJobHandler, InsuranceIntelligence.Api.Documents.DocumentProcessJobHandler>();
builder.Services.AddSingleton<InsuranceIntelligence.Api.Jobs.IBackgroundJobHandler, InsuranceIntelligence.Api.Agents.ClaimReviewJobHandler>();
builder.Services.AddSingleton<InsuranceIntelligence.Api.Jobs.IBackgroundJobProcessor, InsuranceIntelligence.Api.Jobs.BackgroundJobProcessor>();
builder.Services.AddHostedService<InsuranceIntelligence.Api.Jobs.BackgroundJobWorker>();
builder.Services.AddSingleton<InsuranceIntelligence.Api.Agents.ConversationInsightsQueue>();
builder.Services.AddSingleton<IConversationInsightsQueue>(sp => sp.GetRequiredService<InsuranceIntelligence.Api.Agents.ConversationInsightsQueue>());
builder.Services.AddHostedService<InsuranceIntelligence.Api.Agents.ConversationInsightsWorker>();
builder.Services.AddSingleton<InsuranceIntelligence.Api.Jobs.IBackgroundJobHandler, InsuranceIntelligence.Api.Integrations.WhatsAppAssistantJobHandler>();
// WO-14: one instance backs both the emitter (called from the 5 real event sites) and the
// recipient's own feed (list/read-state), same underlying repository either way.
builder.Services.AddScoped<InsuranceIntelligence.Application.Notifications.NotificationService>();
builder.Services.AddScoped<InsuranceIntelligence.Application.Notifications.INotificationEmitter>(
    sp => sp.GetRequiredService<InsuranceIntelligence.Application.Notifications.NotificationService>());
builder.Services.AddScoped<InsuranceIntelligence.Application.Notifications.INotificationFeedService>(
    sp => sp.GetRequiredService<InsuranceIntelligence.Application.Notifications.NotificationService>());
builder.Services.AddScoped<InsuranceIntelligence.Application.AgentWorkflows.IAgentWorkflowService, InsuranceIntelligence.Application.AgentWorkflows.AgentWorkflowService>();
builder.Services.AddScoped<InsuranceIntelligence.Application.MyWork.IMyWorkService, InsuranceIntelligence.Application.MyWork.MyWorkService>();
builder.Services.AddScoped<InsuranceIntelligence.Application.WhatsApp.IWhatsAppMessagingService, InsuranceIntelligence.Application.WhatsApp.WhatsAppMessagingService>();
builder.Services.AddScoped<InsuranceIntelligence.Application.CustomerAssistant.ICustomerAssistantService, InsuranceIntelligence.Application.CustomerAssistant.CustomerAssistantService>();

// Whose assistant Kato is, and which covers he sells, per workspace (build-loop item 27): the organisation from the
// workspace's operating settings and the covers from its published products that carry an offer. Only a developer or
// demonstration runtime may fall back to the built-in demonstration catalogue when a workspace has configured nothing;
// a staging or production workspace with no offers sells nothing rather than another insurer's covers.
var builtInAssistantCatalogue = runtimeProfile.Equals("Development", StringComparison.OrdinalIgnoreCase)
    || runtimeProfile.Equals("LocalTest", StringComparison.OrdinalIgnoreCase)
    || runtimeProfile.Equals("Demo", StringComparison.OrdinalIgnoreCase);
builder.Services.AddScoped<InsuranceIntelligence.Application.CustomerAssistant.IAssistantBrandProvider>(services =>
    new InsuranceIntelligence.Application.CustomerAssistant.TenantAssistantBrandProvider(
        services.GetRequiredService<InsuranceIntelligence.Domain.Administration.ITenantSettingsRepository>(),
        services.GetRequiredService<InsuranceIntelligence.Domain.Products.IProductRepository>(),
        builtInAssistantCatalogue));

// Hidden field-test commands (#reset, #demo) for one shared phone: Demo runtime profile only, and
// only for the number in INSURANCEINTELLIGENCE_DEMO_PHONE_NUMBER. Anywhere else they do not exist.
var demoRuntime = InsuranceIntelligence.Api.Azure.AzureProviderStartupValidator
    .ResolveRuntimeProfile(infrastructureOptions, builder.Environment.EnvironmentName)
    .Equals("Demo", StringComparison.OrdinalIgnoreCase);
var demoPhoneNumber = builder.Configuration["INSURANCEINTELLIGENCE_DEMO_PHONE_NUMBER"];
builder.Services.AddSingleton(demoRuntime && !string.IsNullOrWhiteSpace(demoPhoneNumber)
    ? new InsuranceIntelligence.Application.CustomerAssistant.DemoSessionSettings(true, demoPhoneNumber.Trim())
    : InsuranceIntelligence.Application.CustomerAssistant.DemoSessionSettings.Disabled);
builder.Services.AddSingleton(new InsuranceIntelligence.Application.DemoSeeding.DemoPolicyholderSettings(
    builder.Configuration["INSURANCEINTELLIGENCE_DEMO_POLICYHOLDER_NAME"] ?? string.Empty));
builder.Services.AddScoped<InsuranceIntelligence.Application.DemoSeeding.MicroinsuranceDemoSeeder>();
builder.Services.AddScoped<InsuranceIntelligence.Infrastructure.Translation.SunbirdVoiceNoteTranscriber>();
builder.Services.AddScoped<InsuranceIntelligence.Application.CustomerAssistant.IVoiceNoteTranscriber>(sp => sp.GetRequiredService<InsuranceIntelligence.Infrastructure.Translation.SunbirdVoiceNoteTranscriber>());
builder.Services.AddScoped<InsuranceIntelligence.Application.CustomerAssistant.IVoiceNoteSpeaker>(sp => sp.GetRequiredService<InsuranceIntelligence.Infrastructure.Translation.SunbirdVoiceNoteTranscriber>());
builder.Services.AddScoped<InsuranceIntelligence.Application.CustomerAssistant.IDemoPolicyholderLoader>(
    services => services.GetRequiredService<InsuranceIntelligence.Application.DemoSeeding.MicroinsuranceDemoSeeder>());
builder.Services.AddScoped<InsuranceIntelligence.Application.CustomerAssistant.DemoSessionCommands>();
builder.Services.AddScoped<InsuranceIntelligence.Application.CustomerAssistant.IClaimantAccessLinkIssuer, InsuranceIntelligence.Api.ClientPortal.WhatsAppClaimantAccessLinkIssuer>();
builder.Services.AddScoped<InsuranceIntelligence.Application.Cases.IFnolAcknowledgementService, InsuranceIntelligence.Application.Cases.FnolAcknowledgementService>();
builder.Services.AddScoped<InsuranceIntelligence.Application.Cases.IMissingDocumentRequestService, InsuranceIntelligence.Application.Cases.MissingDocumentRequestService>();
builder.Services.AddScoped<InsuranceIntelligence.Application.Cases.ICaseFollowThroughService, InsuranceIntelligence.Application.Cases.CaseFollowThroughService>();
builder.Services.AddScoped<InsuranceIntelligence.Application.Billing.IBillingQueryService, InsuranceIntelligence.Application.Billing.BillingQueryService>();
builder.Services.AddScoped<InsuranceIntelligence.Application.Billing.IBillingUsageSource, InsuranceIntelligence.Api.Admin.Billing.RecordBackedBillingUsageSource>();
builder.Services.AddScoped<InsuranceIntelligence.Application.Documents.IDocumentQueryService, InsuranceIntelligence.Application.Documents.DocumentQueryService>();
builder.Services.AddScoped<InsuranceIntelligence.Application.Documents.IDocumentQualityService, InsuranceIntelligence.Application.Documents.DocumentQualityService>();
builder.Services.AddScoped<ITenantContext, HttpContextTenantContext>();
builder.Services.AddScoped<InsuranceIntelligence.Api.CommandCenter.ISystemCapabilityStatusService, InsuranceIntelligence.Api.CommandCenter.SystemCapabilityStatusService>();
builder.Services.AddSingleton<InsuranceIntelligence.Domain.Workflow.ICaseTimelineProvider, InsuranceIntelligence.Api.Local.CaseTimelineProvider>();

builder.Configuration["INSURANCEINTELLIGENCE_NOTIFICATION_PROVIDER"] ??= "Mock";
builder.Configuration["INSURANCEINTELLIGENCE_COMMUNICATION_PROVIDER"] ??= "Mock";
builder.Configuration["INSURANCEINTELLIGENCE_LOCAL_DOCUMENT_MAX_BYTES"] ??= DocumentEndpoints.MaxLocalMockFileSizeBytes.ToString();
builder.Configuration["INSURANCEINTELLIGENCE_LOCAL_KNOWLEDGE_MAX_BYTES"] ??= KnowledgeEndpoints.MaxKnowledgeFileSizeBytes.ToString();

// Whose assistant Kato is: Nile Shield Assurance unless configured for a bank selling its partner
// insurers' covers (Assistant:Organisation, Assistant:ShortName, Assistant:PartnerInsurers).
InsuranceIntelligence.Application.CustomerAssistant.KatoBrand.Current = InsuranceIntelligence.Application.CustomerAssistant.KatoBrand.FromSettings(
    builder.Configuration["Assistant:Organisation"],
    builder.Configuration["Assistant:ShortName"],
    builder.Configuration["Assistant:PartnerInsurers"]);

var app = builder.Build();

await app.Services.InitializeInsuranceIntelligenceInfrastructureAsync(app.Environment);

// Offline developer mode: with in-memory storage the database starts empty on every run, so
// seed the demo dataset at boot to give the app something to show without any Azure account.
// Guarded to in-memory storage only, this can never run against SqlServer, and the storage
// provider itself is already restricted to local runtimes.
if (infrastructureOptions.StorageProvider.Equals("InMemory", StringComparison.OrdinalIgnoreCase)
    && infrastructureOptions.SeedDemoData)
{
    using var seedScope = app.Services.CreateScope();
    var seedService = seedScope.ServiceProvider
        .GetRequiredService<InsuranceIntelligence.Application.DemoSeeding.DemoSeedService>();
    var seedScopeIds = InsuranceIntelligence.Domain.Common.TenantScope.Create(
        string.IsNullOrWhiteSpace(infrastructureOptions.TenantId) ? "tenant-demo" : infrastructureOptions.TenantId,
        string.IsNullOrWhiteSpace(infrastructureOptions.WorkspaceId) ? "workspace-operations" : infrastructureOptions.WorkspaceId);
    // Provenance stays with the system seeder; assignment goes to the identity an offline operator
    // actually signs in as (the same default DemoAuthorization.GetUserId falls back to), so "My Work"
    // and the notification feed, both filtered by the current user, are populated on a fresh run
    // instead of showing empty states over a workspace that visibly has 46 claims in it.
    var seedResult = await seedService.SeedAsync(seedScopeIds, "system:local-seed", DemoAuthorization.DefaultLocalUserId);
    app.Logger.LogInformation(
        "Offline mode: seeded {Cases} demo claims, {Documents} documents, {Approvals} approvals into in-memory storage, assigned to {Assignee}.",
        seedResult.Cases, seedResult.Documents, seedResult.Approvals, DemoAuthorization.DefaultLocalUserId);
}

// Always return a generic RFC 7807 response. Exception details and request headers stay server-side.
app.UseExceptionHandler();

app.UseCors();

// Swagger UI + JSON contract document, Development/LocalTest only (secure baseline).
if (developmentDiagnosticsEnabled)
{
    app.UseSwagger();
    app.UseSwaggerUI(ui => ui.SwaggerEndpoint("/swagger/v1/swagger.json", "IIP Operations API v1"));
}

app.UseAuthentication();
app.UseAuthorization();

// Baseline security response headers. The API only returns JSON, so a strict
// content-security-policy and anti-framing headers are safe and reduce
// clickjacking / content-sniffing exposure without affecting the separately
// hosted portal.
app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    headers["X-Content-Type-Options"] = "nosniff";
    headers["X-Frame-Options"] = "DENY";
    headers["Referrer-Policy"] = "no-referrer";
    headers["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'";
    await next();
});

app.Use(async (context, next) =>
{
    var rejection =
        RuntimeHeaderGuard.RejectBrowserAuthorityOutsideDevelopment(context, infrastructureOptions)
        ?? RuntimeHeaderGuard.RejectUnauthenticatedApiOutsideDevelopment(context, infrastructureOptions);
    if (rejection is not null)
    {
        await rejection.ExecuteAsync(context);
        return;
    }

    await next();
});

// Populate the per-request tenant context for query-service endpoints (Channels,
// Customers, Audit, Fraud, Underwriting, Agents, Billing, Documents). These read
// ITenantContextAccessor.Current via their permission/query services; only
// ClaimsController set it explicitly before, so the standalone query endpoints
// otherwise fail with "Tenant context has not been initialized".
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/api/v1"))
    {
        if (infrastructureOptions.AuthMode.Equals("Development", StringComparison.OrdinalIgnoreCase))
        {
            // Development ONLY: demo/header scope + role (rejected in deployed profiles by RuntimeHeaderGuard).
            if (TenantScopeReader.TryGetFromHeadersOrQuery(context, out var tenantScope, out _))
            {
                var devUserId = DemoAuthorization.GetUserId(context, fallback: string.Empty);
                if (!string.IsNullOrWhiteSpace(devUserId))
                {
                    var roleCode = DemoAuthorization.GetEffectiveRole(context, infrastructureOptions).ToString();
                    context.RequestServices.GetRequiredService<ITenantContextSetter>()
                        .SetCurrent(new TenantContext(tenantScope.TenantId, tenantScope.WorkspaceId, devUserId, [roleCode]));
                }
            }
        }
        else if (context.User.Identity?.IsAuthenticated == true)
        {
            // Deployed (Entra), ADR-0005: runtime authority is IIP's membership store, keyed by the
            // authenticated user's stable id (oid). The token tid/workspace claims are ONLY
            // disambiguation hints, never authority. Fine-grained permissions come from the store and
            // are stashed for the synchronous permission checks. No membership => fail closed.
            var userId = DemoAuthorization.GetUserId(context, fallback: string.Empty);
            if (!string.IsNullOrWhiteSpace(userId))
            {
                var tenantHint = RuntimeClaimTypes.TenantScope
                    .Select(t => context.User.FindFirst(t)?.Value)
                    .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
                // The portal's workspace switcher (WO-13) sends the user's chosen workspace as a
                // request header, not a claim, a new token isn't minted per switch. It is only ever
                // a hint: GetEffectiveMembershipAsync below re-validates it against the user's real
                // active membership and fails closed if they don't belong to it, so this can never
                // grant access to a workspace the user isn't actually a member of (I5).
                var requestedWorkspace = context.Request.Headers.TryGetValue(
                    RuntimeClaimTypes.RequestedWorkspaceHeaderName, out var requestedWorkspaceValue)
                    ? requestedWorkspaceValue.ToString()
                    : null;
                var workspaceHint = !string.IsNullOrWhiteSpace(requestedWorkspace)
                    ? requestedWorkspace
                    : RuntimeClaimTypes.WorkspaceScope
                        .Select(t => context.User.FindFirst(t)?.Value)
                        .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

                var membership = await context.RequestServices
                    .GetRequiredService<IPermissionReadRepository>()
                    .GetEffectiveMembershipAsync(userId, tenantHint, workspaceHint, context.RequestAborted);

                if (membership is not null)
                {
                    context.Items[DemoAuthorization.EffectivePermissionsItemKey] = membership.Permissions;
                    context.RequestServices.GetRequiredService<ITenantContextSetter>()
                        .SetCurrent(new TenantContext(membership.TenantId, membership.WorkspaceId, membership.UserId, membership.Roles));
                }
            }
        }
    }

    await next();
});

app.MapGet("/health", (InfrastructureOptions options) => Results.Ok(new
{
    service = "InsuranceIntelligence.Api",
    environment = AzureProviderStartupValidator.ResolveRuntimeProfile(options, app.Environment.EnvironmentName),
    hostEnvironment = app.Environment.EnvironmentName,
    authMode = options.AuthMode,
    status = "healthy"
}));
app.MapGet("/health/live", () => Results.Ok(new { status = "live" }));
app.MapGet("/health/ready", HealthEndpoints.Ready);

app.MapGet("/api/v1/me", RuntimeContextEndpoints.GetMe);

// WO-14: the recipient's own feed, self-scoped like /api/v1/me, no extra permission (any
// authenticated tenant member can see their own notifications).
app.MapGet("/api/v1/notifications", InsuranceIntelligence.Api.Notifications.NotificationEndpoints.GetFeed);
app.MapPost("/api/v1/notifications/{notificationId:guid}/read", InsuranceIntelligence.Api.Notifications.NotificationEndpoints.MarkRead);
app.MapPost("/api/v1/notifications/read-all", InsuranceIntelligence.Api.Notifications.NotificationEndpoints.MarkAllRead);

app.MapGet("/api/v1/search", SearchEndpoints.Search)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAssignedClaims, Permission.ViewAllClaims));

// A claimant is never a tenant member, so these cannot be gated by PermissionEndpointFilter (which
// requires resolved membership permissions outside Development, no claimant will ever have one).
// ClaimantSessionEndpointFilter is the real gate: a verified magic-link session outside
// Development, or the existing demo-header trust inside it.
app.MapGet("/api/v1/client-portal/claims/{caseId:guid}", ClientPortalEndpoints.GetClaim)
    .AddEndpointFilter(new InsuranceIntelligence.Api.ClientPortal.ClaimantSessionEndpointFilter());
app.MapPost("/api/v1/client-portal/claims/{caseId:guid}/documents", ClientPortalEndpoints.UploadDocuments)
    .AddEndpointFilter(new InsuranceIntelligence.Api.ClientPortal.ClaimantSessionEndpointFilter());
app.MapPost("/api/v1/client-portal/claims/{caseId:guid}/uploads", InsuranceIntelligence.Api.ClientPortal.ResumableUploadEndpoints.Start)
    .AddEndpointFilter(new InsuranceIntelligence.Api.ClientPortal.ClaimantSessionEndpointFilter());
app.MapGet("/api/v1/client-portal/claims/{caseId:guid}/uploads/{uploadId:guid}", InsuranceIntelligence.Api.ClientPortal.ResumableUploadEndpoints.Status)
    .AddEndpointFilter(new InsuranceIntelligence.Api.ClientPortal.ClaimantSessionEndpointFilter());
app.MapPut("/api/v1/client-portal/claims/{caseId:guid}/uploads/{uploadId:guid}/chunks/{index:int}", InsuranceIntelligence.Api.ClientPortal.ResumableUploadEndpoints.PutChunk)
    .AddEndpointFilter(new InsuranceIntelligence.Api.ClientPortal.ClaimantSessionEndpointFilter());
app.MapPost("/api/v1/client-portal/claims/{caseId:guid}/uploads/{uploadId:guid}/complete", InsuranceIntelligence.Api.ClientPortal.ResumableUploadEndpoints.Complete)
    .AddEndpointFilter(new InsuranceIntelligence.Api.ClientPortal.ClaimantSessionEndpointFilter());
app.MapDelete("/api/v1/client-portal/claims/{caseId:guid}/uploads/{uploadId:guid}", InsuranceIntelligence.Api.ClientPortal.ResumableUploadEndpoints.Cancel)
    .AddEndpointFilter(new InsuranceIntelligence.Api.ClientPortal.ClaimantSessionEndpointFilter());
app.MapGet("/api/v1/client-portal/claims/{caseId:guid}/on-file", InsuranceIntelligence.Api.ClientPortal.CustomerMemoryEndpoints.GetOnFile)
    .AddEndpointFilter(new InsuranceIntelligence.Api.ClientPortal.ClaimantSessionEndpointFilter());
app.MapPost("/api/v1/client-portal/claims/{caseId:guid}/on-file/use-document", InsuranceIntelligence.Api.ClientPortal.CustomerMemoryEndpoints.UseDocument)
    .AddEndpointFilter(new InsuranceIntelligence.Api.ClientPortal.ClaimantSessionEndpointFilter());
app.MapPost("/api/v1/client-portal/claims/{caseId:guid}/on-file/use-details", InsuranceIntelligence.Api.ClientPortal.CustomerMemoryEndpoints.UseDetails)
    .AddEndpointFilter(new InsuranceIntelligence.Api.ClientPortal.ClaimantSessionEndpointFilter());
app.MapGet("/api/v1/client-portal/known-details", InsuranceIntelligence.Api.ClientPortal.CustomerMemoryEndpoints.GetKnownDetails)
    .AddEndpointFilter(new InsuranceIntelligence.Api.ClientPortal.ClaimantSessionEndpointFilter());
app.MapPost("/api/v1/client-portal/claims/{caseId:guid}/documents/remove", ClientPortalEndpoints.RemoveDocument)
    .AddEndpointFilter(new InsuranceIntelligence.Api.ClientPortal.ClaimantSessionEndpointFilter());
app.MapPost("/api/v1/client-portal/claims/{caseId:guid}/documents/reorder", ClientPortalEndpoints.ReorderPages)
    .AddEndpointFilter(new InsuranceIntelligence.Api.ClientPortal.ClaimantSessionEndpointFilter());
app.MapPost("/api/v1/client-portal/claims/{caseId:guid}/documents/retry", ClientPortalEndpoints.RetryDocument)
    .AddEndpointFilter(new InsuranceIntelligence.Api.ClientPortal.ClaimantSessionEndpointFilter());
app.MapPost("/api/v1/client-portal/claims/{caseId:guid}/request-review", ClientPortalEndpoints.RequestReview)
    .AddEndpointFilter(new InsuranceIntelligence.Api.ClientPortal.ClaimantSessionEndpointFilter());
// Outcome L: "the customer sees their policy," "asks a question about the wording," and "requests
// a change to their contact details." None of these three has a {caseId} route segment (a
// verified policyholder with no claim yet must still reach them), so ClaimantSessionEndpointFilter's
// route-match check never fires for them; each handler requires the stashed session itself.
app.MapGet("/api/v1/client-portal/policy", ClientPortalEndpoints.GetPolicy)
    .AddEndpointFilter(new InsuranceIntelligence.Api.ClientPortal.ClaimantSessionEndpointFilter());
app.MapPost("/api/v1/client-portal/policy-questions", ClientPortalEndpoints.AskPolicyQuestion)
    .AddEndpointFilter(new InsuranceIntelligence.Api.ClientPortal.ClaimantSessionEndpointFilter());
app.MapPost("/api/v1/client-portal/contact-detail-changes", ClientPortalEndpoints.RequestContactDetailChange)
    .AddEndpointFilter(new InsuranceIntelligence.Api.ClientPortal.ClaimantSessionEndpointFilter());
app.MapPost("/api/v1/client-portal/magic-links", InsuranceIntelligence.Api.ClientPortal.ClaimantAuthenticationEndpoints.RequestMagicLink);
app.MapPost("/api/v1/client-portal/magic-links/redeem", InsuranceIntelligence.Api.ClientPortal.ClaimantAuthenticationEndpoints.RedeemMagicLink);
app.MapPost("/api/v1/client-portal/claims", InsuranceIntelligence.Api.ClientPortal.ClaimantAuthenticationEndpoints.CreateClaim)
    .AddEndpointFilter(new InsuranceIntelligence.Api.ClientPortal.ClaimantSessionEndpointFilter());

app.MapPost("/api/v1/cases", CaseEndpoints.CreateCase)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.CreateClaim));
app.MapGet("/api/v1/document-checklist/preview", InsuranceIntelligence.Api.Documents.DocumentEndpoints.PreviewChecklist)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.CreateClaim));
app.MapGet("/api/v1/policy-lookup", InsuranceIntelligence.Api.Cases.PolicyLookupEndpoints.LookupByPolicyNumber)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.CreateClaim));
app.MapGet("/api/v1/cases", CaseEndpoints.ListCases)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAssignedClaims, Permission.ViewAllClaims));
app.MapGet("/api/v1/cases/{caseId:guid}", CaseEndpoints.GetCase)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAssignedClaims, Permission.ViewAllClaims));
app.MapGet("/api/v1/cases/{caseId:guid}/product-stages", CaseEndpoints.GetProductStages)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAssignedClaims, Permission.ViewAllClaims));
app.MapPost("/api/v1/cases/{caseId:guid}/assign", CaseEndpoints.AssignCase)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAssignedClaims, Permission.ViewAllClaims));
app.MapGet("/api/v1/cases/{caseId:guid}/status-options", InsuranceIntelligence.Api.Cases.ClaimStatusEndpoints.Options)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAssignedClaims, Permission.ViewAllClaims));
app.MapPost("/api/v1/cases/{caseId:guid}/status", InsuranceIntelligence.Api.Cases.ClaimStatusEndpoints.Change)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAssignedClaims, Permission.ViewAllClaims));
app.MapPost("/api/v1/cases/{caseId:guid}/claim-link", InsuranceIntelligence.Api.ClientPortal.StaffClaimLinkEndpoints.Send)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.SendCustomerUpdate));
app.MapPost("/api/v1/cases/{caseId:guid}/escalate", CaseEndpoints.EscalateCase)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAssignedClaims, Permission.ViewAllClaims));
app.MapPost("/api/v1/cases/{caseId:guid}/confirm", CaseEndpoints.ConfirmCase)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAssignedClaims, Permission.ViewAllClaims));
app.MapPost("/api/v1/cases/{caseId:guid}/resolve", InsuranceIntelligence.Api.Cases.ClaimResolutionEndpoints.Resolve)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ApproveSettlementWorkflowStep));
app.MapPost("/api/v1/cases/{caseId:guid}/satisfaction-response", InsuranceIntelligence.Api.Cases.SatisfactionEndpoints.RecordResponse)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAssignedClaims, Permission.ViewAllClaims));
app.MapGet("/api/v1/cases/{caseId:guid}/satisfaction-responses", InsuranceIntelligence.Api.Cases.SatisfactionEndpoints.ListResponses)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAssignedClaims, Permission.ViewAllClaims));
app.MapGet("/api/v1/claims", ClaimsController.ListClaims)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAssignedClaims, Permission.ViewAllClaims));
app.MapGet("/api/v1/claims/attention", ClaimsController.GetAttention)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAssignedClaims, Permission.ViewAllClaims));
app.MapGet("/api/v1/claims/daily-summary", ClaimsController.GetDailySummary)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewCommandCenter));
app.MapGet("/api/v1/claims/{claimId:guid}", ClaimsController.GetClaim)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAssignedClaims, Permission.ViewAllClaims));
app.MapGet("/api/v1/claims/{claimId:guid}/draft-context", ClaimsController.GetDraftContext)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAiOutputs));
app.MapPost("/api/v1/cases/{caseId:guid}/documents", DocumentEndpoints.UploadDocuments)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.UploadDocument));
app.MapGet("/api/v1/cases/{caseId:guid}/documents", DocumentEndpoints.ListDocuments)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAssignedClaims, Permission.ViewAllClaims));
app.MapGet("/api/v1/cases/{caseId:guid}/document-checklist", DocumentEndpoints.GetChecklist)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAssignedClaims, Permission.ViewAllClaims));
app.MapGet("/api/v1/cases/{caseId:guid}/documents/{documentId:guid}/content", DocumentEndpoints.GetDocumentContent)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAssignedClaims, Permission.ViewAllClaims));
app.MapGet("/api/v1/cases/{caseId:guid}/document-slots/{slotKey}/pdf", DocumentEndpoints.GetSlotPdf)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAssignedClaims, Permission.ViewAllClaims));
app.MapGet("/api/v1/cases/{caseId:guid}/documents/{documentId:guid}/image-preview", DocumentEndpoints.GetDocumentImagePreview)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAssignedClaims, Permission.ViewAllClaims));
app.MapPost("/api/v1/cases/{caseId:guid}/documents/{documentId:guid}/reprocess", DocumentEndpoints.ReprocessDocument)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.UploadDocument));
app.MapPost("/api/v1/cases/{caseId:guid}/documents/{documentId:guid}/file-into", DocumentEndpoints.FileDocumentIntoSlot)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.UploadDocument));
app.MapPost("/api/v1/cases/{caseId:guid}/documents/{documentId:guid}/reclassify", DocumentEndpoints.ReclassifyDocument)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.UploadDocument));
app.MapPost("/api/v1/cases/{caseId:guid}/documents/{documentId:guid}/approve-extraction", DocumentEndpoints.ApproveExtraction)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.UploadDocument));
app.MapPost("/api/v1/cases/{caseId:guid}/documents/{documentId:guid}/correct-field", DocumentEndpoints.CorrectExtractionField)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.UploadDocument));
app.MapGet("/api/v1/document-field-validation-rules", InsuranceIntelligence.Api.Documents.DocumentFieldValidationRuleEndpoints.ListRules)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageTenantSettings));
app.MapPost("/api/v1/document-field-validation-rules", InsuranceIntelligence.Api.Documents.DocumentFieldValidationRuleEndpoints.SaveRule)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageTenantSettings));
app.MapGet("/api/v1/cases/{caseId:guid}/review-requests", ContestabilityEndpoints.ListReviewRequests)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAssignedClaims, Permission.ViewAllClaims));
app.MapPost("/api/v1/cases/{caseId:guid}/review-requests/{requestId:guid}/acknowledge", ContestabilityEndpoints.AcknowledgeReviewRequest)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAssignedClaims, Permission.ViewAllClaims));
app.MapPost("/api/v1/cases/{caseId:guid}/review-requests/{requestId:guid}/resolve", ContestabilityEndpoints.ResolveReviewRequest)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAssignedClaims, Permission.ViewAllClaims));
// Outcome L: staff side of a policyholder's contact-detail change request.
app.MapGet("/api/v1/contact-detail-changes", InsuranceIntelligence.Api.Customers.ContactDetailChangeEndpoints.ListChangeRequests)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAssignedClaims, Permission.ViewAllClaims));
app.MapPost("/api/v1/contact-detail-changes/{requestId:guid}/acknowledge", InsuranceIntelligence.Api.Customers.ContactDetailChangeEndpoints.AcknowledgeChangeRequest)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAssignedClaims, Permission.ViewAllClaims));
app.MapPost("/api/v1/contact-detail-changes/{requestId:guid}/resolve", InsuranceIntelligence.Api.Customers.ContactDetailChangeEndpoints.ResolveChangeRequest)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAssignedClaims, Permission.ViewAllClaims));
app.MapPost("/api/v1/knowledge-documents", KnowledgeEndpoints.UploadKnowledgeDocument)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageKnowledgeBase));
app.MapGet("/api/v1/knowledge-documents", KnowledgeEndpoints.ListKnowledgeDocuments)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageKnowledgeBase));
app.MapGet("/api/v1/knowledge-documents/{knowledgeDocId:guid}", KnowledgeEndpoints.GetKnowledgeDocument)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageKnowledgeBase));
app.MapPost("/api/v1/knowledge-documents/{knowledgeDocId:guid}/approve", KnowledgeEndpoints.ApproveKnowledgeDocument)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageKnowledgeBase));
app.MapPost("/api/v1/knowledge-documents/{knowledgeDocId:guid}/archive", KnowledgeEndpoints.ArchiveKnowledgeDocument)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageKnowledgeBase));
app.MapPost("/api/v1/knowledge-documents/{knowledgeDocId:guid}/classify", KnowledgeEndpoints.ClassifyKnowledgeDocument)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageKnowledgeBase));
app.MapPost("/api/v1/knowledge-documents/reindex", KnowledgeEndpoints.ReindexKnowledgeDocuments)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageKnowledgeBase));
app.MapPost("/api/v1/assistant/policy-questions", AssistantEndpoints.AskPolicyQuestion)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAiOutputs));
app.MapGet("/api/v1/assistant/actions", AssistantEndpoints.ListActions)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAiOutputs));
app.MapGet("/api/v1/assistant/actions/{caseId:guid}", AssistantEndpoints.ListActionsForCase)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAiOutputs));
// The prose POST /command-center/assistant endpoint was dropped (superseded by chat-first /query).
// The /metrics endpoint is retained via the salvaged CommandCenterMetricsLoader (metric_pills).
app.MapGet("/api/v1/command-center/metrics", CommandCenterMetricsLoader.GetMetrics)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewCommandCenter));
app.MapPost("/api/v1/command-center/query", CommandCenterQueryEndpoints.RunQuery)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewCommandCenter));
app.MapGet("/api/v1/command-center/conversations", CommandCenterConversationEndpoints.List)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewCommandCenter));
app.MapGet("/api/v1/command-center/conversations/{conversationId:guid}", CommandCenterConversationEndpoints.Get)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewCommandCenter));
app.MapPatch("/api/v1/command-center/conversations/{conversationId:guid}", CommandCenterConversationEndpoints.Rename)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewCommandCenter));
app.MapPost("/api/v1/command-center/conversations/{conversationId:guid}/archive", CommandCenterConversationEndpoints.Archive)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewCommandCenter));
app.MapPost("/api/v1/command-center/conversations/{conversationId:guid}/restore", CommandCenterConversationEndpoints.Restore)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewCommandCenter));
app.MapGet("/api/v1/provider-status", ProviderStatusEndpoints.GetProviderStatus)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewCommandCenter));
app.MapGet("/api/v1/system/capabilities", SystemCapabilityEndpoints.GetCapabilities)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewCommandCenter));
app.MapGet("/api/v1/cases/{caseId:guid}/timeline", WorkflowEndpoints.GetTimeline)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAssignedClaims, Permission.ViewAllClaims));
app.MapGet("/api/v1/cases/{caseId:guid}/recommended-actions", WorkflowEndpoints.GetRecommendedActions)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAssignedClaims, Permission.ViewAllClaims));
app.MapPost("/api/v1/cases/{caseId:guid}/workflow/evaluate", WorkflowEndpoints.EvaluateWorkflow)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ApproveSettlementWorkflowStep));
app.MapGet("/api/v1/workflow/tasks", WorkflowEndpoints.ListTasks)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAssignedClaims, Permission.ViewAllClaims));
app.MapGet("/api/v1/my-work", InsuranceIntelligence.Api.MyWork.MyWorkEndpoints.List)
    .AddEndpointFilter(new PermissionEndpointFilter(
        Permission.ViewAssignedClaims,
        Permission.ViewAllClaims,
        Permission.ApproveSettlementWorkflowStep,
        Permission.ManageCustomerCommunications,
        Permission.ReviewAgentWorkItems));
app.MapGet("/api/v1/workflow/tasks/{taskId:guid}", WorkflowEndpoints.GetTask)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAssignedClaims, Permission.ViewAllClaims));
app.MapPost("/api/v1/workflow/tasks/{taskId:guid}/complete", WorkflowEndpoints.CompleteTask)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAssignedClaims, Permission.ViewAllClaims));
app.MapPost("/api/v1/workflow/tasks/{taskId:guid}/assign", WorkflowEndpoints.AssignTask)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAssignedClaims, Permission.ViewAllClaims));
app.MapGet("/api/v1/approvals", ApprovalEndpoints.ListApprovals)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ApproveSettlementWorkflowStep));
app.MapGet("/api/v1/approvals/{approvalId:guid}", ApprovalEndpoints.GetApproval)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ApproveSettlementWorkflowStep));
app.MapPost("/api/v1/cases/{caseId:guid}/approvals", ApprovalEndpoints.CreateApproval)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAssignedClaims, Permission.ViewAllClaims));
app.MapPost("/api/v1/approvals/{approvalId:guid}/approve", ApprovalEndpoints.ApproveApproval)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ApproveSettlementWorkflowStep));
app.MapPost("/api/v1/approvals/{approvalId:guid}/reject", ApprovalEndpoints.RejectApproval)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ApproveSettlementWorkflowStep));
app.MapPost("/api/v1/approvals/{approvalId:guid}/override", ApprovalEndpoints.OverrideApproval)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.OverrideAiRecommendation));
app.MapPost("/api/v1/approvals/{approvalId:guid}/escalate", ApprovalEndpoints.EscalateApproval)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ApproveSettlementWorkflowStep));
app.MapPost("/api/v1/approvals/{approvalId:guid}/execute-next-step", ApprovalEndpoints.ExecuteApprovedNextStep)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ApproveSettlementWorkflowStep));
app.MapPost("/api/v1/cases/{caseId:guid}/customer-updates/draft", CustomerUpdateEndpoints.DraftCustomerUpdate)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageCustomerCommunications));
app.MapPost("/api/v1/cases/{caseId:guid}/summary", InsuranceIntelligence.Api.Claims.ClaimsSummaryEndpoints.SummarizeCase)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAssignedClaims, Permission.ViewAllClaims));
app.MapGet("/api/v1/cases/{caseId:guid}/case-review", InsuranceIntelligence.Api.Claims.ClaimsSummaryEndpoints.GetLatestCaseReview)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAssignedClaims, Permission.ViewAllClaims));
app.MapGet("/api/v1/cases/{caseId:guid}/customer-updates", CustomerUpdateEndpoints.ListCustomerUpdates)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAssignedClaims));
app.MapGet("/api/v1/customer-updates", CustomerUpdateEndpoints.ListAllCustomerUpdates)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAssignedClaims, Permission.ViewAllClaims));
app.MapPost("/api/v1/customer-updates/{updateId:guid}/approve", CustomerUpdateEndpoints.ApproveCustomerUpdate)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageCustomerCommunications));
app.MapPost("/api/v1/customer-updates/{updateId:guid}/reject", CustomerUpdateEndpoints.RejectCustomerUpdate)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageCustomerCommunications));
app.MapPost("/api/v1/customer-updates/{updateId:guid}/send", CustomerUpdateEndpoints.SendCustomerUpdate)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.SendCustomerUpdate));
app.MapPut("/api/v1/customer-updates/{updateId:guid}", CustomerUpdateEndpoints.EditCustomerUpdate)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageCustomerCommunications));
app.MapPost("/api/v1/cases/{caseId:guid}/customer-updates/write", CustomerUpdateEndpoints.WriteCustomerUpdate)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageCustomerCommunications));
app.MapPost("/api/v1/cases/{caseId:guid}/customer-updates/from-template", CustomerUpdateEndpoints.CreateCustomerUpdateFromTemplate)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageCustomerCommunications));

// Message template library (outcome D), versioned exactly like agent behaviour and products.
app.MapGet("/api/v1/communication-templates", InsuranceIntelligence.Api.Workflow.CommunicationTemplateEndpoints.ListTemplates)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageCustomerCommunications));
app.MapGet("/api/v1/communication-templates/{templateKey}", InsuranceIntelligence.Api.Workflow.CommunicationTemplateEndpoints.GetCurrent)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageCustomerCommunications));
app.MapGet("/api/v1/communication-templates/{templateKey}/versions", InsuranceIntelligence.Api.Workflow.CommunicationTemplateEndpoints.ListVersions)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageCustomerCommunications));
app.MapPost("/api/v1/communication-templates/{templateKey}", InsuranceIntelligence.Api.Workflow.CommunicationTemplateEndpoints.SaveVersion)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageCustomerCommunications));
app.MapPost("/api/v1/communication-templates/seed-defaults", InsuranceIntelligence.Api.Workflow.CommunicationTemplateEndpoints.SeedDefaults)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageCustomerCommunications));
app.MapGet("/api/v1/reports/operations-summary", ReportsEndpoints.GetOperationsSummary)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewCommandCenter));
app.MapGet("/api/v1/admin/tenant-settings", InsuranceIntelligence.Api.Admin.TenantSettingsEndpoints.Get)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageTenantSettings));
app.MapPut("/api/v1/admin/tenant-settings", InsuranceIntelligence.Api.Admin.TenantSettingsEndpoints.Put)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageTenantSettings));
app.MapGet("/api/v1/admin/tenant-configuration", AdminEndpoints.GetTenantConfiguration)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageTenantSettings));
app.MapPut("/api/v1/admin/tenant-configuration", AdminEndpoints.UpdateTenantConfiguration)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageTenantSettings));
app.MapGet("/api/v1/admin/provider-modes", AdminEndpoints.GetProviderModes)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageTenantSettings));
app.MapGet("/api/v1/admin/provider-health", AdminEndpoints.GetProviderHealth)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageTenantSettings));
app.MapGet("/api/v1/admin/roles", AdminEndpoints.GetRoles)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageTenantSettings));
app.MapGet("/api/v1/admin/users", AdminEndpoints.GetUsers)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageTenantSettings));
app.MapPost("/api/v1/admin/users", AdminEndpoints.CreateUser)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageTenantSettings));
app.MapPut("/api/v1/admin/users/{userId}", AdminEndpoints.UpdateUserRole)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageTenantSettings));
app.MapPut("/api/v1/admin/users/{userId}/role", AdminEndpoints.UpdateUserRole)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageTenantSettings));
// Demo dataset seeding, hard-gated inside the handler to non-protected runtime profiles
// (see DemoSeedEndpoints), never reachable in Staging/Production/ManagedApplicationReviewer.
app.MapPost("/api/v1/admin/demo-seed", InsuranceIntelligence.Api.Admin.DemoSeedEndpoints.Run);
app.MapPost("/api/v1/admin/demo-seed/policyholders", InsuranceIntelligence.Api.Admin.DemoSeedEndpoints.SeedPolicyholders);
app.MapPost("/api/v1/admin/demo-seed/test-policyholder", InsuranceIntelligence.Api.Admin.DemoSeedEndpoints.SeedTestPolicyholder);
app.MapPost("/api/v1/admin/demo-seed/microinsurance", InsuranceIntelligence.Api.Admin.DemoSeedEndpoints.SeedMicroinsurance);
app.MapPost("/api/v1/admin/demo-seed/bank-customers", InsuranceIntelligence.Api.Admin.DemoSeedEndpoints.SeedBankCustomers);
app.MapPost("/api/v1/admin/demo-seed/conversations", InsuranceIntelligence.Api.Admin.DemoSeedEndpoints.SeedConversations);
app.MapPost("/api/v1/admin/db-migrate", InsuranceIntelligence.Api.Admin.DatabaseMigrationEndpoints.ApplyPending);
app.MapPost("/api/v1/admin/db-migrate/reconcile-history", InsuranceIntelligence.Api.Admin.DatabaseMigrationEndpoints.ReconcileHistory);
app.MapPost("/api/v1/admin/db-migrate/force-reconcile-schema", InsuranceIntelligence.Api.Admin.DatabaseMigrationEndpoints.ForceReconcileSchema);
app.MapGet("/api/v1/admin/db-migrate/diagnose", InsuranceIntelligence.Api.Admin.DatabaseMigrationEndpoints.Diagnose);

if (developmentDiagnosticsEnabled)
{
    app.MapPost("/api/v1/admin/debug-teams-notification", DebugEndpoints.TriggerTeamsNotification)
        .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageTenantSettings));
    // What the Mock communication adapters "sent" (Mock communication provider only).
    app.MapGet("/api/v1/dev/mock-outbox", InsuranceIntelligence.Api.Local.Mock.MockOutboxEndpoints.List)
        .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageTenantSettings));
    app.MapDelete("/api/v1/dev/mock-outbox", InsuranceIntelligence.Api.Local.Mock.MockOutboxEndpoints.Clear)
        .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageTenantSettings));
    // Puts a message into a pretend IMAP mailbox (Mock email provider only).
    app.MapPost("/api/v1/dev/mock-mailbox/deliver", InsuranceIntelligence.Api.Local.Mock.MockMailboxEndpoints.Deliver)
        .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageTenantSettings));
}

// W2.5: Phase 2 core-workspace read routes (see docs/02-development-engineering/iip_api_specification.md).
app.MapGet("/api/v1/channels/overview", ChannelEndpoints.GetOverview)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewChannels));
app.MapGet("/api/v1/customers", CustomerProfileEndpoints.ListCustomers)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewCustomers));
app.MapGet("/api/v1/customers/{customerId}", CustomerProfileEndpoints.GetCustomerProfile)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewCustomers));
app.MapPost("/api/v1/customers/{customerId}/summary", InsuranceIntelligence.Api.Customer.CustomerSummaryEndpoints.Summarize)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewCustomers));
// Outcome M: consent as a first class, staff-visible, auditable record.
app.MapGet("/api/v1/customers/{customerId}/consent", InsuranceIntelligence.Api.Customers.ConsentEndpoints.ListForCustomer)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewCustomers));
app.MapPost("/api/v1/customers/{customerId}/consent", InsuranceIntelligence.Api.Customers.ConsentEndpoints.RecordExplicitOptIn)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageCustomerCommunications));
app.MapPost("/api/v1/customers/{customerId}/consent/withdraw", InsuranceIntelligence.Api.Customers.ConsentEndpoints.Withdraw)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageCustomerCommunications));
// Names for ids, for any staff member who can see claims or customers; also feeds the assignment picker.
app.MapGet("/api/v1/directory/people", InsuranceIntelligence.Api.People.PeopleEndpoints.ListPeople)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAssignedClaims, Permission.ViewAllClaims, Permission.ViewCustomers, Permission.ViewSalesLeads));
app.MapGet("/api/v1/brief", InsuranceIntelligence.Api.Brief.BriefEndpoints.GetBrief)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAssignedClaims, Permission.ViewAllClaims));
app.MapGet("/api/v1/academy/pricing", InsuranceIntelligence.Api.Academy.AcademyEndpoints.GetPricing);
app.MapPut("/api/v1/academy/pricing", InsuranceIntelligence.Api.Academy.AcademyEndpoints.SavePricing)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageTenantSettings));
app.MapGet("/api/v1/sales-leads", SalesLeadEndpoints.ListLeads)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewSalesLeads));
app.MapGet("/api/v1/sales-leads/{leadId}", SalesLeadEndpoints.GetLeadDetail)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewSalesLeads));
app.MapPost("/api/v1/sales-leads", SalesLeadEndpoints.CreateLead)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageSalesLeads));
app.MapPost("/api/v1/sales-leads/{leadId}/notes", SalesLeadEndpoints.AddNote)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageSalesLeads));
app.MapPut("/api/v1/sales-leads/{leadId}/assignee", SalesLeadEndpoints.Assign)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageSalesLeads));
app.MapPost("/api/v1/sales-leads/{leadId}/insights/refresh", SalesLeadEndpoints.RefreshInsights)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewSalesLeads));
app.MapGet("/api/v1/acquisition/escalations", async (IAcquisitionReportService reports, CancellationToken ct) => Results.Ok(await reports.EscalationsAsync(ct)))
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewSalesLeads));
app.MapGet("/api/v1/acquisition/agents", async (IAcquisitionReportService reports, CancellationToken ct) => Results.Ok(await reports.AgentOperationsAsync(ct)))
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewSalesLeads));
app.MapGet("/api/v1/acquisition/channel-intelligence", async (IAcquisitionReportService reports, CancellationToken ct) => Results.Ok(await reports.ChannelIntelligenceAsync(ct)))
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewSalesLeads));
app.MapPost("/api/v1/sales-leads/outreach/draft", SalesLeadEndpoints.DraftOutreach)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageSalesLeads));
app.MapPost("/api/v1/sales-leads/outreach/follow-ups", SalesLeadEndpoints.DraftFollowUps)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageSalesLeads));
app.MapPost("/api/v1/sales-leads/outreach/send", SalesLeadEndpoints.SendOutreachMany)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageCustomerCommunications));
app.MapPut("/api/v1/sales-leads/{leadId}/outreach/{draftId:guid}", SalesLeadEndpoints.EditOutreach)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageSalesLeads));
app.MapPost("/api/v1/sales-leads/{leadId}/outreach/{draftId:guid}/send", SalesLeadEndpoints.SendOutreach)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageCustomerCommunications));
app.MapPost("/api/v1/sales-leads/{leadId}/outreach/{draftId:guid}/dismiss", SalesLeadEndpoints.DismissOutreach)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageSalesLeads));
app.MapPost("/api/v1/sales-leads/triage", SalesLeadEndpoints.RunTriageForAll)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageSalesLeads));
app.MapPost("/api/v1/sales-leads/{leadId}/triage", SalesLeadEndpoints.RunTriageForLead)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageSalesLeads));

// Product configuration, the spine everything else in claim handling reads from (blueprint
// outcome A/C). Every save is a new version; publishing needs an IRA approval reference and a
// second person, distinct from whoever submitted it.
// Claim types (build-loop item 3): dropdown options and suggestions for whoever is filing a claim,
// plus the one-off import of the free-text types already used on existing claims.
app.MapGet("/api/v1/claim-types", InsuranceIntelligence.Api.Products.ClaimTypeEndpoints.List)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.CreateClaim, Permission.ViewAssignedClaims, Permission.ViewAllClaims, Permission.ManageProducts));
app.MapPost("/api/v1/claim-types/suggest", InsuranceIntelligence.Api.Products.ClaimTypeEndpoints.Suggest)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.CreateClaim, Permission.ViewAssignedClaims, Permission.ViewAllClaims, Permission.ManageProducts));
app.MapPost("/api/v1/products/{productId:guid}/claim-types/import-existing", InsuranceIntelligence.Api.Products.ClaimTypeEndpoints.ImportExisting)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageProducts));
// Preview as customer (build-loop item 8): read only, nothing stored or sent.
app.MapGet("/api/v1/products/{productId:guid}/customer-preview", InsuranceIntelligence.Api.Products.ProductPreviewEndpoints.CustomerPreview)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageProducts));
// Policy wording to drafted setup (build-loop item 7): the model proposes, a person accepts, edits or
// rejects each item, and applying makes a draft version of the product (publishing still needs a
// second approver).
app.MapPost("/api/v1/products/{productId:guid}/wording-drafts", InsuranceIntelligence.Api.Products.WordingDraftEndpoints.Create)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageProducts));
app.MapGet("/api/v1/products/{productId:guid}/wording-drafts", InsuranceIntelligence.Api.Products.WordingDraftEndpoints.List)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageProducts));
app.MapGet("/api/v1/products/{productId:guid}/wording-drafts/{draftId:guid}", InsuranceIntelligence.Api.Products.WordingDraftEndpoints.Get)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageProducts));
app.MapPut("/api/v1/products/{productId:guid}/wording-drafts/{draftId:guid}/items/{itemId}", InsuranceIntelligence.Api.Products.WordingDraftEndpoints.UpdateItem)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageProducts));
app.MapPost("/api/v1/products/{productId:guid}/wording-drafts/{draftId:guid}/apply", InsuranceIntelligence.Api.Products.WordingDraftEndpoints.Apply)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageProducts));
app.MapPost("/api/v1/products/{productId:guid}/wording-drafts/{draftId:guid}/discard", InsuranceIntelligence.Api.Products.WordingDraftEndpoints.Discard)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageProducts));
// Privacy notice and consent (build-loop item 9): the claimant reads the notice and agrees before the
// first upload, and can withdraw from the claim page; staff see whether the claimant has agreed.
app.MapGet("/api/v1/client-portal/privacy-notice", InsuranceIntelligence.Api.ClientPortal.ClaimantConsentEndpoints.GetNotice)
    .AddEndpointFilter(new InsuranceIntelligence.Api.ClientPortal.ClaimantSessionEndpointFilter());
app.MapPost("/api/v1/client-portal/consent", InsuranceIntelligence.Api.ClientPortal.ClaimantConsentEndpoints.Grant)
    .AddEndpointFilter(new InsuranceIntelligence.Api.ClientPortal.ClaimantSessionEndpointFilter());
app.MapPost("/api/v1/client-portal/consent/withdraw", InsuranceIntelligence.Api.ClientPortal.ClaimantConsentEndpoints.Withdraw)
    .AddEndpointFilter(new InsuranceIntelligence.Api.ClientPortal.ClaimantSessionEndpointFilter());
app.MapGet("/api/v1/cases/{caseId:guid}/privacy-consent", InsuranceIntelligence.Api.ClientPortal.ClaimantConsentEndpoints.GetForCase)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAssignedClaims, Permission.ViewAllClaims));
app.MapGet("/api/v1/client-portal/claim-types", InsuranceIntelligence.Api.Products.ClaimTypeEndpoints.ListForClaimant)
    .AddEndpointFilter(new InsuranceIntelligence.Api.ClientPortal.ClaimantSessionEndpointFilter());
// Claim fields (build-loop item 5): the questions a product asks about a claim and a claim's answers.
app.MapGet("/api/v1/claim-fields", InsuranceIntelligence.Api.Cases.ClaimFieldEndpoints.GetForm)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.CreateClaim, Permission.ViewAssignedClaims, Permission.ViewAllClaims, Permission.ManageProducts));
// Extracted data into the claim (build-loop item 15): suggestions read from documents, decided by an officer.
// Authenticity checks (build-loop item 16): advice on pictures, with a person's disposition.
// Background work (build-loop item 17): what is waiting, running, done and stuck.
app.MapGet("/api/v1/admin/background-jobs", InsuranceIntelligence.Api.Jobs.BackgroundJobEndpoints.List)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageTenantSettings));
app.MapPost("/api/v1/admin/background-jobs/{jobId:guid}/retry", InsuranceIntelligence.Api.Jobs.BackgroundJobEndpoints.Retry)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageTenantSettings));
app.MapGet("/api/v1/cases/{caseId:guid}/background-work", InsuranceIntelligence.Api.Jobs.BackgroundJobEndpoints.ListForCase)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAssignedClaims, Permission.ViewAllClaims));
app.MapGet("/api/v1/cases/{caseId:guid}/authenticity", InsuranceIntelligence.Api.Documents.AuthenticityEndpoints.List)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAssignedClaims, Permission.ViewAllClaims));
app.MapPost("/api/v1/cases/{caseId:guid}/documents/{documentId:guid}/authenticity/disposition", InsuranceIntelligence.Api.Documents.AuthenticityEndpoints.SetDisposition)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAssignedClaims, Permission.ViewAllClaims));
app.MapGet("/api/v1/admin/claim-status-wording", InsuranceIntelligence.Api.Admin.ClaimStatusWordingEndpoints.Get)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageTenantSettings));
app.MapPut("/api/v1/admin/claim-status-wording", InsuranceIntelligence.Api.Admin.ClaimStatusWordingEndpoints.Put)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageTenantSettings));
app.MapGet("/api/v1/admin/status-notifications", InsuranceIntelligence.Api.Admin.StatusNotificationSettingsEndpoints.Get)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageTenantSettings));
app.MapPut("/api/v1/admin/status-notifications", InsuranceIntelligence.Api.Admin.StatusNotificationSettingsEndpoints.Put)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageTenantSettings));
app.MapGet("/api/v1/cases/{caseId:guid}/assessment", InsuranceIntelligence.Api.Fraud.ClaimAssessmentEndpoints.Get)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewFraudSignals));
app.MapPost("/api/v1/cases/{caseId:guid}/assessment/refresh", InsuranceIntelligence.Api.Fraud.ClaimAssessmentEndpoints.Refresh)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewFraudSignals));
app.MapPost("/api/v1/cases/{caseId:guid}/assessment/response", InsuranceIntelligence.Api.Fraud.ClaimAssessmentEndpoints.Respond)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.OverrideAiRecommendation));
app.MapGet("/api/v1/cases/{caseId:guid}/field-proposals", InsuranceIntelligence.Api.Cases.FieldProposalEndpoints.List)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAssignedClaims, Permission.ViewAllClaims));
app.MapPost("/api/v1/cases/{caseId:guid}/field-proposals/{proposalId:guid}/accept", InsuranceIntelligence.Api.Cases.FieldProposalEndpoints.Accept)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAssignedClaims, Permission.ViewAllClaims));
app.MapPost("/api/v1/cases/{caseId:guid}/field-proposals/{proposalId:guid}/reject", InsuranceIntelligence.Api.Cases.FieldProposalEndpoints.Reject)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAssignedClaims, Permission.ViewAllClaims));
app.MapGet("/api/v1/cases/{caseId:guid}/fields", InsuranceIntelligence.Api.Cases.ClaimFieldEndpoints.GetForClaim)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAssignedClaims, Permission.ViewAllClaims));
app.MapPut("/api/v1/cases/{caseId:guid}/field-values", InsuranceIntelligence.Api.Cases.ClaimFieldEndpoints.SaveForClaim)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.CreateClaim, Permission.ViewAssignedClaims, Permission.ViewAllClaims));
app.MapGet("/api/v1/client-portal/claim-fields", InsuranceIntelligence.Api.Cases.ClaimFieldEndpoints.GetFormForClaimant)
    .AddEndpointFilter(new InsuranceIntelligence.Api.ClientPortal.ClaimantSessionEndpointFilter());
app.MapGet("/api/v1/client-portal/claims/{caseId:guid}/fields", InsuranceIntelligence.Api.Cases.ClaimFieldEndpoints.GetForClaimByClaimant)
    .AddEndpointFilter(new InsuranceIntelligence.Api.ClientPortal.ClaimantSessionEndpointFilter());
app.MapPut("/api/v1/client-portal/claims/{caseId:guid}/field-values", InsuranceIntelligence.Api.Cases.ClaimFieldEndpoints.SaveForClaimant)
    .AddEndpointFilter(new InsuranceIntelligence.Api.ClientPortal.ClaimantSessionEndpointFilter());
app.MapGet("/api/v1/products", InsuranceIntelligence.Api.Products.ProductEndpoints.ListProducts)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageProducts));
app.MapGet("/api/v1/products/default-stage-library", InsuranceIntelligence.Api.Products.ProductEndpoints.GetDefaultStageLibrary)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageProducts));
app.MapGet("/api/v1/products/{productId:guid}", InsuranceIntelligence.Api.Products.ProductEndpoints.GetCurrent)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageProducts));
app.MapGet("/api/v1/products/{productId:guid}/versions", InsuranceIntelligence.Api.Products.ProductEndpoints.ListVersions)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageProducts));
app.MapGet("/api/v1/products/{productId:guid}/versions/{version:int}", InsuranceIntelligence.Api.Products.ProductEndpoints.GetVersion)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageProducts));
app.MapPost("/api/v1/products", InsuranceIntelligence.Api.Products.ProductEndpoints.CreateDraft)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageProducts));
app.MapPut("/api/v1/products/{productId:guid}", InsuranceIntelligence.Api.Products.ProductEndpoints.UpdateDraft)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageProducts));
app.MapPost("/api/v1/products/{productId:guid}/submit", InsuranceIntelligence.Api.Products.ProductEndpoints.SubmitForPublishApproval)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageProducts));
app.MapPost("/api/v1/products/{productId:guid}/approve", InsuranceIntelligence.Api.Products.ProductEndpoints.ApprovePublish)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageProducts));
app.MapPost("/api/v1/products/{productId:guid}/reject", InsuranceIntelligence.Api.Products.ProductEndpoints.RejectPublish)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageProducts));
app.MapPost("/api/v1/products/{productId:guid}/retire", InsuranceIntelligence.Api.Products.ProductEndpoints.Retire)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageProducts));

// Document type catalog (claim intake build prompt outcome A): the eight built-in types plus
// whatever an insurer adds, versioned like communication templates. Backs the document matrix
// dropdown and the report of products whose matrix already holds a key the catalog does not know.
app.MapGet("/api/v1/document-types", InsuranceIntelligence.Api.Documents.DocumentTypeCatalogEndpoints.ListCurrent)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageProducts));
app.MapGet("/api/v1/document-types/{key}/versions", InsuranceIntelligence.Api.Documents.DocumentTypeCatalogEndpoints.ListVersions)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageProducts));
app.MapPost("/api/v1/document-types/{key}", InsuranceIntelligence.Api.Documents.DocumentTypeCatalogEndpoints.SaveVersion)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageProducts));
app.MapPost("/api/v1/document-types/seed-defaults", InsuranceIntelligence.Api.Documents.DocumentTypeCatalogEndpoints.SeedDefaults)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageProducts));
app.MapGet("/api/v1/document-types/unresolved-product-keys", InsuranceIntelligence.Api.Documents.DocumentTypeCatalogEndpoints.ListUnresolvedProductDocumentKeys)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageProducts));

// Outcome N: file import for insurers whose core system has no API. Parse reads the uploaded file
// only (nothing persisted yet); Commit takes the confirmed column mapping back and imports.
app.MapPost("/api/v1/policy-import/parse", InsuranceIntelligence.Api.Policies.PolicyImportEndpoints.Parse)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageProducts));
app.MapPost("/api/v1/policy-import/commit", InsuranceIntelligence.Api.Policies.PolicyImportEndpoints.Commit)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageProducts));

// Outcome O: retention watch, a screen of its own for policies nearing cover end.
app.MapGet("/api/v1/retention/watch", InsuranceIntelligence.Api.Retention.RetentionEndpoints.GetWatch)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewCustomers));
app.MapPost("/api/v1/retention/policies/{policyId}/reminders", InsuranceIntelligence.Api.Retention.RetentionEndpoints.DraftReminder)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageCustomerCommunications));
app.MapPost("/api/v1/retention/reminders/{reminderId:guid}/approve-and-send", InsuranceIntelligence.Api.Retention.RetentionEndpoints.ApproveAndSend)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageCustomerCommunications));

// Outcome K acceptance: "accuracy per document type is measured and visible," computed from the
// labelled examples every reclassification/correction/confirmation accumulates.
app.MapGet("/api/v1/documents/quality/accuracy", InsuranceIntelligence.Api.Documents.DocumentQualityEndpoints.GetAccuracyByType)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageProducts));

// Gate 1 (G1.7): platform-operator tenant onboarding + Entra admin-consent flow (ADR-0005).
app.MapPost("/api/v1/onboarding", InsuranceIntelligence.Api.Onboarding.OnboardingEndpoints.Create)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageTenantOnboarding));
app.MapGet("/api/v1/onboarding", InsuranceIntelligence.Api.Onboarding.OnboardingEndpoints.List)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageTenantOnboarding));
app.MapGet("/api/v1/onboarding/{onboardingId}", InsuranceIntelligence.Api.Onboarding.OnboardingEndpoints.Get)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageTenantOnboarding));
app.MapGet("/api/v1/onboarding/{onboardingId}/consent-url", InsuranceIntelligence.Api.Onboarding.OnboardingEndpoints.GetConsentUrl)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageTenantOnboarding));
// Admin-consent redirect target, authorized by the opaque state value, not a permission.
// Deliberately OFF /api/v1: the insurer admin returns here from Microsoft UNAUTHENTICATED to IIP,
// so it must bypass the /api/v1 authenticated-request guard (like /health).
app.MapGet("/onboarding/consent-callback", InsuranceIntelligence.Api.Onboarding.OnboardingEndpoints.ConsentCallback);
app.MapGet("/api/v1/audit/events", AuditEndpoints.ListEvents)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAuditLog));
// Initiative LP: rule-based fraud advisory signals (deterministic, tenant-scoped, advisory-only).
app.MapGet("/api/v1/fraud/signals", InsuranceIntelligence.Api.Fraud.FraudEndpoints.GetSignals)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewFraudSignals));
// WO-19: fraud signal disposition, triage, assign for investigation, record outcome, close.
app.MapPost("/api/v1/fraud/signals/triage", InsuranceIntelligence.Api.Fraud.FraudDispositionEndpoints.Triage)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewFraudSignals));
app.MapPost("/api/v1/fraud/signals/{signalId:guid}/assign", InsuranceIntelligence.Api.Fraud.FraudDispositionEndpoints.Assign)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewFraudSignals));
app.MapPost("/api/v1/fraud/signals/{signalId:guid}/outcome", InsuranceIntelligence.Api.Fraud.FraudDispositionEndpoints.RecordOutcome)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewFraudSignals));
app.MapPost("/api/v1/fraud/signals/{signalId:guid}/close", InsuranceIntelligence.Api.Fraud.FraudDispositionEndpoints.Close)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewFraudSignals));
// Initiative LP: rule-based underwriting advisory reviews (deterministic, advisory-only).
app.MapGet("/api/v1/underwriting/reviews", InsuranceIntelligence.Api.Underwriting.UnderwritingEndpoints.GetReviews)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewUnderwriting));
// Initiative LP: AI Agent Registry, catalog + tenant-scoped persisted kill switch (ADR-0002).
app.MapGet("/api/v1/admin/agents", InsuranceIntelligence.Api.Admin.Agents.AgentRegistryEndpoints.ListAgents)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageAgents));
app.MapPost("/api/v1/admin/agents/{agentKey}/kill-switch", InsuranceIntelligence.Api.Admin.Agents.AgentRegistryEndpoints.SetKillSwitch)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageAgents));
// Governed agent workflow control plane: immutable Releases 1-5 plus tenant-scoped custom flows.
app.MapGet("/api/v1/agent-workflows", InsuranceIntelligence.Api.AgentWorkflows.AgentWorkflowEndpoints.ListDefinitions)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAgentWorkflows));
app.MapGet("/api/v1/agent-workflows/{workflowKey}", InsuranceIntelligence.Api.AgentWorkflows.AgentWorkflowEndpoints.GetDefinition)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAgentWorkflows));
app.MapGet("/api/v1/agent-workflows/{workflowKey}/versions", InsuranceIntelligence.Api.AgentWorkflows.AgentWorkflowEndpoints.ListVersions)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAgentWorkflows));
app.MapGet("/api/v1/agent-workflows/{workflowKey}/versions/{version:int}", InsuranceIntelligence.Api.AgentWorkflows.AgentWorkflowEndpoints.GetVersion)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAgentWorkflows));
app.MapPost("/api/v1/agent-workflows", InsuranceIntelligence.Api.AgentWorkflows.AgentWorkflowEndpoints.SaveDraft)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageAgentWorkflows));
app.MapPost("/api/v1/agent-workflows/{workflowKey}/draft-new-version", InsuranceIntelligence.Api.AgentWorkflows.AgentWorkflowEndpoints.DraftNewVersion)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageAgentWorkflows));
app.MapPost("/api/v1/agent-workflows/{workflowKey}/publish", InsuranceIntelligence.Api.AgentWorkflows.AgentWorkflowEndpoints.Publish)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageAgentWorkflows));
app.MapPost("/api/v1/agent-workflows/{workflowKey}/runs", InsuranceIntelligence.Api.AgentWorkflows.AgentWorkflowEndpoints.StartRun)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.RunAgentWorkflows));
app.MapGet("/api/v1/agent-workflow-runs", InsuranceIntelligence.Api.AgentWorkflows.AgentWorkflowEndpoints.ListRuns)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAgentWorkflows));
app.MapGet("/api/v1/agent-workflow-runs/{runId:guid}", InsuranceIntelligence.Api.AgentWorkflows.AgentWorkflowEndpoints.GetRun)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAgentWorkflows));
app.MapPost("/api/v1/agent-workflow-runs/{runId:guid}/nodes/{nodeKey}/complete", InsuranceIntelligence.Api.AgentWorkflows.AgentWorkflowEndpoints.CompleteNode)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageAgentWorkflows));
app.MapGet("/api/v1/agent-work-items", InsuranceIntelligence.Api.AgentWorkflows.AgentWorkflowEndpoints.ListWorkItems)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ReviewAgentWorkItems));
app.MapGet("/api/v1/agent-work-items/{workItemId:guid}", InsuranceIntelligence.Api.AgentWorkflows.AgentWorkflowEndpoints.GetWorkItem)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ReviewAgentWorkItems));
app.MapPost("/api/v1/agent-work-items/{workItemId:guid}/decision", InsuranceIntelligence.Api.AgentWorkflows.AgentWorkflowEndpoints.DecideWorkItem)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ReviewAgentWorkItems));
// Initiative LP: Billing & entitlements, default package + real usage meters (ADR-0003).
app.MapGet("/api/v1/admin/billing", InsuranceIntelligence.Api.Admin.Billing.BillingEndpoints.GetSummary)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewBilling));
// Initiative LP: cross-case document library.
app.MapGet("/api/v1/documents", InsuranceIntelligence.Api.Documents.DocumentLibraryEndpoints.ListDocuments)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAllClaims));

app.MapGet("/api/v1/compliance/audit-events", ComplianceEndpoints.ListAuditEvents)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAuditLog));
app.MapGet("/api/v1/compliance/posture", ComplianceEndpoints.GetCompliancePosture)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAuditLog));
app.MapGet("/api/v1/compliance/cases/{caseId:guid}/audit-pack", ComplianceEndpoints.GetAuditPack)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAuditLog));
app.MapPost("/api/v1/compliance/cases/{caseId:guid}/audit-pack/export", ComplianceEndpoints.ExportAuditPack)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ExportEvidencePack));
app.MapPost("/api/v1/compliance/cases/{caseId:guid}/claims-pack/send", InsuranceIntelligence.Api.Compliance.ClaimsPackEmailEndpoints.SendNow)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ExportEvidencePack));
app.MapGet("/api/v1/compliance/cases/{caseId:guid}/claims-pack/history", InsuranceIntelligence.Api.Compliance.ClaimsPackEmailEndpoints.History)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ExportEvidencePack));
app.MapGet("/api/v1/admin/claims-pack-delivery", InsuranceIntelligence.Api.Compliance.ClaimsPackEmailEndpoints.GetSettings)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageTenantSettings));
app.MapPut("/api/v1/admin/claims-pack-delivery", InsuranceIntelligence.Api.Compliance.ClaimsPackEmailEndpoints.PutSettings)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageTenantSettings));
// Public: the secure link in an email for a pack too large to attach. The long random token is the whole proof.
app.MapGet("/api/v1/shared/claims-packs/{token}", InsuranceIntelligence.Api.Compliance.ClaimsPackEmailEndpoints.DownloadShared);
app.MapGet("/api/v1/compliance/cases/{caseId:guid}/claims-pack", InsuranceIntelligence.Api.Compliance.ClaimsPackEndpoints.Download)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ExportEvidencePack));
app.MapGet("/api/v1/compliance/cases/{caseId:guid}/audit-pack/export/download", ComplianceEndpoints.DownloadAuditPackExport)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ExportEvidencePack));
app.MapGet("/api/v1/compliance/retention/policies", InsuranceIntelligence.Api.Compliance.RetentionEndpoints.ListPolicies)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageTenantSettings));
app.MapPost("/api/v1/compliance/retention/policies", InsuranceIntelligence.Api.Compliance.RetentionEndpoints.SavePolicy)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageTenantSettings));
app.MapPost("/api/v1/compliance/retention/enforce", InsuranceIntelligence.Api.Compliance.RetentionEndpoints.EnforceRetention)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageTenantSettings));
// Follow-through (outcome G): missing-document reminders and pre-breach SLA warnings, the same
// service the scheduled CaseFollowThroughTimerFunction calls.
app.MapPost("/api/v1/cases/follow-through/run", InsuranceIntelligence.Api.Cases.CaseFollowThroughEndpoints.Run)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageTenantSettings));
app.MapGet("/api/v1/compliance/data-subjects/{customerId}/export", InsuranceIntelligence.Api.Compliance.DataSubjectRightsEndpoints.Export)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageTenantSettings));
app.MapPatch("/api/v1/compliance/data-subjects/{customerId}", InsuranceIntelligence.Api.Compliance.DataSubjectRightsEndpoints.Rectify)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageTenantSettings));
app.MapPost("/api/v1/compliance/data-subjects/{customerId}/erasure", InsuranceIntelligence.Api.Compliance.DataSubjectRightsEndpoints.RequestErasure)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageTenantSettings));
app.MapGet("/api/v1/integrations/email/health", EmailIntegrationEndpoints.GetHealth)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ConfigureIntegrations));
app.MapGet("/api/v1/integrations/email/mailboxes", EmailIntegrationEndpoints.ListMailboxes)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ConfigureIntegrations));
app.MapPost("/api/v1/integrations/email/mailboxes", EmailIntegrationEndpoints.CreateMailbox)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ConfigureIntegrations));
app.MapPut("/api/v1/integrations/email/mailboxes/{mailboxId:guid}", EmailIntegrationEndpoints.UpdateMailbox)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ConfigureIntegrations));
// Available in every runtime, unlike test-ingestion below: an insurer who connects a mailbox in
// Staging or Production needs a way to collect mail on demand and see that the connection works.
// The MailboxIngestion timer function does this on a schedule; this is the same service, on demand.
app.MapPost("/api/v1/integrations/email/ingest", EmailIntegrationEndpoints.Ingest)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ConfigureIntegrations));
if (developmentDiagnosticsEnabled)
{
    app.MapPost("/api/v1/integrations/email/test-ingestion", EmailIntegrationEndpoints.TestIngestion)
        .AddEndpointFilter(new PermissionEndpointFilter(Permission.ConfigureIntegrations));
}
app.MapPost("/api/v1/integrations/email/process-message", EmailIntegrationEndpoints.ProcessMessage)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ConfigureIntegrations));
app.MapGet("/api/v1/integrations/email/messages", EmailIntegrationEndpoints.ListMessages)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ConfigureIntegrations));
app.MapGet("/api/v1/integrations/email/messages/{messageId:guid}", EmailIntegrationEndpoints.GetMessage)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ConfigureIntegrations));
app.MapPost("/api/v1/integrations/email/graph/webhook", EmailIntegrationEndpoints.GraphWebhook);
app.MapGet("/api/v1/integrations/whatsapp/health", WhatsAppIntegrationEndpoints.GetHealth)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ConfigureIntegrations));
app.MapGet("/api/v1/integrations/whatsapp/configuration", WhatsAppConfigurationEndpoints.GetStatus)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ConfigureIntegrations));
app.MapPost("/api/v1/integrations/whatsapp/configuration", WhatsAppConfigurationEndpoints.SaveConfiguration)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ConfigureIntegrations));
app.MapPost("/api/v1/integrations/whatsapp/configuration/test", WhatsAppConfigurationEndpoints.TestConfiguration)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ConfigureIntegrations));
app.MapDelete("/api/v1/integrations/whatsapp/configuration", WhatsAppConfigurationEndpoints.DeleteConfiguration)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ConfigureIntegrations));
app.MapGet("/api/v1/integrations/whatsapp/webhook", WhatsAppIntegrationEndpoints.VerifyWebhook);
app.MapPost("/api/v1/integrations/whatsapp/webhook", WhatsAppIntegrationEndpoints.ReceiveWebhook);

// Generic connection settings surface (blueprint §3.4), the /app/admin/connections screen's
// backend. {connectionType} is "sms", "email" or "translation"; adding a type is a new
// ConnectionType enum value plus IConnectionValidator implementations, no new routes.
app.MapPost("/api/v1/integrations/connections/translation/try", InsuranceIntelligence.Api.Agents.TranslationStatusEndpoints.TryConnection)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ConfigureIntegrations));
app.MapGet("/api/v1/integrations/connections/{connectionType}/providers", ConnectionEndpoints.ListProviders)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ConfigureIntegrations));
app.MapGet("/api/v1/integrations/connections/{connectionType}", ConnectionEndpoints.GetStatus)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ConfigureIntegrations));
app.MapPost("/api/v1/integrations/connections/{connectionType}", ConnectionEndpoints.SaveConfiguration)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ConfigureIntegrations));
app.MapPost("/api/v1/integrations/connections/{connectionType}/test", ConnectionEndpoints.TestConfiguration)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ConfigureIntegrations));
app.MapDelete("/api/v1/integrations/connections/{connectionType}", ConnectionEndpoints.DeleteConfiguration)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ConfigureIntegrations));

// Agent behaviour configuration (blueprint §6.3), a tenant's voice/scope customization per
// agent, never its authority. Every save is a new version; nothing is overwritten in place.
app.MapGet("/api/v1/agents/{agentKey}/behaviour", InsuranceIntelligence.Api.Agents.AgentBehaviourEndpoints.GetCurrent)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageAgents));
app.MapGet("/api/v1/agents/{agentKey}/behaviour/versions", InsuranceIntelligence.Api.Agents.AgentBehaviourEndpoints.ListVersions)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageAgents));
app.MapPost("/api/v1/agents/{agentKey}/behaviour", InsuranceIntelligence.Api.Agents.AgentBehaviourEndpoints.SaveVersion)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageAgents));
app.MapPost("/api/v1/agents/{agentKey}/behaviour/test", InsuranceIntelligence.Api.Agents.AgentBehaviourEndpoints.TestBehaviour)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageAgents));

// Whether the customer assistant can really answer in Luganda and Kiswahili right now.
app.MapGet("/api/v1/agents/customer-assistant/translation", InsuranceIntelligence.Api.Agents.TranslationStatusEndpoints.Get)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageAgents));
app.MapPost("/api/v1/agents/customer-assistant/translation/test", InsuranceIntelligence.Api.Agents.TranslationStatusEndpoints.Test)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageAgents));

// Tenant agent policy (blueprint §6.4, WO 3.4), real enforcement-source data, not a hardcoded array.
app.MapGet("/api/v1/admin/tenant-policy", InsuranceIntelligence.Api.Administration.TenantPolicyEndpoints.GetPolicy)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageTenantSettings));

// SMS delivery-status webhook (blueprint §5.4), unauthenticated by header (the gateway's payload
// carries no signature), gated by the unguessable path secret instead. No PermissionEndpointFilter:
// the caller is an external SMS gateway, not an authenticated platform user.
app.MapPost("/api/v1/integrations/sms/status/{pathSecret}", SmsIntegrationEndpoints.ReceiveDeliveryStatus);
app.MapGet("/api/v1/channels/whatsapp/threads/{threadId}", WhatsAppIntegrationEndpoints.GetThread)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewChannels));
app.MapGet("/api/v1/channels/whatsapp/threads/{threadId}/control", ConversationControlEndpoints.Get)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageCustomerCommunications, Permission.ViewSalesLeads, Permission.ViewAssignedClaims, Permission.ViewAllClaims));
app.MapPut("/api/v1/channels/whatsapp/threads/{threadId}/control", ConversationControlEndpoints.Set)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageCustomerCommunications));
app.MapGet("/api/v1/channels/whatsapp/threads/{threadId}/prospect", SalesLeadEndpoints.GetByThread)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewSalesLeads));
app.MapPost("/api/v1/admin/sales-leads/merge-duplicates", SalesLeadEndpoints.MergeDuplicates)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageSalesLeads));
app.MapPost("/api/v1/channels/whatsapp/threads/{threadId}/messages", WhatsAppIntegrationEndpoints.SendMessage)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.SendCustomerUpdate));
app.MapPost("/api/v1/communications/send-email", CommunicationEndpoints.SendEmail)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.SendCustomerUpdate));
app.MapPost("/api/v1/communications/send-sms", CommunicationEndpoints.SendSms)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.SendCustomerUpdate));
app.MapPost("/api/v1/communications/send-teams-notification", CommunicationEndpoints.SendTeamsNotification)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ConfigureIntegrations));
app.MapGet("/api/v1/communications/history", CommunicationEndpoints.ListHistory)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAssignedClaims, Permission.ViewAllClaims));
app.MapGet("/api/v1/cases/{caseId:guid}/communications", CommunicationEndpoints.ListForCase)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAssignedClaims, Permission.ViewAllClaims));
app.MapCustomerEndpoints();
app.MapPolicyEndpoints();
app.MapDistributionEndpoints();

app.Run();

static string RequireSetting(string? value, string settingName)
{
    if (string.IsNullOrWhiteSpace(value))
    {
        throw new InvalidOperationException($"{settingName} must be configured when INSURANCEINTELLIGENCE_AUTH_MODE=Entra.");
    }

    return value;
}

public partial class Program;
