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
using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Application.Customers;
using InsuranceIntelligence.Application.SalesLeads;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Api.CommandCenter;
using InsuranceIntelligence.Api.Communications;
using InsuranceIntelligence.Api.Compliance;
using InsuranceIntelligence.Api.Domain;
using InsuranceIntelligence.Api.Documents;
using InsuranceIntelligence.Api.Health;
using InsuranceIntelligence.Api.Integrations;
using InsuranceIntelligence.Api.Knowledge;
using InsuranceIntelligence.Api.Local;
using InsuranceIntelligence.Api.Reports;
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
using Microsoft.Identity.Web;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

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
AzureProviderStartupValidator.ThrowIfInvalid(infrastructureOptions);
AzureProviderStartupValidator.ThrowIfProductionInvalid(
    infrastructureOptions,
    AzureProviderStartupValidator.ResolveRuntimeProfile(infrastructureOptions, builder.Environment.EnvironmentName));

builder.Services.AddAuthorization();
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
                jwt.TokenValidationParameters.ValidAudience = audience;
                jwt.TokenValidationParameters.ValidateLifetime = true;
                jwt.TokenValidationParameters.ValidateIssuerSigningKey = true;
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

var storageProvider = infrastructureOptions.StorageProvider;
if (storageProvider.Equals("InMemory", StringComparison.OrdinalIgnoreCase))
{
    throw new InvalidOperationException(
        "InsuranceIntelligence.Api cannot run with INSURANCEINTELLIGENCE_STORAGE_PROVIDER=InMemory. " +
        "Use InsuranceIntelligence.Api.LocalTestHost or test projects for LocalTest execution.");
}

builder.Services.AddInsuranceIntelligenceInfrastructure(builder.Configuration, builder.Environment);

