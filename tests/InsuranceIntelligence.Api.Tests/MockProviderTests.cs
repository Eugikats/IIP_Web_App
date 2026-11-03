using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using InsuranceIntelligence.Api.Admin;
using InsuranceIntelligence.Api.Azure;
using InsuranceIntelligence.Api.Local;
using InsuranceIntelligence.Api.Local.Mock;
using InsuranceIntelligence.Api.Tests.Support;
using InsuranceIntelligence.Application.Ai;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Documents;
using InsuranceIntelligence.Domain.Workflow;
using InsuranceIntelligence.Infrastructure.Configuration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace InsuranceIntelligence.Api.Tests;

/// <summary>Item 1: Mock providers for local runs. They must work in a developer runtime, be
/// impossible to enable anywhere else, and never be reported as healthy.</summary>
public sealed class MockProviderTests
{
    [Theory]
    [InlineData("Development", "Mock", true)]
    [InlineData("LocalTest", "Mock", true)]
    [InlineData("Test", "mock", true)]
    [InlineData("Production", "Mock", false)]
    [InlineData("Staging", "Mock", false)]
    [InlineData("Demo", "Mock", false)]
    [InlineData("ManagedApplicationReviewer", "Mock", false)]
    [InlineData("SomethingElse", "Mock", false)]
    [InlineData("Development", "AzureOpenAI", false)]
    [InlineData("Development", "NotConfigured", false)]
    public void MockProviderPolicy_ActivatesOnlyForMockSettingInDeveloperRuntime(string profile, string setting, bool expected) =>
        Assert.Equal(expected, MockProviderPolicy.IsActive(setting, profile));

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    [InlineData("Demo")]
    [InlineData("ManagedApplicationReviewer")]
    public void ProtectedRuntimes_RefuseMockAiAndDocumentProviders(string profile)
    {
        var options = new InfrastructureOptions
        {
            RuntimeProfile = profile,
            AuthMode = "Entra",
            StorageProvider = "SqlServer",
            SqlServerConnectionString = "Server=x;Database=y",
            AiProvider = "Mock",
            DocumentProvider = "Mock",
            RetrievalProvider = "Mock"
        };

        var errors = AzureProviderStartupValidator.ValidateProduction(options, profile);

        Assert.Contains(errors, error => error.Contains("INSURANCEINTELLIGENCE_AI_PROVIDER"));
        Assert.Contains(errors, error => error.Contains("INSURANCEINTELLIGENCE_DOCUMENT_PROVIDER"));
        Assert.Contains(errors, error => error.Contains("INSURANCEINTELLIGENCE_RETRIEVAL_PROVIDER"));
    }

    [Fact]
    public void AllowProductionMocksFlag_DoesNotUnlockMocksInProtectedRuntime()
    {
        var options = new InfrastructureOptions
        {
            RuntimeProfile = "Production",
            AuthMode = "Entra",
            StorageProvider = "SqlServer",
            SqlServerConnectionString = "Server=x;Database=y",
            AiProvider = "Mock",
            AllowProductionMockProviders = true
        };

        var errors = AzureProviderStartupValidator.ValidateProduction(options, "Production");

        Assert.Contains(errors, error => error.Contains("ALLOW_PRODUCTION_MOCKS"));
        Assert.Contains(errors, error => error.Contains("INSURANCEINTELLIGENCE_AI_PROVIDER"));
    }

    [Fact]
    public async Task MockDocumentProcessing_ClassifiesFromContentMarker_AndPlantsFields()
    {
        var provider = new MockDocumentProcessingProvider();
        var content = Encoding.UTF8.GetBytes("junk IIP-MOCK-DOC:PoliceAbstract\nIIP-MOCK-FIELD:ReportNumber=ABC-123\nmore");

        var result = await provider.ProcessAsync(Request("IMG_2031.jpg", content));

        Assert.Equal(DocumentType.PoliceAbstract, result.Classification.DocumentType);
        Assert.True(result.Classification.ConfidenceScore >= 0.9m);
        Assert.Equal("ABC-123", result.Extraction.Fields["field.ReportNumber"]);
        Assert.Equal("Mock", result.Extraction.ProviderName);
        Assert.True(result.Extraction.FieldConfidences["field.ReportNumber"] > 0m);
    }

