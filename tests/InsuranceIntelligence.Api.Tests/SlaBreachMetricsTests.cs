using System.Text.Json;
using InsuranceIntelligence.Api.CommandCenter;
using InsuranceIntelligence.Api.Local;
using InsuranceIntelligence.Api.Reports;
using InsuranceIntelligence.Application.CommandCenter;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.LocalTestDoubles;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace InsuranceIntelligence.Api.Tests;

// A closed claim that breached SLA before closing was previously indistinguishable from an open
// one still breaching right now: SlaBreached counted both, so the Command Center could (and did,
// on the live dev environment) report more breached claims than open ones. Fixed by excluding
// closed claims from SlaBreached/SlaAtRisk (an operational "needs attention today" count) and
// tracking them separately in ClosedClaimsWithSlaBreach (a historical fact, never summed back in).
public sealed class SlaBreachMetricsTests
{
    private static readonly TenantScope Scope = TenantScope.Create("tenant-demo", "workspace-claims");

    [Fact]
    public async Task CommandCenterMetrics_ExcludesClosedClaimsFromOperationalBreachCount_ButTracksThemSeparately()
    {
        var dependencies = CreateDependencies();
        var now = DateTimeOffset.UtcNow;

        await dependencies.CaseRepository.SaveAsync(BuildCase("open-breached", CaseStatus.Escalated, now.AddDays(-1)), CancellationToken.None);
        await dependencies.CaseRepository.SaveAsync(BuildCase("closed-breached", CaseStatus.Closed, now.AddDays(-1)), CancellationToken.None);
        await dependencies.CaseRepository.SaveAsync(BuildCase("closed-on-track", CaseStatus.Closed, now.AddDays(5)), CancellationToken.None);
        await dependencies.CaseRepository.SaveAsync(BuildCase("open-on-track", CaseStatus.New, now.AddDays(5)), CancellationToken.None);

        var result = await CommandCenterMetricsLoader.GetMetrics(
            dependencies.CaseRepository, dependencies.DocumentRepository, dependencies.KnowledgeRepository,
            dependencies.ActionLogRepository, dependencies.ApprovalRepository, dependencies.TaskRepository,
            dependencies.CustomerUpdateRepository, dependencies.SlaCalculator, ScopedContext(), CancellationToken.None);
        var (statusCode, metrics) = await ExecuteAsync<CommandCenterMetricsResponse>(result);

        Assert.Equal(StatusCodes.Status200OK, statusCode);
        // Exactly one open claim is breached, the closed one must not be counted here, or this
        // regresses to reporting more breaches than open claims, which is what shipped live.
        Assert.Equal(1, metrics!.SlaBreached);
        Assert.Equal(1, metrics.ClosedClaimsWithSlaBreach);
        Assert.True(metrics.SlaBreached <= metrics.OpenClaims);
    }

    [Fact]
    public async Task OperationsSummary_MirrorsTheSameSlaBreachExclusion_AsCommandCenterMetrics()
    {
        var dependencies = CreateDependencies();
        var now = DateTimeOffset.UtcNow;

        await dependencies.CaseRepository.SaveAsync(BuildCase("open-breached", CaseStatus.Escalated, now.AddDays(-1)), CancellationToken.None);
        await dependencies.CaseRepository.SaveAsync(BuildCase("closed-breached-1", CaseStatus.Closed, now.AddDays(-1)), CancellationToken.None);
        await dependencies.CaseRepository.SaveAsync(BuildCase("closed-breached-2", CaseStatus.Closed, now.AddDays(-2)), CancellationToken.None);

        var result = await ReportsEndpoints.GetOperationsSummary(
            dependencies.CaseRepository, dependencies.TaskRepository, dependencies.ApprovalRepository,
            dependencies.CustomerUpdateRepository, dependencies.ActionLogRepository, dependencies.SlaCalculator,
            ScopedContext(), CancellationToken.None);
        var (statusCode, summary) = await ExecuteAsync<OperationsSummaryResponse>(result);

        Assert.Equal(StatusCodes.Status200OK, statusCode);
        Assert.Equal(1, summary!.SlaBreached);
        Assert.Equal(2, summary.ClosedClaimsWithSlaBreach);
    }

    private static ClaimCase BuildCase(string suffix, CaseStatus status, DateTimeOffset slaDueAt) => new()
    {
        CaseId = Guid.NewGuid(),
        Scope = Scope,
        CustomerName = $"Claimant {suffix}",
        PolicyNumber = $"NSA/MO/2026/{suffix}",
        ProductLine = "Motor",
        ClaimType = "Own damage",
        Channel = "StaffEntry",
        IncidentDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-5)),
        IncidentDescription = "SLA breach metric test fixture.",
        Status = status,
        CompletenessScore = 100m,
        SlaDueAt = slaDueAt,
        CreatedByUserId = "user-claims-1",
        CreatedAt = DateTimeOffset.UtcNow.AddDays(-5),
        UpdatedAt = DateTimeOffset.UtcNow.AddDays(-5),
        CorrelationId = $"corr-sla-{suffix}",
    };

    private sealed record Dependencies(
        InMemoryCaseRepository CaseRepository,
        InMemoryCaseDocumentRepository DocumentRepository,
        InMemoryKnowledgeDocumentRepository KnowledgeRepository,
        InMemoryAgentActionLogRepository ActionLogRepository,
        InMemoryApprovalRepository ApprovalRepository,
        InMemoryWorkflowTaskRepository TaskRepository,
        InMemoryCustomerUpdateRepository CustomerUpdateRepository,
        BasicSlaCalculator SlaCalculator);

    private static Dependencies CreateDependencies() => new(
        new InMemoryCaseRepository(), new InMemoryCaseDocumentRepository(), new InMemoryKnowledgeDocumentRepository(),
        new InMemoryAgentActionLogRepository(), new InMemoryApprovalRepository(), new InMemoryWorkflowTaskRepository(),
        new InMemoryCustomerUpdateRepository(), new BasicSlaCalculator());

    private static DefaultHttpContext ScopedContext()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Tenant-Id"] = Scope.TenantId;
        context.Request.Headers["X-Workspace-Id"] = Scope.WorkspaceId;
        context.Request.Headers["X-User-Id"] = "user-claims-1";
        context.Request.Headers["x-demo-user-id"] = "user-claims-1";
        context.Request.Headers["x-demo-role"] = "ClaimsSupervisor";
        return context;
    }

    private static async Task<(int? StatusCode, T? Body)> ExecuteAsync<T>(IResult result)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.RequestServices = new ServiceCollection()
            .AddLogging()
            .AddProblemDetails()
            .BuildServiceProvider();
        httpContext.Response.Body = new MemoryStream();

        await result.ExecuteAsync(httpContext);

        httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        var body = httpContext.Response.Body.Length == 0
            ? default
            : await JsonSerializer.DeserializeAsync<T>(httpContext.Response.Body, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        return (httpContext.Response.StatusCode, body);
    }
}
