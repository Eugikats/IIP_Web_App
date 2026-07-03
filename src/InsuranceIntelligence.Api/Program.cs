using InsuranceIntelligence.Api.Auth;
using InsuranceIntelligence.Api.Common;
using InsuranceIntelligence.Api.Azure;
using InsuranceIntelligence.Api.Admin;
using InsuranceIntelligence.Api.Assistant;
using InsuranceIntelligence.Api.Cases;
using InsuranceIntelligence.Api.Claims;
using InsuranceIntelligence.Api.ClientPortal;
using InsuranceIntelligence.Application.Claims;
using InsuranceIntelligence.Application.Core;
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
using InsuranceIntelligence.LocalTestDoubles;

using Microsoft.AspNetCore.Authentication.JwtBearer;
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
    var tenantId = RequireSetting(infrastructureOptions.EntraTenantId, "INSURANCEINTELLIGENCE_ENTRA_TENANT_ID");
    var clientId = RequireSetting(infrastructureOptions.EntraClientId, "INSURANCEINTELLIGENCE_ENTRA_CLIENT_ID");
    var configuredAudience = string.IsNullOrWhiteSpace(infrastructureOptions.EntraAudience)
        ? $"api://{clientId}"
        : infrastructureOptions.EntraAudience;
    var authority = $"https://login.microsoftonline.com/{tenantId}/v2.0";
    var validAudiences = new[] { configuredAudience, clientId, $"api://{clientId}" }
        .Where(value => !string.IsNullOrWhiteSpace(value))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.Authority = authority;
            options.RequireHttpsMetadata = true;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = infrastructureOptions.EntraIssuer ?? authority,
                ValidateAudience = true,
                ValidAudiences = validAudiences,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                NameClaimType = "preferred_username",
                RoleClaimType = "roles"
            };
        });
}
else
{
    builder.Services.AddAuthentication();
}

var storageProvider = infrastructureOptions.StorageProvider;
builder.Services.AddInsuranceIntelligenceInfrastructure(builder.Configuration, builder.Environment);
if (storageProvider.Equals("InMemory", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddSingleton<ICaseRepository, InMemoryCaseRepository>();
    builder.Services.AddSingleton<IAuditRepository, InMemoryAuditRepository>();
    builder.Services.AddSingleton<ICaseDocumentRepository, InMemoryCaseDocumentRepository>();
    builder.Services.AddSingleton<IKnowledgeDocumentRepository, InMemoryKnowledgeDocumentRepository>();
    builder.Services.AddSingleton<IAgentActionLogRepository, InMemoryAgentActionLogRepository>();
    builder.Services.AddSingleton<IWorkflowTaskRepository, InMemoryWorkflowTaskRepository>();
    builder.Services.AddSingleton<IApprovalRepository, InMemoryApprovalRepository>();
    builder.Services.AddSingleton<ICustomerUpdateRepository, InMemoryCustomerUpdateRepository>();
}

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
    builder.Services.AddSingleton<IDocumentProcessingProvider, MockDocumentProcessingProvider>();
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
    builder.Services.AddSingleton<IKnowledgeIndexProvider, InMemoryKnowledgeIndexProvider>();
    builder.Services.AddSingleton<IRetrievalProvider, MockRetrievalProvider>();
}

