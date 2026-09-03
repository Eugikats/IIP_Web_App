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
using Microsoft.Extensions.Logging.ApplicationInsights;
using Microsoft.Identity.Web;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddApplicationInsightsTelemetry();
builder.Logging.AddFilter<ApplicationInsightsLoggerProvider>("InsuranceIntelligence", LogLevel.Information);

// Gate 1 (G1.6): load deployment secrets from Azure Key Vault when configured. The vault URI is a
// non-secret pointer (Azure:KeyVaultUri / Azure__KeyVaultUri); secrets are read via managed identity
// (DefaultAzureCredential) — no secrets in config files or env. Only activates when a URI is set, so
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
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(swagger =>
{
    swagger.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "IIP Operations API",
        Version = "v1",
        Description = "Insurance Intelligence Platform (IIP) — canonical /api/v1 contract. "
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
// is not explicitly a local profile — including an unrecognised value — is refused. This is
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
    builder.Services.AddSingleton<IEvidencePackStorageProvider, NotConfiguredEvidencePackStorageProvider>();
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
else
{
    builder.Services.AddSingleton<IDocumentProcessingProvider, NotConfiguredDocumentProcessingProvider>();
}

builder.Services.AddSingleton<ICompletenessScoringService, CompletenessScoringService>();
builder.Services.AddSingleton<IDocumentChecklistProvider, StaticDocumentChecklistProvider>();
builder.Services.AddSingleton<ISourceCitationFormatter, SourceCitationFormatter>();
if (infrastructureOptions.RetrievalProvider.Equals("AzureAiSearch", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddSingleton<IKnowledgeIndexProvider, AzureAiSearchKnowledgeIndexProvider>();
    builder.Services.AddSingleton<IRetrievalProvider, AzureAiSearchRetrievalProvider>();
}
else
{
    builder.Services.AddSingleton<IKnowledgeIndexProvider, NotConfiguredKnowledgeIndexProvider>();
    builder.Services.AddSingleton<IRetrievalProvider, NotConfiguredRetrievalProvider>();
}

if (infrastructureOptions.AiProvider.Equals("AzureOpenAI", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddSingleton<IAiAssistantProvider, AzureOpenAiAssistantProvider>();
    builder.Services.AddSingleton<IEmbeddingProvider, AzureOpenAiEmbeddingProvider>();
    builder.Services.AddSingleton<IChatCompletionProvider, AzureOpenAiChatCompletionProvider>();
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
    builder.Services.AddSingleton<ICustomerUpdateDraftingProvider, AzureOpenAiCustomerUpdateDraftingProvider>();
}
else
{
    builder.Services.AddSingleton<ICustomerUpdateDraftingProvider, NotConfiguredCustomerUpdateDraftingProvider>();
}
if (infrastructureOptions.CommunicationProvider.Equals("GraphEmail", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddSingleton<ICommunicationProvider, ConfiguredCustomerUpdateCommunicationProvider>();
}
else
{
    builder.Services.AddSingleton<ICommunicationProvider, NotConfiguredCommunicationProvider>();
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
builder.Services.AddScoped<ISalesLeadQueryService, SalesLeadQueryService>();
builder.Services.AddScoped<ISalesLeadCommandService, SalesLeadCommandService>();
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
builder.Services.AddScoped<InsuranceIntelligence.Application.AgentWorkflows.IAgentWorkflowService, InsuranceIntelligence.Application.AgentWorkflows.AgentWorkflowService>();
builder.Services.AddScoped<InsuranceIntelligence.Application.MyWork.IMyWorkService, InsuranceIntelligence.Application.MyWork.MyWorkService>();
builder.Services.AddScoped<InsuranceIntelligence.Application.WhatsApp.IWhatsAppMessagingService, InsuranceIntelligence.Application.WhatsApp.WhatsAppMessagingService>();
builder.Services.AddScoped<InsuranceIntelligence.Application.Billing.IBillingQueryService, InsuranceIntelligence.Application.Billing.BillingQueryService>();
builder.Services.AddScoped<InsuranceIntelligence.Application.Billing.IBillingUsageSource, InsuranceIntelligence.Api.Admin.Billing.RecordBackedBillingUsageSource>();
builder.Services.AddScoped<InsuranceIntelligence.Application.Documents.IDocumentQueryService, InsuranceIntelligence.Application.Documents.DocumentQueryService>();
builder.Services.AddScoped<ITenantContext, HttpContextTenantContext>();
builder.Services.AddScoped<InsuranceIntelligence.Api.CommandCenter.ISystemCapabilityStatusService, InsuranceIntelligence.Api.CommandCenter.SystemCapabilityStatusService>();
builder.Services.AddSingleton<InsuranceIntelligence.Domain.Workflow.ICaseTimelineProvider, InsuranceIntelligence.Api.Local.CaseTimelineProvider>();

builder.Configuration["INSURANCEINTELLIGENCE_NOTIFICATION_PROVIDER"] ??= "Mock";
builder.Configuration["INSURANCEINTELLIGENCE_COMMUNICATION_PROVIDER"] ??= "Mock";
builder.Configuration["INSURANCEINTELLIGENCE_LOCAL_DOCUMENT_MAX_BYTES"] ??= DocumentEndpoints.MaxLocalMockFileSizeBytes.ToString();
builder.Configuration["INSURANCEINTELLIGENCE_LOCAL_KNOWLEDGE_MAX_BYTES"] ??= KnowledgeEndpoints.MaxKnowledgeFileSizeBytes.ToString();

var app = builder.Build();

await app.Services.InitializeInsuranceIntelligenceInfrastructureAsync(app.Environment);

// Offline developer mode: with in-memory storage the database starts empty on every run, so
// seed the demo dataset at boot to give the app something to show without any Azure account.
// Guarded to in-memory storage only — this can never run against SqlServer, and the storage
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
    var seedResult = await seedService.SeedAsync(seedScopeIds, "system:local-seed");
    app.Logger.LogInformation(
        "Offline mode: seeded {Cases} demo claims, {Documents} documents, {Approvals} approvals into in-memory storage.",
        seedResult.Cases, seedResult.Documents, seedResult.Approvals);
}

// Always return a generic RFC 7807 response. Exception details and request headers stay server-side.
app.UseExceptionHandler();

app.UseCors();

// Swagger UI + JSON contract document — Development/LocalTest only (secure baseline).
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
            // Deployed (Entra) — ADR-0005: runtime authority is IIP's membership store, keyed by the
            // authenticated user's stable id (oid). The token tid/workspace claims are ONLY
            // disambiguation hints, never authority. Fine-grained permissions come from the store and
            // are stashed for the synchronous permission checks. No membership => fail closed.
            var userId = DemoAuthorization.GetUserId(context, fallback: string.Empty);
            if (!string.IsNullOrWhiteSpace(userId))
            {
                var tenantHint = RuntimeClaimTypes.TenantScope
                    .Select(t => context.User.FindFirst(t)?.Value)
                    .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
                var workspaceHint = RuntimeClaimTypes.WorkspaceScope
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
app.MapGet("/api/v1/search", SearchEndpoints.Search)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAssignedClaims, Permission.ViewAllClaims));

app.MapGet("/api/v1/client-portal/claims/{caseId:guid}", ClientPortalEndpoints.GetClaim)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewClientPortal));
app.MapPost("/api/v1/client-portal/claims/{caseId:guid}/documents", ClientPortalEndpoints.UploadDocuments)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewClientPortal));
app.MapPost("/api/v1/client-portal/claims/{caseId:guid}/request-review", ClientPortalEndpoints.RequestReview)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewClientPortal));

