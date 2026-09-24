using System.Text.Json;
using InsuranceIntelligence.Api.ClientPortal;
using InsuranceIntelligence.LocalTestDoubles;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Communications;
using InsuranceIntelligence.Domain.Workflow;
using InsuranceIntelligence.Infrastructure.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace InsuranceIntelligence.Api.Tests;

public sealed class ClaimantAuthenticationEndpointsTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task RequestMagicLink_WhenCaseAndContactMatch_IssuesATokenAndSendsAnEmail()
    {
        var caseId = Guid.NewGuid();
        var scope = Scope();
        var cases = new InMemoryCaseRepository();
        await cases.SaveAsync(Case(caseId, scope, "sarah@example.com"));
        var tokens = new InMemoryClaimantMagicLinkTokenRepository();
        var communications = new RecordingCommunicationSendingProvider();
        var audit = new InMemoryAuditRepository();

        var result = await ClaimantAuthenticationEndpoints.RequestMagicLink(
            new ClaimantMagicLinkRequest("POL-2025-10024", "sarah@example.com"),
            cases,
            new InMemoryPolicyRepository(),
            new InMemoryCustomerRepository(),
            tokens,
            communications,
            audit,
            Options(),
            Context(),
            NullLogger<ClaimantMagicLinkLogMarker>.Instance,
            CancellationToken.None);

        var (status, _) = await ExecuteAsync<JsonElement>(result);

        Assert.Equal(StatusCodes.Status200OK, status);
        var sent = Assert.Single(communications.EmailsSent);
        Assert.Equal("sarah@example.com", sent.Recipient);
        Assert.Contains("/access/verify?token=", sent.MessageBody);
        Assert.True(sent.Approved);
        Assert.True(sent.LowRisk);

        var auditEvents = await audit.ListForCaseAsync(caseId, CancellationToken.None);
        Assert.Contains(auditEvents, e => e.ActionName == "client_portal.magic_link.requested" && e.AfterStatus == "Issued");
    }

    [Fact]
    public async Task RequestMagicLink_WhenContactDoesNotMatch_ReturnsTheSameGenericResponseAndSendsNothing()
    {
        var caseId = Guid.NewGuid();
        var scope = Scope();
        var cases = new InMemoryCaseRepository();
        await cases.SaveAsync(Case(caseId, scope, "sarah@example.com"));
        var communications = new RecordingCommunicationSendingProvider();
        var audit = new InMemoryAuditRepository();

        var matchResult = await ClaimantAuthenticationEndpoints.RequestMagicLink(
            new ClaimantMagicLinkRequest("POL-2025-10024", "sarah@example.com"),
            cases,
            new InMemoryPolicyRepository(),
            new InMemoryCustomerRepository(),
            new InMemoryClaimantMagicLinkTokenRepository(),
            communications,
            audit,
            Options(),
            Context(),
            NullLogger<ClaimantMagicLinkLogMarker>.Instance,
            CancellationToken.None);
        var mismatchResult = await ClaimantAuthenticationEndpoints.RequestMagicLink(
            new ClaimantMagicLinkRequest("POL-2025-10024", "attacker@example.com"),
            cases,
            new InMemoryPolicyRepository(),
            new InMemoryCustomerRepository(),
            new InMemoryClaimantMagicLinkTokenRepository(),
            communications,
            audit,
            Options(),
            Context(),
            NullLogger<ClaimantMagicLinkLogMarker>.Instance,
            CancellationToken.None);
        var noSuchCaseResult = await ClaimantAuthenticationEndpoints.RequestMagicLink(
            new ClaimantMagicLinkRequest("POL-DOES-NOT-EXIST", "sarah@example.com"),
            cases,
            new InMemoryPolicyRepository(),
            new InMemoryCustomerRepository(),
            new InMemoryClaimantMagicLinkTokenRepository(),
            communications,
            audit,
            Options(),
            Context(),
            NullLogger<ClaimantMagicLinkLogMarker>.Instance,
            CancellationToken.None);

        var (matchStatus, matchBody) = await ExecuteAsync<JsonElement>(matchResult);
        var (mismatchStatus, mismatchBody) = await ExecuteAsync<JsonElement>(mismatchResult);
        var (noSuchCaseStatus, noSuchCaseBody) = await ExecuteAsync<JsonElement>(noSuchCaseResult);

        Assert.Equal(StatusCodes.Status200OK, matchStatus);
        Assert.Equal(matchStatus, mismatchStatus);
        Assert.Equal(matchStatus, noSuchCaseStatus);
        Assert.Equal(matchBody.GetProperty("message").GetString(), mismatchBody.GetProperty("message").GetString());
        Assert.Equal(matchBody.GetProperty("message").GetString(), noSuchCaseBody.GetProperty("message").GetString());
        Assert.Single(communications.EmailsSent);
    }

    [Fact]
    public async Task RequestMagicLink_OutsideDevelopmentWithoutAConfiguredPortalBaseUrl_ReturnsConfigurationRequired()
    {
        var caseId = Guid.NewGuid();
        var cases = new InMemoryCaseRepository();
        await cases.SaveAsync(Case(caseId, Scope(), "sarah@example.com"));

        var result = await ClaimantAuthenticationEndpoints.RequestMagicLink(
            new ClaimantMagicLinkRequest("POL-2025-10024", "sarah@example.com"),
            cases,
            new InMemoryPolicyRepository(),
            new InMemoryCustomerRepository(),
            new InMemoryClaimantMagicLinkTokenRepository(),
            new RecordingCommunicationSendingProvider(),
            new InMemoryAuditRepository(),
            new InfrastructureOptions { AuthMode = "Entra" },
            Context(),
            NullLogger<ClaimantMagicLinkLogMarker>.Instance,
            CancellationToken.None);

        var (status, body) = await ExecuteAsync<JsonElement>(result);

        Assert.Equal(StatusCodes.Status400BadRequest, status);
        Assert.Equal("ConfigurationRequired", body.GetProperty("deliveryStatus").GetString());
    }

    [Fact]
    public async Task RedeemMagicLink_WithAValidToken_IssuesASessionAndConsumesTheToken()
    {
        var caseId = Guid.NewGuid();
        var scope = Scope();
        var cases = new InMemoryCaseRepository();
        await cases.SaveAsync(Case(caseId, scope, "sarah@example.com"));
        var tokens = new InMemoryClaimantMagicLinkTokenRepository();
        var sessions = new InMemoryClaimantPortalSessionRepository();
        var communications = new RecordingCommunicationSendingProvider();
        var audit = new InMemoryAuditRepository();

        await ClaimantAuthenticationEndpoints.RequestMagicLink(
            new ClaimantMagicLinkRequest("POL-2025-10024", "sarah@example.com"),
            cases, new InMemoryPolicyRepository(), new InMemoryCustomerRepository(), tokens, communications, audit, Options(), Context(), NullLogger<ClaimantMagicLinkLogMarker>.Instance, CancellationToken.None);
        var rawToken = ExtractToken(communications.EmailsSent.Single().MessageBody);

        var redeemResult = await ClaimantAuthenticationEndpoints.RedeemMagicLink(
            new ClaimantMagicLinkRedeemRequest(rawToken), tokens, sessions, new InMemoryPolicyRepository(), audit, Context(), CancellationToken.None);
        var (status, body) = await ExecuteAsync<ClaimantMagicLinkRedeemResponse>(redeemResult);

        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.Equal(caseId, body!.CaseId);
        Assert.False(string.IsNullOrWhiteSpace(body.SessionToken));

        var storedSessions = await sessions.GetByTokenHashAsync(
            InsuranceIntelligence.Domain.ClientPortal.ClaimantTokenGenerator.Hash(body.SessionToken), scope, CancellationToken.None);
        Assert.NotNull(storedSessions);
        Assert.Equal("sarah@example.com", storedSessions!.ContactValue);

        var auditEvents = await audit.ListForCaseAsync(caseId, CancellationToken.None);
        Assert.Contains(auditEvents, e => e.ActionName == "client_portal.session.established");

        // The token was single-use: redeeming it again must fail even though it hasn't expired yet.
        var secondRedeem = await ClaimantAuthenticationEndpoints.RedeemMagicLink(
            new ClaimantMagicLinkRedeemRequest(rawToken), tokens, sessions, new InMemoryPolicyRepository(), new InMemoryAuditRepository(), Context(), CancellationToken.None);
        var (secondStatus, _) = await ExecuteAsync<JsonElement>(secondRedeem);
        Assert.Equal(StatusCodes.Status400BadRequest, secondStatus);
    }

    [Fact]
    public async Task RedeemMagicLink_WithAnUnknownToken_IsRejected()
    {
        var result = await ClaimantAuthenticationEndpoints.RedeemMagicLink(
            new ClaimantMagicLinkRedeemRequest("not-a-real-token"),
            new InMemoryClaimantMagicLinkTokenRepository(),
            new InMemoryClaimantPortalSessionRepository(),
            new InMemoryPolicyRepository(),
            new InMemoryAuditRepository(),
            Context(),
            CancellationToken.None);

        var (status, _) = await ExecuteAsync<JsonElement>(result);

        Assert.Equal(StatusCodes.Status400BadRequest, status);
    }

    [Fact]
    public async Task RedeemMagicLink_WithAnExpiredToken_IsRejected()
    {
        var caseId = Guid.NewGuid();
        var scope = Scope();
        var tokens = new InMemoryClaimantMagicLinkTokenRepository();
        var rawToken = InsuranceIntelligence.Domain.ClientPortal.ClaimantTokenGenerator.GenerateRawToken();
        await tokens.SaveAsync(new InsuranceIntelligence.Domain.ClientPortal.ClaimantMagicLinkToken
        {
            TokenId = Guid.NewGuid(),
            Scope = scope,
            CaseId = caseId,
            PolicyNumber = "POL-2025-10024",
            ContactValue = "sarah@example.com",
            Channel = InsuranceIntelligence.Domain.ClientPortal.ClaimantContactChannel.Email,
            TokenHash = InsuranceIntelligence.Domain.ClientPortal.ClaimantTokenGenerator.Hash(rawToken),
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1),
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-16),
            CorrelationId = "corr-expired"
        });

        var result = await ClaimantAuthenticationEndpoints.RedeemMagicLink(
            new ClaimantMagicLinkRedeemRequest(rawToken),
            tokens,
            new InMemoryClaimantPortalSessionRepository(),
            new InMemoryPolicyRepository(),
            new InMemoryAuditRepository(),
            Context(),
            CancellationToken.None);

        var (status, _) = await ExecuteAsync<JsonElement>(result);

        Assert.Equal(StatusCodes.Status400BadRequest, status);
    }

    [Fact]
    public async Task RequestMagicLink_WhenPolicyholderHasNoClaimYet_IssuesANewClaimTokenAndSendsAStartMessage()
    {
        var scope = Scope();
        var cases = new InMemoryCaseRepository();
        var policies = new InMemoryPolicyRepository();
        var customers = new InMemoryCustomerRepository();
        await customers.UpsertAsync(new InsuranceIntelligence.Domain.Customers.Customer(
            "cust-1", scope.TenantId, scope.WorkspaceId, "Grace Auma", "grace@example.com", "+256711000000"));
        await policies.UpsertAsync(new InsuranceIntelligence.Domain.Policies.Policy(
            "policy-1", scope.TenantId, scope.WorkspaceId, "POL-2026-00099", "Motor", "Comprehensive", "cust-1", null, null, null));
        var tokens = new InMemoryClaimantMagicLinkTokenRepository();
        var communications = new RecordingCommunicationSendingProvider();
        var audit = new InMemoryAuditRepository();

        var result = await ClaimantAuthenticationEndpoints.RequestMagicLink(
            new ClaimantMagicLinkRequest("POL-2026-00099", "grace@example.com"),
            cases, policies, customers, tokens, communications, audit, Options(), Context(), NullLogger<ClaimantMagicLinkLogMarker>.Instance, CancellationToken.None);

        var (status, _) = await ExecuteAsync<JsonElement>(result);
        Assert.Equal(StatusCodes.Status200OK, status);

        var sent = Assert.Single(communications.EmailsSent);
        Assert.Equal("grace@example.com", sent.Recipient);
        Assert.Contains("start your claim", sent.MessageBody);

        var rawToken = ExtractToken(sent.MessageBody);
        var storedToken = await tokens.GetByTokenHashAsync(
            InsuranceIntelligence.Domain.ClientPortal.ClaimantTokenGenerator.Hash(rawToken), scope, CancellationToken.None);
        Assert.NotNull(storedToken);
        Assert.Null(storedToken!.CaseId);
        Assert.Equal("POL-2026-00099", storedToken.PolicyNumber);
    }

    [Fact]
    public async Task CreateClaim_WithANewClaimSession_CreatesTheCaseAndUpgradesTheSessionInPlace()
    {
        var scope = Scope();
        var policies = new InMemoryPolicyRepository();
        var customers = new InMemoryCustomerRepository();
        await customers.UpsertAsync(new InsuranceIntelligence.Domain.Customers.Customer(
            "cust-1", scope.TenantId, scope.WorkspaceId, "Grace Auma", "grace@example.com", "+256711000000"));
        await policies.UpsertAsync(new InsuranceIntelligence.Domain.Policies.Policy(
            "policy-1", scope.TenantId, scope.WorkspaceId, "POL-2026-00099", "Motor", "Comprehensive", "cust-1", null, null, null));
        var sessions = new InMemoryClaimantPortalSessionRepository();
        var audit = new InMemoryAuditRepository();
        var caseIntakeService = new CaseIntakeService(new InMemoryCaseRepository(), audit);

        var session = new InsuranceIntelligence.Domain.ClientPortal.ClaimantPortalSession
        {
            SessionId = Guid.NewGuid(),
            Scope = scope,
            CaseId = null,
            PolicyNumber = "POL-2026-00099",
            ContactValue = "grace@example.com",
            SessionTokenHash = "irrelevant-for-this-test",
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(1),
            CreatedAt = DateTimeOffset.UtcNow,
            CorrelationId = "corr-new-claim"
        };
        await sessions.SaveAsync(session, CancellationToken.None);
        var context = Context();
        context.Items[ClaimantSessionEndpointFilter.SessionItemKey] = session;

        var result = await ClaimantAuthenticationEndpoints.CreateClaim(
            new ClaimantCreateClaimRequest("Motor", "Accident", new DateOnly(2026, 8, 1), "Rear-ended at a junction."),
            policies,
            customers,
            caseIntakeService,
            sessions,
            audit,
            context,
            CancellationToken.None);

        var (status, body) = await ExecuteAsync<ClaimantCreateClaimResponse>(result);
        Assert.Equal(StatusCodes.Status201Created, status);
        Assert.NotEqual(Guid.Empty, body!.CaseId);

        var upgradedSession = await sessions.GetByTokenHashAsync("irrelevant-for-this-test", scope, CancellationToken.None);
        Assert.Equal(body.CaseId, upgradedSession!.CaseId);
    }

    [Fact]
    public async Task CreateClaim_WithAnExistingClaimSession_IsRejected()
    {
        var scope = Scope();
        var session = new InsuranceIntelligence.Domain.ClientPortal.ClaimantPortalSession
        {
            SessionId = Guid.NewGuid(),
            Scope = scope,
            CaseId = Guid.NewGuid(),
            PolicyNumber = "POL-2026-00099",
            ContactValue = "grace@example.com",
            SessionTokenHash = "hash",
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(1),
            CreatedAt = DateTimeOffset.UtcNow,
            CorrelationId = "corr-existing"
        };
        var context = Context();
        context.Items[ClaimantSessionEndpointFilter.SessionItemKey] = session;
        var audit = new InMemoryAuditRepository();

        var result = await ClaimantAuthenticationEndpoints.CreateClaim(
            new ClaimantCreateClaimRequest("Motor", "Accident", new DateOnly(2026, 8, 1), "Rear-ended at a junction."),
            new InMemoryPolicyRepository(),
            new InMemoryCustomerRepository(),
            new CaseIntakeService(new InMemoryCaseRepository(), audit),
            new InMemoryClaimantPortalSessionRepository(),
            audit,
            context,
            CancellationToken.None);

        var (status, _) = await ExecuteAsync<JsonElement>(result);
        Assert.Equal(StatusCodes.Status409Conflict, status);
    }

    [Fact]
    public async Task CreateClaim_WithoutAVerifiedSession_IsRejected()
    {
        var audit = new InMemoryAuditRepository();
        var result = await ClaimantAuthenticationEndpoints.CreateClaim(
            new ClaimantCreateClaimRequest("Motor", "Accident", new DateOnly(2026, 8, 1), "Rear-ended at a junction."),
            new InMemoryPolicyRepository(),
            new InMemoryCustomerRepository(),
            new CaseIntakeService(new InMemoryCaseRepository(), audit),
            new InMemoryClaimantPortalSessionRepository(),
            audit,
            Context(),
            CancellationToken.None);

        var (status, _) = await ExecuteAsync<JsonElement>(result);
        Assert.Equal(StatusCodes.Status401Unauthorized, status);
    }

    private static string ExtractToken(string messageBody)
    {
        const string marker = "token=";
        var index = messageBody.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(index >= 0, "Message body did not contain a token.");
        var start = index + marker.Length;
        var end = messageBody.IndexOfAny([' ', '\r', '\n'], start);
        return end < 0 ? messageBody[start..] : messageBody[start..end];
    }

    private static TenantScope Scope() => TenantScope.Create("tenant-demo", "workspace-claims");

    private static InfrastructureOptions Options() => new() { AuthMode = "Development", ClientPortalBaseUrl = "https://portal.example-insurer.com" };

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
            IncidentDescription = "Magic link auth test claim.",
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
            CorrelationId = "corr-magic-link"
        };

    private static DefaultHttpContext Context()
    {
        var context = new DefaultHttpContext { TraceIdentifier = "trace-magic-link" };
        context.Request.Headers["X-Tenant-Id"] = Scope().TenantId;
        context.Request.Headers["X-Workspace-Id"] = Scope().WorkspaceId;
        context.RequestServices = new ServiceCollection().AddLogging().AddProblemDetails().BuildServiceProvider();
        return context;
    }

    private static async Task<(int? StatusCode, T? Body)> ExecuteAsync<T>(IResult result)
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

    private sealed class RecordingCommunicationSendingProvider : ICommunicationSendingProvider
    {
        public List<CommunicationSendRequest> EmailsSent { get; } = [];
        public List<CommunicationSendRequest> SmsSent { get; } = [];

        public Task<CommunicationSendResult> SendEmailAsync(CommunicationSendRequest request, CancellationToken cancellationToken = default)
        {
            EmailsSent.Add(request);
            return Task.FromResult(new CommunicationSendResult("SimulatedSent", "test-message-id", null));
        }

        public Task<CommunicationSendResult> SendSmsAsync(CommunicationSendRequest request, CancellationToken cancellationToken = default)
        {
            SmsSent.Add(request);
            return Task.FromResult(new CommunicationSendResult("SimulatedSent", "test-message-id", null));
        }
    }
}