if (infrastructureOptions.AiProvider.Equals("AzureOpenAI", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddSingleton<IAiAssistantProvider, AzureOpenAiAssistantProvider>();
    builder.Services.AddSingleton<IEmbeddingProvider, AzureOpenAiEmbeddingProvider>();
}
else
{
    builder.Services.AddSingleton<IAiAssistantProvider, MockAiAssistantProvider>();
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
    builder.Services.AddSingleton<ICustomerUpdateDraftingProvider, MockCustomerUpdateDraftingProvider>();
}
if (infrastructureOptions.CommunicationProvider.Equals("Mock", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddSingleton<ICommunicationProvider, MockCommunicationProvider>();
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
if (storageProvider.Equals("InMemory", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddSingleton<IPermissionReadRepository, LocalTestPermissionReadRepository>();
    builder.Services.AddSingleton<IAuditEventRepository, LocalTestAuditEventRepository>();
    builder.Services.AddScoped<IClaimsReadRepository, LocalTestClaimsReadRepository>();
    builder.Services.AddSingleton<IProviderHealthCheck, LocalTestProviderHealthCheck>();
}
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

if (!storageProvider.Equals("InMemory", StringComparison.OrdinalIgnoreCase))
{
    await app.Services.InitializeInsuranceIntelligenceInfrastructureAsync(app.Environment);
}
else
{
    // LocalTest/InMemory ONLY: seed a believable motor-claim spread so the Command
    // Center demo renders every block type with real-looking data. This branch is
    // unreachable for any Azure/real provider (guarded by storageProvider above), so
    // no simulated data can ever reach a deployed staging/production/reviewer runtime.
#pragma warning disable CS0618 // DemoDataSeeder is intentionally obsolete; LocalTest-only.
    await InsuranceIntelligence.Infrastructure.Simulated.DemoDataSeeder.SeedAsync(app.Services);
#pragma warning restore CS0618
}

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

app.MapGet("/api/client-portal/claims/{caseId:guid}", ClientPortalEndpoints.GetClaim)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewClientPortal));
app.MapPost("/api/client-portal/claims/{caseId:guid}/documents", ClientPortalEndpoints.UploadDocuments)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewClientPortal));