app.MapPost("/api/v1/cases", CaseEndpoints.CreateCase)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.CreateClaim));
app.MapGet("/api/v1/cases", CaseEndpoints.ListCases)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAssignedClaims, Permission.ViewAllClaims));
app.MapGet("/api/v1/cases/{caseId:guid}", CaseEndpoints.GetCase)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAssignedClaims, Permission.ViewAllClaims));
app.MapPost("/api/v1/cases/{caseId:guid}/assign", CaseEndpoints.AssignCase)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAssignedClaims, Permission.ViewAllClaims));
app.MapPost("/api/v1/cases/{caseId:guid}/escalate", CaseEndpoints.EscalateCase)
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
app.MapPost("/api/v1/cases/{caseId:guid}/documents/{documentId:guid}/reclassify", DocumentEndpoints.ReclassifyDocument)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.UploadDocument));
app.MapPost("/api/v1/cases/{caseId:guid}/documents/{documentId:guid}/approve-extraction", DocumentEndpoints.ApproveExtraction)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.UploadDocument));
app.MapPost("/api/v1/cases/{caseId:guid}/documents/{documentId:guid}/correct-field", DocumentEndpoints.CorrectExtractionField)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.UploadDocument));
app.MapGet("/api/v1/cases/{caseId:guid}/review-requests", ContestabilityEndpoints.ListReviewRequests)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAssignedClaims, Permission.ViewAllClaims));
app.MapPost("/api/v1/cases/{caseId:guid}/review-requests/{requestId:guid}/acknowledge", ContestabilityEndpoints.AcknowledgeReviewRequest)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAssignedClaims, Permission.ViewAllClaims));
app.MapPost("/api/v1/cases/{caseId:guid}/review-requests/{requestId:guid}/resolve", ContestabilityEndpoints.ResolveReviewRequest)
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
app.MapGet("/api/v1/cases/{caseId:guid}/customer-updates", CustomerUpdateEndpoints.ListCustomerUpdates)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAssignedClaims));
app.MapPost("/api/v1/customer-updates/{updateId:guid}/approve", CustomerUpdateEndpoints.ApproveCustomerUpdate)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageCustomerCommunications));
app.MapPost("/api/v1/customer-updates/{updateId:guid}/reject", CustomerUpdateEndpoints.RejectCustomerUpdate)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageCustomerCommunications));
app.MapPost("/api/v1/customer-updates/{updateId:guid}/send", CustomerUpdateEndpoints.SendCustomerUpdate)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.SendCustomerUpdate));
app.MapGet("/api/v1/reports/operations-summary", ReportsEndpoints.GetOperationsSummary)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewCommandCenter));
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
// Demo dataset seeding — hard-gated inside the handler to non-protected runtime profiles
// (see DemoSeedEndpoints), never reachable in Staging/Production/ManagedApplicationReviewer.
app.MapPost("/api/v1/admin/demo-seed", InsuranceIntelligence.Api.Admin.DemoSeedEndpoints.Run);
app.MapPost("/api/v1/admin/db-migrate", InsuranceIntelligence.Api.Admin.DatabaseMigrationEndpoints.ApplyPending);
app.MapPost("/api/v1/admin/db-migrate/reconcile-history", InsuranceIntelligence.Api.Admin.DatabaseMigrationEndpoints.ReconcileHistory);
app.MapPost("/api/v1/admin/db-migrate/force-reconcile-schema", InsuranceIntelligence.Api.Admin.DatabaseMigrationEndpoints.ForceReconcileSchema);
app.MapGet("/api/v1/admin/db-migrate/diagnose", InsuranceIntelligence.Api.Admin.DatabaseMigrationEndpoints.Diagnose);

