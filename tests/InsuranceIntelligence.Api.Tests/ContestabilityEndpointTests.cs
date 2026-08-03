using System.Text.Json;
using InsuranceIntelligence.Api.Contestability;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Contestability;
using InsuranceIntelligence.LocalTestDoubles;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace InsuranceIntelligence.Api.Tests;

public sealed class ContestabilityEndpointTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task ListReviewRequests_ReturnsTenantScopedList()
    {
        var caseId = Guid.NewGuid();
        var scope = Scope();
        var cases = new InMemoryCaseRepository();
        var reviews = new InMemoryClaimReviewRequestRepository();
        await cases.SaveAsync(Case(caseId, scope));
        await reviews.SaveAsync(ClaimReviewRequest.Create(
            scope, caseId, ClaimReviewRequestOrigin.Customer, "customer-1", "Please re-check.", "corr-1"));

        var result = await ContestabilityEndpoints.ListReviewRequests(
            caseId, cases, reviews, StaffContext("officer-1"), CancellationToken.None);

        var (status, body) = await ExecuteAsync<JsonElement>(result);
        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.Single(body.EnumerateArray());
        Assert.Equal("Open", body[0].GetProperty("status").GetString());
    }

    [Fact]
    public async Task AcknowledgeReviewRequest_WhenOpen_TransitionsAndAudits()
    {
        var caseId = Guid.NewGuid();
        var scope = Scope();
        var cases = new InMemoryCaseRepository();
        var reviews = new InMemoryClaimReviewRequestRepository();
        var audit = new InMemoryAuditRepository();
        await cases.SaveAsync(Case(caseId, scope));
        var request = ClaimReviewRequest.Create(
            scope, caseId, ClaimReviewRequestOrigin.Customer, "customer-1", "Please re-check.", "corr-1");
        await reviews.SaveAsync(request);

        var result = await ContestabilityEndpoints.AcknowledgeReviewRequest(
            caseId, request.RequestId, cases, reviews, audit, StaffContext("officer-1"), CancellationToken.None);

        var (status, body) = await ExecuteAsync<JsonElement>(result);
        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.Equal("Acknowledged", body.GetProperty("status").GetString());

        var stored = await reviews.GetAsync(request.RequestId, scope, CancellationToken.None);
        Assert.Equal(ClaimReviewRequestStatus.Acknowledged, stored!.Status);

        var events = await audit.ListForCaseAsync(caseId, CancellationToken.None);
        Assert.Contains(events, e => e.ActionName == "claim.review_acknowledged");
    }

    [Fact]
    public async Task AcknowledgeReviewRequest_WhenMissing_ReturnsNotFound()
    {
        var caseId = Guid.NewGuid();
        var cases = new InMemoryCaseRepository();
        await cases.SaveAsync(Case(caseId, Scope()));

        var result = await ContestabilityEndpoints.AcknowledgeReviewRequest(
            caseId,
            Guid.NewGuid(),
            cases,
            new InMemoryClaimReviewRequestRepository(),
            new InMemoryAuditRepository(),
            StaffContext("officer-1"),
            CancellationToken.None);

        var (status, _) = await ExecuteAsync<JsonElement>(result);
        Assert.Equal(StatusCodes.Status404NotFound, status);
    }

    [Fact]
    public async Task ResolveReviewRequest_TransitionsAndAudits()
    {
        var caseId = Guid.NewGuid();
        var scope = Scope();
        var cases = new InMemoryCaseRepository();
        var reviews = new InMemoryClaimReviewRequestRepository();
        var audit = new InMemoryAuditRepository();
        await cases.SaveAsync(Case(caseId, scope));
        var request = ClaimReviewRequest.Create(
            scope, caseId, ClaimReviewRequestOrigin.Customer, "customer-1", "Please re-check.", "corr-1")
            .Acknowledge();
        await reviews.SaveAsync(request);

        var result = await ContestabilityEndpoints.ResolveReviewRequest(
            caseId,
            request.RequestId,
            new StaffResolveReviewRequestBody("Reviewed with customer; no change to outcome."),
            cases,
            reviews,
            audit,
            StaffContext("officer-1"),
            CancellationToken.None);

        var (status, body) = await ExecuteAsync<JsonElement>(result);
        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.Equal("Resolved", body.GetProperty("status").GetString());
        Assert.Equal("officer-1", body.GetProperty("resolvedByUserId").GetString());
        Assert.Equal("Reviewed with customer; no change to outcome.", body.GetProperty("resolutionNote").GetString());

        var events = await audit.ListForCaseAsync(caseId, CancellationToken.None);
        Assert.Contains(events, e => e.ActionName == "claim.review_resolved");
    }

    [Fact]
    public async Task ResolveReviewRequest_WhenAlreadyResolved_IsIdempotent()
    {
        var caseId = Guid.NewGuid();
        var scope = Scope();
        var cases = new InMemoryCaseRepository();
        var reviews = new InMemoryClaimReviewRequestRepository();
        var audit = new InMemoryAuditRepository();
        await cases.SaveAsync(Case(caseId, scope));
        var request = ClaimReviewRequest.Create(
            scope, caseId, ClaimReviewRequestOrigin.Customer, "customer-1", "Please re-check.", "corr-1")
            .Resolve("officer-1", "Done once");
        await reviews.SaveAsync(request);

        var result = await ContestabilityEndpoints.ResolveReviewRequest(
            caseId,
            request.RequestId,
            new StaffResolveReviewRequestBody("Second attempt"),
            cases,
            reviews,
            audit,
            StaffContext("officer-2"),
            CancellationToken.None);

        var (status, body) = await ExecuteAsync<JsonElement>(result);
        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.Equal("Resolved", body.GetProperty("status").GetString());
        Assert.Equal("officer-1", body.GetProperty("resolvedByUserId").GetString());
        Assert.Equal("Done once", body.GetProperty("resolutionNote").GetString());

        var events = await audit.ListForCaseAsync(caseId, CancellationToken.None);
        Assert.DoesNotContain(events, e => e.ActionName == "claim.review_resolved");
    }

    [Fact]
    public async Task AcknowledgeReviewRequest_WhenDifferentScope_ReturnsNotFound()
    {
        var caseId = Guid.NewGuid();
        var foreignScope = TenantScope.Create("tenant-other", "workspace-other");
        var cases = new InMemoryCaseRepository();
        var reviews = new InMemoryClaimReviewRequestRepository();
        await cases.SaveAsync(Case(caseId, Scope()));
        var request = ClaimReviewRequest.Create(
            foreignScope, caseId, ClaimReviewRequestOrigin.Customer, "customer-1", "Please re-check.", "corr-1");
        await reviews.SaveAsync(request);

        var result = await ContestabilityEndpoints.AcknowledgeReviewRequest(
            caseId, request.RequestId, cases, reviews, new InMemoryAuditRepository(),
            StaffContext("officer-1"), CancellationToken.None);

        var (status, _) = await ExecuteAsync<JsonElement>(result);
        Assert.Equal(StatusCodes.Status404NotFound, status);
    }

    private static TenantScope Scope() => TenantScope.Create("tenant-demo", "workspace-claims");

    private static ClaimCase Case(Guid caseId, TenantScope scope) =>
        new()
        {
            CaseId = caseId,
            Scope = scope,
            CustomerName = "Sarah Nakato",
            CustomerEmail = "sarah@example.com",
            PolicyNumber = "POL-1",
            ProductLine = "Motor",
            ClaimType = "Accident",
            Channel = "Portal",
            IncidentDate = new DateOnly(2025, 5, 14),
            IncidentDescription = "Contestability staff test claim.",
            Status = CaseStatus.InReview,
            CompletenessScore = 50m,
            CreatedByUserId = "officer",
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-1),
            UpdatedAt = DateTimeOffset.UtcNow,
            CorrelationId = "corr-contestability"
        };

    private static DefaultHttpContext StaffContext(string userId)
    {
        var context = new DefaultHttpContext { TraceIdentifier = "trace-contestability" };
        context.Request.Headers["X-Tenant-Id"] = Scope().TenantId;
        context.Request.Headers["X-Workspace-Id"] = Scope().WorkspaceId;
        context.Request.Headers["x-demo-role"] = "ClaimsOfficer";
        context.Request.Headers["x-demo-user-id"] = userId;
        context.RequestServices = new ServiceCollection().AddLogging().AddProblemDetails().BuildServiceProvider();
        return context;
    }

    private static async Task<(int StatusCode, T Body)> ExecuteAsync<T>(IResult result)
    {
        var context = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection().AddLogging().AddProblemDetails().BuildServiceProvider()
        };
        context.Response.Body = new MemoryStream();
        await result.ExecuteAsync(context);
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        var json = await reader.ReadToEndAsync();
        var body = string.IsNullOrWhiteSpace(json)
            ? default!
            : JsonSerializer.Deserialize<T>(json, JsonOptions)!;
        return (context.Response.StatusCode, body);
    }
}
