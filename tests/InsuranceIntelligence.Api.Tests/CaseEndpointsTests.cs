using System.Text.Json;
using System.Security.Claims;
using InsuranceIntelligence.Api.Cases;
using InsuranceIntelligence.Api.Local;
using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Application.Notifications;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.LocalTestDoubles;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Documents;
using InsuranceIntelligence.Domain.Notifications;
using InsuranceIntelligence.Domain.Workflow;
using InsuranceIntelligence.Infrastructure.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace InsuranceIntelligence.Api.Tests;

public sealed class CaseEndpointsTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task CreateCase_ReturnsCreatedResponseAndAuditSummary()
    {
        var dependencies = CreateDependencies();
        var httpContext = ScopedContext();
        httpContext.TraceIdentifier = "trace-001";
        httpContext.Request.Headers["X-Correlation-ID"] = "corr-api-001";

        var result = await CaseEndpoints.CreateCase(
            ValidRequest(),
            dependencies.Service,
            dependencies.AuditRepository,
            httpContext,
            CancellationToken.None);

        var (statusCode, response) = await ExecuteAsync<CaseResponse>(result);

        Assert.Equal(StatusCodes.Status201Created, statusCode);
        Assert.NotEqual(Guid.Empty, response!.CaseId);
        Assert.Equal("New", response.Status);
        Assert.Equal("corr-api-001", response.CorrelationId);
        Assert.Equal("tenant-demo", response.TenantId);
        Assert.Equal("workspace-claims", response.WorkspaceId);
        Assert.Equal("user-claims-1", response.CreatedByUserId);
        Assert.Equal("UAX 123B", response.MotorFnolDetails?.VehicleRegistration);
        Assert.Equal("17:40", response.MotorFnolDetails?.AccidentTime);
        Assert.Equal("Kampala Road", response.MotorFnolDetails?.AccidentLocation);
        Assert.Equal(1, response.Audit.EventCount);
        Assert.Equal("case.created", response.Audit.LastActionName);
        Assert.Contains(response.AllowedActions, action =>
            action.Action == "openCaseReview"
            && action.RequiredPermission == "ViewAssignedClaims");
        Assert.Contains(response.AllowedActions, action =>
            action.Action == "evaluateCaseWorkflow"
            && action.RequiredPermission == "ApproveSettlementWorkflowStep");
        Assert.Contains(response.AllowedActions, action =>
            action.Action == "createApprovalRequest"
            && action.RequiredPermission == "ApproveSettlementWorkflowStep");
        Assert.Contains(response.AllowedActions, action =>
            action.Action == "draftCustomerUpdate"
            && action.RequiredPermission == "ManageCustomerCommunications");
        Assert.Empty(response.BlockedActions);
    }

    [Fact]
    public async Task CreateCase_ReturnsValidationProblemForMissingRequiredFields()
    {
        var dependencies = CreateDependencies();
        var httpContext = ScopedContext();

        var result = await CaseEndpoints.CreateCase(
            ValidRequest() with { CustomerName = "" },
            dependencies.Service,
            dependencies.AuditRepository,
            httpContext,
            CancellationToken.None);

        var (statusCode, _) = await ExecuteAsync<JsonElement>(result);

        Assert.Equal(StatusCodes.Status400BadRequest, statusCode);
    }

    [Fact]
    public async Task CreateCase_ReturnsValidationProblemWhenRuntimeScopeIsMissing()
    {
        var dependencies = CreateDependencies();

        var result = await CaseEndpoints.CreateCase(
            ValidRequest(),
            dependencies.Service,
            dependencies.AuditRepository,
            new DefaultHttpContext(),
            CancellationToken.None);
        var (statusCode, body) = await ExecuteAsync<JsonElement>(result);

        Assert.Equal(StatusCodes.Status400BadRequest, statusCode);
        Assert.True(body.GetProperty("errors").TryGetProperty("tenantScope", out _));
    }

    [Fact]
    public async Task CreateCase_UsesAuthenticatedClaimsInsteadOfBrowserAuthorityHeaders()
    {
        var dependencies = CreateDependencies();
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim("oid", "entra-user-1"),
                new Claim("iip_tenant_id", "tenant-entra"),
                new Claim("iip_workspace_id", "workspace-entra")
            ], "Bearer")),
            RequestServices = new ServiceCollection()
                .AddSingleton(new InfrastructureOptions { AuthMode = "EntraId" })
                .BuildServiceProvider()
        };
        context.Request.Headers["X-Tenant-Id"] = "attacker-tenant";
        context.Request.Headers["X-Workspace-Id"] = "attacker-workspace";
        context.Request.Headers["X-User-Id"] = "attacker-user";

        var result = await CaseEndpoints.CreateCase(
            ValidRequest(),
            dependencies.Service,
            dependencies.AuditRepository,
            context,
            CancellationToken.None);
        var (statusCode, response) = await ExecuteAsync<CaseResponse>(result);

        Assert.Equal(StatusCodes.Status201Created, statusCode);
        Assert.Equal("tenant-entra", response!.TenantId);
        Assert.Equal("workspace-entra", response.WorkspaceId);
        Assert.Equal("entra-user-1", response.CreatedByUserId);
    }

    [Fact]
    public async Task CreateCase_UsesResolvedMembershipContextInsteadOfBrowserAuthorityHeaders()
    {
        var dependencies = CreateDependencies();
        var tenantContext = new TenantContextAccessor();
        tenantContext.SetCurrent(new TenantContext(
            "tenant-membership",
            "workspace-membership",
            "membership-user-1",
            [PlatformRole.Admin.ToString()]));
        var context = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection()
                .AddSingleton<ITenantContextAccessor>(tenantContext)
                .BuildServiceProvider()
        };
        context.Request.Headers["X-Tenant-Id"] = "attacker-tenant";
        context.Request.Headers["X-Workspace-Id"] = "attacker-workspace";
        context.Request.Headers["X-User-Id"] = "attacker-user";
        context.Request.Headers["x-demo-user-id"] = "attacker-user";

        var result = await CaseEndpoints.CreateCase(
            ValidRequest(),
            dependencies.Service,
            dependencies.AuditRepository,
            context,
            CancellationToken.None);
        var (statusCode, response) = await ExecuteAsync<CaseResponse>(result);

        Assert.Equal(StatusCodes.Status201Created, statusCode);
        Assert.Equal("tenant-membership", response!.TenantId);
        Assert.Equal("workspace-membership", response.WorkspaceId);
        Assert.Equal("membership-user-1", response.CreatedByUserId);
    }

    [Fact]
    public async Task GetCase_ReturnsNotFoundForDifferentTenantWorkspace()
    {
        var dependencies = CreateDependencies();
        var createContext = ScopedContext();
        createContext.TraceIdentifier = "trace-002";
        var createResult = await CaseEndpoints.CreateCase(
            ValidRequest(),
            dependencies.Service,
            dependencies.AuditRepository,
            createContext,
            CancellationToken.None);
        var (_, created) = await ExecuteAsync<CaseResponse>(createResult);

        var getContext = ScopedContext(workspaceId: "wrong-workspace");

        var getResult = await CaseEndpoints.GetCase(
            created!.CaseId,
            dependencies.Service,
            dependencies.AuditRepository,
            getContext,
            CancellationToken.None);

        var (statusCode, _) = await ExecuteAsync<JsonElement>(getResult);

        Assert.Equal(StatusCodes.Status404NotFound, statusCode);
    }

    [Fact]
    public async Task GetCase_ReturnsCaseForMatchingTenantWorkspace()
    {
        var dependencies = CreateDependencies();
        var createContext = ScopedContext();
        createContext.TraceIdentifier = "trace-003";
        var createResult = await CaseEndpoints.CreateCase(
            ValidRequest(),
            dependencies.Service,
            dependencies.AuditRepository,
            createContext,
            CancellationToken.None);
        var (_, created) = await ExecuteAsync<CaseResponse>(createResult);

        var getContext = ScopedContext();
        getContext.Request.QueryString = new QueryString("?tenantId=tenant-demo&workspaceId=workspace-claims");

        var getResult = await CaseEndpoints.GetCase(
            created!.CaseId,
            dependencies.Service,
            dependencies.AuditRepository,
            getContext,
            CancellationToken.None);

        var (statusCode, response) = await ExecuteAsync<CaseResponse>(getResult);

        Assert.Equal(StatusCodes.Status200OK, statusCode);
        Assert.Equal(created.CaseId, response!.CaseId);
        Assert.Equal("tenant-demo", response.TenantId);
        Assert.Equal("workspace-claims", response.WorkspaceId);
    }

    [Fact]
    public async Task ListCases_ReturnsBackendOwnedActionStates()
    {
        var dependencies = CreateDependencies();
        var createContext = ScopedContext();
        createContext.TraceIdentifier = "trace-004";
        var createResult = await CaseEndpoints.CreateCase(
            ValidRequest(),
            dependencies.Service,
            dependencies.AuditRepository,
            createContext,
            CancellationToken.None);
        var (_, created) = await ExecuteAsync<CaseResponse>(createResult);

        var listContext = ScopedContext();

        var listResult = await CaseEndpoints.ListCases(
            dependencies.CaseRepository,
            dependencies.DocumentRepository,
            dependencies.ChecklistProvider,
            dependencies.AuditRepository,
            dependencies.SlaCalculator,
            dependencies.NotificationEmitter,
            listContext,
            CancellationToken.None);
        var (statusCode, response) = await ExecuteAsync<ClaimInboxItemResponse[]>(listResult);

        Assert.Equal(StatusCodes.Status200OK, statusCode);
        var claim = Assert.Single(response!);
        Assert.Equal(created!.CaseId, claim.CaseId);
        Assert.Contains(claim.AllowedActions, action => action.Action == "openCaseReview");
        Assert.Contains(claim.AllowedActions, action => action.Action == "evaluateCaseWorkflow");
        Assert.Empty(claim.BlockedActions);
    }

    [Fact]
    public async Task ListCases_ReturnsReadyForReviewWithoutPlaceholderText()
    {
        var dependencies = CreateDependencies();
        var claimCase = await dependencies.Service.CreateCaseAsync(ValidCommand(), CancellationToken.None);
        foreach (var documentType in new[] { DocumentType.ClaimForm, DocumentType.PoliceAbstract, DocumentType.PhotoEvidence })
        {
            await dependencies.DocumentRepository.SaveAsync(ProcessedDocument(claimCase, documentType), CancellationToken.None);
        }

        var listResult = await CaseEndpoints.ListCases(
            dependencies.CaseRepository,
            dependencies.DocumentRepository,
            dependencies.ChecklistProvider,
            dependencies.AuditRepository,
            dependencies.SlaCalculator,
            dependencies.NotificationEmitter,
            ScopedContext(),
            CancellationToken.None);
        var (statusCode, response) = await ExecuteAsync<ClaimInboxItemResponse[]>(listResult);

        Assert.Equal(StatusCodes.Status200OK, statusCode);
        var claim = Assert.Single(response!);
        Assert.Equal("Ready For Review", claim.DocumentStatus);
        Assert.DoesNotContain("placeholder", claim.DocumentStatus, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CaseEndpoints_RejectCrossTenantCaseAccessAndActions()
    {
        var dependencies = CreateDependencies();
        var tenantAContext = ScopedContext("Admin", "user-a", "tenant-a", "workspace-a");
        var tenantBContext = ScopedContext("Admin", "user-b", "tenant-b", "workspace-b");

        var createA = await CaseEndpoints.CreateCase(
            ValidRequest() with { CustomerName = "Tenant A Customer", PolicyNumber = "POL-TENANT-A-001" },
            dependencies.Service,
            dependencies.AuditRepository,
            tenantAContext,
            CancellationToken.None);
        var (createAStatus, caseA) = await ExecuteAsync<CaseResponse>(createA);

        var createB = await CaseEndpoints.CreateCase(
            ValidRequest() with { CustomerName = "Tenant B Customer", PolicyNumber = "POL-TENANT-B-001" },
            dependencies.Service,
            dependencies.AuditRepository,
            tenantBContext,
            CancellationToken.None);
        var (createBStatus, caseB) = await ExecuteAsync<CaseResponse>(createB);

        Assert.Equal(StatusCodes.Status201Created, createAStatus);
        Assert.Equal(StatusCodes.Status201Created, createBStatus);
        Assert.NotEqual(Guid.Empty, caseA!.CaseId);
        Assert.NotEqual(Guid.Empty, caseB!.CaseId);

        var listA = await CaseEndpoints.ListCases(
            dependencies.CaseRepository,
            dependencies.DocumentRepository,
            dependencies.ChecklistProvider,
            dependencies.AuditRepository,
            dependencies.SlaCalculator,
            dependencies.NotificationEmitter,
            ScopedContext("Admin", "user-a", "tenant-a", "workspace-a"),
            CancellationToken.None);
        var (listAStatus, casesForA) = await ExecuteAsync<ClaimInboxItemResponse[]>(listA);

        var listB = await CaseEndpoints.ListCases(
            dependencies.CaseRepository,
            dependencies.DocumentRepository,
            dependencies.ChecklistProvider,
            dependencies.AuditRepository,
            dependencies.SlaCalculator,
            dependencies.NotificationEmitter,
            ScopedContext("Admin", "user-b", "tenant-b", "workspace-b"),
            CancellationToken.None);
        var (listBStatus, casesForB) = await ExecuteAsync<ClaimInboxItemResponse[]>(listB);

        Assert.Equal(StatusCodes.Status200OK, listAStatus);
        Assert.Equal(StatusCodes.Status200OK, listBStatus);
        Assert.Contains(casesForA!, item => item.CaseId == caseA.CaseId);
        Assert.DoesNotContain(casesForA!, item => item.CaseId == caseB.CaseId);
        Assert.Contains(casesForB!, item => item.CaseId == caseB.CaseId);
        Assert.DoesNotContain(casesForB!, item => item.CaseId == caseA.CaseId);

        var tenantAReadsTenantB = await CaseEndpoints.GetCase(
            caseB.CaseId,
            dependencies.Service,
            dependencies.AuditRepository,
            ScopedContext("Admin", "user-a", "tenant-a", "workspace-a"),
            CancellationToken.None);
        var (tenantAReadsTenantBStatus, _) = await ExecuteAsync<JsonElement>(tenantAReadsTenantB);

        var tenantBReadsTenantA = await CaseEndpoints.GetCase(
            caseA.CaseId,
            dependencies.Service,
            dependencies.AuditRepository,
            ScopedContext("Admin", "user-b", "tenant-b", "workspace-b"),
            CancellationToken.None);
        var (tenantBReadsTenantAStatus, _) = await ExecuteAsync<JsonElement>(tenantBReadsTenantA);

        Assert.Equal(StatusCodes.Status404NotFound, tenantAReadsTenantBStatus);
        Assert.Equal(StatusCodes.Status404NotFound, tenantBReadsTenantAStatus);

        var tenantAAssignsTenantB = await CaseEndpoints.AssignCase(
            caseB.CaseId,
            new AssignCaseRequest("user-a", "Tenant A Team", "cross-tenant assign attempt"),
            dependencies.CaseRepository,
            dependencies.AuditRepository,
            ScopedContext("Admin", "user-a", "tenant-a", "workspace-a"),
            CancellationToken.None);
        var (tenantAAssignsTenantBStatus, _) = await ExecuteAsync<JsonElement>(tenantAAssignsTenantB);

        var tenantBAssignsTenantA = await CaseEndpoints.AssignCase(
            caseA.CaseId,
            new AssignCaseRequest("user-b", "Tenant B Team", "cross-tenant assign attempt"),
            dependencies.CaseRepository,
            dependencies.AuditRepository,
            ScopedContext("Admin", "user-b", "tenant-b", "workspace-b"),
            CancellationToken.None);
        var (tenantBAssignsTenantAStatus, _) = await ExecuteAsync<JsonElement>(tenantBAssignsTenantA);

        Assert.Equal(StatusCodes.Status404NotFound, tenantAAssignsTenantBStatus);
        Assert.Equal(StatusCodes.Status404NotFound, tenantBAssignsTenantAStatus);

        var tenantAEscalatesTenantB = await CaseEndpoints.EscalateCase(
            caseB.CaseId,
            new EscalateCaseRequest("cross-tenant escalate attempt", null),
            dependencies.CaseRepository,
            dependencies.AuditRepository,
            dependencies.NotificationEmitter,
            ScopedContext("Admin", "user-a", "tenant-a", "workspace-a"),
            CancellationToken.None);
        var (tenantAEscalatesTenantBStatus, _) = await ExecuteAsync<JsonElement>(tenantAEscalatesTenantB);

        var tenantBEscalatesTenantA = await CaseEndpoints.EscalateCase(
            caseA.CaseId,
            new EscalateCaseRequest("cross-tenant escalate attempt", null),
            dependencies.CaseRepository,
            dependencies.AuditRepository,
            dependencies.NotificationEmitter,
            ScopedContext("Admin", "user-b", "tenant-b", "workspace-b"),
            CancellationToken.None);
        var (tenantBEscalatesTenantAStatus, _) = await ExecuteAsync<JsonElement>(tenantBEscalatesTenantA);

        Assert.Equal(StatusCodes.Status404NotFound, tenantAEscalatesTenantBStatus);
        Assert.Equal(StatusCodes.Status404NotFound, tenantBEscalatesTenantAStatus);

        var savedA = await dependencies.CaseRepository.GetAsync(caseA.CaseId, CancellationToken.None);
        var savedB = await dependencies.CaseRepository.GetAsync(caseB.CaseId, CancellationToken.None);
        Assert.Equal(CaseStatus.New, savedA!.Status);
        Assert.Equal(CaseStatus.New, savedB!.Status);
        Assert.Null(savedA.AssignedUserId);
        Assert.Null(savedB.AssignedUserId);
    }

    [Fact]
    public async Task AssignCase_UpdatesAssignmentAndWritesAudit()
    {
        var dependencies = CreateDependencies();
        var created = await dependencies.Service.CreateCaseAsync(ValidCommand(), CancellationToken.None);
        var context = ScopedContext("ClaimsSupervisor", "supervisor-1");
        context.Request.Headers["X-Correlation-ID"] = "corr-assign-001";

        var result = await CaseEndpoints.AssignCase(
            created.CaseId,
            new AssignCaseRequest("handler-42", "Motor Review", "Supervisor allocation"),
            dependencies.CaseRepository,
            dependencies.AuditRepository,
            context,
            CancellationToken.None);
        var (statusCode, response) = await ExecuteAsync<CaseResponse>(result);

        Assert.Equal(StatusCodes.Status200OK, statusCode);
        Assert.Equal("Assigned", response!.Status);
        Assert.Equal("handler-42", response!.AssignedUserId);
        Assert.Equal("Motor Review", response.AssignedTeam);
        Assert.Equal("case.assigned", response.Audit.LastActionName);
        Assert.Equal("corr-assign-001", response.Audit.CorrelationId);

        var saved = await dependencies.CaseRepository.GetAsync(created.CaseId, CancellationToken.None);
        Assert.Equal("handler-42", saved!.AssignedUserId);
    }

    [Fact]
    public async Task EscalateCase_SetsEscalatedUrgentAndWritesAudit()
    {
        var dependencies = CreateDependencies();
        var created = await dependencies.Service.CreateCaseAsync(ValidCommand(), CancellationToken.None);
        var context = ScopedContext("ClaimsSupervisor", "supervisor-1");
        context.Request.Headers["X-Correlation-ID"] = "corr-escalate-001";

        var result = await CaseEndpoints.EscalateCase(
            created.CaseId,
            new EscalateCaseRequest("SLA breach needs supervisor action", null),
            dependencies.CaseRepository,
            dependencies.AuditRepository,
            dependencies.NotificationEmitter,
            context,
            CancellationToken.None);
        var (statusCode, response) = await ExecuteAsync<CaseResponse>(result);

        Assert.Equal(StatusCodes.Status200OK, statusCode);
        Assert.Equal("Escalated", response!.Status);
        Assert.Equal("Claims Supervision", response.AssignedTeam);
        Assert.Equal("case.escalated", response.Audit.LastActionName);

        var saved = await dependencies.CaseRepository.GetAsync(created.CaseId, CancellationToken.None);
        Assert.Equal(CasePriority.Urgent, saved!.Priority);
    }

    [Fact]
    public async Task EscalateCase_NotifiesTheAssignedOfficer_ButNotAnotherUser()
    {
        // WO-14: proves the real "claim escalated" event actually creates a real, recipient-scoped
        // in-app notification — not just an audit row.
        var dependencies = CreateDependencies();
        var created = await dependencies.Service.CreateCaseAsync(ValidCommand(), CancellationToken.None);
        await CaseEndpoints.AssignCase(
            created.CaseId,
            new AssignCaseRequest("handler-42", "Motor Review", "Supervisor allocation"),
            dependencies.CaseRepository,
            dependencies.AuditRepository,
            ScopedContext("ClaimsSupervisor", "supervisor-1"),
            CancellationToken.None);

        await CaseEndpoints.EscalateCase(
            created.CaseId,
            new EscalateCaseRequest("SLA breach needs supervisor action", null),
            dependencies.CaseRepository,
            dependencies.AuditRepository,
            dependencies.NotificationEmitter,
            ScopedContext("ClaimsSupervisor", "supervisor-1"),
            CancellationToken.None);

        var scope = TenantScope.Create("tenant-demo", "workspace-claims");
        var assignedOfficerFeed = await dependencies.NotificationRepository.ListForUserAsync(scope, "handler-42", 20, CancellationToken.None);
        var notification = Assert.Single(assignedOfficerFeed);
        Assert.Equal(NotificationType.ClaimEscalated, notification.Type);
        Assert.Equal(created.CaseId, notification.CaseId);
        Assert.False(notification.IsRead);

        var supervisorFeed = await dependencies.NotificationRepository.ListForUserAsync(scope, "supervisor-1", 20, CancellationToken.None);
        Assert.Empty(supervisorFeed);
    }

    [Fact]
    public async Task ListCases_NotifiesTheAssignedOfficerOnceForABreachedSla_NotOnEveryList()
    {
        // WO-14: SLA breach has no one-shot event — it's recomputed on every list read — so this
        // proves NotifyOnceAsync's dedup actually works end to end through the real endpoint.
        var dependencies = CreateDependencies();
        var created = await dependencies.Service.CreateCaseAsync(ValidCommand(), CancellationToken.None);
        await CaseEndpoints.AssignCase(
            created.CaseId,
            new AssignCaseRequest("handler-42", "Motor Review", "Supervisor allocation"),
            dependencies.CaseRepository,
            dependencies.AuditRepository,
            ScopedContext("ClaimsSupervisor", "supervisor-1"),
            CancellationToken.None);
        var saved = await dependencies.CaseRepository.GetAsync(created.CaseId, CancellationToken.None);
        await dependencies.CaseRepository.SaveAsync(saved! with { SlaDueAt = DateTimeOffset.UtcNow.AddHours(-1) }, CancellationToken.None);

        await CaseEndpoints.ListCases(
            dependencies.CaseRepository,
            dependencies.DocumentRepository,
            dependencies.ChecklistProvider,
            dependencies.AuditRepository,
            dependencies.SlaCalculator,
            dependencies.NotificationEmitter,
            ScopedContext(),
            CancellationToken.None);
        await CaseEndpoints.ListCases(
            dependencies.CaseRepository,
            dependencies.DocumentRepository,
            dependencies.ChecklistProvider,
            dependencies.AuditRepository,
            dependencies.SlaCalculator,
            dependencies.NotificationEmitter,
            ScopedContext(),
            CancellationToken.None);

        var scope = TenantScope.Create("tenant-demo", "workspace-claims");
        var feed = await dependencies.NotificationRepository.ListForUserAsync(scope, "handler-42", 20, CancellationToken.None);
        var notification = Assert.Single(feed);
        Assert.Equal(NotificationType.SlaBreach, notification.Type);
    }

    [Fact]
    public async Task CreateCase_InjuryRoutesToDesignatedEscalationTeam()
    {
        var dependencies = CreateDependencies();
        var result = await CaseEndpoints.CreateCase(
            ValidRequest() with
            {
                ClaimType = "Bodily injury",
                MotorFnolDetails = new MotorFnolDetails(null, null, null, null, null, null, "Yes", null, null, null)
            },
            dependencies.Service,
            dependencies.AuditRepository,
            ScopedContext(),
            CancellationToken.None);
        var (statusCode, response) = await ExecuteAsync<CaseResponse>(result);

        Assert.Equal(StatusCodes.Status201Created, statusCode);
        Assert.Equal("Escalated", response!.Status);
        Assert.Equal(ClaimRoutingPolicy.DesignatedInjuryEscalationTeam, response.AssignedTeam);
    }

    [Fact]
    public async Task CreateCase_ThirdPartyRoutesToThirdPartyDesk()
    {
        var dependencies = CreateDependencies();
        var result = await CaseEndpoints.CreateCase(
            ValidRequest() with { ClaimType = "Third-party property damage" },
            dependencies.Service,
            dependencies.AuditRepository,
            ScopedContext(),
            CancellationToken.None);
        var (statusCode, response) = await ExecuteAsync<CaseResponse>(result);

        Assert.Equal(StatusCodes.Status201Created, statusCode);
        Assert.Equal("New", response!.Status);
        Assert.Equal(ClaimRoutingPolicy.ThirdPartyDeskTeam, response.AssignedTeam);
    }

    [Fact]
    public async Task EscalateCase_InjuryUsesDesignatedInjuryTeam()
    {
        var dependencies = CreateDependencies();
        var created = await dependencies.Service.CreateCaseAsync(
            ValidCommand() with { ClaimType = "Own damage" },
            CancellationToken.None);
        // Simulate later injury discovery via claim-type correction before escalate.
        created = created with { ClaimType = "Bodily injury" };
        await dependencies.CaseRepository.SaveAsync(created, CancellationToken.None);

        var result = await CaseEndpoints.EscalateCase(
            created.CaseId,
            new EscalateCaseRequest("Injury confirmed — route to designated human.", null),
            dependencies.CaseRepository,
            dependencies.AuditRepository,
            dependencies.NotificationEmitter,
            ScopedContext("ClaimsSupervisor", "supervisor-1"),
            CancellationToken.None);
        var (statusCode, response) = await ExecuteAsync<CaseResponse>(result);

        Assert.Equal(StatusCodes.Status200OK, statusCode);
        Assert.Equal(ClaimRoutingPolicy.DesignatedInjuryEscalationTeam, response!.AssignedTeam);
    }

    [Fact]
    public async Task EscalateCase_RequiresAuditReason()
    {
        var dependencies = CreateDependencies();
        var created = await dependencies.Service.CreateCaseAsync(ValidCommand(), CancellationToken.None);

        var result = await CaseEndpoints.EscalateCase(
            created.CaseId,
            new EscalateCaseRequest("", null),
            dependencies.CaseRepository,
            dependencies.AuditRepository,
            dependencies.NotificationEmitter,
            ScopedContext("ClaimsSupervisor", "supervisor-1"),
            CancellationToken.None);
        var (statusCode, body) = await ExecuteAsync<JsonElement>(result);

        Assert.Equal(StatusCodes.Status400BadRequest, statusCode);
        Assert.True(body.GetProperty("errors").TryGetProperty("auditReason", out _));
    }

    [Fact]
    public async Task ListCases_FiltersUnassignedCasesForAssignedClaimViewer()
    {
        var dependencies = CreateDependencies();
        await dependencies.Service.CreateCaseAsync(ValidCommand(), CancellationToken.None);
        await dependencies.Service.CreateCaseAsync(ValidCommand("other-user"), CancellationToken.None);

        var assignedResult = await CaseEndpoints.ListCases(
            dependencies.CaseRepository,
            dependencies.DocumentRepository,
            dependencies.ChecklistProvider,
            dependencies.AuditRepository,
            dependencies.SlaCalculator,
            dependencies.NotificationEmitter,
            ScopedContext("ClaimsOfficer", "user-claims-1"),
            CancellationToken.None);
        var (_, assignedCases) = await ExecuteAsync<ClaimInboxItemResponse[]>(assignedResult);

        var supervisorResult = await CaseEndpoints.ListCases(
            dependencies.CaseRepository,
            dependencies.DocumentRepository,
            dependencies.ChecklistProvider,
            dependencies.AuditRepository,
            dependencies.SlaCalculator,
            dependencies.NotificationEmitter,
            ScopedContext("ClaimsSupervisor", "supervisor-1"),
            CancellationToken.None);
        var (_, supervisorCases) = await ExecuteAsync<ClaimInboxItemResponse[]>(supervisorResult);

        Assert.Single(assignedCases!);
        Assert.Equal(2, supervisorCases!.Length);
    }

    [Fact]
    public async Task GetCase_ReturnsNotFoundForUnassignedClaimViewer()
    {
        var dependencies = CreateDependencies();
        var created = await dependencies.Service.CreateCaseAsync(
            ValidCommand("other-user"),
            CancellationToken.None);

        var result = await CaseEndpoints.GetCase(
            created.CaseId,
            dependencies.Service,
            dependencies.AuditRepository,
            ScopedContext("ClaimsOfficer", "user-claims-1"),
            CancellationToken.None);
        var (statusCode, _) = await ExecuteAsync<JsonElement>(result);

        Assert.Equal(StatusCodes.Status404NotFound, statusCode);
    }

    private static Dependencies CreateDependencies()
    {
        var caseRepository = new InMemoryCaseRepository();
        var auditRepository = new InMemoryAuditRepository();
        var scoringService = new CompletenessScoringService();
        var notificationRepository = new InMemoryNotificationRepository();
        return new Dependencies(
            new CaseIntakeService(caseRepository, auditRepository),
            caseRepository,
            new InMemoryCaseDocumentRepository(),
            new StaticDocumentChecklistProvider(scoringService),
            auditRepository,
            new BasicSlaCalculator(),
            new NotificationService(new TenantContextAccessor(), notificationRepository),
            notificationRepository);
    }

    private static CreateCaseRequest ValidRequest() =>
        new(
            "Amina Okello",
            "amina@example.test",
            "+256700000000",
            "POL-1001",
            "Motor",
            "Accident",
            "WebPortal",
            new DateOnly(2026, 5, 24),
            "Minor collision reported at branch intake.",
            MotorDetails());

    private static CreateCaseCommand ValidCommand(string createdByUserId = "user-claims-1") =>
        new(
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
            createdByUserId,
            "corr-api-001",
            MotorDetails());

    private static MotorFnolDetails MotorDetails() =>
        new(
            "UAX 123B",
            "17:40",
            "Kampala Road",
            "Amina Okello",
            "DP-445566",
            "Yes",
            "No injuries reported",
            "Received",
            "Phone",
            "Police report and photos uploaded");

    private static CaseDocument ProcessedDocument(ClaimCase claimCase, DocumentType documentType) =>
        new()
        {
            DocumentId = Guid.NewGuid(),
            CaseId = claimCase.CaseId,
            Scope = claimCase.Scope,
            OriginalFileName = $"{documentType}.pdf",
            ContentType = "application/pdf",
            FileSizeBytes = 1024,
            ObjectKey = $"cases/{claimCase.CaseId:N}/{documentType}.pdf",
            DocumentType = documentType,
            ProcessingStatus = DocumentProcessingStatus.Processed,
            Classification = new DocumentClassificationResult(documentType, 0.99m, "TestDocumentProcessor", DateTimeOffset.UtcNow),
            Extraction = new DocumentExtractionResult(
                new Dictionary<string, string>(),
                new Dictionary<string, decimal>(),
                "TestDocumentProcessor",
                DateTimeOffset.UtcNow,
                Approved: false,
                ApprovedByUserId: null,
                ApprovedAt: null),
            ValidationWarnings = Array.Empty<DocumentValidationWarning>(),
            UploadedByUserId = "user-claims-1",
            UploadedAt = DateTimeOffset.UtcNow,
            ProcessedAt = DateTimeOffset.UtcNow,
            CorrelationId = "corr-api-001"
        };

    private static DefaultHttpContext ScopedContext(
        string role = "ClaimsOfficer",
        string userId = "user-claims-1",
        string tenantId = "tenant-demo",
        string workspaceId = "workspace-claims")
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Tenant-Id"] = tenantId;
        context.Request.Headers["X-Workspace-Id"] = workspaceId;
        context.Request.Headers["X-User-Id"] = userId;
        context.Request.Headers["x-demo-user-id"] = userId;
        context.Request.Headers["x-demo-role"] = role;
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
        CaseIntakeService Service,
        InMemoryCaseRepository CaseRepository,
        InMemoryCaseDocumentRepository DocumentRepository,
        IDocumentChecklistProvider ChecklistProvider,
        IAuditRepository AuditRepository,
        ISlaCalculator SlaCalculator,
        INotificationEmitter NotificationEmitter,
        InMemoryNotificationRepository NotificationRepository);
}
