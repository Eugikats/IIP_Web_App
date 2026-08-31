using System.Security.Claims;
using System.Text.Json;
using InsuranceIntelligence.Api.Admin;
using InsuranceIntelligence.Api.Azure;
using InsuranceIntelligence.Api.CommandCenter;
using InsuranceIntelligence.Api.Local;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Documents;
using InsuranceIntelligence.Domain.Queue;
using InsuranceIntelligence.Functions;
using InsuranceIntelligence.Infrastructure.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace InsuranceIntelligence.Api.Tests;

public sealed class ProtectedRuntimeReconciliationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    [Fact]
    public async Task SystemCapabilities_NotConfiguredProvidersReportInactiveCapabilities()
    {
        var service = new SystemCapabilityStatusService(
            new InfrastructureOptions
            {
                AiProvider = "NotConfigured",
                DocumentProvider = "NotConfigured",
                RetrievalProvider = "NotConfigured",
                EmailProvider = "Disabled",
                SmsProvider = "Mock",
                NotificationProvider = "Disabled",
                ChannelProvider = "Mock",
                ProvisionerProvider = "Mock"
            },
            new TestHostEnvironment("Production"));

        var status = await service.GetAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.False(status.ActiveAi);
        Assert.False(status.ActiveDocumentExtraction);
        Assert.False(status.ActiveKnowledgeRetrieval);
        Assert.False(status.ActiveEmailIngestion);
        Assert.False(status.ActiveSmsDelivery);
        Assert.False(status.ActiveTeamsNotification);
        Assert.False(status.ActiveChannels);
        Assert.False(status.ActiveOnboarding);
    }

    [Fact]
    public async Task SystemCapabilities_LiveProviderModesReportActiveCapabilities()
    {
        var service = new SystemCapabilityStatusService(
            new InfrastructureOptions
            {
                AiProvider = "AzureOpenAI",
                DocumentProvider = "AzureDocumentIntelligence",
                RetrievalProvider = "AzureAiSearch",
                EmailProvider = "Graph",
                SmsProvider = "AfricasTalking",
                NotificationProvider = "IncomingWebhook",
                ChannelProvider = "WhatsAppCloudApi"
            },
            new TestHostEnvironment("Production"));

        var status = await service.GetAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.True(status.ActiveAi);
        Assert.True(status.ActiveDocumentExtraction);
        Assert.True(status.ActiveKnowledgeRetrieval);
        Assert.True(status.ActiveEmailIngestion);
        Assert.True(status.ActiveSmsDelivery);
        Assert.True(status.ActiveTeamsNotification);
        Assert.True(status.ActiveChannels);
    }

    [Fact]
    public async Task ProviderHealth_DisabledIntegrationsSurfaceNotConfigured()
    {
        var options = new InfrastructureOptions
        {
            AuthMode = "Entra",
            StorageProvider = "SqlServer",
            SqlServerConnectionString = "Server=prod;Database=iip;",
            BlobProvider = "AzureBlob",
            AzureBlob = new AzureBlobOptions { Endpoint = "https://example.blob.core.windows.net" },
            EmailProvider = "Disabled",
            CommunicationProvider = "Disabled",
            NotificationProvider = "Disabled"
        };

        var result = AdminEndpoints.GetProviderHealth(options, AuthenticatedContext());
        var (_, body) = await ExecuteAsync<JsonElement>(result);

        Assert.Contains(body.GetProperty("providers").EnumerateArray(), item =>
            item.GetProperty("name").GetString() == "graphMail"
            && item.GetProperty("status").GetString() == "not_configured");
        Assert.Contains(body.GetProperty("providers").EnumerateArray(), item =>
            item.GetProperty("name").GetString() == "communications"
            && item.GetProperty("status").GetString() == "not_configured");
        Assert.Contains(body.GetProperty("providers").EnumerateArray(), item =>
            item.GetProperty("name").GetString() == "notifications"
            && item.GetProperty("status").GetString() == "not_configured");
    }

    [Fact]
    public void ProtectedRuntimeStartupValidation_RejectsMockSmsChannelAndProvisionerProviders()
    {
        var errors = AzureProviderStartupValidator.ValidateProduction(
            new InfrastructureOptions
            {
                RuntimeProfile = "ManagedApplicationReviewer",
                AuthMode = "Entra",
                StorageProvider = "SqlServer",
                SqlServerConnectionString = "Server=prod;Database=iip;",
                BlobProvider = "AzureBlob",
                DocumentProvider = "NotConfigured",
                RetrievalProvider = "NotConfigured",
                AiProvider = "NotConfigured",
                QueueProvider = "AzureServiceBus",
                EmailProvider = "Disabled",
                CommunicationProvider = "Disabled",
                NotificationProvider = "Disabled",
                SmsProvider = "Mock",
                ChannelProvider = "Mock",
                ProvisionerProvider = "Mock",
                ServiceBus = new ServiceBusOptions { FullyQualifiedNamespace = "iip-prod.servicebus.windows.net" }
            },
            "ManagedApplicationReviewer");

        Assert.Contains(errors, error => error.Contains("INSURANCEINTELLIGENCE_SMS_PROVIDER", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("INSURANCEINTELLIGENCE_CHANNEL_PROVIDER", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("INSURANCEINTELLIGENCE_PROVISIONER_PROVIDER", StringComparison.Ordinal));
    }

    [Fact]
    public void WorkflowProtectedRuntimeValidation_UsesExplicitRuntimeProfileOverHostEnvironment()
    {
        var errors = WorkflowShell.ValidateProtectedRuntime(
            new InfrastructureOptions
            {
                RuntimeProfile = "ManagedApplicationReviewer",
                AuthMode = "Development",
                QueueProvider = "Mock"
            },
            "Development");

        Assert.Contains(errors, error => error.Contains("Development auth is not allowed", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("INSURANCEINTELLIGENCE_QUEUE_PROVIDER=AzureServiceBus", StringComparison.Ordinal));
    }

    [Fact]
    public async Task NotConfiguredQueueDispatcher_FailsClosedInsteadOfSimulatingSuccess()
    {
        var dispatcher = new NotConfiguredMessageQueueDispatcher();
        var envelope = new QueueMessageEnvelope(
            "agent-workflow",
            "AgentWorkflow.Execute",
            "tenant",
            "workspace",
            "correlation",
            new { });

        await Assert.ThrowsAsync<InvalidOperationException>(() => dispatcher.DispatchAsync(envelope));
    }

    [Fact]
    public async Task NotConfiguredDocumentStorage_FailsClosedInsteadOfUsingLocalMockPath()
    {
        var storage = new NotConfiguredDocumentStorageProvider();
        var request = new DocumentStorageRequest(
            Guid.NewGuid(),
            Guid.NewGuid(),
            TenantScope.Create("tenant-1", "workspace-1"),
            "evidence.pdf",
            "application/pdf",
            10,
            new MemoryStream([1, 2, 3]));

        await Assert.ThrowsAsync<ProviderNotConfiguredException>(() => storage.StoreAsync(request));
    }

    private static HttpContext AuthenticatedContext()
    {
        var context = new DefaultHttpContext { TraceIdentifier = "trace-reconciliation" };
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, "entra-admin"),
            new Claim(ClaimTypes.Role, "ITAdmin"),
            new Claim("tenant_id", "tenant-demo"),
            new Claim("workspace_id", "workspace-claims")
        ], "Entra"));
        context.Items[InsuranceIntelligence.Api.Auth.DemoAuthorization.EffectivePermissionsItemKey] =
            (IReadOnlyCollection<InsuranceIntelligence.Domain.Administration.Permission>)(
                InsuranceIntelligence.Domain.Administration.RolePermissionMap
                    .ForRole(InsuranceIntelligence.Domain.Administration.PlatformRole.ITAdmin)
                    .ToArray());
        context.RequestServices = new ServiceCollection().AddLogging().AddProblemDetails().BuildServiceProvider();
        return context;
    }

    private static async Task<(int StatusCode, T Body)> ExecuteAsync<T>(IResult result)
    {
        var httpContext = new DefaultHttpContext
        {
            TraceIdentifier = "trace-reconciliation-exec",
            Response = { Body = new MemoryStream() }
        };
        httpContext.RequestServices = new ServiceCollection().AddLogging().AddProblemDetails().BuildServiceProvider();
        await result.ExecuteAsync(httpContext);
        httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        var body = (await JsonSerializer.DeserializeAsync<T>(httpContext.Response.Body, JsonOptions))!;
        return (httpContext.Response.StatusCode, body);
    }

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "InsuranceIntelligence.Api.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
