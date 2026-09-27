using System.Text.Json;
using System.Text;
using System.Security.Claims;
using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Application.Notifications;
using InsuranceIntelligence.Api.ClientPortal;
using InsuranceIntelligence.Api.Local;
using InsuranceIntelligence.LocalTestDoubles;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Communications;
using InsuranceIntelligence.Domain.Documents;
using InsuranceIntelligence.Domain.Workflow;
using InsuranceIntelligence.Infrastructure.Configuration;
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
            new InMemoryClaimReviewRequestRepository(),
            DevelopmentOptions(),
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
            new InMemoryClaimReviewRequestRepository(),
            DevelopmentOptions(),
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

        var protectedOptions = new InfrastructureOptions
        {
            AuthMode = "Entra",
            TenantId = authenticatedScope.TenantId,
            WorkspaceId = authenticatedScope.WorkspaceId
        };
        var result = await ClientPortalEndpoints.GetClaim(
            caseId,
            cases,
            documents,
            new StaticDocumentChecklistProvider(new CompletenessScoringService()),
            communications,
            new InMemoryClaimReviewRequestRepository(),
            protectedOptions,
            ProtectedContext("sarah@example.com", authenticatedScope, spoofedHeaderScope, protectedOptions),
            CancellationToken.None);

        var (status, body) = await ExecuteAsync<JsonElement>(result);

        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.Equal(caseId, body.GetProperty("caseId").GetGuid());
    }

    [Fact]
    public async Task GetClaim_InProtectedRuntimeWithoutAVerifiedIdentityIsRejectedEvenWithMatchingHeaders()
    {
        var caseId = Guid.NewGuid();
        var scope = TenantScope.Create("tenant-protected", "workspace-protected");
        var cases = new InMemoryCaseRepository();
        await cases.SaveAsync(Case(caseId, scope, "sarah@example.com"));

        // An unauthenticated request (no JwtBearer principal, no ITenantContextAccessor, no
        // magic-link session) that merely sends the right headers must never be treated as the
        // claimant, even outside Development, this is the gap magic-link auth closes.
        var context = new DefaultHttpContext { TraceIdentifier = "trace-client-portal-unauth" };
        context.Request.Headers["X-Tenant-Id"] = scope.TenantId;
        context.Request.Headers["X-Workspace-Id"] = scope.WorkspaceId;
        context.Request.Headers["X-User-Id"] = "sarah@example.com";
        context.Request.Headers["x-demo-user-id"] = "sarah@example.com";
        var options = new InfrastructureOptions { AuthMode = "Entra", TenantId = scope.TenantId, WorkspaceId = scope.WorkspaceId };
        context.RequestServices = new ServiceCollection().AddLogging().AddProblemDetails().AddSingleton(options).BuildServiceProvider();

        var result = await ClientPortalEndpoints.GetClaim(
            caseId,
            cases,
            new InMemoryCaseDocumentRepository(),
            new StaticDocumentChecklistProvider(new CompletenessScoringService()),
            new InMemoryCommunicationRepository(),
            new InMemoryClaimReviewRequestRepository(),
            options,
            context,
            CancellationToken.None);

        var (status, _) = await ExecuteAsync<JsonElement>(result);

        Assert.Equal(StatusCodes.Status404NotFound, status);
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
            new InMemoryDocumentFieldValidationRuleRepository(),
            communications,
            audit,
            DevelopmentOptions(),
            UploadContext("sarah@example.com"),
            CancellationToken.None);

        var (status, body) = await ExecuteAsync<JsonElement>(result);
        var json = body.ToString();

        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.Equal(caseId, body.GetProperty("caseId").GetGuid());
        Assert.Equal(33.33m, body.GetProperty("completenessScore").GetDecimal());
        Assert.Contains(body.GetProperty("requiredDocuments").EnumerateArray(), item =>
            item.GetProperty("documentTypeKey").GetString() == "ClaimForm"
            && item.GetProperty("uploaded").GetBoolean());
        Assert.DoesNotContain("objectKey", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("extractedFields", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("confidence", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(await audit.ListForCaseAsync(caseId), auditEvent => auditEvent.ActionName == "document.uploaded");
    }

    [Fact]
    public async Task UploadDocuments_UsesResolvedMembershipContextInsteadOfBrowserAuthorityHeaders()
    {
        var caseId = Guid.NewGuid();
        var membershipScope = TenantScope.Create("tenant-membership", "workspace-membership");
        var spoofedScope = TenantScope.Create("tenant-spoofed", "workspace-spoofed");
        var cases = new InMemoryCaseRepository();
        var documents = new InMemoryCaseDocumentRepository();
        var communications = new InMemoryCommunicationRepository();
        var audit = new InMemoryAuditRepository();
        await cases.SaveAsync(Case(caseId, membershipScope, "sarah@example.com"));

        var result = await ClientPortalEndpoints.UploadDocuments(
            caseId,
            cases,
            documents,
            new StubDocumentStorageProvider(),
            new ClaimFormProcessingProvider(),
            new StaticDocumentChecklistProvider(new CompletenessScoringService()),
            new InMemoryDocumentFieldValidationRuleRepository(),
            communications,
            audit,
            DevelopmentOptions(),
            UploadContext(
                "sarah@example.com",
                context => ApplyMembershipScope(context, membershipScope, spoofedScope, "sarah@example.com", "spoofed-user")),
            CancellationToken.None);

        var (status, body) = await ExecuteAsync<JsonElement>(result);
        var membershipDocuments = await documents.ListForCaseAsync(caseId, membershipScope);
        var spoofedDocuments = await documents.ListForCaseAsync(caseId, spoofedScope);
        var auditEvents = await audit.ListAsync(membershipScope, CancellationToken.None);

        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.Equal(caseId, body.GetProperty("caseId").GetGuid());
        var uploadedDocument = Assert.Single(membershipDocuments);
        Assert.Empty(spoofedDocuments);
        Assert.Equal(membershipScope, uploadedDocument.Scope);
        Assert.Equal("sarah@example.com", uploadedDocument.UploadedByUserId);
        Assert.Contains(
            auditEvents,
            auditEvent => auditEvent.ActionName == "document.uploaded"
                && auditEvent.Scope == membershipScope);
    }

    [Fact]
    public async Task UploadDocuments_WithADocumentTypeKey_SatisfiesThatRowEvenWhenTheClassifierDisagrees()
    {
        // Claim intake build prompt outcome B: the customer's choice decides which requirement is
        // satisfied; a disagreement with the classifier's own guess is flagged, never shown to the
        // customer as an error. ClaimFormProcessingProvider always classifies as ClaimForm, so
        // choosing PoliceAbstract here deliberately disagrees with it.
        var caseId = Guid.NewGuid();
        var scope = Scope();
        var cases = new InMemoryCaseRepository();
        var documents = new InMemoryCaseDocumentRepository();
        await cases.SaveAsync(Case(caseId, scope, "sarah@example.com"));

        var result = await ClientPortalEndpoints.UploadDocuments(
            caseId,
            cases,
            documents,
            new StubDocumentStorageProvider(),
            new ClaimFormProcessingProvider(),
            new StaticDocumentChecklistProvider(new CompletenessScoringService()),
            new InMemoryDocumentFieldValidationRuleRepository(),
            new InMemoryCommunicationRepository(),
            new InMemoryAuditRepository(),
            DevelopmentOptions(),
            UploadContext("sarah@example.com", documentTypeKey: "PoliceAbstract"),
            CancellationToken.None);

        var (status, body) = await ExecuteAsync<JsonElement>(result);

        Assert.Equal(StatusCodes.Status200OK, status);
        var row = Assert.Single(body.GetProperty("requiredDocuments").EnumerateArray(),
            item => item.GetProperty("documentTypeKey").GetString() == "PoliceAbstract");
        Assert.True(row.GetProperty("uploaded").GetBoolean());
        Assert.Equal("Received", row.GetProperty("status").GetString());
        Assert.Equal("claim-form.pdf", row.GetProperty("fileName").GetString());
        Assert.True(row.GetProperty("disagreesWithClassifier").GetBoolean());

        var stored = Assert.Single(await documents.ListForCaseAsync(caseId, scope));
        Assert.Equal("PoliceAbstract", stored.DocumentTypeKey);
        Assert.Equal(DocumentType.ClaimForm, stored.Classification.DocumentType);
    }

    [Fact]
    public async Task UploadDocuments_ReplacingADocument_SupersedesTheOldOneButNeverDeletesIt()
    {
        var caseId = Guid.NewGuid();
        var scope = Scope();
        var cases = new InMemoryCaseRepository();
        var documents = new InMemoryCaseDocumentRepository();
        await cases.SaveAsync(Case(caseId, scope, "sarah@example.com"));

        await ClientPortalEndpoints.UploadDocuments(
            caseId, cases, documents, new StubDocumentStorageProvider(), new ClaimFormProcessingProvider(),
            new StaticDocumentChecklistProvider(new CompletenessScoringService()), new InMemoryDocumentFieldValidationRuleRepository(),
            new InMemoryCommunicationRepository(), new InMemoryAuditRepository(), DevelopmentOptions(),
            UploadContext("sarah@example.com", documentTypeKey: "ClaimForm", fileName: "first-attempt.pdf"),
            CancellationToken.None);

        var second = await ClientPortalEndpoints.UploadDocuments(
            caseId, cases, documents, new StubDocumentStorageProvider(), new ClaimFormProcessingProvider(),
            new StaticDocumentChecklistProvider(new CompletenessScoringService()), new InMemoryDocumentFieldValidationRuleRepository(),
            new InMemoryCommunicationRepository(), new InMemoryAuditRepository(), DevelopmentOptions(),
            UploadContext("sarah@example.com", documentTypeKey: "ClaimForm", fileName: "corrected-copy.pdf"),
            CancellationToken.None);

        var (_, body) = await ExecuteAsync<JsonElement>(second);
        var row = Assert.Single(body.GetProperty("requiredDocuments").EnumerateArray(),
            item => item.GetProperty("documentTypeKey").GetString() == "ClaimForm");
        Assert.Equal("corrected-copy.pdf", row.GetProperty("fileName").GetString());

        var stored = await documents.ListForCaseAsync(caseId, scope);
        Assert.Equal(2, stored.Count);
        Assert.Single(stored, document => document.IsSuperseded && document.OriginalFileName == "first-attempt.pdf");
        Assert.Single(stored, document => !document.IsSuperseded && document.OriginalFileName == "corrected-copy.pdf");
    }

    [Fact]
    public async Task RemoveDocument_MarksTheActiveDocumentSupersededWithoutDeletingIt()
    {
        var caseId = Guid.NewGuid();
        var scope = Scope();
        var cases = new InMemoryCaseRepository();
        var documents = new InMemoryCaseDocumentRepository();
        await cases.SaveAsync(Case(caseId, scope, "sarah@example.com"));
        await ClientPortalEndpoints.UploadDocuments(
            caseId, cases, documents, new StubDocumentStorageProvider(), new ClaimFormProcessingProvider(),
            new StaticDocumentChecklistProvider(new CompletenessScoringService()), new InMemoryDocumentFieldValidationRuleRepository(),
            new InMemoryCommunicationRepository(), new InMemoryAuditRepository(), DevelopmentOptions(),
            UploadContext("sarah@example.com", documentTypeKey: "ClaimForm"),
            CancellationToken.None);
        var audit = new InMemoryAuditRepository();

        var result = await ClientPortalEndpoints.RemoveDocument(
            caseId,
            new ClientPortalRemoveDocumentRequest("ClaimForm"),
            cases,
            documents,
            new StaticDocumentChecklistProvider(new CompletenessScoringService()),
            new InMemoryCommunicationRepository(),
            audit,
            DevelopmentOptions(),
            Context("sarah@example.com"),
            CancellationToken.None);

        var (status, body) = await ExecuteAsync<JsonElement>(result);

        Assert.Equal(StatusCodes.Status200OK, status);
        var row = Assert.Single(body.GetProperty("requiredDocuments").EnumerateArray(),
            item => item.GetProperty("documentTypeKey").GetString() == "ClaimForm");
        Assert.False(row.GetProperty("uploaded").GetBoolean());
        Assert.Equal("Not uploaded", row.GetProperty("status").GetString());

        var stored = Assert.Single(await documents.ListForCaseAsync(caseId, scope));
        Assert.True(stored.IsSuperseded);
        Assert.Contains(await audit.ListForCaseAsync(caseId), auditEvent => auditEvent.ActionName == "document.superseded");
    }

    [Fact]
    public async Task RemoveDocument_WhenNothingWasEverSentForThatKey_ReturnsNotFound()
    {
        var caseId = Guid.NewGuid();
        var scope = Scope();
        var cases = new InMemoryCaseRepository();
        await cases.SaveAsync(Case(caseId, scope, "sarah@example.com"));

        var result = await ClientPortalEndpoints.RemoveDocument(
            caseId,
            new ClientPortalRemoveDocumentRequest("ClaimForm"),
            cases,
            new InMemoryCaseDocumentRepository(),
            new StaticDocumentChecklistProvider(new CompletenessScoringService()),
            new InMemoryCommunicationRepository(),
            new InMemoryAuditRepository(),
            DevelopmentOptions(),
            Context("sarah@example.com"),
            CancellationToken.None);

        var (status, _) = await ExecuteAsync<JsonElement>(result);
        Assert.Equal(StatusCodes.Status404NotFound, status);
    }

    [Fact]
    public async Task RequestReview_WhenOwnerAndReasonProvided_LogsRequestAndAudit()
    {
        var caseId = Guid.NewGuid();
        var scope = Scope();
        var cases = new InMemoryCaseRepository();
        await cases.SaveAsync(Case(caseId, scope, "sarah@example.com"));
        var reviewRequests = new InMemoryClaimReviewRequestRepository();
        var audit = new InMemoryAuditRepository();

        var result = await ClientPortalEndpoints.RequestReview(
            caseId,
            new ClientPortalReviewRequestBody("The payout does not reflect my repair costs."),
            cases,
            reviewRequests,
            audit,
            new NotificationService(new TenantContextAccessor(), new InMemoryNotificationRepository()),
            DevelopmentOptions(),
            Context("sarah@example.com"),
            CancellationToken.None);

        var (status, body) = await ExecuteAsync<JsonElement>(result);
        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.Equal("Open", body.GetProperty("status").GetString());

        var stored = await reviewRequests.ListForCaseAsync(caseId, scope, CancellationToken.None);
        Assert.Single(stored);
        Assert.Equal("The payout does not reflect my repair costs.", stored.First().Reason);

        var auditEvents = await audit.ListForCaseAsync(caseId, CancellationToken.None);
        Assert.Contains(auditEvents, auditEvent => auditEvent.ActionName == "claim.review_requested");
    }

    [Fact]
    public async Task RequestReview_WhenNotOwner_ReturnsNotFound()
    {
        var caseId = Guid.NewGuid();
        var cases = new InMemoryCaseRepository();
        await cases.SaveAsync(Case(caseId, Scope(), "sarah@example.com"));

        var result = await ClientPortalEndpoints.RequestReview(
            caseId,
            new ClientPortalReviewRequestBody("Please re-check my claim."),
            cases,
            new InMemoryClaimReviewRequestRepository(),
            new InMemoryAuditRepository(),
            new NotificationService(new TenantContextAccessor(), new InMemoryNotificationRepository()),
            DevelopmentOptions(),
            Context("intruder@example.com"),
            CancellationToken.None);

        var (status, _) = await ExecuteAsync<JsonElement>(result);
        Assert.Equal(StatusCodes.Status404NotFound, status);
    }

    [Fact]
    public async Task RequestReview_WhenReasonMissing_ReturnsValidationProblem()
    {
        var caseId = Guid.NewGuid();
        var cases = new InMemoryCaseRepository();
        await cases.SaveAsync(Case(caseId, Scope(), "sarah@example.com"));

        var result = await ClientPortalEndpoints.RequestReview(
            caseId,
            new ClientPortalReviewRequestBody("   "),
            cases,
            new InMemoryClaimReviewRequestRepository(),
            new InMemoryAuditRepository(),
            new NotificationService(new TenantContextAccessor(), new InMemoryNotificationRepository()),
            DevelopmentOptions(),
            Context("sarah@example.com"),
            CancellationToken.None);

        var (status, _) = await ExecuteAsync<JsonElement>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, status);
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

    private static InfrastructureOptions DevelopmentOptions() => new();

    private static DefaultHttpContext ProtectedContext(
        string userId,
        TenantScope authenticatedScope,
        TenantScope spoofedHeaderScope,
        InfrastructureOptions options)
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
            .AddSingleton(options)
            .BuildServiceProvider();
        return context;
    }

    private static DefaultHttpContext UploadContext(
        string userId,
        Action<DefaultHttpContext>? configure = null,
        string? documentTypeKey = null,
        string fileName = "claim-form.pdf")
    {
        var context = Context(userId);
        var bytes = Encoding.UTF8.GetBytes("customer claim form");
        var file = new FormFile(new MemoryStream(bytes), 0, bytes.Length, "files", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/pdf"
        };
        var fields = documentTypeKey is null
            ? new Dictionary<string, StringValues>()
            : new Dictionary<string, StringValues> { ["documentTypeKey"] = documentTypeKey };
        context.Request.ContentType = "multipart/form-data; boundary=test";
        context.Request.Form = new FormCollection(fields, new FormFileCollection { file });
        configure?.Invoke(context);
        return context;
    }

    private static void ApplyMembershipScope(
        DefaultHttpContext context,
        TenantScope membershipScope,
        TenantScope spoofedScope,
        string membershipUserId,
        string spoofedUserId)
    {
        var tenantContext = new TenantContextAccessor();
        tenantContext.SetCurrent(new TenantContext(
            membershipScope.TenantId,
            membershipScope.WorkspaceId,
            membershipUserId,
            ["Client"]));

        context.RequestServices = new ServiceCollection()
            .AddSingleton<ITenantContextAccessor>(tenantContext)
            .AddLogging()
            .AddProblemDetails()
            .BuildServiceProvider();
        context.Request.Headers["X-Tenant-Id"] = spoofedScope.TenantId;
        context.Request.Headers["X-Workspace-Id"] = spoofedScope.WorkspaceId;
        context.Request.Headers["X-User-Id"] = spoofedUserId;
        context.Request.Headers["x-demo-user-id"] = spoofedUserId;
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
