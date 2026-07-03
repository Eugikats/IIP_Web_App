using System.Text.Json;
using System.Text;
using System.Security.Claims;
using InsuranceIntelligence.Api.ClientPortal;
using InsuranceIntelligence.Api.Local;
using InsuranceIntelligence.LocalTestDoubles;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Communications;
using InsuranceIntelligence.Domain.Documents;
using InsuranceIntelligence.Domain.Workflow;
using InsuranceIntelligence.Infrastructure.Configuration;
using InsuranceIntelligence.Infrastructure.Simulated;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;

namespace InsuranceIntelligence.Api.Tests;

public sealed class ClientPortalEndpointTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task GetClaim_ReturnsCustomerSafeClaimForOwningCustomer()
    {
        var caseId = Guid.NewGuid();
        var scope = Scope();
        var cases = new InMemoryCaseRepository();
        var documents = new InMemoryCaseDocumentRepository();
        var communications = new InMemoryCommunicationRepository();
        await cases.SaveAsync(Case(caseId, scope, "sarah@example.com"));
        await communications.SaveAsync(new Communication
        {
            CommunicationId = Guid.NewGuid(),
            Scope = scope,
            CaseId = caseId,
            Channel = CommunicationChannel.Email,
            Direction = "Outbound",
            MessageBody = "We received your claim and will request any missing documents.",
            SentBy = "claims-handler",
            DeliveryStatus = "Sent",
            CreatedAt = DateTimeOffset.UtcNow,
            CorrelationId = "corr-client-safe"
        });
        await communications.SaveAsync(new Communication
        {
            CommunicationId = Guid.NewGuid(),
            Scope = scope,
            CaseId = caseId,
            Channel = CommunicationChannel.Email,
            Direction = "Outbound",
            MessageBody = "Internal note: fraud flag and reserve discussion.",
            SentBy = "claims-handler",
            DeliveryStatus = "Sent",
            CreatedAt = DateTimeOffset.UtcNow,
            CorrelationId = "corr-unsafe"
        });

        var result = await ClientPortalEndpoints.GetClaim(
            caseId,
            cases,
            documents,
            new StaticDocumentChecklistProvider(new CompletenessScoringService()),
            communications,
            Context("sarah@example.com"),
            CancellationToken.None);

        var (status, body) = await ExecuteAsync<JsonElement>(result);
        var json = body.ToString();

        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.Equal(caseId, body.GetProperty("caseId").GetGuid());
        Assert.Equal("Motor", body.GetProperty("productLine").GetString());
        Assert.Contains(body.GetProperty("requiredDocuments").EnumerateArray(), item =>
            item.GetProperty("label").GetString() == "Completed claim form"
            && item.GetProperty("uploaded").GetBoolean() == false);
        Assert.Single(body.GetProperty("messages").EnumerateArray());
        Assert.DoesNotContain("fraud flag", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("reserve", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("riskLevel", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("customerEmail", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("assignedUserId", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("confidence", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetClaim_HidesClaimFromDifferentCustomer()
    {
        var caseId = Guid.NewGuid();
        var cases = new InMemoryCaseRepository();
        await cases.SaveAsync(Case(caseId, Scope(), "sarah@example.com"));

        var result = await ClientPortalEndpoints.GetClaim(
            caseId,
            cases,
            new InMemoryCaseDocumentRepository(),
            new StaticDocumentChecklistProvider(new CompletenessScoringService()),
            new InMemoryCommunicationRepository(),
            Context("other@example.com"),
            CancellationToken.None);

        var (status, body) = await ExecuteAsync<JsonElement>(result);

        Assert.Equal(StatusCodes.Status404NotFound, status);
        Assert.Equal("Claim was not found for the signed-in customer.", body.GetProperty("message").GetString());
    }

    [Fact]
    public async Task GetClaim_InProtectedRuntimeUsesAuthenticatedScopeInsteadOfBrowserHeaders()
    {
        var caseId = Guid.NewGuid();
        var authenticatedScope = TenantScope.Create("tenant-protected", "workspace-protected");
        var spoofedHeaderScope = TenantScope.Create("tenant-spoofed", "workspace-spoofed");
        var cases = new InMemoryCaseRepository();
        var documents = new InMemoryCaseDocumentRepository();
        var communications = new InMemoryCommunicationRepository();
        await cases.SaveAsync(Case(caseId, authenticatedScope, "sarah@example.com"));

        var result = await ClientPortalEndpoints.GetClaim(
            caseId,
            cases,
            documents,
            new StaticDocumentChecklistProvider(new CompletenessScoringService()),
            communications,
            ProtectedContext("sarah@example.com", authenticatedScope, spoofedHeaderScope),
            CancellationToken.None);

        var (status, body) = await ExecuteAsync<JsonElement>(result);

        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.Equal(caseId, body.GetProperty("caseId").GetGuid());
    }

    [Fact]
    public async Task UploadDocuments_ProcessesOwnedClaimAndReturnsCustomerSafeShape()
    {
        var caseId = Guid.NewGuid();
        var scope = Scope();
        var cases = new InMemoryCaseRepository();
        var documents = new InMemoryCaseDocumentRepository();
        var communications = new InMemoryCommunicationRepository();
        var audit = new InMemoryAuditRepository();
        await cases.SaveAsync(Case(caseId, scope, "sarah@example.com"));

        var result = await ClientPortalEndpoints.UploadDocuments(
            caseId,
            cases,
            documents,
            new StubDocumentStorageProvider(),
            new ClaimFormProcessingProvider(),
            new StaticDocumentChecklistProvider(new CompletenessScoringService()),
            communications,
            audit,
            UploadContext("sarah@example.com"),
            CancellationToken.None);

        var (status, body) = await ExecuteAsync<JsonElement>(result);
        var json = body.ToString();

        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.Equal(caseId, body.GetProperty("caseId").GetGuid());
        Assert.Equal(33.33m, body.GetProperty("completenessScore").GetDecimal());
        Assert.Contains(body.GetProperty("requiredDocuments").EnumerateArray(), item =>
            item.GetProperty("documentType").GetString() == "ClaimForm"
            && item.GetProperty("uploaded").GetBoolean());
        Assert.DoesNotContain("objectKey", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("extractedFields", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("confidence", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(await audit.ListForCaseAsync(caseId), auditEvent => auditEvent.ActionName == "document.uploaded");
    }

    private static ClaimCase Case(Guid caseId, TenantScope scope, string customerEmail) =>
        new()
        {
            CaseId = caseId,
            Scope = scope,
            CustomerName = "Sarah Nakato",
            CustomerEmail = customerEmail,
            CustomerPhone = "+256700000000",
            PolicyNumber = "POL-2025-10024",
            ProductLine = "Motor",
            ClaimType = "Accident",
            Channel = "Portal",
            IncidentDate = new DateOnly(2025, 5, 14),
            IncidentDescription = "Customer-safe portal test claim.",
            Status = CaseStatus.MissingDocuments,
            CompletenessScore = 33m,
            RiskLevel = "High",
            Priority = CasePriority.Normal,
            SlaDueAt = DateTimeOffset.UtcNow.AddDays(3),
            AssignedUserId = "claims-handler",
            AssignedTeam = "Claims",
            CreatedByUserId = customerEmail,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-1),
            UpdatedAt = DateTimeOffset.UtcNow,
            CorrelationId = "corr-client-portal"
        };

    private static DefaultHttpContext Context(string userId)
    {
        var context = new DefaultHttpContext { TraceIdentifier = "trace-client-portal" };
        context.Request.Headers["X-Tenant-Id"] = Scope().TenantId;
        context.Request.Headers["X-Workspace-Id"] = Scope().WorkspaceId;
        context.Request.Headers["x-demo-role"] = "Client";
        context.Request.Headers["x-demo-user-id"] = userId;
        context.RequestServices = new ServiceCollection().AddLogging().AddProblemDetails().BuildServiceProvider();
        return context;
    }

    private static DefaultHttpContext ProtectedContext(
        string userId,
        TenantScope authenticatedScope,
        TenantScope spoofedHeaderScope)
    {
        var context = new DefaultHttpContext { TraceIdentifier = "trace-client-portal-protected" };
        context.Request.Headers["X-Tenant-Id"] = spoofedHeaderScope.TenantId;
        context.Request.Headers["X-Workspace-Id"] = spoofedHeaderScope.WorkspaceId;
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim(ClaimTypes.Role, "Client"),
                new Claim("tenant_id", authenticatedScope.TenantId),
                new Claim("workspace_id", authenticatedScope.WorkspaceId)
            ],
            authenticationType: "TestAuth"));
        context.RequestServices = new ServiceCollection()
            .AddLogging()
            .AddProblemDetails()
            .AddSingleton(new InfrastructureOptions
            {
                AuthMode = "Entra",
                TenantId = authenticatedScope.TenantId,
                WorkspaceId = authenticatedScope.WorkspaceId
            })
            .BuildServiceProvider();
        return context;
    }

    private static DefaultHttpContext UploadContext(string userId)
    {
        var context = Context(userId);
        var bytes = Encoding.UTF8.GetBytes("customer claim form");
        var file = new FormFile(new MemoryStream(bytes), 0, bytes.Length, "files", "claim-form.pdf")
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/pdf"
        };
        context.Request.ContentType = "multipart/form-data; boundary=test";
        context.Request.Form = new FormCollection(new Dictionary<string, StringValues>(), new FormFileCollection { file });
        return context;
    }

    private static TenantScope Scope() => TenantScope.Create("tenant-demo", "workspace-claims");

    private static async Task<(int StatusCode, T Body)> ExecuteAsync<T>(IResult result)
    {
        var httpContext = Context("sarah@example.com");
        httpContext.Response.Body = new MemoryStream();
        await result.ExecuteAsync(httpContext);
        httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        var body = (await JsonSerializer.DeserializeAsync<T>(httpContext.Response.Body, JsonOptions))!;
        return (httpContext.Response.StatusCode, body);
    }

    private sealed class StubDocumentStorageProvider : IDocumentStorageProvider
    {
        public Task<DocumentStorageResult> StoreAsync(DocumentStorageRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(new DocumentStorageResult(
                $"{request.Scope.TenantId}/{request.Scope.WorkspaceId}/{request.CaseId}/{request.DocumentId}_{request.OriginalFileName}",
                request.ContentType,
                request.FileSizeBytes));

        public Task<Stream> ReadAsync(TenantScope scope, string objectKey, CancellationToken cancellationToken = default) =>
            Task.FromResult<Stream>(new MemoryStream(Encoding.UTF8.GetBytes("customer claim form")));
    }

    private sealed class ClaimFormProcessingProvider : IDocumentProcessingProvider
    {
        public Task<DocumentProcessingResult> ProcessAsync(DocumentProcessingRequest request, CancellationToken cancellationToken = default)
        {
            var now = DateTimeOffset.UtcNow;
            return Task.FromResult(new DocumentProcessingResult(
                new DocumentClassificationResult(DocumentType.ClaimForm, 0.99m, "ClientPortalTest", now),
                new DocumentExtractionResult(
                    new Dictionary<string, string> { ["fileName"] = request.OriginalFileName },
                    new Dictionary<string, decimal> { ["fileName"] = 0.99m },
                    "ClientPortalTest",
                    now,
                    Approved: false,
                    ApprovedByUserId: null,
                    ApprovedAt: null),
                Array.Empty<DocumentValidationWarning>()));
        }
    }
}