if (infrastructureOptions.BlobProvider.Equals("AzureBlob", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddSingleton<IDocumentStorageProvider, AzureBlobDocumentStorageProvider>();
    builder.Services.AddSingleton<IKnowledgeStorageProvider, AzureBlobKnowledgeStorageProvider>();
    builder.Services.AddSingleton<IEvidencePackStorageProvider, AzureBlobEvidencePackStorageProvider>();
}
else
{
    builder.Services.AddSingleton<IDocumentStorageProvider, LocalMockDocumentStorageProvider>();
    builder.Services.AddSingleton<IKnowledgeStorageProvider, LocalMockKnowledgeStorageProvider>();
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
else
{
    builder.Services.AddSingleton<IMessageQueueDispatcher, MockMessageQueueDispatcher>();
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
if (infrastructureOptions.CommunicationProvider.Equals("Mock", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddSingleton<ICommunicationProvider, NotConfiguredCommunicationProvider>();
}
else
{
    builder.Services.AddSingleton<ICommunicationProvider, ConfiguredCustomerUpdateCommunicationProvider>();
}
builder.Services.AddSingleton<ISlaCalculator, BasicSlaCalculator>();

if (infrastructureOptions.NotificationProvider.Equals("GraphTeams", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddSingleton<ITeamsNotificationProvider, GraphTeamsNotificationProvider>();
}
else if (infrastructureOptions.NotificationProvider.Equals("IncomingWebhook", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.Configure<IncomingWebhookTeamsProviderOptions>(builder.Configuration.GetSection("TeamsWebhook"));
    builder.Services.AddHttpClient<ITeamsNotificationProvider, IncomingWebhookTeamsProvider>();
}
else
{
    builder.Services.AddSingleton<ITeamsNotificationProvider, MockTeamsNotificationProvider>();
}

builder.Services.AddSingleton<ITeamsNotificationQueue, DefaultTeamsNotificationQueue>();
builder.Services.AddSingleton<CaseIntakeService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<TenantContextAccessor>();
builder.Services.AddScoped<ITenantContextAccessor>(serviceProvider => serviceProvider.GetRequiredService<TenantContextAccessor>());
builder.Services.AddScoped<ITenantContextSetter>(serviceProvider => serviceProvider.GetRequiredService<TenantContextAccessor>());
builder.Services.AddScoped<IPermissionService, PermissionService>();
builder.Services.AddScoped<IAuditWriter, AuditWriter>();
builder.Services.AddScoped<IClaimsQueryService, ClaimsQueryService>();
builder.Services.AddSingleton<IAiOutputValidator, DeterministicAiOutputValidator>();
builder.Services.AddSingleton<IIntentClassifier, DeterministicThenAiIntentClassifier>();
builder.Services.AddSingleton<IProviderRequirementResolver, ProviderRequirementResolver>();
builder.Services.AddScoped<INotificationDraftService, CustomerUpdateNotificationDraftService>();
builder.Services.AddScoped<ICommandCenterQueryService, CommandCenterQueryService>();
// W2.5: Phase 2 core-workspace query services.
builder.Services.AddScoped<IChannelQueryService, ChannelQueryService>();
builder.Services.AddScoped<ICustomerQueryService, CustomerQueryService>();
builder.Services.AddScoped<ISalesLeadQueryService, SalesLeadQueryService>();
builder.Services.AddScoped<IAuditQueryService, AuditQueryService>();
// Initiative LP: rule-based advisory domains + admin domains (Fraud/Underwriting/Agents/Billing).
builder.Services.AddScoped<InsuranceIntelligence.Application.Fraud.IFraudQueryService, InsuranceIntelligence.Application.Fraud.FraudQueryService>();
builder.Services.AddScoped<InsuranceIntelligence.Application.Underwriting.IUnderwritingQueryService, InsuranceIntelligence.Application.Underwriting.UnderwritingQueryService>();
builder.Services.AddScoped<InsuranceIntelligence.Application.Underwriting.IUnderwritingSubmissionSource, InsuranceIntelligence.Api.Underwriting.NotConfiguredUnderwritingSubmissionSource>();
builder.Services.AddScoped<InsuranceIntelligence.Application.Agents.IAgentRegistryService, InsuranceIntelligence.Application.Agents.AgentRegistryService>();
builder.Services.AddScoped<InsuranceIntelligence.Application.Billing.IBillingQueryService, InsuranceIntelligence.Application.Billing.BillingQueryService>();
builder.Services.AddScoped<InsuranceIntelligence.Application.Billing.IBillingUsageSource, InsuranceIntelligence.Api.Admin.Billing.RecordBackedBillingUsageSource>();
builder.Services.AddScoped<InsuranceIntelligence.Application.Documents.IDocumentQueryService, InsuranceIntelligence.Application.Documents.DocumentQueryService>();
builder.Services.AddScoped<ITenantContext, HttpContextTenantContext>();
builder.Services.AddScoped<InsuranceIntelligence.Api.CommandCenter.ISystemCapabilityStatusService, InsuranceIntelligence.Api.CommandCenter.SystemCapabilityStatusService>();
builder.Services.AddSingleton<InsuranceIntelligence.Domain.Workflow.ICaseTimelineProvider, InsuranceIntelligence.Api.Local.CaseTimelineProvider>();
// NOTE (v2 migration): the code-only agent framework (IAgentTool/AgentToolRegistry/
// AgentIntentRouter/AssistantOrchestrator/IModuleAgent + per-module agents) was dropped.
// It will be re-derived to the governing docs (AgentToolRegistry + AgentRun audit +
// IIntentClassifier per iip_ai_agent_architecture_tool_registry_updated /
// iip_multi_agent_orchestration / iip_command_center_query_service_updated_logic) in a
// later sub-step. The deterministic /query planner remains functional without it.

builder.Configuration["INSURANCEINTELLIGENCE_NOTIFICATION_PROVIDER"] ??= "Mock";
builder.Configuration["INSURANCEINTELLIGENCE_COMMUNICATION_PROVIDER"] ??= "Mock";
builder.Configuration["INSURANCEINTELLIGENCE_LOCAL_DOCUMENT_MAX_BYTES"] ??= DocumentEndpoints.MaxLocalMockFileSizeBytes.ToString();
builder.Configuration["INSURANCEINTELLIGENCE_LOCAL_KNOWLEDGE_MAX_BYTES"] ??= KnowledgeEndpoints.MaxKnowledgeFileSizeBytes.ToString();

var app = builder.Build();

await app.Services.InitializeInsuranceIntelligenceInfrastructureAsync(app.Environment);

app.UseCors();
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

app.MapGet("/api/v1/client-portal/claims/{caseId:guid}", ClientPortalEndpoints.GetClaim)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewClientPortal));
app.MapPost("/api/v1/client-portal/claims/{caseId:guid}/documents", ClientPortalEndpoints.UploadDocuments)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewClientPortal));

app.MapPost("/api/v1/cases", CaseEndpoints.CreateCase)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.CreateClaim));
app.MapGet("/api/v1/cases", CaseEndpoints.ListCases)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAssignedClaims, Permission.ViewAllClaims));
app.MapGet("/api/v1/cases/{caseId:guid}", CaseEndpoints.GetCase)
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
app.MapPost("/api/v1/cases/{caseId:guid}/customer-updates/draft", CustomerUpdateEndpoints.DraftCustomerUpdate)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageCustomerCommunications));
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