app.MapPost("/api/cases", CaseEndpoints.CreateCase)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.CreateClaim));
app.MapGet("/api/cases", CaseEndpoints.ListCases)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAssignedClaims, Permission.ViewAllClaims));
app.MapGet("/api/cases/{caseId:guid}", CaseEndpoints.GetCase)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAssignedClaims, Permission.ViewAllClaims));
app.MapGet("/api/claims", ClaimsController.ListClaims)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAssignedClaims, Permission.ViewAllClaims));
app.MapGet("/api/claims/attention", ClaimsController.GetAttention)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAssignedClaims, Permission.ViewAllClaims));
app.MapGet("/api/claims/daily-summary", ClaimsController.GetDailySummary)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewCommandCenter));
app.MapGet("/api/claims/{claimId:guid}", ClaimsController.GetClaim)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAssignedClaims, Permission.ViewAllClaims));
app.MapGet("/api/claims/{claimId:guid}/draft-context", ClaimsController.GetDraftContext)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAiOutputs));
app.MapPost("/api/cases/{caseId:guid}/documents", DocumentEndpoints.UploadDocuments)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.UploadDocument));
app.MapGet("/api/cases/{caseId:guid}/documents", DocumentEndpoints.ListDocuments)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAssignedClaims, Permission.ViewAllClaims));
app.MapGet("/api/cases/{caseId:guid}/document-checklist", DocumentEndpoints.GetChecklist)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAssignedClaims, Permission.ViewAllClaims));
app.MapGet("/api/cases/{caseId:guid}/documents/{documentId:guid}/content", DocumentEndpoints.GetDocumentContent)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAssignedClaims, Permission.ViewAllClaims));
app.MapPost("/api/cases/{caseId:guid}/documents/{documentId:guid}/reclassify", DocumentEndpoints.ReclassifyDocument)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.UploadDocument));
app.MapPost("/api/cases/{caseId:guid}/documents/{documentId:guid}/approve-extraction", DocumentEndpoints.ApproveExtraction)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.UploadDocument));
app.MapPost("/api/cases/{caseId:guid}/documents/{documentId:guid}/correct-field", DocumentEndpoints.CorrectExtractionField)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.UploadDocument));
app.MapPost("/api/knowledge-documents", KnowledgeEndpoints.UploadKnowledgeDocument)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageKnowledgeBase));
app.MapGet("/api/knowledge-documents", KnowledgeEndpoints.ListKnowledgeDocuments)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageKnowledgeBase));
app.MapGet("/api/knowledge-documents/{knowledgeDocId:guid}", KnowledgeEndpoints.GetKnowledgeDocument)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageKnowledgeBase));
app.MapPost("/api/knowledge-documents/{knowledgeDocId:guid}/approve", KnowledgeEndpoints.ApproveKnowledgeDocument)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageKnowledgeBase));
app.MapPost("/api/knowledge-documents/{knowledgeDocId:guid}/archive", KnowledgeEndpoints.ArchiveKnowledgeDocument)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageKnowledgeBase));
app.MapPost("/api/knowledge-documents/reindex", KnowledgeEndpoints.ReindexKnowledgeDocuments)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageKnowledgeBase));
app.MapPost("/api/assistant/policy-questions", AssistantEndpoints.AskPolicyQuestion)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAiOutputs));
app.MapGet("/api/assistant/actions", AssistantEndpoints.ListActions)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAiOutputs));
app.MapGet("/api/assistant/actions/{caseId:guid}", AssistantEndpoints.ListActionsForCase)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAiOutputs));
// The prose POST /command-center/assistant endpoint was dropped (superseded by chat-first /query).
// The /metrics endpoint is retained via the salvaged CommandCenterMetricsLoader (metric_pills).
app.MapGet("/api/v1/command-center/metrics", CommandCenterMetricsLoader.GetMetrics)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewCommandCenter));
app.MapPost("/api/v1/command-center/query", CommandCenterQueryEndpoints.RunQuery)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewCommandCenter));
app.MapGet("/api/provider-status", ProviderStatusEndpoints.GetProviderStatus)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewCommandCenter));
app.MapGet("/api/v1/provider-status", ProviderStatusEndpoints.GetProviderStatus)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewCommandCenter));
app.MapGet("/api/v1/system/capabilities", SystemCapabilityEndpoints.GetCapabilities)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewCommandCenter));
app.MapGet("/api/cases/{caseId:guid}/timeline", WorkflowEndpoints.GetTimeline)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAssignedClaims, Permission.ViewAllClaims));
app.MapGet("/api/cases/{caseId:guid}/recommended-actions", WorkflowEndpoints.GetRecommendedActions)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAssignedClaims, Permission.ViewAllClaims));
app.MapPost("/api/cases/{caseId:guid}/workflow/evaluate", WorkflowEndpoints.EvaluateWorkflow)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ApproveSettlementWorkflowStep));
app.MapGet("/api/workflow/tasks", WorkflowEndpoints.ListTasks)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAssignedClaims, Permission.ViewAllClaims));
app.MapGet("/api/workflow/tasks/{taskId:guid}", WorkflowEndpoints.GetTask)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAssignedClaims, Permission.ViewAllClaims));
app.MapPost("/api/workflow/tasks/{taskId:guid}/complete", WorkflowEndpoints.CompleteTask)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAssignedClaims, Permission.ViewAllClaims));
app.MapPost("/api/workflow/tasks/{taskId:guid}/assign", WorkflowEndpoints.AssignTask)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAssignedClaims, Permission.ViewAllClaims));
app.MapGet("/api/approvals", ApprovalEndpoints.ListApprovals)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ApproveSettlementWorkflowStep));
app.MapGet("/api/approvals/{approvalId:guid}", ApprovalEndpoints.GetApproval)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ApproveSettlementWorkflowStep));
app.MapPost("/api/cases/{caseId:guid}/approvals", ApprovalEndpoints.CreateApproval)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAssignedClaims, Permission.ViewAllClaims));
app.MapPost("/api/approvals/{approvalId:guid}/approve", ApprovalEndpoints.ApproveApproval)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ApproveSettlementWorkflowStep));
app.MapPost("/api/approvals/{approvalId:guid}/reject", ApprovalEndpoints.RejectApproval)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ApproveSettlementWorkflowStep));
app.MapPost("/api/approvals/{approvalId:guid}/override", ApprovalEndpoints.OverrideApproval)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.OverrideAiRecommendation));
app.MapPost("/api/approvals/{approvalId:guid}/escalate", ApprovalEndpoints.EscalateApproval)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ApproveSettlementWorkflowStep));
app.MapPost("/api/cases/{caseId:guid}/customer-updates/draft", CustomerUpdateEndpoints.DraftCustomerUpdate)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageCustomerCommunications));
app.MapGet("/api/cases/{caseId:guid}/customer-updates", CustomerUpdateEndpoints.ListCustomerUpdates)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAssignedClaims));
app.MapPost("/api/customer-updates/{updateId:guid}/approve", CustomerUpdateEndpoints.ApproveCustomerUpdate)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageCustomerCommunications));
app.MapPost("/api/customer-updates/{updateId:guid}/reject", CustomerUpdateEndpoints.RejectCustomerUpdate)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageCustomerCommunications));
app.MapPost("/api/customer-updates/{updateId:guid}/send", CustomerUpdateEndpoints.SendCustomerUpdate)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.SendCustomerUpdate));
app.MapGet("/api/reports/operations-summary", ReportsEndpoints.GetOperationsSummary)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewCommandCenter));
app.MapGet("/api/admin/tenant-configuration", AdminEndpoints.GetTenantConfiguration)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageTenantSettings));
app.MapPut("/api/admin/tenant-configuration", AdminEndpoints.UpdateTenantConfiguration)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageTenantSettings));
app.MapGet("/api/admin/provider-modes", AdminEndpoints.GetProviderModes)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageTenantSettings));
app.MapGet("/api/admin/provider-health", AdminEndpoints.GetProviderHealth)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageTenantSettings));
app.MapGet("/api/admin/roles", AdminEndpoints.GetRoles)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageTenantSettings));
app.MapGet("/api/admin/users", AdminEndpoints.GetUsers)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageTenantSettings));
app.MapPost("/api/admin/users", AdminEndpoints.CreateUser)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageTenantSettings));
app.MapPut("/api/admin/users/{userId}", AdminEndpoints.UpdateUserRole)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageTenantSettings));
app.MapPut("/api/admin/users/{userId}/role", AdminEndpoints.UpdateUserRole)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageTenantSettings));

