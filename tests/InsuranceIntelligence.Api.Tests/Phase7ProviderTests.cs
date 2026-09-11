using System.Text.Json;
using System.Security.Claims;
using Azure.Core;
using InsuranceIntelligence.Api.Admin;
using InsuranceIntelligence.Api.Azure;
using InsuranceIntelligence.Api.CommandCenter;
using InsuranceIntelligence.Api.Health;
using InsuranceIntelligence.Api.Integrations;
using InsuranceIntelligence.Api.Local;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Communications;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Documents;
using InsuranceIntelligence.Domain.Integrations;
using InsuranceIntelligence.Domain.Knowledge;
using InsuranceIntelligence.Domain.Notifications;
using InsuranceIntelligence.Domain.Workflow;
using InsuranceIntelligence.Infrastructure.Configuration;
using InsuranceIntelligence.Infrastructure.DependencyInjection;
using InsuranceIntelligence.Infrastructure.Graph;
using InsuranceIntelligence.Infrastructure.Email;
using InsuranceIntelligence.Infrastructure.Notifications;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace InsuranceIntelligence.Api.Tests;

public sealed class Phase7ProviderTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task ProviderHealth_ReturnsMockProvidersHealthyWithoutSecrets()
    {
        var result = AdminEndpoints.GetProviderHealth(new InfrastructureOptions { AuthMode = "Development" }, Context());
        var (status, body) = await ExecuteAsync<JsonElement>(result);
        var json = body.ToString();

        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.Equal("healthy", body.GetProperty("overallStatus").GetString());
        Assert.Contains(body.GetProperty("providers").EnumerateArray(), item =>
            item.GetProperty("name").GetString() == "mockProviders"
            && item.GetProperty("status").GetString() == "healthy");
        Assert.DoesNotContain("connectionstring", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("token", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ProviderStatus_ReturnsSafeOperationalProviderStripShape()
    {
        var result = ProviderStatusEndpoints.GetProviderStatus(new InfrastructureOptions { AuthMode = "Development" });
        var (status, body) = await ExecuteAsync<JsonElement>(result);
        var json = body.ToString();

        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.Equal("healthy", body.GetProperty("overallStatus").GetString());
        Assert.Contains(body.GetProperty("providers").EnumerateArray(), item =>
            item.GetProperty("name").GetString() == "graphMail");
        Assert.Contains(body.GetProperty("providers").EnumerateArray(), item =>
            item.GetProperty("name").GetString() == "documentIntelligence");
        Assert.DoesNotContain("connectionstring", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("clientsecret", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("token", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ProviderHealth_EntraModeDoesNotAdvertiseMockProviders()
    {
        var result = AdminEndpoints.GetProviderHealth(new InfrastructureOptions { AuthMode = "Entra" }, AuthenticatedContext("ITAdmin"));
        var (status, body) = await ExecuteAsync<JsonElement>(result);

        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.Equal("degraded", body.GetProperty("overallStatus").GetString());
        Assert.DoesNotContain(body.GetProperty("providers").EnumerateArray(), item =>
            item.GetProperty("name").GetString() == "mockProviders");
        Assert.Contains(body.GetProperty("providers").EnumerateArray(), item =>
            item.GetProperty("mode").GetString() == "Mock"
            && item.GetProperty("status").GetString() == "missing");
    }

    [Fact]
    public async Task ProviderHealth_AzureModeMissingConfigReturnsSafeDegradedStatus()
    {
        var options = new InfrastructureOptions
        {
            AuthMode = "Development",
            BlobProvider = "AzureBlob",
            DocumentProvider = "AzureDocumentIntelligence",
            RetrievalProvider = "AzureAiSearch",
            AiProvider = "AzureOpenAI",
            QueueProvider = "AzureServiceBus",
            EmailProvider = "Graph",
            CommunicationProvider = "GraphEmail"
        };

        var result = AdminEndpoints.GetProviderHealth(options, Context());
        var (status, body) = await ExecuteAsync<JsonElement>(result);
        var json = body.ToString();

        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.Equal("degraded", body.GetProperty("overallStatus").GetString());
        Assert.Contains(body.GetProperty("providers").EnumerateArray(), item =>
            item.GetProperty("name").GetString() == "azureOpenAI"
            && item.GetProperty("status").GetString() == "missing");
        Assert.Contains(body.GetProperty("providers").EnumerateArray(), item =>
            item.GetProperty("name").GetString() == "serviceBus"
            && item.GetProperty("status").GetString() == "missing");
        Assert.Contains(body.GetProperty("providers").EnumerateArray(), item =>
            item.GetProperty("name").GetString() == "graphMail"
            && item.GetProperty("status").GetString() == "missing");
        Assert.Contains(body.GetProperty("providers").EnumerateArray(), item =>
            item.GetProperty("name").GetString() == "communications"
            && item.GetProperty("status").GetString() == "missing");
        Assert.DoesNotContain("stack", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("connectionstring", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ProviderHealth_NotConfiguredProvidersSurfaceExplicitNotConfiguredStatus()
    {
        var options = new InfrastructureOptions
        {
            AuthMode = "Entra",
            StorageProvider = "SqlServer",
            DocumentProvider = "NotConfigured",
            RetrievalProvider = "NotConfigured",
            AiProvider = "NotConfigured",
            QueueProvider = "AzureServiceBus",
            ServiceBus = new ServiceBusOptions { FullyQualifiedNamespace = "iip-prod.servicebus.windows.net" }
        };

        var result = AdminEndpoints.GetProviderHealth(options, AuthenticatedContext("ITAdmin"));
        var (status, body) = await ExecuteAsync<JsonElement>(result);

        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.Equal("degraded", body.GetProperty("overallStatus").GetString());
        Assert.Contains(body.GetProperty("providers").EnumerateArray(), item =>
            item.GetProperty("name").GetString() == "database"
            && item.GetProperty("status").GetString() == "missing");
        Assert.Contains(body.GetProperty("providers").EnumerateArray(), item =>
            item.GetProperty("name").GetString() == "documentIntelligence"
            && item.GetProperty("status").GetString() == "not_configured");
        Assert.Contains(body.GetProperty("providers").EnumerateArray(), item =>
            item.GetProperty("name").GetString() == "azureOpenAI"
            && item.GetProperty("status").GetString() == "not_configured");
        Assert.Contains(body.GetProperty("providers").EnumerateArray(), item =>
            item.GetProperty("name").GetString() == "aiSearch"
            && item.GetProperty("status").GetString() == "not_configured");
    }

    [Theory]
    [InlineData("NotConfigured")]
    [InlineData("Disabled")]
    public async Task ProviderHealth_NotConfiguredStorageModesSurfaceExplicitNotConfiguredStatus(string storageProvider)
    {
        var options = new InfrastructureOptions
        {
            AuthMode = "Entra",
            StorageProvider = storageProvider
        };

        var result = AdminEndpoints.GetProviderHealth(options, AuthenticatedContext("ITAdmin"));
        var (status, body) = await ExecuteAsync<JsonElement>(result);

        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.Equal("degraded", body.GetProperty("overallStatus").GetString());
        Assert.Contains(body.GetProperty("providers").EnumerateArray(), item =>
            item.GetProperty("name").GetString() == "database"
            && item.GetProperty("status").GetString() == "not_configured");
    }

    [Fact]
    public async Task ProviderHealth_AzureOpenAiRequiresEmbeddingDeployment()
    {
        var options = new InfrastructureOptions
        {
            AuthMode = "Development",
            AiProvider = "AzureOpenAI",
            AzureOpenAI = new AzureOpenAiOptions
            {
                Endpoint = "https://example.openai.azure.com",
                ChatDeployment = "chat"
            }
        };

        var result = AdminEndpoints.GetProviderHealth(options, Context());
        var (status, body) = await ExecuteAsync<JsonElement>(result);

        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.Equal("degraded", body.GetProperty("overallStatus").GetString());
        Assert.Contains(body.GetProperty("providers").EnumerateArray(), item =>
            item.GetProperty("name").GetString() == "azureOpenAI"
            && item.GetProperty("status").GetString() == "missing"
            && item.GetProperty("detail").GetString()?.Contains("AzureOpenAI:EmbeddingDeployment", StringComparison.Ordinal) == true);
    }

    [Fact]
    public async Task ProviderHealth_GraphRequiresMailboxScopeBeforeConfigured()
    {
        var options = new InfrastructureOptions
        {
            AuthMode = "Development",
            EmailProvider = "Graph",
            CommunicationProvider = "GraphEmail",
            GraphMail = new GraphMailOptions
            {
                TenantId = "tenant-id",
                ClientId = "client-id",
                ClientSecret = "configured-outside-source",
                DefaultMailboxAddress = "claims@insurer.example"
            }
        };

        var result = AdminEndpoints.GetProviderHealth(options, Context());
        var (status, body) = await ExecuteAsync<JsonElement>(result);
        var json = body.ToString();

        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.Equal("degraded", body.GetProperty("overallStatus").GetString());
        Assert.Contains(body.GetProperty("providers").EnumerateArray(), item =>
            item.GetProperty("name").GetString() == "graphMail"
            && item.GetProperty("status").GetString() == "missing");
        Assert.Contains(body.GetProperty("providers").EnumerateArray(), item =>
            item.GetProperty("name").GetString() == "communications"
            && item.GetProperty("status").GetString() == "missing");
        Assert.Contains("MailboxAccessScoped", json, StringComparison.Ordinal);
        Assert.DoesNotContain("configured-outside-source", json, StringComparison.Ordinal);

        var scopedResult = AdminEndpoints.GetProviderHealth(
            options with { GraphMail = options.GraphMail with { MailboxAccessScoped = true } },
            Context());
        var (_, scopedBody) = await ExecuteAsync<JsonElement>(scopedResult);

        Assert.Contains(scopedBody.GetProperty("providers").EnumerateArray(), item =>
            item.GetProperty("name").GetString() == "graphMail"
            && item.GetProperty("status").GetString() == "configured");
        Assert.Contains(scopedBody.GetProperty("providers").EnumerateArray(), item =>
            item.GetProperty("name").GetString() == "communications"
            && item.GetProperty("status").GetString() == "configured");
    }

    [Fact]
    public async Task Readiness_DevelopmentDefaultsAreReadyForLocalExecution()
    {
        var result = HealthEndpoints.Ready(new InfrastructureOptions(), new TestEnvironment("Development"));
        var (status, body) = await ExecuteAsync<JsonElement>(result);

        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.Equal("ready", body.GetProperty("status").GetString());
        Assert.Equal("Development", body.GetProperty("environment").GetString());
        Assert.Equal("Development", body.GetProperty("hostEnvironment").GetString());
        Assert.Equal("Mock", body.GetProperty("notificationProvider").GetString());
        Assert.False(body.GetProperty("seededBusinessData").GetBoolean());
    }

    [Fact]
    public async Task Readiness_UsesExplicitRuntimeProfileForProtectedChecks()
    {
        var result = HealthEndpoints.Ready(new InfrastructureOptions
        {
            RuntimeProfile = "ManagedApplicationReviewer",
            AuthMode = "Development",
            StorageProvider = "InMemory",
            BlobProvider = "Mock",
            DocumentProvider = "Mock",
            RetrievalProvider = "Mock",
            AiProvider = "Mock",
            QueueProvider = "Mock",
            EmailProvider = "Mock",
            CommunicationProvider = "Mock"
        }, new TestEnvironment("Production"));
        var (status, body) = await ExecuteAsync<JsonElement>(result);

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, status);
        Assert.Equal("ManagedApplicationReviewer", body.GetProperty("environment").GetString());
        Assert.Equal("Production", body.GetProperty("hostEnvironment").GetString());
        Assert.Contains(body.GetProperty("errors").EnumerateArray(), item =>
            item.GetString()?.Contains("ManagedApplicationReviewer requires", StringComparison.Ordinal) == true);
    }

    [Fact]
    public async Task Readiness_ProtectedRuntimeWithMockProvidersIsNotReady()
    {
        var result = HealthEndpoints.Ready(new InfrastructureOptions
        {
            AuthMode = "Development",
            StorageProvider = "EfSqlite",
            BlobProvider = "Mock",
            DocumentProvider = "Mock",
            RetrievalProvider = "Mock",
            AiProvider = "Mock",
            QueueProvider = "Mock",
            EmailProvider = "Mock",
            CommunicationProvider = "Mock",
            SeedDemoData = true
        }, new TestEnvironment("Staging"));
        var (status, body) = await ExecuteAsync<JsonElement>(result);

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, status);
        Assert.Equal("not_ready", body.GetProperty("status").GetString());
        Assert.Equal("Mock", body.GetProperty("notificationProvider").GetString());
        Assert.True(body.GetProperty("seededBusinessData").GetBoolean());
        Assert.Contains(body.GetProperty("errors").EnumerateArray(), item =>
            item.GetString()?.Contains("INSURANCEINTELLIGENCE_STORAGE_PROVIDER", StringComparison.Ordinal) == true);
        Assert.Contains(body.GetProperty("errors").EnumerateArray(), item =>
            item.GetString()?.Contains("INSURANCEINTELLIGENCE_AI_PROVIDER", StringComparison.Ordinal) == true);
    }

    [Fact]
    public async Task Readiness_SelectedLiveProvidersMissingConfigAreNotReady()
    {
        var result = HealthEndpoints.Ready(new InfrastructureOptions
        {
            BlobProvider = "AzureBlob",
            DocumentProvider = "AzureDocumentIntelligence",
            RetrievalProvider = "AzureAiSearch",
            AiProvider = "AzureOpenAI",
            QueueProvider = "AzureServiceBus",
            EmailProvider = "Graph",
            CommunicationProvider = "GraphEmail"
        }, new TestEnvironment("Development"));
        var (status, body) = await ExecuteAsync<JsonElement>(result);

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, status);
        Assert.Equal("not_ready", body.GetProperty("status").GetString());
        Assert.DoesNotContain("secret", body.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("token", body.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AzureOpenAiAssistant_NoApprovedSourcesEscalatesWithoutModelMemory()
    {
        var provider = new AzureOpenAiAssistantProvider(
            new InfrastructureOptions
            {
                AiProvider = "AzureOpenAI",
                AzureOpenAI = new AzureOpenAiOptions
                {
                    Endpoint = "https://example.openai.azure.com",
                    ChatDeployment = "chat"
                }
            },
            NullLogger<AzureOpenAiAssistantProvider>.Instance);
        var question = new AssistantQuestion(
            TenantScope.Create("tenant-demo", "workspace-claims"),
            null,
            "Is windscreen replacement covered?",
            "Motor",
            "Customer",
            "local-admin",
            "corr-phase7");

        var answer = await provider.AnswerPolicyQuestionAsync(
            question,
            new RetrievalResult(question.Question, question.ProductLine, Array.Empty<KnowledgeChunk>(), Array.Empty<SourceCitation>()));

        Assert.True(answer.EscalationRequired);
        Assert.Equal("no_source", answer.RiskFlag);
        Assert.Empty(answer.Sources);
        Assert.Contains("cannot answer this from approved knowledge", answer.AnswerText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("claim is denied", answer.AnswerText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("fraud", answer.AnswerText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AzureBlobReferences_AreTenantWorkspacePrefixedAndSanitized()
    {
        var reference = AzureProviderSupport.SafeBlobName(
            TenantScope.Create("tenant-demo", "workspace-claims"),
            "cases",
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa").ToString(),
            "documents",
            "../claim form with spaces.pdf");

        Assert.StartsWith("tenant-demo/workspace-claims/cases/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/documents/", reference);
        Assert.EndsWith("claim_form_with_spaces.pdf", reference);
        Assert.DoesNotContain("..", reference, StringComparison.Ordinal);
        Assert.DoesNotContain("\\", reference, StringComparison.Ordinal);
    }

    [Fact]
    public void StartupValidation_OnlyFailsForSelectedLiveProvidersWithMissingConfig()
    {
        Assert.Empty(AzureProviderStartupValidator.Validate(new InfrastructureOptions()));

        var errors = AzureProviderStartupValidator.Validate(new InfrastructureOptions
        {
            BlobProvider = "AzureBlob",
            DocumentProvider = "AzureDocumentIntelligence",
            RetrievalProvider = "AzureAiSearch",
            AiProvider = "AzureOpenAI",
            QueueProvider = "AzureServiceBus",
            EmailProvider = "Graph",
            CommunicationProvider = "GraphEmail"
        });

        Assert.Contains(errors, error => error.Contains("AzureBlob", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("DocumentIntelligence:Endpoint", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("AzureAiSearch:Endpoint", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("AzureAiSearch:IndexName", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("AzureOpenAI:Endpoint", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("AzureOpenAI:ChatDeployment", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("AzureOpenAI:EmbeddingDeployment", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("ServiceBus:FullyQualifiedNamespace", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("Graph email mode", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("GraphEmail communication mode", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("MailboxAccessScoped", StringComparison.Ordinal));
        Assert.DoesNotContain(errors, error => error.Contains("secret", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(errors, error => error.Contains("token", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void StartupValidation_GraphRequiresMailboxScopeAcknowledgement()
    {
        var options = new InfrastructureOptions
        {
            EmailProvider = "Graph",
            CommunicationProvider = "GraphEmail",
            GraphMail = new GraphMailOptions
            {
                TenantId = "tenant-id",
                ClientId = "client-id",
                ClientSecret = "configured-outside-source",
                DefaultMailboxAddress = "claims@insurer.example"
            }
        };

        var errors = AzureProviderStartupValidator.Validate(options);

        Assert.Contains(errors, error => error.Contains("Graph email mode", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("GraphEmail communication mode", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("MailboxAccessScoped=true", StringComparison.Ordinal));

        var scopedErrors = AzureProviderStartupValidator.Validate(
            options with { GraphMail = options.GraphMail with { MailboxAccessScoped = true } });

        Assert.Empty(scopedErrors);
    }

    [Fact]
    public void ProductionStartupValidation_RejectsBlobConnectionStringAndDisabledManagedIdentity()
    {
        var errors = AzureProviderStartupValidator.ValidateProduction(
            new InfrastructureOptions
            {
                AuthMode = "Entra",
                StorageProvider = "SqlServer",
                SqlServerConnectionString = "Server=prod;Database=iip;Authentication=Active Directory Managed Identity;",
                BlobProvider = "AzureBlob",
                DocumentProvider = "NotConfigured",
                RetrievalProvider = "AzureAiSearch",
                AiProvider = "NotConfigured",
                QueueProvider = "AzureServiceBus",
                EmailProvider = "Disabled",
                CommunicationProvider = "Disabled",
                NotificationProvider = "Disabled",
                SmsProvider = "Disabled",
                ChannelProvider = "Disabled",
                ProvisionerProvider = "Disabled",
                AzureBlob = new AzureBlobOptions
                {
                    AccountName = "stiipprod",
                    UseManagedIdentity = false,
                    ConnectionString = "DefaultEndpointsProtocol=https;AccountName=stiipprod;AccountKey=REDACTED;"
                },
                AzureAiSearch = new AzureAiSearchOptions
                {
                    Endpoint = "https://search.example.search.windows.net",
                    IndexName = "insuranceintelligence-knowledge",
                    UseManagedIdentity = false
                },
                ServiceBus = new ServiceBusOptions
                {
                    FullyQualifiedNamespace = "sb-prod.servicebus.windows.net",
                    UseManagedIdentity = false
                },
                DataResidencyCountry = "UG",
                DataResidencyGeography = "Africa",
                AzureRegion = "southafricanorth",
                DataResidencyMode = "regional",
                DataResidencyAllowedAzureRegions = "southafricanorth",
                DataResidencyApprovalReference = "RES-TEST-001"
            },
            "Production");

        Assert.Contains(errors, error => error.Contains("AzureBlob:UseManagedIdentity", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("AzureBlob:ConnectionString", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("AzureAiSearch:UseManagedIdentity", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("ServiceBus:UseManagedIdentity", StringComparison.Ordinal));
        Assert.DoesNotContain(errors, error => error.Contains("REDACTED", StringComparison.Ordinal));
        Assert.DoesNotContain(errors, error => error.Contains("AccountKey", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ProductionStartupValidation_RejectsUnsafeLocalAndMockModes()
    {
        var errors = AzureProviderStartupValidator.ValidateProduction(
            new InfrastructureOptions
            {
                AuthMode = "Development",
                StorageProvider = "InMemory",
                SeedDemoData = true,
                BlobProvider = "Mock",
                DocumentProvider = "Mock",
                RetrievalProvider = "Mock",
                AiProvider = "Mock",
                QueueProvider = "Mock",
                EmailProvider = "Mock",
                CommunicationProvider = "Mock",
                NotificationProvider = "Mock"
            },
            "Production");

        Assert.Contains(errors, error => error.Contains("INSURANCEINTELLIGENCE_AUTH_MODE", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("INSURANCEINTELLIGENCE_STORAGE_PROVIDER", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("INSURANCEINTELLIGENCE_SEED_DEMO_DATA", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("INSURANCEINTELLIGENCE_AI_PROVIDER", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("INSURANCEINTELLIGENCE_NOTIFICATION_PROVIDER", StringComparison.Ordinal));
        Assert.DoesNotContain(errors, error => error.Contains("secret", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(errors, error => error.Contains("token", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ProductionStartupValidation_RejectsExplicitDemoEscapeHatch()
    {
        var errors = AzureProviderStartupValidator.ValidateProduction(
            new InfrastructureOptions
            {
                AuthMode = "Development",
                StorageProvider = "InMemory",
                SeedDemoData = true,
                AllowProductionMockProviders = true
            },
            "Production");

        Assert.NotEmpty(errors);
        Assert.Contains(errors, error => error.Contains("INSURANCEINTELLIGENCE_AUTH_MODE", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("INSURANCEINTELLIGENCE_ALLOW_PRODUCTION_MOCKS", StringComparison.Ordinal));
    }

    [Fact]
    public void ProductionStartupValidation_RejectsSqlServerWithoutConnection()
    {
        var errors = AzureProviderStartupValidator.ValidateProduction(
            new InfrastructureOptions
            {
                AuthMode = "Entra",
                StorageProvider = "SqlServer",
                BlobProvider = "AzureBlob",
                DocumentProvider = "AzureDocumentIntelligence",
                RetrievalProvider = "AzureAiSearch",
                AiProvider = "AzureOpenAI",
                QueueProvider = "AzureServiceBus",
                EmailProvider = "Graph",
                CommunicationProvider = "GraphEmail",
                NotificationProvider = "Disabled"
            },
            "Production");

        Assert.Contains(errors, error => error.Contains("INSURANCEINTELLIGENCE_SQLSERVER_CONNECTION_STRING", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("Staging")]
    [InlineData("ManagedApplicationReviewer")]
    public void ProtectedRuntimeStartupValidation_RejectsUnsafeLocalAndMockModes(string runtime)
    {
        var errors = AzureProviderStartupValidator.ValidateProduction(
            new InfrastructureOptions
            {
                AuthMode = "Development",
                StorageProvider = "EfSqlite",
                SeedDemoData = true,
                BlobProvider = "Mock",
                DocumentProvider = "Mock",
                RetrievalProvider = "Mock",
                AiProvider = "Mock",
                QueueProvider = "Mock",
                EmailProvider = "Mock",
                CommunicationProvider = "Mock"
            },
            runtime);

        Assert.Contains(errors, error => error.Contains(runtime, StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("EfSqlite", StringComparison.Ordinal));
    }

    [Fact]
    public void ProtectedRuntimeStartupValidation_UsesExplicitProfileOverHostEnvironment()
    {
        var errors = AzureProviderStartupValidator.ValidateProduction(
            new InfrastructureOptions
            {
                RuntimeProfile = "Staging",
                AuthMode = "Development",
                StorageProvider = "InMemory",
                BlobProvider = "Mock",
                DocumentProvider = "Mock",
                RetrievalProvider = "Mock",
                AiProvider = "Mock",
                QueueProvider = "Mock",
                EmailProvider = "Mock",
                CommunicationProvider = "Mock"
            },
            "Production");

        Assert.Contains(errors, error => error.Contains("Staging requires", StringComparison.Ordinal));
        Assert.DoesNotContain(errors, error => error.Contains("Production requires", StringComparison.Ordinal));
    }

    [Fact]
    public void ProductionStartupValidation_DoesNotAffectDevelopmentMockMode()
    {
        var errors = AzureProviderStartupValidator.ValidateProduction(new InfrastructureOptions(), "Development");

        Assert.Empty(errors);
    }

    [Fact]
    public void ProductionResidencyValidation_RejectsMissingApprovalMetadata()
    {
        var errors = AzureProviderStartupValidator.ValidateProductionResidency(
            new InfrastructureOptions(),
            "Production");

        Assert.Contains(errors, error => error.Contains("INSURANCEINTELLIGENCE_DATA_RESIDENCY_COUNTRY", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("INSURANCEINTELLIGENCE_DATA_RESIDENCY_GEOGRAPHY", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("INSURANCEINTELLIGENCE_AZURE_REGION", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("INSURANCEINTELLIGENCE_DATA_RESIDENCY_ALLOWED_AZURE_REGIONS", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("INSURANCEINTELLIGENCE_DATA_RESIDENCY_APPROVAL_REFERENCE", StringComparison.Ordinal));
    }

    [Fact]
    public void ProductionResidencyValidation_RejectsRegionOutsideApprovedList()
    {
        var errors = AzureProviderStartupValidator.ValidateProductionResidency(
            ApprovedResidencyOptions() with { AzureRegion = "unapproved-region" },
            "Production");

        Assert.Single(errors);
        Assert.Contains("not included", errors.Single(), StringComparison.Ordinal);
    }

    [Fact]
    public void ProductionResidencyValidation_RejectsOpenAiDeploymentTypeMismatch()
    {
        var errors = AzureProviderStartupValidator.ValidateProductionResidency(
            ApprovedResidencyOptions() with
            {
                AiProvider = "AzureOpenAI",
                AzureOpenAI = new AzureOpenAiOptions { DeploymentType = "GlobalStandard" }
            },
            "Production");

        Assert.Single(errors);
        Assert.Contains("requires AzureOpenAI:DeploymentType 'Standard'", errors.Single(), StringComparison.Ordinal);
    }

    [Fact]
    public void ProductionResidencyValidation_AcceptsApprovedRegion()
    {
        var errors = AzureProviderStartupValidator.ValidateProductionResidency(
            ApprovedResidencyOptions(),
            "Production");

        Assert.Empty(errors);
    }

    [Fact]
    public void DemoRuntimeStartupValidation_AllowsSyntheticSeedButStillRequiresRealProviders()
    {
        var errors = AzureProviderStartupValidator.ValidateProduction(
            new InfrastructureOptions
            {
                RuntimeProfile = "Demo",
                AuthMode = "Development",
                StorageProvider = "InMemory",
                SeedDemoData = true,
                AllowProductionMockProviders = true,
                BlobProvider = "Mock",
                DocumentProvider = "Mock",
                RetrievalProvider = "Mock",
                AiProvider = "Mock",
                QueueProvider = "Mock",
                EmailProvider = "Mock",
                CommunicationProvider = "Mock",
                NotificationProvider = "Mock"
            },
            "Development");

        // Demo is a real deployed sandbox: it must still use real identity, Azure SQL,
        // real providers, and must not allow the production-mock escape hatch...
        Assert.Contains(errors, error => error.Contains("INSURANCEINTELLIGENCE_AUTH_MODE", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("INSURANCEINTELLIGENCE_STORAGE_PROVIDER", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("INSURANCEINTELLIGENCE_ALLOW_PRODUCTION_MOCKS", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("INSURANCEINTELLIGENCE_AI_PROVIDER", StringComparison.Ordinal));
        // ...but seeding synthetic demo data is the whole point of Demo, so it is NOT rejected.
        Assert.DoesNotContain(errors, error => error.Contains("INSURANCEINTELLIGENCE_SEED_DEMO_DATA", StringComparison.Ordinal));
    }

    [Fact]
    public void DemoRuntimeStartupValidation_AcceptsRealProvidersWithSyntheticSeed()
    {
        var errors = AzureProviderStartupValidator.ValidateProduction(
            new InfrastructureOptions
            {
                RuntimeProfile = "Demo",
                AuthMode = "Entra",
                StorageProvider = "SqlServer",
                SqlServerConnectionString = "Server=demo;Database=iip;Authentication=Active Directory Managed Identity;",
                SeedDemoData = true,
                BlobProvider = "AzureBlob",
                DocumentProvider = "AzureDocumentIntelligence",
                RetrievalProvider = "AzureAiSearch",
                AiProvider = "AzureOpenAI",
                QueueProvider = "AzureServiceBus",
                EmailProvider = "Disabled",
                CommunicationProvider = "Disabled",
                NotificationProvider = "Disabled",
                SmsProvider = "Disabled",
                ChannelProvider = "Disabled",
                ProvisionerProvider = "Disabled"
            },
            "Development");

        Assert.Empty(errors);
    }

    [Theory]
    [InlineData("Production", null, false, "Production")]
    [InlineData("Staging", null, true, "Production")]
    [InlineData("ManagedApplicationReviewer", null, true, "Production")]
    [InlineData("Demo", null, true, "Demo")]
    [InlineData(null, "Development", true, "LocalDevelopment")]
    public void ResolveReviewMode_ClassifiesEnvironmentsForTheBanner(
        string? runtimeProfile,
        string? environmentName,
        bool seedDemoData,
        string expectedReviewMode)
    {
        var options = new InfrastructureOptions
        {
            RuntimeProfile = runtimeProfile,
            SeedDemoData = seedDemoData
        };

        var reviewMode = AzureProviderStartupValidator.ResolveReviewMode(options, environmentName ?? "Development");

        Assert.Equal(expectedReviewMode, reviewMode);
    }

    [Fact]
    public void UsesSyntheticData_IsTrueForSeededDemoButNeverForProduction()
    {
        var demo = new InfrastructureOptions { RuntimeProfile = "Demo", SeedDemoData = true };
        var localSeeded = new InfrastructureOptions { SeedDemoData = true };
        var production = new InfrastructureOptions { RuntimeProfile = "Production", SeedDemoData = true };

        Assert.True(AzureProviderStartupValidator.UsesSyntheticData(demo, "Development"));
        Assert.True(AzureProviderStartupValidator.UsesSyntheticData(localSeeded, "Development"));
        // Production can never boot with seed data, and even if the flag is set the banner
        // classification refuses to mark it synthetic.
        Assert.False(AzureProviderStartupValidator.UsesSyntheticData(production, "Production"));
    }

    [Fact]
    public async Task AzureOpenAiAssistant_UsesConfiguredPromptTemplateForGroundedAnswers()
    {
        var templatePath = Path.Combine(Path.GetTempPath(), $"insuranceintelligence-prompt-{Guid.NewGuid():N}.txt");
        await File.WriteAllTextAsync(templatePath, "Template: {audienceLabel}: Question={userQuestion}; Product={productLine}; Sources={sourceCount}; {audiencePrefix}{sourceText} Human review required.");
        var handler = new CapturingOpenAiHandler("""
            {
              "choices": [
                {
                  "message": {
                    "content": "Template: Based on approved policy information, Windscreen replacement is covered when listed in the schedule. Human review required."
                  }
                }
              ]
            }
            """);
        try
        {
            var provider = new AzureOpenAiAssistantProvider(
                new InfrastructureOptions
                {
                    AiProvider = "AzureOpenAI",
                    AzureOpenAI = new AzureOpenAiOptions
                    {
                        Endpoint = "https://example.openai.azure.com",
                        ChatDeployment = "chat",
                        PolicyAnswerPromptTemplatePath = templatePath
                    }
                },
                NullLogger<AzureOpenAiAssistantProvider>.Instance,
                new HttpClient(handler),
                new StaticTokenCredential());
            var question = new AssistantQuestion(
                TenantScope.Create("tenant-demo", "workspace-claims"),
                null,
                "Is windscreen replacement covered?",
                "Motor",
                "Customer",
                "local-admin",
                "corr-phase7");
            var documentId = Guid.NewGuid();
            var chunk = new KnowledgeChunk
            {
                ChunkId = "chunk-1",
                KnowledgeDocumentId = documentId,
                Scope = question.Scope,
                DocumentName = "Motor Policy",
                ProductLine = "Motor",
                Version = "1.0",
                ChunkIndex = 0,
                Content = "Windscreen replacement is covered when listed in the schedule.",
                SourceReference = new KnowledgeSourceReference(documentId, "chunk-1", "Motor Policy", "Motor", "1.0", "knowledge/motor.md")
            };
            var citation = new SourceCitation(documentId, "chunk-1", "Motor Policy", "Motor", "1.0", "Windscreen replacement is covered", "Motor Policy");

            var answer = await provider.AnswerPolicyQuestionAsync(
                question,
                new RetrievalResult(question.Question, question.ProductLine, [chunk], [citation]));

            Assert.StartsWith("Template: Based on approved policy information", answer.AnswerText, StringComparison.Ordinal);
            Assert.Contains("Windscreen replacement is covered", answer.AnswerText, StringComparison.Ordinal);
            Assert.True(answer.EscalationRequired);
            Assert.Equal("needs_review", answer.RiskFlag);
            Assert.Equal("AzureOpenAI", answer.ModelProvider);
            Assert.Equal("chat", answer.ModelDeployment);
            Assert.Equal("policy-answer-v2", answer.PromptVersion);
            Assert.Equal("CustomerSafe", answer.Visibility);
            Assert.False(answer.CanTriggerDecision);
            Assert.EndsWith("/openai/deployments/chat/chat/completions?api-version=2024-10-21", handler.LastRequestUri?.ToString(), StringComparison.Ordinal);
            Assert.Equal("Bearer", handler.LastAuthorizationScheme);
            Assert.Contains("customer-facing", handler.LastRequestBody, StringComparison.Ordinal);
            Assert.Contains("Question=Is windscreen replacement covered?", handler.LastRequestBody, StringComparison.Ordinal);
            Assert.Contains("Product=Motor", handler.LastRequestBody, StringComparison.Ordinal);
            Assert.Contains("Sources=1", handler.LastRequestBody, StringComparison.Ordinal);
            Assert.Contains("calm, clear, and natural", handler.LastRequestBody, StringComparison.Ordinal);
            Assert.Contains("\"temperature\":0.25", handler.LastRequestBody, StringComparison.Ordinal);
            Assert.DoesNotContain("tenant-demo", handler.LastRequestBody, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("workspace-claims", handler.LastRequestBody, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (File.Exists(templatePath))
            {
                File.Delete(templatePath);
            }
        }
    }

    [Fact]
    public async Task AzureOpenAiAssistant_ProviderFailureEscalatesSafely()
    {
        var provider = new AzureOpenAiAssistantProvider(
            new InfrastructureOptions
            {
                AiProvider = "AzureOpenAI",
                AzureOpenAI = new AzureOpenAiOptions
                {
                    Endpoint = "https://example.openai.azure.com",
                    ChatDeployment = "chat"
                }
            },
            NullLogger<AzureOpenAiAssistantProvider>.Instance,
            new HttpClient(new CapturingOpenAiHandler("{}", System.Net.HttpStatusCode.TooManyRequests)),
            new StaticTokenCredential());
        var question = new AssistantQuestion(
            TenantScope.Create("tenant-demo", "workspace-claims"),
            null,
            "Is windscreen replacement covered?",
            "Motor",
            "Customer",
            "local-admin",
            "corr-phase7");
        var documentId = Guid.NewGuid();
        var chunk = new KnowledgeChunk
        {
            ChunkId = "chunk-1",
            KnowledgeDocumentId = documentId,
            Scope = question.Scope,
            DocumentName = "Motor Policy",
            ProductLine = "Motor",
            Version = "1.0",
            ChunkIndex = 0,
            Content = "Windscreen replacement is covered when listed in the schedule.",
            SourceReference = new KnowledgeSourceReference(documentId, "chunk-1", "Motor Policy", "Motor", "1.0", "knowledge/motor.md")
        };

        var answer = await provider.AnswerPolicyQuestionAsync(
            question,
            new RetrievalResult(question.Question, question.ProductLine, [chunk], []));

        Assert.True(answer.EscalationRequired);
        Assert.Equal("provider_failure", answer.RiskFlag);
        Assert.DoesNotContain("stack", answer.GuardrailMessage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("token", answer.GuardrailMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AzureAiSearchFilter_RequiresTenantWorkspaceApprovedIndexedAndProductLine()
    {
        var filter = AzureAiSearchKnowledgeIndexProvider.BuildSearchFilter(TenantScope.Create("tenant-demo", "workspace-claims"), "Motor");

        Assert.Contains("tenantId eq 'tenant-demo'", filter, StringComparison.Ordinal);
        Assert.Contains("workspaceId eq 'workspace-claims'", filter, StringComparison.Ordinal);
        Assert.Contains("approvalStatus eq 'Approved'", filter, StringComparison.Ordinal);
        Assert.Contains("indexedStatus eq 'Indexed'", filter, StringComparison.Ordinal);
        Assert.Contains("productLine eq 'Motor'", filter, StringComparison.Ordinal);
        Assert.DoesNotContain("Archived", filter, StringComparison.Ordinal);
        Assert.DoesNotContain("Draft", filter, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AzureOpenAiCustomerUpdateDraftingProvider_UsesChatCompletions()
    {
        const string responseBody = """
        {
          "choices": [
            {
              "message": {
                "content": "Hello Amina, we are reviewing your motor claim and need the police abstract before the claims team can continue."
              }
            }
          ]
        }
        """;
        var handler = new CapturingOpenAiHandler(responseBody);
        var provider = new AzureOpenAiCustomerUpdateDraftingProvider(
            OpenAiOptions(),
            new StaticWorkflowRuleProvider(),
            NullLogger<AzureOpenAiCustomerUpdateDraftingProvider>.Instance,
            new HttpClient(handler),
            new StaticTokenCredential());
        var claimCase = Case(TenantScope.Create("tenant-demo", "workspace-claims"));
        var checklist = new DocumentCompletenessChecklist(
            claimCase.CaseId,
            "Motor",
            [DocumentType.ClaimForm, DocumentType.PoliceAbstract],
            [],
            [DocumentType.ClaimForm],
            [new MissingDocumentRequirement(DocumentType.PoliceAbstract, "Police abstract", true)],
            CompletenessScore.Calculate([DocumentType.ClaimForm, DocumentType.PoliceAbstract], [DocumentType.ClaimForm]));

        var draft = await provider.DraftAsync(
            claimCase,
            checklist,
            CustomerUpdateReasonCategory.MissingDocuments,
            CommunicationChannel.Email,
            "claims-officer",
            "corr-customer-update");

        Assert.Contains("/openai/deployments/chat/chat/completions", handler.LastRequestUri?.ToString(), StringComparison.Ordinal);
        Assert.Equal("Bearer", handler.LastAuthorizationScheme);
        Assert.Contains("Missing required documents: Police abstract", handler.LastRequestBody, StringComparison.Ordinal);
        Assert.Contains("police abstract", draft.MessageBody, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(CustomerUpdateStatus.Draft, draft.Status);
    }

    [Fact]
    public void InfrastructureRegistration_UsesDisabledSyntheticEmailProvidersWhenGraphIsSelected()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["INSURANCEINTELLIGENCE_STORAGE_PROVIDER"] = "SqlServer",
                ["INSURANCEINTELLIGENCE_SQLSERVER_CONNECTION_STRING"] = "Server=(localdb)\\MSSQLLocalDB;Database=IipProviderTests;Trusted_Connection=True;TrustServerCertificate=True",
                ["INSURANCEINTELLIGENCE_EMAIL_PROVIDER"] = "Graph",
                ["INSURANCEINTELLIGENCE_COMMUNICATION_PROVIDER"] = "GraphEmail",
                ["INSURANCEINTELLIGENCE_NOTIFICATION_PROVIDER"] = "Disabled"
            })
            .Build();
        var services = new ServiceCollection();

        services.AddInsuranceIntelligenceInfrastructure(configuration, new TestEnvironment("Development"));
        services.AddDataProtection();
        using var provider = services.BuildServiceProvider();

        Assert.IsType<DisabledEmailIngestionProvider>(provider.GetRequiredService<IEmailIngestionProvider>());
        Assert.IsType<DisabledWebhookSubscriptionProvider>(provider.GetRequiredService<IWebhookSubscriptionProvider>());
        // ISmsProvider is always TenantAwareSmsProvider now — SMS_PROVIDER unset here means its
        // env-var fallback (SmsProviderFactory.CreateFallback) resolves to DisabledSmsProvider,
        // which is exactly what this test cares about; see TenantAwareSmsProviderTests for
        // resolution-order coverage.
        Assert.IsType<InsuranceIntelligence.Infrastructure.Communications.TenantAwareSmsProvider>(provider.GetRequiredService<ISmsProvider>());
        Assert.IsType<DisabledTeamsNotificationProvider>(provider.GetRequiredService<ITeamsNotificationProvider>());
    }

    [Fact]
    public void InfrastructureRegistration_MockNotificationModeResolvesToDisabledProvider()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["INSURANCEINTELLIGENCE_STORAGE_PROVIDER"] = "SqlServer",
                ["INSURANCEINTELLIGENCE_SQLSERVER_CONNECTION_STRING"] = "Server=(localdb)\\MSSQLLocalDB;Database=IipProviderTests;Trusted_Connection=True;TrustServerCertificate=True",
                ["INSURANCEINTELLIGENCE_EMAIL_PROVIDER"] = "Mock",
                ["INSURANCEINTELLIGENCE_COMMUNICATION_PROVIDER"] = "Mock",
                ["INSURANCEINTELLIGENCE_NOTIFICATION_PROVIDER"] = "Mock"
            })
            .Build();
        var services = new ServiceCollection();

        services.AddInsuranceIntelligenceInfrastructure(
            configuration,
            new TestEnvironment("LocalTest"));
        services.AddDataProtection();
        using var provider = services.BuildServiceProvider();

        Assert.IsType<DisabledTeamsNotificationProvider>(
            provider.GetRequiredService<ITeamsNotificationProvider>());
        Assert.IsType<EfTeamsNotificationRepository>(
            provider.GetRequiredService<ITeamsNotificationRepository>());
        Assert.IsType<InsuranceIntelligence.Infrastructure.Email.LocalEmailIngestionProvider>(
            provider.GetRequiredService<IEmailIngestionProvider>());
        Assert.IsType<InsuranceIntelligence.Infrastructure.Email.LocalEmailAttachmentProcessor>(
            provider.GetRequiredService<IEmailAttachmentProcessor>());
        Assert.IsType<DisabledCommunicationSendingProvider>(
            provider.GetRequiredService<ICommunicationSendingProvider>());
        Assert.IsType<InsuranceIntelligence.Infrastructure.Communications.TenantAwareSmsProvider>(
            provider.GetRequiredService<ISmsProvider>());
        Assert.IsType<DisabledWebhookSubscriptionProvider>(
            provider.GetRequiredService<IWebhookSubscriptionProvider>());
    }

    private static DefaultHttpContext Context()
    {
        var context = new DefaultHttpContext { TraceIdentifier = "trace-phase7" };
        context.Request.Headers["X-Tenant-Id"] = "tenant-demo";
        context.Request.Headers["X-Workspace-Id"] = "workspace-claims";
        context.Request.Headers["x-demo-role"] = "Admin";
        context.Request.Headers["x-demo-user-id"] = "local-admin";
        context.RequestServices = new ServiceCollection().AddLogging().AddProblemDetails().BuildServiceProvider();
        return context;
    }

    private static DefaultHttpContext AuthenticatedContext(string role)
    {
        var context = new DefaultHttpContext { TraceIdentifier = "trace-phase7-authenticated" };
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, "entra-admin"),
            new Claim(ClaimTypes.Role, role),
            new Claim("tenant_id", "tenant-demo"),
            new Claim("workspace_id", "workspace-claims")
        ], "Entra"));
        // ADR-0005: deployed-mode authz reads store-resolved permissions the middleware stashes.
        context.Items[InsuranceIntelligence.Api.Auth.DemoAuthorization.EffectivePermissionsItemKey] =
            (IReadOnlyCollection<InsuranceIntelligence.Domain.Administration.Permission>)(
                Enum.TryParse<InsuranceIntelligence.Domain.Administration.PlatformRole>(role, true, out var pr)
                    ? InsuranceIntelligence.Domain.Administration.RolePermissionMap.ForRole(pr).ToArray()
                    : Array.Empty<InsuranceIntelligence.Domain.Administration.Permission>());
        context.RequestServices = new ServiceCollection().AddLogging().AddProblemDetails().BuildServiceProvider();
        return context;
    }

    private static async Task<(int StatusCode, T Body)> ExecuteAsync<T>(IResult result)
    {
        var httpContext = Context();
        httpContext.Response.Body = new MemoryStream();
        await result.ExecuteAsync(httpContext);
        httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        var body = (await JsonSerializer.DeserializeAsync<T>(httpContext.Response.Body, JsonOptions))!;
        return (httpContext.Response.StatusCode, body);
    }

    private static InfrastructureOptions ApprovedResidencyOptions() =>
        new()
        {
            AzureRegion = "approved-region-1",
            DataResidencyCountry = "Approved jurisdiction",
            DataResidencyGeography = "Approved geography",
            DataResidencyMode = "regional",
            DataResidencyAllowedAzureRegions = "approved-region-1,approved-region-2",
            DataResidencyApprovalReference = "RES-TEST-001"
        };

    private static InfrastructureOptions OpenAiOptions() =>
        new()
        {
            AiProvider = "AzureOpenAI",
            AzureOpenAI = new AzureOpenAiOptions
            {
                Endpoint = "https://example-openai.openai.azure.com",
                ChatDeployment = "chat",
                EmbeddingDeployment = "embedding"
            }
        };

    private static ClaimCase Case(TenantScope scope) =>
        new()
        {
            CaseId = Guid.NewGuid(),
            Scope = scope,
            CustomerName = "Amina",
            CustomerEmail = "amina@example.test",
            CustomerPhone = "+256700000000",
            PolicyNumber = "MOT-STAGE-1001",
            ProductLine = "Motor",
            ClaimType = "Accident",
            Channel = "Portal",
            IncidentDate = DateOnly.FromDateTime(DateTime.UtcNow.Date),
            IncidentDescription = "Synthetic motor claim.",
            Status = CaseStatus.New,
            CompletenessScore = 0.5m,
            CreatedByUserId = "claims-officer",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            CorrelationId = "corr"
        };

    private sealed class StaticTokenCredential : TokenCredential
    {
        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
            new("test-token", DateTimeOffset.UtcNow.AddMinutes(10));

        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
            ValueTask.FromResult(GetToken(requestContext, cancellationToken));
    }

    private sealed class CapturingOpenAiHandler(string responseBody, System.Net.HttpStatusCode statusCode = System.Net.HttpStatusCode.OK) : HttpMessageHandler
    {
        public Uri? LastRequestUri { get; private set; }
        public string? LastAuthorizationScheme { get; private set; }
        public string? LastRequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri;
            LastAuthorizationScheme = request.Headers.Authorization?.Scheme;
            LastRequestBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(responseBody, System.Text.Encoding.UTF8, "application/json")
            };
        }
    }

    private sealed class TestEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "InsuranceIntelligence.Api.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