app.MapPost("/api/v1/admin/debug-teams-notification", DebugEndpoints.TriggerTeamsNotification)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageTenantSettings));

// W2.5: Phase 2 core-workspace read routes (see docs/02-development-engineering/iip_api_specification.md).
app.MapGet("/api/v1/channels/overview", ChannelEndpoints.GetOverview)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewChannels));
app.MapGet("/api/v1/customers", CustomerProfileEndpoints.ListCustomers)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewCustomers));
app.MapGet("/api/v1/customers/{customerId}", CustomerProfileEndpoints.GetCustomerProfile)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewCustomers));
app.MapGet("/api/v1/sales-leads/{leadId}", SalesLeadEndpoints.GetLeadDetail)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewSalesLeads));
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
// Initiative LP: Billing & entitlements — default package + real usage meters (ADR-0003).
app.MapGet("/api/v1/admin/billing", InsuranceIntelligence.Api.Admin.Billing.BillingEndpoints.GetSummary)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewBilling));
// Initiative LP: cross-case document library.
app.MapGet("/api/v1/documents", InsuranceIntelligence.Api.Documents.DocumentLibraryEndpoints.ListDocuments)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAllClaims));

app.MapGet("/api/v1/compliance/audit-events", ComplianceEndpoints.ListAuditEvents)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAuditLog));
app.MapGet("/api/v1/compliance/cases/{caseId:guid}/audit-pack", ComplianceEndpoints.GetAuditPack)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAuditLog));
app.MapGet("/api/v1/integrations/email/health", EmailIntegrationEndpoints.GetHealth)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ConfigureIntegrations));
app.MapGet("/api/v1/integrations/email/mailboxes", EmailIntegrationEndpoints.ListMailboxes)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ConfigureIntegrations));
app.MapPost("/api/v1/integrations/email/mailboxes", EmailIntegrationEndpoints.CreateMailbox)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ConfigureIntegrations));
app.MapPost("/api/v1/integrations/email/test-ingestion", EmailIntegrationEndpoints.TestIngestion)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ConfigureIntegrations));
app.MapPost("/api/v1/integrations/email/process-message", EmailIntegrationEndpoints.ProcessMessage)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ConfigureIntegrations));
app.MapGet("/api/v1/integrations/email/messages", EmailIntegrationEndpoints.ListMessages)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ConfigureIntegrations));
app.MapGet("/api/v1/integrations/email/messages/{messageId:guid}", EmailIntegrationEndpoints.GetMessage)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ConfigureIntegrations));
app.MapPost("/api/v1/integrations/email/graph/webhook", EmailIntegrationEndpoints.GraphWebhook);
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
