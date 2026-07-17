using System.Security.Claims;
using System.Text.Json;
using InsuranceIntelligence.Api.CommandCenter;
using InsuranceIntelligence.Api.Documents;
using InsuranceIntelligence.Api.Local;
using InsuranceIntelligence.Api.Reports;
using InsuranceIntelligence.Api.Workflow;
using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Communications;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Documents;
using InsuranceIntelligence.Domain.Knowledge;
using InsuranceIntelligence.Domain.Workflow;
using InsuranceIntelligence.Infrastructure.Configuration;
using InsuranceIntelligence.LocalTestDoubles;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace InsuranceIntelligence.Api.Tests;

public sealed class WorkflowEndpointsTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task GetRecommendedActions_ReturnsMissingDocumentActionForIncompleteCase()
    {
        var dependencies = CreateDependencies();
        var claimCase = await CreateCaseAsync(dependencies);

        var result = await WorkflowEndpoints.GetRecommendedActions(
            claimCase.CaseId,
            dependencies.CaseRepository,
            dependencies.DocumentRepository,
            dependencies.ChecklistProvider,
            dependencies.ActionLogRepository,
            dependencies.SlaCalculator,
            dependencies.WorkflowRuleProvider,
            ScopedContext(),
            CancellationToken.None);
        var (statusCode, response) = await ExecuteAsync<RecommendedActionResponse[]>(result);

        Assert.Equal(StatusCodes.Status200OK, statusCode);
        Assert.Contains(response!, action => action.ActionType == "RequestMissingDocuments");
    }

    [Fact]
    public async Task EvaluateWorkflow_CreatesTasksAndAuditEvents()
    {
        var dependencies = CreateDependencies();
        var claimCase = await CreateCaseAsync(dependencies);

        var result = await EvaluateAsync(dependencies, claimCase.CaseId);
        var (statusCode, response) = await ExecuteAsync<WorkflowEvaluationResponse>(result);
        var auditEvents = await dependencies.AuditRepository.ListForCaseAsync(claimCase.CaseId);
        var saved = await dependencies.CaseRepository.GetAsync(claimCase.CaseId);

        Assert.Equal(StatusCodes.Status200OK, statusCode);
        Assert.NotEmpty(response!.CreatedTasks);
        Assert.Equal(CaseStatus.MissingDocuments, saved!.Status);
        Assert.Contains(auditEvents, auditEvent => auditEvent.ActionName == "case.status.updated");
        Assert.Contains(auditEvents, auditEvent => auditEvent.ActionName == "workflow.evaluated");
        Assert.Contains(auditEvents, auditEvent => auditEvent.ActionName == "workflow.task.created");
    }

    [Fact]
    public async Task EvaluateWorkflow_UsesResolvedMembershipContextInsteadOfBrowserAuthorityHeaders()
    {
        var dependencies = CreateDependencies();
        var membershipScope = TenantScope.Create("tenant-membership", "workspace-membership");
        var claimCase = await CreateCaseAsync(dependencies, createdByUserId: "membership-user-1", scope: membershipScope);
        var context = MembershipScopedContext(
            membershipScope,
            "membership-user-1",
            "attacker-tenant",
            "attacker-workspace",
            "attacker-user");

        var result = await WorkflowEndpoints.EvaluateWorkflow(
            claimCase.CaseId,
            dependencies.CaseRepository,
            dependencies.DocumentRepository,
            dependencies.ChecklistProvider,
            dependencies.ActionLogRepository,
            dependencies.SlaCalculator,
            dependencies.WorkflowRuleProvider,
            dependencies.WorkflowTaskRepository,
            dependencies.ApprovalRepository,
            dependencies.AuditRepository,
            context,
            CancellationToken.None);
        var (statusCode, _) = await ExecuteAsync<WorkflowEvaluationResponse>(result);
        var membershipTasks = await dependencies.WorkflowTaskRepository.ListAsync(membershipScope, CancellationToken.None);
        var attackerTasks = await dependencies.WorkflowTaskRepository.ListAsync(
            TenantScope.Create("attacker-tenant", "attacker-workspace"),
            CancellationToken.None);
        var auditEvents = await dependencies.AuditRepository.ListForCaseAsync(claimCase.CaseId);

        Assert.Equal(StatusCodes.Status200OK, statusCode);
        Assert.NotEmpty(membershipTasks);
        Assert.Empty(attackerTasks);
        Assert.All(membershipTasks, task =>
        {
            Assert.Equal(membershipScope, task.Scope);
            Assert.Equal("membership-user-1", task.CreatedByUserId);
        });
        Assert.Contains(auditEvents, auditEvent => auditEvent.ActionName == "workflow.evaluated");
    }

    [Fact]
    public async Task ListTasks_FiltersTasksByVisibleAssignedClaims()
    {
        var dependencies = CreateDependencies();
        var assignedCase = await CreateCaseAsync(dependencies);
        var otherCase = await CreateCaseAsync(dependencies, createdByUserId: "other-user");
        await dependencies.WorkflowTaskRepository.SaveAsync(TaskForCase(assignedCase.CaseId, "user-claims-1"));
        await dependencies.WorkflowTaskRepository.SaveAsync(TaskForCase(otherCase.CaseId, "other-user"));

        var result = await WorkflowEndpoints.ListTasks(
            dependencies.WorkflowTaskRepository,
            dependencies.CaseRepository,
            ScopedContext(),
            CancellationToken.None);
        var (statusCode, response) = await ExecuteAsync<WorkflowTaskResponse[]>(result);

        Assert.Equal(StatusCodes.Status200OK, statusCode);
        var task = Assert.Single(response!);
        Assert.Equal(assignedCase.CaseId, task.CaseId);
    }

    [Fact]
    public async Task EvaluateWorkflow_BlocksTerminalCases()
    {
        var dependencies = CreateDependencies();
        var claimCase = await CreateCaseAsync(dependencies);
        await dependencies.CaseRepository.SaveAsync(claimCase with
        {
            Status = CaseStatus.Closed,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        var result = await EvaluateAsync(dependencies, claimCase.CaseId);
        var (statusCode, body) = await ExecuteAsync<JsonElement>(result);

        Assert.Equal(StatusCodes.Status409Conflict, statusCode);
        Assert.Contains("closed", body.GetProperty("message").GetString(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal("evaluateCaseWorkflow", body.GetProperty("blockedAction").GetProperty("action").GetString());
    }

    [Fact]
    public async Task CreateApproval_BlocksTerminalCases()
    {
        var dependencies = CreateDependencies();
        var claimCase = await CreateCaseAsync(dependencies);
        await dependencies.CaseRepository.SaveAsync(claimCase with
        {
            Status = CaseStatus.Settled,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        var result = await ApprovalEndpoints.CreateApproval(
            claimCase.CaseId,
            new CreateApprovalRequest("SupervisorApproval", "Supervisor should review this workflow step.", ["case-file"], "High"),
            dependencies.CaseRepository,
            dependencies.ApprovalRepository,
            dependencies.AuditRepository,
            dependencies.TeamsNotificationQueue,
            ScopedContext(),
            CancellationToken.None);
        var (statusCode, body) = await ExecuteAsync<JsonElement>(result);

        Assert.Equal(StatusCodes.Status409Conflict, statusCode);
        Assert.Contains("settled", body.GetProperty("message").GetString(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal("createApprovalRequest", body.GetProperty("blockedAction").GetProperty("action").GetString());
    }

    [Fact]
    public async Task DraftCustomerUpdate_BlocksClosedCases()
    {
        var dependencies = CreateDependencies();
        var claimCase = await CreateCaseAsync(dependencies);
        await dependencies.CaseRepository.SaveAsync(claimCase with
        {
            Status = CaseStatus.Closed,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        var result = await CustomerUpdateEndpoints.DraftCustomerUpdate(
            claimCase.CaseId,
            new DraftCustomerUpdateRequest("MissingDocuments", "Portal"),
            dependencies.CaseRepository,
            dependencies.DocumentRepository,
            dependencies.ChecklistProvider,
            dependencies.DraftingProvider,
            dependencies.CustomerUpdateRepository,
            dependencies.ActionLogRepository,
            dependencies.AuditRepository,
            ScopedContext(),
            CancellationToken.None);
        var (statusCode, body) = await ExecuteAsync<JsonElement>(result);

        Assert.Equal(StatusCodes.Status409Conflict, statusCode);
        Assert.Contains("closed", body.GetProperty("message").GetString(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal("draftCustomerUpdate", body.GetProperty("blockedAction").GetProperty("action").GetString());
    }

    [Fact]
    public async Task DraftCustomerUpdate_ReturnsNotConfiguredWhenDraftingProviderIsAbsent()
    {
        var dependencies = CreateDependencies();
        var claimCase = await CreateCaseAsync(dependencies);

        var result = await CustomerUpdateEndpoints.DraftCustomerUpdate(
            claimCase.CaseId,
            new DraftCustomerUpdateRequest("MissingDocuments", "Portal"),
            dependencies.CaseRepository,
            dependencies.DocumentRepository,
            dependencies.ChecklistProvider,
            new NotConfiguredCustomerUpdateDraftingProvider(),
            dependencies.CustomerUpdateRepository,
            dependencies.ActionLogRepository,
            dependencies.AuditRepository,
            ScopedContext(),
            CancellationToken.None);
        var (statusCode, body) = await ExecuteAsync<JsonElement>(result);

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, statusCode);
        Assert.Equal("NotConfigured", body.GetProperty("status").GetString());
        Assert.Equal("CustomerUpdateDrafting", body.GetProperty("provider").GetString());
        Assert.Empty(await dependencies.CustomerUpdateRepository.ListForCaseAsync(
            claimCase.CaseId,
            TenantScope.Create("tenant-demo", "workspace-claims")));
    }

    [Fact]
    public async Task CreateAndApproveApprovalRequest()
    {
        var dependencies = CreateDependencies();
        var claimCase = await CreateCaseAsync(dependencies);
        var approval = await CreateApprovalAsync(dependencies, claimCase.CaseId);

        Assert.Contains(approval.AllowedActions, action =>
            action.Action == "approveApproval"
            && action.RequiredPermission == "ApproveSettlementWorkflowStep");
        Assert.Contains(approval.AllowedActions, action =>
            action.Action == "overrideApproval"
            && action.RequiredPermission == "OverrideAiRecommendation");

        const string auditReason = "Supervisor reviewed the evidence bundle and approved the workflow step.";
        var result = await ApprovalEndpoints.ApproveApproval(
            approval.ApprovalId,
            new ApprovalDecisionRequest("Approved for workflow progression."),
            dependencies.ApprovalRepository,
            dependencies.AuditRepository,
            ScopedContext(auditReason: auditReason),
            CancellationToken.None);
        var (statusCode, response) = await ExecuteAsync<ApprovalResponse>(result);
        var auditEvents = await dependencies.AuditRepository.ListForCaseAsync(claimCase.CaseId);

        Assert.Equal(StatusCodes.Status200OK, statusCode);
        Assert.Equal("Approved", response!.Status);
        Assert.Equal("Approved", response.Decision);
        Assert.Contains(auditEvents, auditEvent =>
            auditEvent.ActionName == "approval.approved"
            && auditEvent.AuditReason == auditReason);
        Assert.Contains(response.AllowedActions, action =>
            action.Action == "executeApprovedNextStep"
            && action.RequiredPermission == "ApproveSettlementWorkflowStep");
        Assert.Contains(response.BlockedActions, action =>
            action.Action == "approveApproval"
            && action.Reason is not null
            && action.Reason.Contains("already approved", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ExecuteApprovedNextStep_AdvancesCaseToReadyForExportAndBlocksRepeat()
    {
        var dependencies = CreateDependencies();
        var claimCase = await CreateCaseAsync(dependencies);
        var approval = await CreateApprovalAsync(dependencies, claimCase.CaseId);
        var approveResult = await ApprovalEndpoints.ApproveApproval(
            approval.ApprovalId,
            new ApprovalDecisionRequest("Approved for workflow progression."),
            dependencies.ApprovalRepository,
            dependencies.AuditRepository,
            ScopedContext(auditReason: "Approve before execution."),
            CancellationToken.None);
        var (_, approved) = await ExecuteAsync<ApprovalResponse>(approveResult);

        var executeResult = await ApprovalEndpoints.ExecuteApprovedNextStep(
            approved!.ApprovalId,
            new ExecuteApprovedNextStepRequest("Move the approved workflow step forward."),
            dependencies.ApprovalRepository,
            dependencies.CaseRepository,
            dependencies.AuditRepository,
            ScopedContext(auditReason: "Execute approved workflow next step."),
            CancellationToken.None);
        var (statusCode, executed) = await ExecuteAsync<ApprovalResponse>(executeResult);
        var savedCase = await dependencies.CaseRepository.GetAsync(claimCase.CaseId);
        var auditEvents = await dependencies.AuditRepository.ListForCaseAsync(claimCase.CaseId);

        Assert.Equal(StatusCodes.Status200OK, statusCode);
        Assert.Equal("user-claims-1", executed!.ExecutedByUserId);
        Assert.NotNull(executed.ExecutedAt);
        Assert.Equal(CaseStatus.ReadyForExport, savedCase!.Status);
        Assert.Contains(auditEvents, auditEvent => auditEvent.ActionName == "approval.next_step.executed");
        Assert.Contains(auditEvents, auditEvent => auditEvent.ActionName == "case.status.updated");

        var repeatResult = await ApprovalEndpoints.ExecuteApprovedNextStep(
            approved.ApprovalId,
            new ExecuteApprovedNextStepRequest("Repeat should fail."),
            dependencies.ApprovalRepository,
            dependencies.CaseRepository,
            dependencies.AuditRepository,
            ScopedContext(auditReason: "Attempt repeat execution."),
            CancellationToken.None);
        var (repeatStatus, repeatBody) = await ExecuteAsync<JsonElement>(repeatResult);

        Assert.Equal(StatusCodes.Status409Conflict, repeatStatus);
        Assert.Contains("already been executed", repeatBody.GetProperty("message").GetString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteApprovedNextStep_BlocksPendingApproval()
    {
        var dependencies = CreateDependencies();
        var claimCase = await CreateCaseAsync(dependencies);
        var approval = await CreateApprovalAsync(dependencies, claimCase.CaseId);

        var result = await ApprovalEndpoints.ExecuteApprovedNextStep(
            approval.ApprovalId,
            new ExecuteApprovedNextStepRequest("Execution before approval should fail."),
            dependencies.ApprovalRepository,
            dependencies.CaseRepository,
            dependencies.AuditRepository,
            ScopedContext(auditReason: "Attempt pending execution."),
            CancellationToken.None);
        var (statusCode, body) = await ExecuteAsync<JsonElement>(result);

        Assert.Equal(StatusCodes.Status409Conflict, statusCode);
        Assert.Contains("Only approved", body.GetProperty("message").GetString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ApproveApproval_RejectsMissingReviewStepReason()
    {
        var dependencies = CreateDependencies();
        var claimCase = await CreateCaseAsync(dependencies);
        var approval = await CreateApprovalAsync(dependencies, claimCase.CaseId);

        var result = await ApprovalEndpoints.ApproveApproval(
            approval.ApprovalId,
            new ApprovalDecisionRequest(null),
            dependencies.ApprovalRepository,
            dependencies.AuditRepository,
            ScopedContext(),
            CancellationToken.None);
        var (statusCode, _) = await ExecuteAsync<JsonElement>(result);

        Assert.Equal(StatusCodes.Status400BadRequest, statusCode);
    }

    [Theory]
    [InlineData("reject", "Rejected")]
    [InlineData("override", "Overridden")]
    [InlineData("escalate", "Escalated")]
    public async Task ApprovalDecisionEndpoints_RecordDecision(string endpoint, string expectedStatus)
    {
        var dependencies = CreateDependencies();
        var claimCase = await CreateCaseAsync(dependencies);
        var approval = await CreateApprovalAsync(dependencies, claimCase.CaseId);
        var request = new ApprovalDecisionRequest("Decision comments required for this action.");
        IResult result = endpoint switch
        {
            "reject" => await ApprovalEndpoints.RejectApproval(approval.ApprovalId, request, dependencies.ApprovalRepository, dependencies.AuditRepository, ScopedContext(auditReason: "Reject workflow step after human review."), CancellationToken.None),
            "override" => await ApprovalEndpoints.OverrideApproval(approval.ApprovalId, request, dependencies.ApprovalRepository, dependencies.AuditRepository, ScopedContext(auditReason: "Override prepared recommendation after human review."), CancellationToken.None),
            _ => await ApprovalEndpoints.EscalateApproval(approval.ApprovalId, request, dependencies.ApprovalRepository, dependencies.AuditRepository, ScopedContext(auditReason: "Escalate workflow step for additional human review."), CancellationToken.None)
        };

        var (statusCode, response) = await ExecuteAsync<ApprovalResponse>(result);

        Assert.Equal(StatusCodes.Status200OK, statusCode);
        Assert.Equal(expectedStatus, response!.Status);
    }

    [Theory]
    [InlineData("approve")]
    [InlineData("reject")]
    [InlineData("override")]
    [InlineData("escalate")]
    public async Task ApprovalDecisionEndpoints_RequireCommentsForEveryDecision(string endpoint)
    {
        var dependencies = CreateDependencies();
        var claimCase = await CreateCaseAsync(dependencies);
        var approval = await CreateApprovalAsync(dependencies, claimCase.CaseId);
        var request = new ApprovalDecisionRequest(" ");
        IResult result = endpoint switch
        {
            "approve" => await ApprovalEndpoints.ApproveApproval(approval.ApprovalId, request, dependencies.ApprovalRepository, dependencies.AuditRepository, ScopedContext(auditReason: "Attempt decision without comments."), CancellationToken.None),
            "reject" => await ApprovalEndpoints.RejectApproval(approval.ApprovalId, request, dependencies.ApprovalRepository, dependencies.AuditRepository, ScopedContext(auditReason: "Attempt decision without comments."), CancellationToken.None),
            "override" => await ApprovalEndpoints.OverrideApproval(approval.ApprovalId, request, dependencies.ApprovalRepository, dependencies.AuditRepository, ScopedContext(auditReason: "Attempt decision without comments."), CancellationToken.None),
            _ => await ApprovalEndpoints.EscalateApproval(approval.ApprovalId, request, dependencies.ApprovalRepository, dependencies.AuditRepository, ScopedContext(auditReason: "Attempt decision without comments."), CancellationToken.None)
        };

        var (statusCode, body) = await ExecuteAsync<JsonElement>(result);

        Assert.Equal(StatusCodes.Status400BadRequest, statusCode);
        Assert.Contains(
            "Reviewer comments are required",
            body.GetProperty("errors").GetProperty("comments")[0].GetString(),
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ApprovalDecision_RequiresAuditReasonBeforeMutation()
    {
        var dependencies = CreateDependencies();
        var claimCase = await CreateCaseAsync(dependencies);
        var approval = await CreateApprovalAsync(dependencies, claimCase.CaseId);

        var result = await ApprovalEndpoints.ApproveApproval(
            approval.ApprovalId,
            new ApprovalDecisionRequest("Reviewer confirmed the workflow evidence."),
            dependencies.ApprovalRepository,
            dependencies.AuditRepository,
            ScopedContext(),
            CancellationToken.None);
        var (statusCode, body) = await ExecuteAsync<JsonElement>(result);
        var persisted = await dependencies.ApprovalRepository.GetAsync(
            approval.ApprovalId,
            TenantScope.Create("tenant-demo", "workspace-claims"),
            CancellationToken.None);

        Assert.Equal(StatusCodes.Status400BadRequest, statusCode);
        Assert.True(body.GetProperty("errors").TryGetProperty("auditReason", out _));
        Assert.Equal(ApprovalStatus.Pending, persisted!.Status);
    }

    [Fact]
    public async Task ApprovalDecision_DerivesReviewerFromAuthenticatedClaims()
    {
        var dependencies = CreateDependencies();
        var claimCase = await CreateCaseAsync(dependencies);
        var approval = await CreateApprovalAsync(dependencies, claimCase.CaseId);
        const string auditReason = "Authenticated supervisor approved the workflow evidence.";

        var result = await ApprovalEndpoints.ApproveApproval(
            approval.ApprovalId,
            new ApprovalDecisionRequest("Reviewer confirmed the workflow evidence."),
            dependencies.ApprovalRepository,
            dependencies.AuditRepository,
            AuthenticatedScopedContext("entra-supervisor", auditReason),
            CancellationToken.None);
        var (statusCode, response) = await ExecuteAsync<ApprovalResponse>(result);
        var auditEvents = await dependencies.AuditRepository.ListForCaseAsync(claimCase.CaseId);

        Assert.Equal(StatusCodes.Status200OK, statusCode);
        Assert.Equal("entra-supervisor", response!.ReviewerUserId);
        Assert.Contains(auditEvents, auditEvent =>
            auditEvent.ActionName == "approval.approved"
            && auditEvent.UserId == "entra-supervisor"
            && auditEvent.AuditReason == auditReason);
    }

    [Fact]
    public async Task ApprovalDecisionEndpoints_BlockCompletedApprovals()
    {
        var dependencies = CreateDependencies();
        var claimCase = await CreateCaseAsync(dependencies);
        var approval = await CreateApprovalAsync(dependencies, claimCase.CaseId);

        var approveResult = await ApprovalEndpoints.ApproveApproval(
            approval.ApprovalId,
            new ApprovalDecisionRequest("Approved for workflow progression."),
            dependencies.ApprovalRepository,
            dependencies.AuditRepository,
            ScopedContext(auditReason: "Approve before repeat decision validation."),
            CancellationToken.None);
        var (approveStatus, _) = await ExecuteAsync<ApprovalResponse>(approveResult);

        var repeatResult = await ApprovalEndpoints.RejectApproval(
            approval.ApprovalId,
            new ApprovalDecisionRequest("Changing a completed decision should be blocked."),
            dependencies.ApprovalRepository,
            dependencies.AuditRepository,
            ScopedContext(auditReason: "Attempt repeat decision."),
            CancellationToken.None);
        var (repeatStatus, body) = await ExecuteAsync<JsonElement>(repeatResult);

        Assert.Equal(StatusCodes.Status200OK, approveStatus);
        Assert.Equal(StatusCodes.Status409Conflict, repeatStatus);
        Assert.Contains("already approved", body.GetProperty("message").GetString(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal("rejectApproval", body.GetProperty("blockedAction").GetProperty("action").GetString());
    }

    [Fact]
    public async Task DraftAndSendSimulatedCustomerUpdate()
    {
        var dependencies = CreateDependencies();
        var claimCase = await CreateCaseAsync(dependencies);
        var draft = await DraftUpdateAsync(dependencies, claimCase.CaseId);

        Assert.Contains(draft.AllowedActions, action =>
            action.Action == "sendCustomerUpdate"
            && action.RequiredPermission == "SendCustomerUpdate");

        const string auditReason = "Service agent confirmed this customer-safe update can be sent.";
        var sendResult = await CustomerUpdateEndpoints.SendCustomerUpdate(
            draft.UpdateId,
            new CustomerUpdateDecisionRequest(),
            dependencies.CaseRepository,
            dependencies.CustomerUpdateRepository,
            dependencies.CommunicationProvider,
            dependencies.AuditRepository,
            ScopedContext(auditReason: auditReason),
            CancellationToken.None);
        var (statusCode, sent) = await ExecuteAsync<CustomerUpdateResponse>(sendResult);
        var auditEvents = await dependencies.AuditRepository.ListForCaseAsync(claimCase.CaseId);

        Assert.Equal(StatusCodes.Status200OK, statusCode);
        Assert.Equal("SimulatedSent", sent!.DeliveryStatus);
        Assert.Contains(auditEvents, auditEvent => auditEvent.ActionName == "customer_update.drafted");
        Assert.Contains(auditEvents, auditEvent =>
            auditEvent.ActionName == "customer_update.sent"
            && auditEvent.AuditReason == auditReason);
    }

    [Fact]
    public async Task ApprovalRequiredCustomerUpdate_ReturnsBlockedSendActionUntilApproved()
    {
        var dependencies = CreateDependencies();
        var claimCase = await CreateCaseAsync(dependencies);
        var draft = await DraftUpdateAsync(dependencies, claimCase.CaseId, reasonCategory: "ReadyForReview");

        Assert.Equal("PendingApproval", draft.Status);
        Assert.Contains(draft.AllowedActions, action => action.Action == "approveCustomerUpdate");
        Assert.Contains(draft.BlockedActions, action =>
            action.Action == "sendCustomerUpdate"
            && action.RequiredPermission == "SendCustomerUpdate"
            && action.Reason is not null
            && action.Reason.Contains("approval", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ApprovalRequiredCustomerUpdate_RequiresReasonAndBlocksRepeatDecision()
    {
        var dependencies = CreateDependencies();
        var claimCase = await CreateCaseAsync(dependencies);
        var draft = await DraftUpdateAsync(dependencies, claimCase.CaseId, reasonCategory: "ReadyForReview");

        var missingReasonResult = await CustomerUpdateEndpoints.ApproveCustomerUpdate(
            draft.UpdateId,
            new CustomerUpdateDecisionRequest(),
            dependencies.CaseRepository,
            dependencies.CustomerUpdateRepository,
            dependencies.AuditRepository,
            ScopedContext(),
            CancellationToken.None);
        var (missingReasonStatus, missingReasonBody) = await ExecuteAsync<JsonElement>(missingReasonResult);

        const string auditReason = "Customer service supervisor approved the customer-safe update.";
        var approveResult = await CustomerUpdateEndpoints.ApproveCustomerUpdate(
            draft.UpdateId,
            new CustomerUpdateDecisionRequest(),
            dependencies.CaseRepository,
            dependencies.CustomerUpdateRepository,
            dependencies.AuditRepository,
            ScopedContext(auditReason: auditReason),
            CancellationToken.None);
        var (approveStatus, approved) = await ExecuteAsync<CustomerUpdateResponse>(approveResult);
        var auditEvents = await dependencies.AuditRepository.ListForCaseAsync(claimCase.CaseId);

        var repeatResult = await CustomerUpdateEndpoints.RejectCustomerUpdate(
            draft.UpdateId,
            new CustomerUpdateDecisionRequest(),
            dependencies.CaseRepository,
            dependencies.CustomerUpdateRepository,
            dependencies.AuditRepository,
            ScopedContext(auditReason: "Attempt repeat customer update decision."),
            CancellationToken.None);
        var (repeatStatus, repeatBody) = await ExecuteAsync<JsonElement>(repeatResult);

        Assert.Equal(StatusCodes.Status400BadRequest, missingReasonStatus);
        Assert.True(missingReasonBody.GetProperty("errors").TryGetProperty("auditReason", out _));
        Assert.Equal(StatusCodes.Status200OK, approveStatus);
        Assert.Equal("Approved", approved!.Status);
        Assert.Equal("user-claims-1", approved.ApprovedByUserId);
        Assert.Contains(auditEvents, auditEvent =>
            auditEvent.ActionName == "customer_update.approved"
            && auditEvent.UserId == "user-claims-1"
            && auditEvent.AuditReason == auditReason);
        Assert.Equal(StatusCodes.Status409Conflict, repeatStatus);
        Assert.Equal("rejectCustomerUpdate", repeatBody.GetProperty("blockedAction").GetProperty("action").GetString());
    }

    [Fact]
    public async Task CustomerUpdateSend_RequiresAuditReasonBeforeProviderCall()
    {
        var dependencies = CreateDependencies();
        var claimCase = await CreateCaseAsync(dependencies);
        var draft = await DraftUpdateAsync(dependencies, claimCase.CaseId);

        var result = await CustomerUpdateEndpoints.SendCustomerUpdate(
            draft.UpdateId,
            new CustomerUpdateDecisionRequest(),
            dependencies.CaseRepository,
            dependencies.CustomerUpdateRepository,
            dependencies.CommunicationProvider,
            dependencies.AuditRepository,
            ScopedContext(),
            CancellationToken.None);
        var (statusCode, body) = await ExecuteAsync<JsonElement>(result);
        var stored = await dependencies.CustomerUpdateRepository.GetAsync(
            draft.UpdateId,
            TenantScope.Create("tenant-demo", "workspace-claims"));

        Assert.Equal(StatusCodes.Status400BadRequest, statusCode);
        Assert.True(body.GetProperty("errors").TryGetProperty("auditReason", out _));
        Assert.Equal(CustomerUpdateStatus.Draft, stored!.Status);
        Assert.Null(stored.DeliveryStatus);
    }

    [Fact]
    public async Task CustomerUpdateDraft_DerivesCreatorFromAuthenticatedClaims()
    {
        var dependencies = CreateDependencies();
        var claimCase = await CreateCaseAsync(dependencies, createdByUserId: "entra-service-agent");

        var result = await CustomerUpdateEndpoints.DraftCustomerUpdate(
            claimCase.CaseId,
            new DraftCustomerUpdateRequest("MissingDocuments", "Portal"),
            dependencies.CaseRepository,
            dependencies.DocumentRepository,
            dependencies.ChecklistProvider,
            dependencies.DraftingProvider,
            dependencies.CustomerUpdateRepository,
            dependencies.ActionLogRepository,
            dependencies.AuditRepository,
            AuthenticatedScopedContext("entra-service-agent", "Authenticated draft context."),
            CancellationToken.None);
        var (statusCode, response) = await ExecuteAsync<CustomerUpdateResponse>(result);

        Assert.Equal(StatusCodes.Status201Created, statusCode);
        Assert.Equal("entra-service-agent", response!.CreatedByUserId);
    }

    [Fact]
    public async Task ConfiguredCustomerUpdateSend_RequiresRecipient()
    {
        var dependencies = CreateDependencies(new ConfiguredCustomerUpdateCommunicationProvider(new FakeCommunicationSendingProvider()));
        var claimCase = await CreateCaseAsync(dependencies);
        var draft = await DraftUpdateAsync(dependencies, claimCase.CaseId, channel: "Email");

        var sendResult = await CustomerUpdateEndpoints.SendCustomerUpdate(
            draft.UpdateId,
            new CustomerUpdateDecisionRequest(),
            dependencies.CaseRepository,
            dependencies.CustomerUpdateRepository,
            dependencies.CommunicationProvider,
            dependencies.AuditRepository,
            ScopedContext(auditReason: "Attempt configured send without recipient."),
            CancellationToken.None);
        var (statusCode, body) = await ExecuteAsync<JsonElement>(sendResult);
        var stored = await dependencies.CustomerUpdateRepository.GetAsync(draft.UpdateId, TenantScope.Create("tenant-demo", "workspace-claims"));

        Assert.Equal(StatusCodes.Status400BadRequest, statusCode);
        Assert.Equal("ConfigurationRequired", body.GetProperty("deliveryStatus").GetString());
        Assert.Equal("Draft", stored!.Status.ToString());
    }

    [Fact]
    public async Task ConfiguredCustomerUpdateSend_RecordsRealSentStatus()
    {
        var dependencies = CreateDependencies(new ConfiguredCustomerUpdateCommunicationProvider(new FakeCommunicationSendingProvider()));
        var claimCase = await CreateCaseAsync(dependencies);
        var draft = await DraftUpdateAsync(dependencies, claimCase.CaseId, channel: "Email");
        const string auditReason = "Service agent confirmed this configured customer email can be sent.";

        var sendResult = await CustomerUpdateEndpoints.SendCustomerUpdate(
            draft.UpdateId,
            new CustomerUpdateDecisionRequest("customer@example.test"),
            dependencies.CaseRepository,
            dependencies.CustomerUpdateRepository,
            dependencies.CommunicationProvider,
            dependencies.AuditRepository,
            ScopedContext(auditReason: auditReason),
            CancellationToken.None);
        var (statusCode, sent) = await ExecuteAsync<CustomerUpdateResponse>(sendResult);
        var auditEvents = await dependencies.AuditRepository.ListForCaseAsync(claimCase.CaseId);

        Assert.Equal(StatusCodes.Status200OK, statusCode);
        Assert.Equal("Sent", sent!.Status);
        Assert.Equal("Sent", sent.DeliveryStatus);
        Assert.Contains(auditEvents, auditEvent =>
            auditEvent.ActionName == "customer_update.sent"
            && auditEvent.ModelProvider == "GraphEmail"
            && auditEvent.ValidationResult == "sent"
            && auditEvent.AuditReason == auditReason);
    }

    [Fact]
    public async Task GetCaseTimeline_ReturnsSafeOrderedEvents()
    {
        var dependencies = CreateDependencies();
        var claimCase = await CreateCaseAsync(dependencies);
        await EvaluateAsync(dependencies, claimCase.CaseId);

        var result = await WorkflowEndpoints.GetTimeline(
            claimCase.CaseId,
            dependencies.CaseRepository,
            dependencies.AuditRepository,
            dependencies.TimelineProvider,
            ScopedContext(),
            CancellationToken.None);
        var (statusCode, timeline) = await ExecuteAsync<CaseTimelineEventResponse[]>(result);

        Assert.Equal(StatusCodes.Status200OK, statusCode);
        Assert.Contains(timeline!, item => item.EventType == "case.created");
        Assert.Contains(timeline!, item => item.EventType == "workflow.evaluated");
        Assert.DoesNotContain(timeline!, item => item.Summary.Contains("prompt", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task TenantWorkspaceMismatchRejectedForNewCaseScopedEndpoints()
    {
        var dependencies = CreateDependencies();
        var claimCase = await CreateCaseAsync(dependencies);

        var result = await WorkflowEndpoints.GetRecommendedActions(
            claimCase.CaseId,
            dependencies.CaseRepository,
            dependencies.DocumentRepository,
            dependencies.ChecklistProvider,
            dependencies.ActionLogRepository,
            dependencies.SlaCalculator,
            dependencies.WorkflowRuleProvider,
            ScopedContext(workspaceId: "wrong-workspace"),
            CancellationToken.None);
        var (statusCode, _) = await ExecuteAsync<JsonElement>(result);

        Assert.Equal(StatusCodes.Status404NotFound, statusCode);
    }

    [Fact]
    public async Task CommandCenterMetricsIncludeWorkflowCounts()
    {
        var dependencies = CreateDependencies();
        var claimCase = await CreateCaseAsync(dependencies);
        await EvaluateAsync(dependencies, claimCase.CaseId);
        await DraftUpdateAsync(dependencies, claimCase.CaseId);

        var result = await CommandCenterMetricsLoader.GetMetrics(
            dependencies.CaseRepository,
            dependencies.DocumentRepository,
            dependencies.KnowledgeRepository,
            dependencies.ActionLogRepository,
            dependencies.ApprovalRepository,
            dependencies.WorkflowTaskRepository,
            dependencies.CustomerUpdateRepository,
            dependencies.SlaCalculator,
            ScopedContext(),
            CancellationToken.None);
        var (statusCode, metrics) = await ExecuteAsync<JsonElement>(result);

        Assert.Equal(StatusCodes.Status200OK, statusCode);
        Assert.True(metrics.GetProperty("openTasks").GetInt32() > 0);
        Assert.True(metrics.GetProperty("customerUpdatesPending").GetInt32() > 0);
    }

    [Fact]
    public async Task CommandCenterMetrics_UsesResolvedMembershipContextInsteadOfBrowserAuthorityHeaders()
    {
        var dependencies = CreateDependencies();
        var membershipScope = TenantScope.Create("tenant-membership", "workspace-membership");
        var attackerScope = TenantScope.Create("attacker-tenant", "attacker-workspace");
        await CreateCaseAsync(dependencies, createdByUserId: "membership-user-1", scope: membershipScope);
        await CreateCaseAsync(dependencies, createdByUserId: "attacker-user", scope: attackerScope);
        await CreateCaseAsync(dependencies, createdByUserId: "attacker-user", scope: attackerScope);

        var result = await CommandCenterMetricsLoader.GetMetrics(
            dependencies.CaseRepository,
            dependencies.DocumentRepository,
            dependencies.KnowledgeRepository,
            dependencies.ActionLogRepository,
            dependencies.ApprovalRepository,
            dependencies.WorkflowTaskRepository,
            dependencies.CustomerUpdateRepository,
            dependencies.SlaCalculator,
            MembershipScopedContext(
                membershipScope,
                "membership-user-1",
                attackerScope.TenantId,
                attackerScope.WorkspaceId,
                "attacker-user"),
            CancellationToken.None);
        var (statusCode, metrics) = await ExecuteAsync<JsonElement>(result);

        Assert.Equal(StatusCodes.Status200OK, statusCode);
        Assert.Equal(1, metrics.GetProperty("totalCases").GetInt32());
        Assert.Equal(1, metrics.GetProperty("openClaims").GetInt32());
    }

    [Fact]
    public async Task OperationsSummaryReturnsExpectedMetrics()
    {
        var dependencies = CreateDependencies();
        var claimCase = await CreateCaseAsync(dependencies);
        await EvaluateAsync(dependencies, claimCase.CaseId);

        var result = await ReportsEndpoints.GetOperationsSummary(
            dependencies.CaseRepository,
            dependencies.WorkflowTaskRepository,
            dependencies.ApprovalRepository,
            dependencies.CustomerUpdateRepository,
            dependencies.ActionLogRepository,
            dependencies.SlaCalculator,
            ScopedContext(),
            CancellationToken.None);
        var (statusCode, summary) = await ExecuteAsync<OperationsSummaryResponse>(result);

        Assert.Equal(StatusCodes.Status200OK, statusCode);
        Assert.Equal(1, summary!.OpenClaims);
        Assert.True(summary.OpenTasks > 0);
    }

    [Fact]
    public async Task OperationsSummary_UsesResolvedMembershipContextInsteadOfBrowserAuthorityHeaders()
    {
        var dependencies = CreateDependencies();
        var membershipScope = TenantScope.Create("tenant-membership", "workspace-membership");
        var attackerScope = TenantScope.Create("attacker-tenant", "attacker-workspace");
        await CreateCaseAsync(dependencies, createdByUserId: "membership-user-1", scope: membershipScope);
        await CreateCaseAsync(dependencies, createdByUserId: "attacker-user", scope: attackerScope);
        await CreateCaseAsync(dependencies, createdByUserId: "attacker-user", scope: attackerScope);

        var result = await ReportsEndpoints.GetOperationsSummary(
            dependencies.CaseRepository,
            dependencies.WorkflowTaskRepository,
            dependencies.ApprovalRepository,
            dependencies.CustomerUpdateRepository,
            dependencies.ActionLogRepository,
            dependencies.SlaCalculator,
            MembershipScopedContext(
                membershipScope,
                "membership-user-1",
                attackerScope.TenantId,
                attackerScope.WorkspaceId,
                "attacker-user"),
            CancellationToken.None);
        var (statusCode, summary) = await ExecuteAsync<OperationsSummaryResponse>(result);

        Assert.Equal(StatusCodes.Status200OK, statusCode);
        Assert.Equal(1, summary!.OpenClaims);
        Assert.Equal(1, summary.ClaimsByStatus.Sum(item => item.Count));
    }

    private static async Task<IResult> EvaluateAsync(Dependencies dependencies, Guid caseId) =>
        await WorkflowEndpoints.EvaluateWorkflow(
            caseId,
            dependencies.CaseRepository,
            dependencies.DocumentRepository,
            dependencies.ChecklistProvider,
            dependencies.ActionLogRepository,
            dependencies.SlaCalculator,
            dependencies.WorkflowRuleProvider,
            dependencies.WorkflowTaskRepository,
            dependencies.ApprovalRepository,
            dependencies.AuditRepository,
            ScopedContext(),
            CancellationToken.None);

    private static async Task<ApprovalResponse> CreateApprovalAsync(Dependencies dependencies, Guid caseId)
    {
        var result = await ApprovalEndpoints.CreateApproval(
            caseId,
            new CreateApprovalRequest("SupervisorApproval", "Supervisor should review this workflow step.", ["case-file"], "High"),
            dependencies.CaseRepository,
            dependencies.ApprovalRepository,
            dependencies.AuditRepository,
            dependencies.TeamsNotificationQueue,
            ScopedContext(),
            CancellationToken.None);
        var (statusCode, response) = await ExecuteAsync<ApprovalResponse>(result);

        Assert.Equal(StatusCodes.Status201Created, statusCode);
        return response!;
    }

    private static async Task<CustomerUpdateResponse> DraftUpdateAsync(Dependencies dependencies, Guid caseId, string channel = "Portal", string reasonCategory = "MissingDocuments")
    {
        var result = await CustomerUpdateEndpoints.DraftCustomerUpdate(
            caseId,
            new DraftCustomerUpdateRequest(reasonCategory, channel),
            dependencies.CaseRepository,
            dependencies.DocumentRepository,
            dependencies.ChecklistProvider,
            dependencies.DraftingProvider,
            dependencies.CustomerUpdateRepository,
            dependencies.ActionLogRepository,
            dependencies.AuditRepository,
            ScopedContext(),
            CancellationToken.None);
        var (statusCode, response) = await ExecuteAsync<CustomerUpdateResponse>(result);

        Assert.Equal(StatusCodes.Status201Created, statusCode);
        return response!;
    }

    private static async Task<ClaimCase> CreateCaseAsync(
        Dependencies dependencies,
        string createdByUserId = "user-claims-1",
        TenantScope? scope = null)
    {
        var service = new CaseIntakeService(dependencies.CaseRepository, dependencies.AuditRepository);

        return await service.CreateCaseAsync(
            new CreateCaseCommand(
                scope ?? TenantScope.Create("tenant-demo", "workspace-claims"),
                "Amina Okello",
                "amina@example.test",
                "+256700000000",
                "POL-1001",
                "Motor",
                "Accident",
                "WebPortal",
                new DateOnly(2026, 5, 24),
                "Minor collision reported at branch intake.",
                createdByUserId,
                "corr-workflow-001"));
    }

    private static WorkflowTask TaskForCase(Guid caseId, string createdByUserId) =>
        new()
        {
            TaskId = Guid.NewGuid(),
            Scope = TenantScope.Create("tenant-demo", "workspace-claims"),
            CaseId = caseId,
            TaskType = WorkflowTaskType.ReviewCase,
            Status = WorkflowTaskStatus.Open,
            Title = "Review claim",
            Summary = "Review claim summary.",
            CreatedByUserId = createdByUserId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            CorrelationId = "corr-workflow-001"
        };

    private static Dependencies CreateDependencies(ICommunicationProvider? communicationProvider = null)
    {
        var workflowRuleProvider = new StaticWorkflowRuleProvider();
        var scoringService = new CompletenessScoringService();

        return new Dependencies(
            new InMemoryCaseRepository(),
            new InMemoryCaseDocumentRepository(),
            new StaticDocumentChecklistProvider(scoringService),
            new InMemoryAuditRepository(),
            new InMemoryAgentActionLogRepository(),
            new InMemoryWorkflowTaskRepository(),
            new InMemoryApprovalRepository(),
            new InMemoryCustomerUpdateRepository(),
            new InMemoryKnowledgeDocumentRepository(),
            workflowRuleProvider,
            new BasicSlaCalculator(),
            new CaseTimelineProvider(),
            new MockCustomerUpdateDraftingProvider(workflowRuleProvider),
            communicationProvider ?? new MockCommunicationProvider(),
            new FakeTeamsNotificationQueue());
    }

    private static DefaultHttpContext ScopedContext(
        string? tenantId = "tenant-demo",
        string? workspaceId = "workspace-claims",
        string? auditReason = null)
    {
        var context = new DefaultHttpContext { TraceIdentifier = "trace-workflow-001" };
        if (tenantId is not null)
        {
            context.Request.Headers["X-Tenant-Id"] = tenantId;
        }

        if (workspaceId is not null)
        {
            context.Request.Headers["X-Workspace-Id"] = workspaceId;
        }

        context.Request.Headers["X-User-Id"] = "user-claims-1";
        context.Request.Headers["x-demo-user-id"] = "user-claims-1";
        context.Request.Headers["x-demo-role"] = "ClaimsOfficer";
        context.Request.Headers["X-Correlation-ID"] = "corr-workflow-001";
        if (auditReason is not null)
        {
            context.Request.Headers["X-Audit-Reason"] = auditReason;
        }
        return context;
    }

    private static DefaultHttpContext MembershipScopedContext(
        TenantScope membershipScope,
        string membershipUserId,
        string spoofedTenantId,
        string spoofedWorkspaceId,
        string spoofedUserId)
    {
        var tenantContext = new TenantContextAccessor();
        tenantContext.SetCurrent(new TenantContext(
            membershipScope.TenantId,
            membershipScope.WorkspaceId,
            membershipUserId,
            ["ClaimsOfficer"]));
        var context = ScopedContext(spoofedTenantId, spoofedWorkspaceId);
        context.RequestServices = new ServiceCollection()
            .AddSingleton<ITenantContextAccessor>(tenantContext)
            .BuildServiceProvider();
        context.Request.Headers["X-User-Id"] = spoofedUserId;
        context.Request.Headers["x-demo-user-id"] = spoofedUserId;
        return context;
    }

    private static DefaultHttpContext AuthenticatedScopedContext(string userId, string auditReason)
    {
        var context = new DefaultHttpContext { TraceIdentifier = "trace-workflow-authenticated" };
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(ClaimTypes.Role, "ClaimsSupervisor"),
            new Claim("tenant_id", "tenant-demo"),
            new Claim("workspace_id", "workspace-claims")
        ], "Entra"));
        context.Request.Headers["X-User-Id"] = "spoofed-browser-reviewer";
        context.Request.Headers["X-Audit-Reason"] = auditReason;
        context.RequestServices = new ServiceCollection()
            .AddSingleton(new InfrastructureOptions { AuthMode = "Entra" })
            .BuildServiceProvider();
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

    private sealed record Dependencies(
        InMemoryCaseRepository CaseRepository,
        InMemoryCaseDocumentRepository DocumentRepository,
        IDocumentChecklistProvider ChecklistProvider,
        InMemoryAuditRepository AuditRepository,
        InMemoryAgentActionLogRepository ActionLogRepository,
        InMemoryWorkflowTaskRepository WorkflowTaskRepository,
        InMemoryApprovalRepository ApprovalRepository,
        InMemoryCustomerUpdateRepository CustomerUpdateRepository,
        InMemoryKnowledgeDocumentRepository KnowledgeRepository,
        IWorkflowRuleProvider WorkflowRuleProvider,
        ISlaCalculator SlaCalculator,
        ICaseTimelineProvider TimelineProvider,
        ICustomerUpdateDraftingProvider DraftingProvider,
        ICommunicationProvider CommunicationProvider,
        InsuranceIntelligence.Domain.Notifications.ITeamsNotificationQueue TeamsNotificationQueue);

    private sealed class FakeCommunicationSendingProvider : ICommunicationSendingProvider
    {
        public Task<CommunicationSendResult> SendEmailAsync(CommunicationSendRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(new CommunicationSendResult("Sent", "fake-email", null));

        public Task<CommunicationSendResult> SendSmsAsync(CommunicationSendRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(new CommunicationSendResult("Sent", "fake-sms", null));
    }

    private sealed class FakeTeamsNotificationQueue : InsuranceIntelligence.Domain.Notifications.ITeamsNotificationQueue
    {
        public Task EnqueueAsync(InsuranceIntelligence.Domain.Notifications.TeamsNotification notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