if (developmentDiagnosticsEnabled)
{
    app.MapPost("/api/v1/admin/debug-teams-notification", DebugEndpoints.TriggerTeamsNotification)
        .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageTenantSettings));
}

// W2.5: Phase 2 core-workspace read routes (see docs/02-development-engineering/iip_api_specification.md).
app.MapGet("/api/v1/channels/overview", ChannelEndpoints.GetOverview)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewChannels));
app.MapGet("/api/v1/customers", CustomerProfileEndpoints.ListCustomers)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewCustomers));
app.MapGet("/api/v1/customers/{customerId}", CustomerProfileEndpoints.GetCustomerProfile)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewCustomers));
app.MapGet("/api/v1/sales-leads", SalesLeadEndpoints.ListLeads)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewSalesLeads));
app.MapGet("/api/v1/sales-leads/{leadId}", SalesLeadEndpoints.GetLeadDetail)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewSalesLeads));
app.MapPost("/api/v1/sales-leads", SalesLeadEndpoints.CreateLead)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageSalesLeads));

// Gate 1 (G1.7): platform-operator tenant onboarding + Entra admin-consent flow (ADR-0005).
app.MapPost("/api/v1/onboarding", InsuranceIntelligence.Api.Onboarding.OnboardingEndpoints.Create)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageTenantOnboarding));
app.MapGet("/api/v1/onboarding", InsuranceIntelligence.Api.Onboarding.OnboardingEndpoints.List)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageTenantOnboarding));
app.MapGet("/api/v1/onboarding/{onboardingId}", InsuranceIntelligence.Api.Onboarding.OnboardingEndpoints.Get)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageTenantOnboarding));
app.MapGet("/api/v1/onboarding/{onboardingId}/consent-url", InsuranceIntelligence.Api.Onboarding.OnboardingEndpoints.GetConsentUrl)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageTenantOnboarding));
// Admin-consent redirect target — authorized by the opaque state value, not a permission.
// Deliberately OFF /api/v1: the insurer admin returns here from Microsoft UNAUTHENTICATED to IIP,
// so it must bypass the /api/v1 authenticated-request guard (like /health).
app.MapGet("/onboarding/consent-callback", InsuranceIntelligence.Api.Onboarding.OnboardingEndpoints.ConsentCallback);
app.MapGet("/api/v1/audit/events", AuditEndpoints.ListEvents)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAuditLog));
// Initiative LP: rule-based fraud advisory signals (deterministic, tenant-scoped, advisory-only).
app.MapGet("/api/v1/fraud/signals", InsuranceIntelligence.Api.Fraud.FraudEndpoints.GetSignals)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewFraudSignals));
// Initiative LP: rule-based underwriting advisory reviews (deterministic, advisory-only).
app.MapGet("/api/v1/underwriting/reviews", InsuranceIntelligence.Api.Underwriting.UnderwritingEndpoints.GetReviews)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewUnderwriting));
// Initiative LP: AI Agent Registry — catalog + tenant-scoped persisted kill switch (ADR-0002).
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
// Initiative LP: Billing & entitlements — default package + real usage meters (ADR-0003).
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
app.MapGet("/api/v1/integrations/email/health", EmailIntegrationEndpoints.GetHealth)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ConfigureIntegrations));
app.MapGet("/api/v1/integrations/email/mailboxes", EmailIntegrationEndpoints.ListMailboxes)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ConfigureIntegrations));
app.MapPost("/api/v1/integrations/email/mailboxes", EmailIntegrationEndpoints.CreateMailbox)
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
app.MapGet("/api/v1/integrations/whatsapp/webhook", WhatsAppIntegrationEndpoints.VerifyWebhook);
app.MapPost("/api/v1/integrations/whatsapp/webhook", WhatsAppIntegrationEndpoints.ReceiveWebhook);
app.MapGet("/api/v1/channels/whatsapp/threads/{threadId}", WhatsAppIntegrationEndpoints.GetThread)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewChannels));
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
