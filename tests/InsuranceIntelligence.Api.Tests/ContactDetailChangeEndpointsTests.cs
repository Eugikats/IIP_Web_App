using System.Text.Json;
using InsuranceIntelligence.Api.Customers;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Customers;
using InsuranceIntelligence.LocalTestDoubles;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace InsuranceIntelligence.Api.Tests;

public sealed class ContactDetailChangeEndpointsTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task ListChangeRequests_ReturnsRequestsForTheTenant()
    {
        var scope = Scope();
        var repository = new InMemoryContactDetailChangeRequestRepository();
        await repository.SaveAsync(Request(scope), CancellationToken.None);

        var result = await ContactDetailChangeEndpoints.ListChangeRequests(repository, Context(), CancellationToken.None);
        var (status, body) = await ExecuteAsync<JsonElement>(result);

        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.Single(body.EnumerateArray());
    }

    [Fact]
    public async Task AcknowledgeChangeRequest_MovesOpenToAcknowledgedAndAudits()
    {
        var scope = Scope();
        var repository = new InMemoryContactDetailChangeRequestRepository();
        var request = Request(scope);
        await repository.SaveAsync(request, CancellationToken.None);
        var audit = new InMemoryAuditRepository();

        var result = await ContactDetailChangeEndpoints.AcknowledgeChangeRequest(
            request.RequestId, repository, audit, Context(), CancellationToken.None);
        var (status, body) = await ExecuteAsync<StaffContactDetailChangeResponse>(result);

        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.Equal("Acknowledged", body!.Status);
        var auditEvents = await audit.ListAsync(scope, CancellationToken.None);
        Assert.Contains(auditEvents, auditEvent => auditEvent.ActionName == "customer.contact_change_acknowledged");
    }

    [Fact]
    public async Task ResolveChangeRequest_RecordsTheResolvingStaffMemberAndNote()
    {
        var scope = Scope();
        var repository = new InMemoryContactDetailChangeRequestRepository();
        var request = Request(scope);
        await repository.SaveAsync(request, CancellationToken.None);
        var audit = new InMemoryAuditRepository();

        var result = await ContactDetailChangeEndpoints.ResolveChangeRequest(
            request.RequestId,
            new StaffResolveContactDetailChangeBody("Confirmed by phone and updated in the policy system."),
            repository,
            audit,
            Context(),
            CancellationToken.None);
        var (status, body) = await ExecuteAsync<StaffContactDetailChangeResponse>(result);

        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.Equal("Resolved", body!.Status);
        Assert.Equal("Confirmed by phone and updated in the policy system.", body.ResolutionNote);
        Assert.NotNull(body.ResolvedByUserId);
        var auditEvents = await audit.ListAsync(scope, CancellationToken.None);
        Assert.Contains(auditEvents, auditEvent => auditEvent.ActionName == "customer.contact_change_resolved");
    }

    [Fact]
    public async Task ResolveChangeRequest_UnknownRequestId_ReturnsNotFound()
    {
        var result = await ContactDetailChangeEndpoints.ResolveChangeRequest(
            Guid.NewGuid(),
            new StaffResolveContactDetailChangeBody(null),
            new InMemoryContactDetailChangeRequestRepository(),
            new InMemoryAuditRepository(),
            Context(),
            CancellationToken.None);
        var (status, _) = await ExecuteAsync<JsonElement>(result);

        Assert.Equal(StatusCodes.Status404NotFound, status);
    }

    private static ContactDetailChangeRequest Request(TenantScope scope) =>
        ContactDetailChangeRequest.Create(
            scope, "POL-1", caseId: null, requestedByUserId: "verified@example.test",
            requestedEmail: "new-email@example.test", requestedPhone: null, note: "Changed provider.",
            correlationId: "corr-contact-change");

    private static DefaultHttpContext Context()
    {
        var context = new DefaultHttpContext { TraceIdentifier = "trace-contact-change" };
        context.Request.Headers["X-Tenant-Id"] = Scope().TenantId;
        context.Request.Headers["X-Workspace-Id"] = Scope().WorkspaceId;
        context.Request.Headers["x-demo-user-id"] = "claims-officer-1";
        context.Request.Headers["x-demo-role"] = "ClaimsOfficer";
        context.RequestServices = new ServiceCollection().AddLogging().AddProblemDetails().BuildServiceProvider();
        return context;
    }

    private static TenantScope Scope() => TenantScope.Create("tenant-demo", "workspace-claims");

    private static async Task<(int StatusCode, T? Body)> ExecuteAsync<T>(IResult result)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.RequestServices = new ServiceCollection().AddLogging().AddProblemDetails().BuildServiceProvider();
        httpContext.Response.Body = new MemoryStream();

        await result.ExecuteAsync(httpContext);

        httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        var body = httpContext.Response.Body.Length == 0
            ? default
            : await JsonSerializer.DeserializeAsync<T>(httpContext.Response.Body, JsonOptions);

        return (httpContext.Response.StatusCode, body);
    }
}
