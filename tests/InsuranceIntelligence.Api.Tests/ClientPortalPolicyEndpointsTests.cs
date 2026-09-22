using System.Text.Json;
using InsuranceIntelligence.Api.ClientPortal;
using InsuranceIntelligence.Domain.ClientPortal;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Customers;
using InsuranceIntelligence.Domain.Knowledge;
using InsuranceIntelligence.Domain.Policies;
using InsuranceIntelligence.LocalTestDoubles;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace InsuranceIntelligence.Api.Tests;

// Outcome L: "the customer sees their policy," "asks a question about the wording," and "requests
// a change to their contact details." Self-contained fixture, not sharing ClientPortalEndpointTests'
// larger one, to keep this slice's blast radius small.
public sealed class ClientPortalPolicyEndpointsTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task GetPolicy_ReturnsCoverDatesAndAnHonestNotConfiguredPremiumStatus()
    {
        var scope = Scope();
        var policies = new InMemoryPolicyRepository();
        var coverages = new InMemoryCoverageRepository();
        await policies.UpsertAsync(Policy(scope), CancellationToken.None);
        await coverages.UpsertAsync(
            new Coverage("cov-1", scope.TenantId, scope.WorkspaceId, "policy-1", "Own damage", "UGX 20,000,000", "UGX 100,000", "MTR-8821", "Comprehensive own-damage cover"),
            CancellationToken.None);
        var context = SessionContext(Session(scope, caseId: null));
        var audit = new InMemoryAuditRepository();

        var result = await ClientPortalEndpoints.GetPolicy(policies, coverages, context, CancellationToken.None, audit);
        var (status, body) = await ExecuteAsync<JsonElement>(result);

        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.Equal("POL-1", body.GetProperty("policyNumber").GetString());
        Assert.Equal("Active", body.GetProperty("coverStatus").GetString());
        Assert.Equal("NotConfigured", body.GetProperty("premiumStatus").GetString());
        Assert.Single(body.GetProperty("coverages").EnumerateArray());
        var auditEvents = await audit.ListAsync(scope, CancellationToken.None);
        Assert.Contains(auditEvents, auditEvent => auditEvent.ActionName == "client_portal.disclosure.viewed" && auditEvent.AfterStatus == "Policy");
    }

    [Fact]
    public async Task GetPolicy_WithoutAVerifiedSession_IsRejected()
    {
        var result = await ClientPortalEndpoints.GetPolicy(
            new InMemoryPolicyRepository(), new InMemoryCoverageRepository(), Context(), CancellationToken.None);
        var (status, _) = await ExecuteAsync<JsonElement>(result);
        Assert.Equal(StatusCodes.Status401Unauthorized, status);
    }

    [Fact]
    public async Task AskPolicyQuestion_ResolvesProductLineFromTheSessionsOwnPolicy_NotTheCaller()
    {
        var scope = Scope();
        var policies = new InMemoryPolicyRepository();
        await policies.UpsertAsync(Policy(scope), CancellationToken.None);
        var context = SessionContext(Session(scope, caseId: null));
        var actionLog = new InMemoryAgentActionLogRepository();
        var audit = new InMemoryAuditRepository();
        var capturingRetrieval = new CapturingRetrievalProvider();

        var result = await ClientPortalEndpoints.AskPolicyQuestion(
            new ClientPortalPolicyQuestionRequest("Is windscreen damage covered?"),
            policies,
            capturingRetrieval,
            new MockAiAssistantProvider(),
            new LocalTestAgentGate(),
            actionLog,
            context,
            CancellationToken.None,
            audit);
        var (status, body) = await ExecuteAsync<ClientPortalPolicyQuestionResponse>(result);

        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.NotNull(body);
        Assert.Equal("Motor", capturingRetrieval.LastQuestion?.ProductLine);
        Assert.Equal("Customer", capturingRetrieval.LastQuestion?.Audience);
        Assert.Single(await actionLog.ListAsync(scope, CancellationToken.None));
        var auditEvents = await audit.ListAsync(scope, CancellationToken.None);
        Assert.Contains(auditEvents, auditEvent => auditEvent.ActionName == "client_portal.disclosure.viewed" && auditEvent.AfterStatus == "PolicyQuestion");
    }

    [Fact]
    public async Task AskPolicyQuestion_WithoutAVerifiedSession_IsRejected()
    {
        var result = await ClientPortalEndpoints.AskPolicyQuestion(
            new ClientPortalPolicyQuestionRequest("Is windscreen damage covered?"),
            new InMemoryPolicyRepository(),
            new CapturingRetrievalProvider(),
            new MockAiAssistantProvider(),
            new LocalTestAgentGate(),
            new InMemoryAgentActionLogRepository(),
            Context(),
            CancellationToken.None);
        var (status, _) = await ExecuteAsync<JsonElement>(result);
        Assert.Equal(StatusCodes.Status401Unauthorized, status);
    }

    [Fact]
    public async Task RequestContactDetailChange_RecordsAnOpenRequestForStaff()
    {
        var scope = Scope();
        var context = SessionContext(Session(scope, caseId: null));
        var changeRequests = new InMemoryContactDetailChangeRequestRepository();
        var audit = new InMemoryAuditRepository();

        var result = await ClientPortalEndpoints.RequestContactDetailChange(
            new ClientPortalContactDetailChangeRequestBody("new-email@example.test", null, "Changed provider."),
            changeRequests,
            audit,
            context,
            CancellationToken.None);
        var (status, body) = await ExecuteAsync<ClientPortalContactDetailChangeResponse>(result);

        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.Equal("Open", body!.Status);
        var saved = Assert.Single(await changeRequests.ListAsync(scope, CancellationToken.None));
        Assert.Equal("new-email@example.test", saved.RequestedEmail);
        Assert.Equal("POL-1", saved.PolicyNumber);
        var auditEvents = await audit.ListAsync(scope, CancellationToken.None);
        Assert.Contains(auditEvents, auditEvent => auditEvent.ActionName == "customer.contact_change_requested");
    }

    [Fact]
    public async Task RequestContactDetailChange_RequiresANewEmailOrPhone()
    {
        var context = SessionContext(Session(Scope(), caseId: null));

        var result = await ClientPortalEndpoints.RequestContactDetailChange(
            new ClientPortalContactDetailChangeRequestBody(null, null, null),
            new InMemoryContactDetailChangeRequestRepository(),
            new InMemoryAuditRepository(),
            context,
            CancellationToken.None);
        var (status, _) = await ExecuteAsync<JsonElement>(result);

        Assert.Equal(StatusCodes.Status400BadRequest, status);
    }

    private static Policy Policy(TenantScope scope) =>
        new("policy-1", scope.TenantId, scope.WorkspaceId, "POL-1", "Motor", "Comprehensive", "cust-1", "branch-1",
            DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-2)), DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(10)),
            1_200_000m, "UGX", 25_000_000m);

    private static ClaimantPortalSession Session(TenantScope scope, Guid? caseId) =>
        new()
        {
            SessionId = Guid.NewGuid(),
            Scope = scope,
            CaseId = caseId,
            PolicyNumber = "POL-1",
            ContactValue = "verified@example.test",
            SessionTokenHash = "hash",
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(1),
            CreatedAt = DateTimeOffset.UtcNow,
            CorrelationId = "corr-policy-endpoints"
        };

    private static DefaultHttpContext Context()
    {
        var context = new DefaultHttpContext { TraceIdentifier = "trace-client-portal-policy" };
        context.RequestServices = new ServiceCollection().AddLogging().AddProblemDetails().BuildServiceProvider();
        return context;
    }

    private static DefaultHttpContext SessionContext(ClaimantPortalSession session)
    {
        var context = Context();
        context.Items[ClaimantSessionEndpointFilter.SessionItemKey] = session;
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

    private sealed class CapturingRetrievalProvider : IRetrievalProvider
    {
        public AssistantQuestion? LastQuestion { get; private set; }

        public Task<RetrievalResult> RetrieveAsync(AssistantQuestion question, CancellationToken cancellationToken = default)
        {
            LastQuestion = question;
            return Task.FromResult(new RetrievalResult(question.Question, question.ProductLine, Array.Empty<KnowledgeChunk>(), Array.Empty<SourceCitation>()));
        }
    }
}