    [Fact]
    public async Task MockDocumentProcessing_IsDeterministic()
    {
        var provider = new MockDocumentProcessingProvider();
        var content = Encoding.UTF8.GetBytes("IIP-MOCK-DOC:Invoice");

        var first = await provider.ProcessAsync(Request("a.pdf", content));
        var second = await provider.ProcessAsync(Request("a.pdf", content));

        Assert.Equal(first.Classification.DocumentType, second.Classification.DocumentType);
        Assert.Equal(first.Extraction.Fields["field.InvoiceTotal"], second.Extraction.Fields["field.InvoiceTotal"]);
    }

    [Theory]
    [InlineData("police_letter.pdf", DocumentType.PoliceAbstract)]
    [InlineData("claim-form.pdf", DocumentType.ClaimForm)]
    [InlineData("national_id.jpg", DocumentType.CustomerId)]
    [InlineData("accident_report.pdf", DocumentType.Unknown)]
    [InlineData("valid.pdf", DocumentType.Unknown)]
    [InlineData("IMG_2031.jpg", DocumentType.Unknown)]
    public void MockDocumentProcessing_FileNameMatchesWholeTokensOnly(string fileName, DocumentType expected) =>
        Assert.Equal(expected, MockDocumentProcessingProvider.ClassifyFileName(fileName));

