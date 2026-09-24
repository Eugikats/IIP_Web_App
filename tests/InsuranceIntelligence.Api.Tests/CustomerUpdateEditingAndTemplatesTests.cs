using System.Text.Json;
using InsuranceIntelligence.Api.Local;
using InsuranceIntelligence.Api.Workflow;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Documents;
using InsuranceIntelligence.Domain.Workflow;
using InsuranceIntelligence.LocalTestDoubles;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace InsuranceIntelligence.Api.Tests;

public sealed class CustomerUpdateEditingAndTemplatesTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task WriteCustomerUpdate_CreatesAManuallyWrittenDraft_WithNoModelInvolved()
    {
        var deps = CreateDependencies();
        var claimCase = await CreateCaseAsync(deps);

        var result = await CustomerUpdateEndpoints.WriteCustomerUpdate(
            claimCase.CaseId,
            new WriteCustomerUpdateRequest("StatusUpdate", "Portal", "We are still reviewing your file.", null),
            deps.CaseRepository,
            deps.CustomerUpdateRepository,
            deps.WorkflowRuleProvider,
            deps.AuditRepository,
            ScopedContext(),
            CancellationToken.None);
        var (statusCode, body) = await ExecuteAsync<CustomerUpdateResponse>(result);

        Assert.Equal(StatusCodes.Status201Created, statusCode);
        Assert.Equal("ManuallyWritten", body!.CreationMethod);
        Assert.Null(body.OriginalMessageBody);
        Assert.Equal("We are still reviewing your file.", body.MessageBody);
        Assert.Equal("Draft", body.Status);
    }

    [Fact]
    public async Task WriteCustomerUpdate_EscalationReason_StillRequiresApproval()
    {
        // Outcome D: a human-written message is not exempt from the same approval rule an AI
        // draft would face for a sensitive reason category.
        var deps = CreateDependencies();
        var claimCase = await CreateCaseAsync(deps);

        var result = await CustomerUpdateEndpoints.WriteCustomerUpdate(
            claimCase.CaseId,
            new WriteCustomerUpdateRequest("EscalationReceived", "Portal", "Your case has been escalated.", null),
            deps.CaseRepository,
            deps.CustomerUpdateRepository,
            deps.WorkflowRuleProvider,
            deps.AuditRepository,
            ScopedContext(),
            CancellationToken.None);
        var (_, body) = await ExecuteAsync<CustomerUpdateResponse>(result);

        Assert.True(body!.RequiresApproval);
        Assert.Equal("PendingApproval", body.Status);
    }

    [Fact]
    public async Task EditCustomerUpdate_KeepsTheOriginalAiTextOnFirstEdit()
    {
        var deps = CreateDependencies();
        var claimCase = await CreateCaseAsync(deps);
        var draft = await SaveDraftAsync(deps, claimCase.CaseId, CustomerUpdateStatus.Draft, CustomerUpdateCreationMethod.AiDrafted, "Original AI text.");

        var result = await CustomerUpdateEndpoints.EditCustomerUpdate(
            draft.UpdateId,
            new EditCustomerUpdateRequest("Edited by an officer.", null),
            deps.CaseRepository,
            deps.CustomerUpdateRepository,
            deps.AuditRepository,
            ScopedContext(auditReason: "Correcting the policy number mentioned."),
            CancellationToken.None);
        var (statusCode, body) = await ExecuteAsync<CustomerUpdateResponse>(result);

        Assert.Equal(StatusCodes.Status200OK, statusCode);
        Assert.Equal("Edited by an officer.", body!.MessageBody);
        Assert.Equal("Original AI text.", body.OriginalMessageBody);
        Assert.Equal("user-claims-1", body.EditedByUserId);
        Assert.NotNull(body.EditedAt);
    }

    [Fact]
    public async Task EditCustomerUpdate_OnAnApprovedDraft_ReturnsItToPendingApproval()
    {
        var deps = CreateDependencies();
        var claimCase = await CreateCaseAsync(deps);
        var draft = await SaveDraftAsync(deps, claimCase.CaseId, CustomerUpdateStatus.Approved, CustomerUpdateCreationMethod.AiDrafted, "Approved text.");

        var result = await CustomerUpdateEndpoints.EditCustomerUpdate(
            draft.UpdateId,
            new EditCustomerUpdateRequest("Changed after approval.", null),
            deps.CaseRepository,
            deps.CustomerUpdateRepository,
            deps.AuditRepository,
            ScopedContext(auditReason: "Fixing a typo after approval."),
            CancellationToken.None);
        var (_, body) = await ExecuteAsync<CustomerUpdateResponse>(result);

        Assert.Equal("PendingApproval", body!.Status);
    }

    [Fact]
    public async Task EditCustomerUpdate_ApproveInSameStep_StaysApproved_AndIsAuditedAsBoth()
    {
        var deps = CreateDependencies();
        var claimCase = await CreateCaseAsync(deps);
        var draft = await SaveDraftAsync(deps, claimCase.CaseId, CustomerUpdateStatus.Approved, CustomerUpdateCreationMethod.AiDrafted, "Approved text.");

        var result = await CustomerUpdateEndpoints.EditCustomerUpdate(
            draft.UpdateId,
            new EditCustomerUpdateRequest("Changed and approved in one step.", null, ApproveInSameStep: true),
            deps.CaseRepository,
            deps.CustomerUpdateRepository,
            deps.AuditRepository,
            ScopedContext(auditReason: "Correcting and approving together."),
            CancellationToken.None);
        var (_, body) = await ExecuteAsync<CustomerUpdateResponse>(result);

        Assert.Equal("Approved", body!.Status);
        var scope = TenantScope.Create("tenant-demo", "workspace-claims");
        var events = await deps.AuditRepository.ListAsync(scope);
        var auditEvent = Assert.Single(events, e => e.ActionName == "customer_update.edited_and_approved");
        Assert.Equal("user-claims-1", auditEvent.UserId);
    }

    [Fact]
    public async Task EditCustomerUpdate_RefusesToEditASentMessage()
    {
        var deps = CreateDependencies();
        var claimCase = await CreateCaseAsync(deps);
        var draft = await SaveDraftAsync(deps, claimCase.CaseId, CustomerUpdateStatus.Sent, CustomerUpdateCreationMethod.AiDrafted, "Already sent.");

        var result = await CustomerUpdateEndpoints.EditCustomerUpdate(
            draft.UpdateId,
            new EditCustomerUpdateRequest("Trying to change a sent message.", null),
            deps.CaseRepository,
            deps.CustomerUpdateRepository,
            deps.AuditRepository,
            ScopedContext(auditReason: "Attempt."),
            CancellationToken.None);
        var (statusCode, body) = await ExecuteAsync<JsonElement>(result);

        Assert.Equal(StatusCodes.Status409Conflict, statusCode);
        Assert.Contains("never be edited", body.GetProperty("message").GetString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateFromTemplate_RendersVariables_AndInheritsTheTemplateApprovalRule()
    {
        var deps = CreateDependencies();
        var claimCase = await CreateCaseAsync(deps);
        var template = await SeedTemplateAsync(deps, "missing-documents-portal", CustomerUpdateReasonCategory.MissingDocuments, CommunicationChannel.Portal);

        var result = await CustomerUpdateEndpoints.CreateCustomerUpdateFromTemplate(
            claimCase.CaseId,
            new CreateCustomerUpdateFromTemplateRequest(template.TemplateKey, null),
            deps.CaseRepository,
            deps.DocumentRepository,
            deps.ChecklistProvider,
            deps.TemplateRepository,
            deps.CustomerUpdateRepository,
            deps.WorkflowRuleProvider,
            deps.AuditRepository,
            ScopedContext(),
            CancellationToken.None);
        var (statusCode, body) = await ExecuteAsync<CustomerUpdateResponse>(result);

        Assert.Equal(StatusCodes.Status201Created, statusCode);
        Assert.Equal("Template", body!.CreationMethod);
        Assert.Equal(template.TemplateKey, body.SourceTemplateKey);
        Assert.Contains(claimCase.CustomerName, body.MessageBody, StringComparison.Ordinal);
        Assert.Contains(claimCase.PolicyNumber, body.MessageBody, StringComparison.Ordinal);
        Assert.DoesNotContain("{{", body.MessageBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateFromTemplate_UnknownTemplate_ReturnsNotFound()
    {
        var deps = CreateDependencies();
        var claimCase = await CreateCaseAsync(deps);

        var result = await CustomerUpdateEndpoints.CreateCustomerUpdateFromTemplate(
            claimCase.CaseId,
            new CreateCustomerUpdateFromTemplateRequest("does-not-exist", null),
            deps.CaseRepository,
            deps.DocumentRepository,
            deps.ChecklistProvider,
            deps.TemplateRepository,
            deps.CustomerUpdateRepository,
            deps.WorkflowRuleProvider,
            deps.AuditRepository,
            ScopedContext(),
            CancellationToken.None);
        var (statusCode, _) = await ExecuteAsync<JsonElement>(result);

        Assert.Equal(StatusCodes.Status404NotFound, statusCode);
    }

    [Fact]
    public async Task SeedDefaults_CreatesTheDefaultLibrary_AndIsIdempotent()
    {
        var deps = CreateDependencies();

        var first = await CommunicationTemplateEndpoints.SeedDefaults(deps.TemplateRepository, deps.AuditRepository, ScopedContext(), CancellationToken.None);
        var (_, firstBody) = await ExecuteAsync<JsonElement>(first);

        var second = await CommunicationTemplateEndpoints.SeedDefaults(deps.TemplateRepository, deps.AuditRepository, ScopedContext(), CancellationToken.None);
        var (_, secondBody) = await ExecuteAsync<JsonElement>(second);

        Assert.Equal(DefaultCommunicationTemplates.Defaults().Count, firstBody.GetProperty("seeded").GetInt32());
        Assert.Equal(0, secondBody.GetProperty("seeded").GetInt32()); // never overwrites an existing key
    }

    [Fact]
    public async Task SaveVersion_IncrementsVersionAndKeepsHistory()
    {
        var deps = CreateDependencies();
        var scope = TenantScope.Create("tenant-demo", "workspace-claims");

        var v1 = await CommunicationTemplateEndpoints.SaveVersion(
            "custom-template",
            new SaveCommunicationTemplateRequest("Custom", "StatusUpdate", "SMS", "en", null, "Body {{customer_name}}", "Short {{customer_name}}", true),
            deps.TemplateRepository,
            deps.AuditRepository,
            ScopedContext(auditReason: "Created."),
            CancellationToken.None);
        var (_, v1Body) = await ExecuteAsync<CommunicationTemplateResponse>(v1);

        var v2 = await CommunicationTemplateEndpoints.SaveVersion(
            "custom-template",
            new SaveCommunicationTemplateRequest("Custom v2", "StatusUpdate", "SMS", "en", null, "Body v2 {{customer_name}}", "Short v2", true),
            deps.TemplateRepository,
            deps.AuditRepository,
            ScopedContext(auditReason: "Reworded."),
            CancellationToken.None);
        var (_, v2Body) = await ExecuteAsync<CommunicationTemplateResponse>(v2);

        Assert.Equal(1, v1Body!.Version);
        Assert.Equal(2, v2Body!.Version);
        var versions = await deps.TemplateRepository.ListVersionsAsync(scope, "custom-template");
        Assert.Equal(2, versions.Count);
    }

    private static async Task<CommunicationTemplate> SeedTemplateAsync(Dependencies deps, string key, CustomerUpdateReasonCategory reason, CommunicationChannel channel)
    {
        var scope = TenantScope.Create("tenant-demo", "workspace-claims");
        var template = new CommunicationTemplate
        {
            TemplateId = Guid.NewGuid(),
            VersionId = Guid.NewGuid(),
            Scope = scope,
            Version = 1,
            TemplateKey = key,
            Name = "Test template",
            ReasonCategory = reason,
            Channel = channel,
            Subject = "Subject",
            Body = "Hello {{customer_name}}, claim {{claim_reference}}: please send {{missing_documents}}.",
            ShortBody = "Claim {{claim_reference}}: send {{missing_documents}}.",
            Active = true,
            UpdatedByUserId = "admin-1",
            UpdatedAt = DateTimeOffset.UtcNow,
            ChangeReason = "Seed for test.",
        };
        await deps.TemplateRepository.SaveAsync(template);
        return template;
    }

    private static async Task<CustomerUpdateDraft> SaveDraftAsync(
        Dependencies deps, Guid caseId, CustomerUpdateStatus status, CustomerUpdateCreationMethod creationMethod, string messageBody)
    {
        var draft = new CustomerUpdateDraft
        {
            UpdateId = Guid.NewGuid(),
            Scope = TenantScope.Create("tenant-demo", "workspace-claims"),
            CaseId = caseId,
            ReasonCategory = CustomerUpdateReasonCategory.StatusUpdate,
            Channel = CommunicationChannel.Portal,
            Status = status,
            MessageBody = messageBody,
            ShortMessageBody = messageBody,
            CreationMethod = creationMethod,
            RequiresApproval = status == CustomerUpdateStatus.PendingApproval,
            Sensitive = false,
            CreatedByUserId = "user-claims-1",
            ApprovedByUserId = status is CustomerUpdateStatus.Approved or CustomerUpdateStatus.Sent ? "supervisor-1" : null,
            ApprovedAt = status is CustomerUpdateStatus.Approved or CustomerUpdateStatus.Sent ? DateTimeOffset.UtcNow : null,
            SentAt = status == CustomerUpdateStatus.Sent ? DateTimeOffset.UtcNow : null,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            CorrelationId = "corr-edit-tests"
        };
        await deps.CustomerUpdateRepository.SaveAsync(draft, CancellationToken.None);
        return draft;
    }

    private static async Task<ClaimCase> CreateCaseAsync(Dependencies deps) =>
        await new CaseIntakeService(deps.CaseRepository, deps.AuditRepository).CreateCaseAsync(
            new CreateCaseCommand(
                TenantScope.Create("tenant-demo", "workspace-claims"),
                "Amina Okello",
                "amina@example.test",
                "+256700000000",
                "POL-1001",
                "Motor",
                "Accident",
                "WebPortal",
                new DateOnly(2026, 5, 24),
                "Minor collision reported at branch intake.",
                "user-claims-1",
                "corr-edit-tests-001"));

    private static Dependencies CreateDependencies() =>
        new(
            new InMemoryCaseRepository(),
            new InMemoryCaseDocumentRepository(),
            new StaticDocumentChecklistProvider(new CompletenessScoringService()),
            new InMemoryCustomerUpdateRepository(),
            new InMemoryCommunicationTemplateRepository(),
            new InMemoryAuditRepository(),
            new StaticWorkflowRuleProvider());

    private static DefaultHttpContext ScopedContext(string? auditReason = null)
    {
        var context = new DefaultHttpContext { TraceIdentifier = "trace-customer-update-editing" };
        context.Request.Headers["X-Tenant-Id"] = "tenant-demo";
        context.Request.Headers["X-Workspace-Id"] = "workspace-claims";
        context.Request.Headers["X-User-Id"] = "user-claims-1";
        context.Request.Headers["x-demo-user-id"] = "user-claims-1";
        context.Request.Headers["x-demo-role"] = "ClaimsOfficer";
        context.Request.Headers["X-Correlation-ID"] = "corr-edit-tests";
        if (auditReason is not null)
        {
            context.Request.Headers["X-Audit-Reason"] = auditReason;
        }

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

    private sealed record Dependencies(
        InMemoryCaseRepository CaseRepository,
        InMemoryCaseDocumentRepository DocumentRepository,
        IDocumentChecklistProvider ChecklistProvider,
        InMemoryCustomerUpdateRepository CustomerUpdateRepository,
        InMemoryCommunicationTemplateRepository TemplateRepository,
        InMemoryAuditRepository AuditRepository,
        IWorkflowRuleProvider WorkflowRuleProvider);
}
