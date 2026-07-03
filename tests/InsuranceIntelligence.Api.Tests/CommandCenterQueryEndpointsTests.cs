using System.Text.Json;
using InsuranceIntelligence.Api.CommandCenter;
using InsuranceIntelligence.Api.Local;
using InsuranceIntelligence.LocalTestDoubles;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Workflow;
using InsuranceIntelligence.Infrastructure.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace InsuranceIntelligence.Api.Tests;

public sealed class CommandCenterQueryEndpointsTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly TenantScope Scope = TenantScope.Create("tenant-demo", "workspace-claims");

    [Fact]
    public async Task DefaultBriefingReturnsGovernedBlocksAndWritesAudit()
    {
        var harness = new Harness();
        await harness.SeedIncompleteCaseAsync("Amina Okello", SlaState.Breached);
        await harness.SeedPendingApprovalAsync();

        var (statusCode, body) = await harness.RunAsync("What needs my attention today?");

        Assert.Equal(StatusCodes.Status200OK, statusCode);

        // Envelope guardrail: preparation only, never a decision trigger.
        Assert.False(body.GetProperty("canTriggerDecision").GetBoolean());
        Assert.Equal("Deterministic", body.GetProperty("modelProvider").GetString());
        Assert.Equal("command-center-query-v1", body.GetProperty("promptVersion").GetString());
        Assert.Equal("Internal", body.GetProperty("visibility").GetString());

        var types = BlockTypes(body);
        Assert.Contains("metric_pills", types);
        Assert.Contains("document_gap_summary", types);
        Assert.Contains("approval_queue", types);

        // Document gap surfaces the incomplete case by customer name.
        var gapBlock = Block(body, "document_gap_summary");
        var gapCustomers = gapBlock.GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("customer").GetString());
        Assert.Contains("Amina Okello", gapCustomers);

        // Every proposed action is draft-only: requiresHumanApproval is always true.
        foreach (var actionBlock in Blocks(body, "action_proposal"))
        {
            foreach (var action in actionBlock.GetProperty("actions").EnumerateArray())
            {
                Assert.True(action.GetProperty("requiresHumanApproval").GetBoolean());
            }
        }

        // The query is audited: an action log + audit event are written server-side.
        var actionLogId = body.GetProperty("agentActionLogId").GetGuid();
        var actionLogs = await harness.ActionLogRepository.ListAsync(Scope);
        var auditEvents = await harness.AuditRepository.ListAsync(Scope);
        Assert.Contains(actionLogs, log => log.AgentActionLogId == actionLogId && !log.CanTriggerDecision);
        Assert.Contains(auditEvents, evt => evt.ActionName == "assistant.command_center.answered");
    }

    [Fact]
    public async Task ApprovalIntentReturnsApprovalQueue()
    {
        var harness = new Harness();
        await harness.SeedIncompleteCaseAsync("Brian Mukasa", SlaState.OnTrack);
        await harness.SeedPendingApprovalAsync();

        var (statusCode, body) = await harness.RunAsync("Show me pending approvals to sign off.");

        Assert.Equal(StatusCodes.Status200OK, statusCode);
        var approvalBlock = Block(body, "approval_queue");
        Assert.NotEmpty(approvalBlock.GetProperty("items").EnumerateArray());
        Assert.All(
            approvalBlock.GetProperty("items").EnumerateArray(),
            item => Assert.Equal("/approvals", item.GetProperty("href").GetString()));
    }

    [Fact]
    public async Task MissingProviderRendersNotConfiguredBlock()
    {
        var harness = new Harness(new InfrastructureOptions { AuthMode = "Entra" });
        await harness.SeedIncompleteCaseAsync("Carol Nabirye", SlaState.OnTrack);

        var (statusCode, body) = await harness.RunAsync("What needs my attention today?");

        Assert.Equal(StatusCodes.Status200OK, statusCode);
        var notConfigured = Blocks(body, "not_configured").ToList();
        Assert.NotEmpty(notConfigured);
        Assert.All(notConfigured, block =>
            Assert.False(string.IsNullOrWhiteSpace(block.GetProperty("provider").GetString())));
    }

    [Fact]
    public async Task EmptyQuestionIsRejected()
    {
        var harness = new Harness();

        var (statusCode, _) = await harness.RunAsync("   ");

        Assert.Equal(StatusCodes.Status400BadRequest, statusCode);
    }

    private static IReadOnlyList<string> BlockTypes(JsonElement body) =>
        body.GetProperty("blocks").EnumerateArray()
            .Select(block => block.GetProperty("type").GetString()!)
            .ToList();

    private static IEnumerable<JsonElement> Blocks(JsonElement body, string type) =>
        body.GetProperty("blocks").EnumerateArray()
            .Where(block => block.GetProperty("type").GetString() == type);

    private static JsonElement Block(JsonElement body, string type) => Blocks(body, type).First();

    private enum SlaState
    {
        OnTrack,
        Breached,
    }

    private sealed class Harness
    {
        private readonly InfrastructureOptions _options;

        public Harness(InfrastructureOptions? options = null)
        {
            _options = options ?? new InfrastructureOptions { AuthMode = "Development" };
        }

        public InMemoryCaseRepository CaseRepository { get; } = new();
        public InMemoryCaseDocumentRepository DocumentRepository { get; } = new();
        public InMemoryKnowledgeDocumentRepository KnowledgeRepository { get; } = new();
        public InMemoryAgentActionLogRepository ActionLogRepository { get; } = new();
        public InMemoryApprovalRepository ApprovalRepository { get; } = new();
        public InMemoryWorkflowTaskRepository WorkflowTaskRepository { get; } = new();
        public InMemoryCustomerUpdateRepository CustomerUpdateRepository { get; } = new();
        public InMemoryAuditRepository AuditRepository { get; } = new();
        public StaticDocumentChecklistProvider ChecklistProvider { get; } = new(new CompletenessScoringService());
        public BasicSlaCalculator SlaCalculator { get; } = new();

        public async Task<ClaimCase> SeedIncompleteCaseAsync(string customer, SlaState slaState)
        {
            var now = DateTimeOffset.UtcNow;
            var slaDueAt = slaState == SlaState.Breached ? now.AddHours(-6) : now.AddDays(3);
            var claimCase = new ClaimCase
            {
                CaseId = Guid.NewGuid(),
                Scope = Scope,
                CustomerName = customer,
                PolicyNumber = "POL-1001",
                ProductLine = "Motor",
                ClaimType = "Accident",
                Channel = "WebPortal",
                IncidentDate = new DateOnly(2026, 5, 24),
                IncidentDescription = "Minor collision reported at branch intake.",
                Status = CaseStatus.MissingDocuments,
                CompletenessScore = 40m,
                SlaDueAt = slaDueAt,
                AssignedUserId = "user-claims-1",
                CreatedByUserId = "user-claims-1",
                CreatedAt = now,
                UpdatedAt = now,
                CorrelationId = "corr-query-001",
            };

            await CaseRepository.SaveAsync(claimCase);
            return claimCase;
        }

        public async Task SeedPendingApprovalAsync()
        {
            var now = DateTimeOffset.UtcNow;
            await ApprovalRepository.SaveAsync(new ApprovalRequest
            {
                ApprovalId = Guid.NewGuid(),
                Scope = Scope,
                CaseId = Guid.NewGuid(),
                ApprovalType = "SupervisorApproval",
                Status = ApprovalStatus.Pending,
                RiskLevel = "High",
                EvidenceSummary = "Supervisor review required before proceeding.",
                SourceReferences = ["case-file"],
                RequestedByUserId = "user-claims-1",
                CreatedAt = now,
                UpdatedAt = now,
                CorrelationId = "corr-query-001",
            });
        }

        public async Task<(int? StatusCode, JsonElement Body)> RunAsync(string question)
        {
            var result = await CommandCenterQueryEndpoints.RunQuery(
                new CommandCenterQueryRequest(question),
                _options,
                CaseRepository,
                DocumentRepository,
                ChecklistProvider,
                KnowledgeRepository,
                ActionLogRepository,
                ApprovalRepository,
                WorkflowTaskRepository,
                CustomerUpdateRepository,
                AuditRepository,
                SlaCalculator,
                ScopedContext(),
                CancellationToken.None);

            return await ExecuteAsync(result);
        }

        private static DefaultHttpContext ScopedContext()
        {
            var context = new DefaultHttpContext { TraceIdentifier = "trace-query-001" };
            context.Request.Headers["X-Tenant-Id"] = "tenant-demo";
            context.Request.Headers["X-Workspace-Id"] = "workspace-claims";
            context.Request.Headers["X-User-Id"] = "user-claims-1";
            context.Request.Headers["x-demo-user-id"] = "user-claims-1";
            context.Request.Headers["x-demo-role"] = "ClaimsSupervisor";
            context.Request.Headers["X-Correlation-ID"] = "corr-query-001";
            return context;
        }

        private static async Task<(int? StatusCode, JsonElement Body)> ExecuteAsync(IResult result)
        {
            var httpContext = new DefaultHttpContext
            {
                RequestServices = new ServiceCollection()
                    .AddLogging()
                    .AddProblemDetails()
                    .BuildServiceProvider(),
            };
            httpContext.Response.Body = new MemoryStream();

            await result.ExecuteAsync(httpContext);

            httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
            var body = httpContext.Response.Body.Length == 0
                ? default
                : (await JsonSerializer.DeserializeAsync<JsonElement>(httpContext.Response.Body, JsonOptions));

            return (httpContext.Response.StatusCode, body);
        }
    }
}