    [Fact]
    public async Task MockChatCompletion_IsLabelledDeterministicAndFreeOfDecisionOrEmDashLanguage()
    {
        var provider = new MockChatCompletionProvider();
        var prompt = "CLAIM: CLM-0042\nCustomer: Jane Doe\nProduct line: Motor | Claim type: Theft | Status: New\nCompleteness: 50%";

        var first = await provider.CompleteAsync(new ChatCompletionRequest(prompt, "t1"));
        var second = await provider.CompleteAsync(new ChatCompletionRequest(prompt, "t1"));

        Assert.Equal(first.Content, second.Content);
        Assert.StartsWith(MockChatCompletionProvider.Label, first.Content);
        Assert.Contains("CLM-0042", first.Content);
        Assert.Equal("Mock", first.Provider);
        Assert.DoesNotContain((char)0x2014, first.Content);
        Assert.DoesNotContain("approved", first.Content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MockToolCalling_ReportsClaimThenFilesItOnlyAfterYes()
    {
        var model = new MockToolCallingChatProvider();
        var tools = Array.Empty<AgentToolSpec>();
        var messages = new List<AgentChatMessage>
        {
            AgentChatMessage.System("system"),
            AgentChatMessage.User("my car was stolen last night"),
        };

        var step1 = await model.NextAsync(messages, tools, null);
        Assert.Equal("get_my_account", Assert.Single(step1.ToolCalls).Name);

        messages.Add(AgentChatMessage.Assistant(null, step1.ToolCalls));
        messages.Add(AgentChatMessage.Tool(step1.ToolCalls[0].Id, "{\"registered\":true}"));
        var step2 = await model.NextAsync(messages, tools, null);
        Assert.Empty(step2.ToolCalls);
        Assert.Contains("reply YES", step2.Content);

        // Next turn: the customer says yes after the read-back.
        var turn2 = new List<AgentChatMessage>
        {
            AgentChatMessage.System("system"),
            AgentChatMessage.User("my car was stolen last night"),
            AgentChatMessage.Assistant(step2.Content),
            AgentChatMessage.User("yes"),
        };
        var step3 = await model.NextAsync(turn2, tools, null);
        Assert.Equal("file_claim", Assert.Single(step3.ToolCalls).Name);
    }

    [Fact]
    public async Task MockToolCalling_DoesNotFileAClaimOnABareYes()
    {
        var model = new MockToolCallingChatProvider();
        var step = await model.NextAsync(
            [AgentChatMessage.System("s"), AgentChatMessage.User("yes")], [], null);

        Assert.Empty(step.ToolCalls);
    }

    [Fact]
    public void MockOutbox_IsScopedByTenantAndWorkspace()
    {
        var outbox = new MockOutbox();
        outbox.Add("tenant-a", "ws", CommunicationChannel.Email, "a@example.com", "s", "body a");
        outbox.Add("tenant-b", "ws", CommunicationChannel.Email, "b@example.com", "s", "body b");

        Assert.Single(outbox.ListFor("tenant-a", "ws"));
        Assert.Equal("a@example.com", outbox.ListFor("tenant-a", "ws")[0].Recipient);
        Assert.Empty(outbox.ListFor("tenant-a", "other-ws"));

        outbox.Clear("tenant-a", "ws");
        Assert.Empty(outbox.ListFor("tenant-a", "ws"));
        Assert.Single(outbox.ListFor("tenant-b", "ws"));
    }

    [Fact]
    public async Task LocalMockStorage_RefusesReadsOutsideTheCallersTenantPrefix()
    {
        var root = Path.Combine(Path.GetTempPath(), "iip-storage-test-" + Guid.NewGuid().ToString("N"));
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["INSURANCEINTELLIGENCE_LOCAL_DOCUMENT_STORAGE_PATH"] = root })
            .Build();
        var storage = new LocalMockDocumentStorageProvider(new FakeWebHost(), configuration);
        var owner = TenantScope.Create("tenant-a", "ws");
        var caseId = Guid.NewGuid();
        using var content = new MemoryStream(Encoding.UTF8.GetBytes("hello"));
        var stored = await storage.StoreAsync(new DocumentStorageRequest(Guid.NewGuid(), caseId, owner, "a.txt", "text/plain", 5, content));

        await using (var read = await storage.ReadAsync(owner, stored.ObjectKey))
        using (var reader = new StreamReader(read))
        {
            Assert.Equal("hello", await reader.ReadToEndAsync());
        }

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => storage.ReadAsync(TenantScope.Create("tenant-b", "ws"), stored.ObjectKey));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => storage.ReadAsync(owner, $"tenant-a/ws/../../tenant-b/ws/x"));
    }

    [Fact]
    public void ProviderHealth_ReportsMockAsMock_NeverHealthy_AndNotificationsAsNotConfigured()
    {
        var options = new InfrastructureOptions { AuthMode = "Development" };

        var health = AdminEndpoints.BuildProviderHealth(options);

        Assert.DoesNotContain(health.Providers, provider => provider.Status == "healthy");
        Assert.Equal("mock", health.Providers.Single(provider => provider.Name == "documentIntelligence").Status);
        Assert.Equal("mock", health.Providers.Single(provider => provider.Name == "azureOpenAI").Status);
        Assert.Equal("mock", health.Providers.Single(provider => provider.Name == "communications").Status);
        Assert.Equal("not_configured", health.Providers.Single(provider => provider.Name == "notifications").Status);
        Assert.Equal("degraded", health.OverallStatus);
    }

    [Fact]
    public void ProviderHealth_OverallIsMockWhenOnlyMocksAreActive()
    {
        var options = new InfrastructureOptions { AuthMode = "Development", NotificationProvider = "IncomingWebhook" };

        var health = AdminEndpoints.BuildProviderHealth(options);

        Assert.Equal("mock", health.OverallStatus);
    }

    [Fact]
    public void ProviderHealth_OutsideDevelopmentAuth_MockIsMissingNotMock()
    {
        var options = new InfrastructureOptions { AuthMode = "Entra" };

        var health = AdminEndpoints.BuildProviderHealth(options);

        Assert.DoesNotContain(health.Providers, provider => provider.Status is "mock" or "healthy");
    }

    private static DocumentProcessingRequest Request(string fileName, byte[] content) =>
        new(Guid.NewGuid(), Guid.NewGuid(), fileName, "application/octet-stream", content.Length, "k/" + fileName,
            _ => Task.FromResult<Stream>(new MemoryStream(content)));

    private sealed class FakeWebHost : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "test";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = string.Empty;
        public string EnvironmentName { get; set; } = "Development";
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}

/// <summary>Item 1, HTTP level: the real API running on Mock providers.</summary>
public sealed class MockedApiTests(MockedApiFactory factory) : IClassFixture<MockedApiFactory>
{
    [Fact]
    public async Task ProviderStatus_ReportsMocksAsMock()
    {
        using var client = factory.CreateScopedClient(MockedApiFactory.NewTenant());

        var response = await client.GetAsync("/api/v1/provider-status");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await MockedApiFactory.ReadJsonAsync(response);
        var providers = body.GetProperty("providers").EnumerateArray().ToDictionary(
            item => item.GetProperty("name").GetString()!,
            item => item.GetProperty("status").GetString()!);
        Assert.Equal("mock", providers["azureOpenAI"]);
        Assert.Equal("mock", providers["documentIntelligence"]);
        Assert.Equal("mock", providers["aiSearch"]);
        Assert.DoesNotContain("healthy", providers.Values);
    }

