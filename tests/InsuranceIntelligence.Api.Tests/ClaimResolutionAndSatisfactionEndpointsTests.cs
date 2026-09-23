using System.Text.Json;
using InsuranceIntelligence.Api.Cases;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Workflow;
using InsuranceIntelligence.LocalTestDoubles;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace InsuranceIntelligence.Api.Tests;

/// <summary>Outcome O: "when a claim closes, ask one satisfaction question, and open a service
/// recovery task on a poor answer." Covers the new terminal-status action (no path in this
/// repository previously wrote Settled/Closed) and the satisfaction capture it triggers.</summary>
public sealed class ClaimResolutionAndSatisfactionEndpointsTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly TenantScope Scope = TenantScope.Create("tenant-demo", "workspace-claims");

    [Fact]
    public async Task Resolve_FromReadyForExport_SetsStatusAndDraftsASatisfactionQuestion()
    {
        var (caseRepository, templateRepository, updateRepository, auditRepository, taskRepository, _) = Build();
        var claimCase = await Seed(caseRepository, CaseStatus.ReadyForExport);
        var context = ScopedContext();
        context.Request.Headers["X-Audit-Reason"] = "Confirmed paid via core system export.";

        var result = await ClaimResolutionEndpoints.Resolve(
            claimCase.CaseId,
            new ResolveClaimRequest("Settled"),
            caseRepository,
            templateRepository,
            updateRepository,
            auditRepository,
            context,
            CancellationToken.None);

        var (statusCode, body) = await ExecuteAsync<JsonElement>(result);
        Assert.Equal(StatusCodes.Status200OK, statusCode);
        Assert.Equal("Settled", body.GetProperty("status").GetString());

        var saved = await caseRepository.GetAsync(claimCase.CaseId, CancellationToken.None);
        Assert.Equal(CaseStatus.Settled, saved!.Status);

        var drafts = await updateRepository.ListForCaseAsync(claimCase.CaseId, Scope, CancellationToken.None);
        var satisfactionDraft = Assert.Single(drafts);
        Assert.Equal(CustomerUpdateReasonCategory.SatisfactionQuestion, satisfactionDraft.ReasonCategory);
        Assert.True(satisfactionDraft.RequiresApproval);
        Assert.Equal(CustomerUpdateStatus.PendingApproval, satisfactionDraft.Status);

        // The default template was auto-created on first use.
        var seededTemplate = await templateRepository.GetCurrentAsync(Scope, "satisfaction-question-portal", CancellationToken.None);
        Assert.NotNull(seededTemplate);
    }

    [Fact]
    public async Task Resolve_FromAnEarlierStatus_ReturnsConflict()
    {
        var (caseRepository, templateRepository, updateRepository, auditRepository, _, _) = Build();
        var claimCase = await Seed(caseRepository, CaseStatus.InReview);
        var context = ScopedContext();
        context.Request.Headers["X-Audit-Reason"] = "Attempting early close.";

        var result = await ClaimResolutionEndpoints.Resolve(
            claimCase.CaseId,
            new ResolveClaimRequest("Closed"),
            caseRepository,
            templateRepository,
            updateRepository,
            auditRepository,
            context,
            CancellationToken.None);

        var (statusCode, _) = await ExecuteAsync<JsonElement>(result);
        Assert.Equal(StatusCodes.Status409Conflict, statusCode);

        var saved = await caseRepository.GetAsync(claimCase.CaseId, CancellationToken.None);
        Assert.Equal(CaseStatus.InReview, saved!.Status);
    }

    [Fact]
    public async Task Resolve_RequiresAuditReason()
    {
        var (caseRepository, templateRepository, updateRepository, auditRepository, _, _) = Build();
        var claimCase = await Seed(caseRepository, CaseStatus.ReadyForExport);

        var result = await ClaimResolutionEndpoints.Resolve(
            claimCase.CaseId,
            new ResolveClaimRequest("Settled"),
            caseRepository,
            templateRepository,
            updateRepository,
            auditRepository,
            ScopedContext(),
            CancellationToken.None);

        var (statusCode, body) = await ExecuteAsync<JsonElement>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, statusCode);
        Assert.True(body.GetProperty("errors").TryGetProperty("auditReason", out _));
    }

    [Fact]
    public async Task Resolve_InvalidResolutionValue_ReturnsValidationProblem()
    {
        var (caseRepository, templateRepository, updateRepository, auditRepository, _, _) = Build();
        var claimCase = await Seed(caseRepository, CaseStatus.ReadyForExport);
        var context = ScopedContext();
        context.Request.Headers["X-Audit-Reason"] = "test";

        var result = await ClaimResolutionEndpoints.Resolve(
            claimCase.CaseId,
            new ResolveClaimRequest("Paid"),
            caseRepository,
            templateRepository,
            updateRepository,
            auditRepository,
            context,
            CancellationToken.None);

        var (statusCode, body) = await ExecuteAsync<JsonElement>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, statusCode);
        Assert.True(body.GetProperty("errors").TryGetProperty("resolution", out _));
    }

    [Fact]
    public async Task RecordResponse_PoorRating_OpensAServiceRecoveryTask()
    {
        var (caseRepository, _, _, auditRepository, taskRepository, responseRepository) = Build();
        var claimCase = await Seed(caseRepository, CaseStatus.Closed);

        var result = await SatisfactionEndpoints.RecordResponse(
            claimCase.CaseId,
            new RecordSatisfactionResponseRequest("Poor", "Took too long to respond."),
            caseRepository,
            responseRepository,
            taskRepository,
            auditRepository,
            ScopedContext(),
            CancellationToken.None);

        var (statusCode, body) = await ExecuteAsync<SatisfactionResponseRecordedResponse>(result);
        Assert.Equal(StatusCodes.Status200OK, statusCode);
        Assert.NotNull(body!.ServiceRecoveryTaskId);

        var tasks = await taskRepository.ListForCaseAsync(claimCase.CaseId, Scope, CancellationToken.None);
        var task = Assert.Single(tasks);
        Assert.Equal(WorkflowTaskType.ServiceRecoveryFollowUp, task.TaskType);
        Assert.Equal(WorkflowTaskStatus.Open, task.Status);
    }

    [Fact]
    public async Task RecordResponse_GoodRating_DoesNotOpenAServiceRecoveryTask()
    {
        var (caseRepository, _, _, auditRepository, taskRepository, responseRepository) = Build();
        var claimCase = await Seed(caseRepository, CaseStatus.Closed);

        var result = await SatisfactionEndpoints.RecordResponse(
            claimCase.CaseId,
            new RecordSatisfactionResponseRequest("Good", null),
            caseRepository,
            responseRepository,
            taskRepository,
            auditRepository,
            ScopedContext(),
            CancellationToken.None);

        var (statusCode, body) = await ExecuteAsync<SatisfactionResponseRecordedResponse>(result);
        Assert.Equal(StatusCodes.Status200OK, statusCode);
        Assert.Null(body!.ServiceRecoveryTaskId);

        var tasks = await taskRepository.ListForCaseAsync(claimCase.CaseId, Scope, CancellationToken.None);
        Assert.Empty(tasks);
    }

    [Fact]
    public async Task ListResponses_ReturnsRecordedAnswers()
    {
        var (caseRepository, _, _, auditRepository, taskRepository, responseRepository) = Build();
        var claimCase = await Seed(caseRepository, CaseStatus.Closed);
        await SatisfactionEndpoints.RecordResponse(
            claimCase.CaseId,
            new RecordSatisfactionResponseRequest("Neutral", "It was fine."),
            caseRepository,
            responseRepository,
            taskRepository,
            auditRepository,
            ScopedContext(),
            CancellationToken.None);

        var result = await SatisfactionEndpoints.ListResponses(
            claimCase.CaseId,
            caseRepository,
            responseRepository,
            ScopedContext(),
            CancellationToken.None);

        var (statusCode, body) = await ExecuteAsync<SatisfactionResponseDto[]>(result);
        Assert.Equal(StatusCodes.Status200OK, statusCode);
        var response = Assert.Single(body!);
        Assert.Equal("Neutral", response.Rating);
        Assert.Equal("It was fine.", response.Notes);
    }

    private static (InMemoryCaseRepository CaseRepository, InMemoryCommunicationTemplateRepository TemplateRepository, InMemoryCustomerUpdateRepository UpdateRepository, InMemoryAuditRepository AuditRepository, InMemoryWorkflowTaskRepository TaskRepository, InMemorySatisfactionResponseRepository ResponseRepository) Build() =>
        (new InMemoryCaseRepository(), new InMemoryCommunicationTemplateRepository(), new InMemoryCustomerUpdateRepository(), new InMemoryAuditRepository(), new InMemoryWorkflowTaskRepository(), new InMemorySatisfactionResponseRepository());

    private static async Task<ClaimCase> Seed(InMemoryCaseRepository caseRepository, CaseStatus status)
    {
        var claimCase = new ClaimCase
        {
            CaseId = Guid.NewGuid(),
            Scope = Scope,
            CustomerName = "Grace Nabirye",
            PolicyNumber = "POL-3001",
            ProductLine = "Motor",
            ClaimType = "Accident",
            Channel = "Email",
            IncidentDate = DateOnly.FromDateTime(DateTime.UtcNow),
            IncidentDescription = "Rear collision.",
            Status = status,
            CompletenessScore = 100,
            RiskLevel = "Medium",
            Priority = CasePriority.Normal,
            AssignedUserId = "officer-1",
            AssignedTeam = "Claims",
            CreatedByUserId = "officer-1",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            CorrelationId = "corr-resolution-1"
        };
        await caseRepository.SaveAsync(claimCase, CancellationToken.None);
        return claimCase;
    }

    private static DefaultHttpContext ScopedContext()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Tenant-Id"] = Scope.TenantId;
        context.Request.Headers["X-Workspace-Id"] = Scope.WorkspaceId;
        context.Request.Headers["X-User-Id"] = "officer-1";
        context.Request.Headers["x-demo-user-id"] = "officer-1";
        context.Request.Headers["x-demo-role"] = "ClaimsSupervisor";
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

    private sealed class InMemoryCommunicationTemplateRepository : ICommunicationTemplateRepository
    {
        private readonly List<CommunicationTemplate> versions = [];

        public Task<CommunicationTemplate?> GetCurrentAsync(TenantScope scope, string templateKey, CancellationToken cancellationToken = default) =>
            Task.FromResult(versions
                .Where(v => v.Scope.Matches(scope) && string.Equals(v.TemplateKey, templateKey, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(v => v.Version)
                .FirstOrDefault());

        public Task<IReadOnlyCollection<CommunicationTemplate>> ListVersionsAsync(TenantScope scope, string templateKey, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<CommunicationTemplate>>(versions
                .Where(v => v.Scope.Matches(scope) && string.Equals(v.TemplateKey, templateKey, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(v => v.Version)
                .ToArray());

        public Task<IReadOnlyCollection<CommunicationTemplate>> ListCurrentForAllTemplatesAsync(TenantScope scope, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<CommunicationTemplate>>(versions
                .Where(v => v.Scope.Matches(scope))
                .GroupBy(v => v.TemplateKey, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.OrderByDescending(v => v.Version).First())
                .ToArray());

        public Task SaveAsync(CommunicationTemplate version, CancellationToken cancellationToken = default)
        {
            versions.Add(version);
            return Task.CompletedTask;
        }
    }
}