app.MapPost("/api/admin/debug-teams-notification", DebugEndpoints.TriggerTeamsNotification)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ManageTenantSettings));

app.MapGet("/api/compliance/audit-events", ComplianceEndpoints.ListAuditEvents)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAuditLog));
app.MapGet("/api/compliance/cases/{caseId:guid}/audit-pack", ComplianceEndpoints.GetAuditPack)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAuditLog));
app.MapGet("/api/v1/integrations/email/health", EmailIntegrationEndpoints.GetHealth)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ConfigureIntegrations));
app.MapGet("/api/integrations/email/mailboxes", EmailIntegrationEndpoints.ListMailboxes)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ConfigureIntegrations));
app.MapPost("/api/integrations/email/mailboxes", EmailIntegrationEndpoints.CreateMailbox)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ConfigureIntegrations));
app.MapPost("/api/integrations/email/test-ingestion", EmailIntegrationEndpoints.TestIngestion)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ConfigureIntegrations));
app.MapPost("/api/integrations/email/process-message", EmailIntegrationEndpoints.ProcessMessage)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ConfigureIntegrations));
app.MapGet("/api/integrations/email/messages", EmailIntegrationEndpoints.ListMessages)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ConfigureIntegrations));
app.MapGet("/api/integrations/email/messages/{messageId:guid}", EmailIntegrationEndpoints.GetMessage)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ConfigureIntegrations));
app.MapPost("/api/integrations/email/graph/webhook", EmailIntegrationEndpoints.GraphWebhook);
app.MapPost("/api/communications/send-email", CommunicationEndpoints.SendEmail)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.SendCustomerUpdate));
app.MapPost("/api/communications/send-sms", CommunicationEndpoints.SendSms)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.SendCustomerUpdate));
app.MapPost("/api/communications/send-teams-notification", CommunicationEndpoints.SendTeamsNotification)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ConfigureIntegrations));
app.MapGet("/api/communications/history", CommunicationEndpoints.ListHistory)
    .AddEndpointFilter(new PermissionEndpointFilter(Permission.ViewAssignedClaims, Permission.ViewAllClaims));
app.MapGet("/api/cases/{caseId:guid}/communications", CommunicationEndpoints.ListForCase)
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