    [Fact]
    public async Task Upload_IsProcessedByTheMockDocumentProvider_EndToEnd()
    {
        var tenant = MockedApiFactory.NewTenant();
        using var client = factory.CreateScopedClient(tenant);
        var caseId = await CreateMotorCaseAsync(client);

        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes("scanned text IIP-MOCK-DOC:PoliceAbstract IIP-MOCK-FIELD:ReportNumber=RN-77"));
        file.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        form.Add(file, "files", "IMG_2031.txt");
        var upload = await client.PostAsync($"/api/v1/cases/{caseId}/documents", form);
        Assert.Equal(HttpStatusCode.OK, upload.StatusCode);

        var documents = await MockedApiFactory.ReadJsonAsync(await client.GetAsync($"/api/v1/cases/{caseId}/documents"));
        var document = Assert.Single(documents.EnumerateArray());
        Assert.Equal("PoliceAbstract", document.GetProperty("documentType").GetString());
        Assert.Equal("Processed", document.GetProperty("processingStatus").GetString());
        Assert.Contains(document.GetProperty("fields").EnumerateArray(), field => field.ToString().Contains("RN-77"));
    }

    [Fact]
    public async Task ClaimSummary_ReturnsLabelledMockOutput()
    {
        var tenant = MockedApiFactory.NewTenant();
        using var client = factory.CreateScopedClient(tenant);
        var caseId = await CreateMotorCaseAsync(client);

        var response = await client.PostAsync($"/api/v1/cases/{caseId}/summary", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("MOCK AI OUTPUT", body);
    }

    [Fact]
    public async Task MockOutbox_IsEmptyForAnotherTenant()
    {
        var tenantA = MockedApiFactory.NewTenant();
        var tenantB = MockedApiFactory.NewTenant();
        using var clientA = factory.CreateScopedClient(tenantA);
        using var clientB = factory.CreateScopedClient(tenantB);
        var caseId = await CreateMotorCaseAsync(clientA);

        var write = await clientA.PostAsJsonAsync($"/api/v1/cases/{caseId}/customer-updates/write", new
        {
            reasonCategory = "StatusUpdate",
            channel = "Email",
            messageBody = "Your claim is being reviewed.",
        });
        Assert.True(write.IsSuccessStatusCode, await write.Content.ReadAsStringAsync());
        var written = await MockedApiFactory.ReadJsonAsync(write);
        var updateId = written.GetProperty("updateId").GetGuid();
        if (written.GetProperty("status").GetString() == "PendingApproval")
        {
            var approve = await clientA.PostAsJsonAsync($"/api/v1/customer-updates/{updateId}/approve", new { });
            Assert.True(approve.IsSuccessStatusCode, await approve.Content.ReadAsStringAsync());
        }

        var send = await clientA.PostAsJsonAsync($"/api/v1/customer-updates/{updateId}/send", new { recipient = "customer@example.com" });
        Assert.True(send.IsSuccessStatusCode, await send.Content.ReadAsStringAsync());

        var outboxA = await MockedApiFactory.ReadJsonAsync(await clientA.GetAsync("/api/v1/dev/mock-outbox"));
        var outboxB = await MockedApiFactory.ReadJsonAsync(await clientB.GetAsync("/api/v1/dev/mock-outbox"));
        // Case intake also sends an SMS acknowledgement, so look for the email by recipient.
        var sent = Assert.Single(outboxA.EnumerateArray(), message => message.GetProperty("recipient").GetString() == "customer@example.com");
        Assert.Contains("being reviewed", sent.GetProperty("body").GetString());
        Assert.Empty(outboxB.EnumerateArray());
    }

    internal static async Task<Guid> CreateMotorCaseAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/v1/cases", new
        {
            customerName = "Test Customer",
            customerEmail = "customer@example.com",
            customerPhone = "+256700000001",
            policyNumber = "POL-" + Guid.NewGuid().ToString("N")[..6],
            productLine = "Motor",
            claimType = "Own damage",
            channel = "Phone",
            incidentDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-2)).ToString("yyyy-MM-dd"),
            incidentDescription = "Collision on the highway."
        });
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        var body = await MockedApiFactory.ReadJsonAsync(response);
        return body.GetProperty("caseId").GetGuid();
    }
}
