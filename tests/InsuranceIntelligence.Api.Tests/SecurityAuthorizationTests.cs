using InsuranceIntelligence.Api.ClientPortal;
using System.Security.Claims;
using System.Runtime.CompilerServices;
using System.Text.Json;
using InsuranceIntelligence.Api.Auth;
using InsuranceIntelligence.Api.Common;
using InsuranceIntelligence.Api.Customer;
using InsuranceIntelligence.Api.Workflow;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Workflow;
using InsuranceIntelligence.Infrastructure.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace InsuranceIntelligence.Api.Tests;

public sealed class SecurityAuthorizationTests
{
    private static readonly InfrastructureOptions DevelopmentOptions = new() { AuthMode = "Development" };
    private static readonly InfrastructureOptions ProductionOptions = new() { AuthMode = "Entra" };
    private static readonly InfrastructureOptions ProductionDeploymentOptions = new()
    {
        AuthMode = "Entra",
        TenantId = "configured-tenant",
        WorkspaceId = "configured-workspace"
    };
    private static readonly Microsoft.Extensions.Hosting.IHostEnvironment DevelopmentHost = new TestHostEnvironment("Development");
    private static readonly Microsoft.Extensions.Hosting.IHostEnvironment ProductionHost = new TestHostEnvironment("Production");
    private static readonly InsuranceIntelligence.Application.Tenants.IWorkspaceCatalogService EmptyWorkspaceCatalog = new StubWorkspaceCatalog();

    public static IEnumerable<object[]> ConsequentialPermissionCases()
    {
        var consequentialPermissions = new[]
        {
            Permission.ApproveSettlementWorkflowStep,
            Permission.OverrideAiRecommendation,
            Permission.ExportEvidencePack,
            Permission.SendCustomerUpdate,
            Permission.ManageAgentWorkflows,
            Permission.ManageTenantOnboarding
        };

        foreach (var role in Enum.GetValues<PlatformRole>())
        {
            foreach (var permission in consequentialPermissions)
            {
                yield return [role, permission, RolePermissionMap.HasPermission(role, permission)];
            }
        }
    }

    private sealed class StubWorkspaceCatalog : InsuranceIntelligence.Application.Tenants.IWorkspaceCatalogService
    {
        public Task<IReadOnlyList<InsuranceIntelligence.Application.Tenants.WorkspaceSummaryDto>> ListForTenantAsync(
            string tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<InsuranceIntelligence.Application.Tenants.WorkspaceSummaryDto>>([]);
    }

    // Simulates the per-request middleware having resolved (or not) the effective membership context.
    private static readonly InsuranceIntelligence.Application.Core.ITenantContextAccessor NoTenantContext = new StubTenantContext(null);
    private static InsuranceIntelligence.Application.Core.ITenantContextAccessor TenantContextWith(string tenant, string workspace, string user, params string[] roles) =>
        new StubTenantContext(new InsuranceIntelligence.Application.Core.TenantContext(tenant, workspace, user, roles));

    private sealed class StubTenantContext(InsuranceIntelligence.Application.Core.TenantContext? current)
        : InsuranceIntelligence.Application.Core.ITenantContextAccessor
    {
        public bool HasCurrent => current is not null;
        public InsuranceIntelligence.Application.Core.TenantContext Current =>
            current ?? throw new InvalidOperationException("No tenant context resolved.");
    }

    private sealed class TestHostEnvironment(string environmentName) : Microsoft.Extensions.Hosting.IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "InsuranceIntelligence.Api.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();
    }

    [Fact]
    public void GetRole_DefaultsToLeastPrivilegeClient_WhenHeaderMissing()
    {
        var context = new DefaultHttpContext();

        var role = DemoAuthorization.GetRole(context);

        Assert.Equal(PlatformRole.Client, role);
    }

    [Fact]
    public void GetRole_DefaultsToClient_WhenHeaderUnrecognized()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["x-demo-role"] = "SuperUser";

        var role = DemoAuthorization.GetRole(context);

        Assert.Equal(PlatformRole.Client, role);
    }

    [Fact]
    public void GetRole_ParsesExplicitRole()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["x-demo-role"] = "ClaimsSupervisor";

        var role = DemoAuthorization.GetRole(context);

        Assert.Equal(PlatformRole.ClaimsSupervisor, role);
    }

    [Theory]
    [MemberData(nameof(ConsequentialPermissionCases))]
    public async Task PermissionFilter_EnforcesEveryRoleConsequentialPermission(
        PlatformRole role,
        Permission permission,
        bool expectedAllowed)
    {
        var filter = new PermissionEndpointFilter(permission);
        var context = CreateFilterContext(DevelopmentOptions, demoRole: role.ToString());

        var result = await filter.InvokeAsync(context, _ => ValueTask.FromResult<object?>("handler-ran"));

        if (expectedAllowed)
        {
            Assert.Equal("handler-ran", result);
        }
        else
        {
            Assert.Equal(StatusCodes.Status403Forbidden, await ExecuteStatusAsync((IResult)result!));
        }
    }

    [Fact]
    public async Task Require_RejectsDemoHeaderImpersonation_OutsideDevelopmentMode()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["x-demo-role"] = "Admin";

        var result = DemoAuthorization.Require(context, ProductionOptions, Permission.ManageTenantSettings);

        Assert.NotNull(result);
        var status = await ExecuteStatusAsync(result!);
        Assert.Equal(StatusCodes.Status403Forbidden, status);
    }

    [Fact]
    public async Task Require_RejectsLegacyUserIdentityHeader_OutsideDevelopmentMode()
    {
        var context = AuthenticatedContext("ITAdmin");
        context.Request.Headers["X-User-Id"] = "spoofed-browser-user";

        var result = DemoAuthorization.Require(context, ProductionOptions, Permission.ManageTenantSettings);

        Assert.NotNull(result);
        Assert.Equal(StatusCodes.Status403Forbidden, await ExecuteStatusAsync(result!));
    }

    [Fact]
    public void GetUserId_DoesNotFallThroughToHeadersForAuthenticatedIdentity()
    {
        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.Role, "ITAdmin")
        ], "Entra"));
        context.Request.Headers["X-User-Id"] = "spoofed-browser-user";

        var userId = DemoAuthorization.GetUserId(context, fallback: string.Empty);

        Assert.Equal(string.Empty, userId);
    }

    [Fact]
    public async Task Require_RejectsUnauthenticatedUser_OutsideDevelopmentMode()
    {
        var context = new DefaultHttpContext();

        var result = DemoAuthorization.Require(context, ProductionOptions, Permission.ManageTenantSettings);

        Assert.NotNull(result);
        Assert.Equal(StatusCodes.Status401Unauthorized, await ExecuteStatusAsync(result!));
    }

    [Fact]
    public void Require_AllowsWhenStoreGrantsPermission_OutsideDevelopmentMode()
    {
        // ADR-0005: deployed-mode authz reads the store-resolved permissions the middleware stashes,
        // NOT the token role. A stash granting ConfigureIntegrations allows the request.
        var context = AuthenticatedContextWithStore(Permission.ConfigureIntegrations, Permission.ManageTenantSettings);

        var result = DemoAuthorization.Require(context, ProductionOptions, Permission.ConfigureIntegrations);

        Assert.Null(result);
    }

    [Fact]
    public async Task Require_FailsClosedWhenNoMembershipStashed_OutsideDevelopmentMode()
    {
        // Authenticated but the middleware resolved no active membership -> no stash -> fail closed.
        var context = AuthenticatedContext("ITAdmin");

        var result = DemoAuthorization.Require(context, ProductionOptions, Permission.ConfigureIntegrations);

        Assert.NotNull(result);
        Assert.Equal(StatusCodes.Status403Forbidden, await ExecuteStatusAsync(result!));
    }

    [Fact]
    public async Task Require_ForbidsWhenStoreLacksPermission_OutsideDevelopmentMode()
    {
        // A token role claim must never grant a permission the store does not hold: even an "Admin"
        // role claim is ignored; only the stashed store permissions count.
        var context = AuthenticatedContextWithStore(Permission.ViewAssignedClaims);
        context.User = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(
        [
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, "entra-user"),
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, "Admin")
        ], "Entra"));
        context.Items[DemoAuthorization.EffectivePermissionsItemKey] = (IReadOnlyCollection<Permission>)[Permission.ViewAssignedClaims];

        var result = DemoAuthorization.Require(context, ProductionOptions, Permission.ManageTenantSettings);

        Assert.NotNull(result);
        Assert.Equal(StatusCodes.Status403Forbidden, await ExecuteStatusAsync(result!));
    }

    [Fact]
    public async Task Require_ForbidsRoleWithoutPermission_InDevelopmentMode()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["x-demo-role"] = "Client";

        var result = DemoAuthorization.Require(context, DevelopmentOptions, Permission.ManageKnowledgeBase);

        Assert.NotNull(result);
        var status = await ExecuteStatusAsync(result!);
        Assert.Equal(StatusCodes.Status403Forbidden, status);
    }

    [Fact]
    public void Require_AllowsRoleWithPermission_InDevelopmentMode()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["x-demo-role"] = "KnowledgeManager";

        var result = DemoAuthorization.Require(context, DevelopmentOptions, Permission.ManageKnowledgeBase);

        Assert.Null(result);
    }

    [Fact]
    public void RequireAny_AllowsRoleWithOneAcceptedPermission_InDevelopmentMode()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["x-demo-role"] = "Compliance";

        var result = DemoAuthorization.RequireAny(
            context,
            DevelopmentOptions,
            [Permission.ViewAssignedClaims, Permission.ViewAllClaims]);

        Assert.Null(result);
    }

    [Fact]
    public async Task RequireAny_ForbidsRoleWithoutAnyAcceptedPermission_InDevelopmentMode()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["x-demo-role"] = "Client";

        var result = DemoAuthorization.RequireAny(
            context,
            DevelopmentOptions,
            [Permission.ViewAssignedClaims, Permission.ViewAllClaims]);

        Assert.NotNull(result);
        Assert.Equal(StatusCodes.Status403Forbidden, await ExecuteStatusAsync(result!));
    }

    [Fact]
    public async Task PermissionEndpointFilter_ShortCircuits_WhenRoleLacksPermission()
    {
        var filter = new PermissionEndpointFilter(Permission.ManageKnowledgeBase);
        var context = CreateFilterContext(DevelopmentOptions, demoRole: "Client");
        var nextCalled = false;

        var result = await filter.InvokeAsync(context, _ =>
        {
            nextCalled = true;
            return ValueTask.FromResult<object?>("handler-ran");
        });

        Assert.False(nextCalled);
        Assert.IsAssignableFrom<IResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, await ExecuteStatusAsync((IResult)result!));
    }

    [Fact]
    public async Task PermissionEndpointFilter_InvokesHandler_WhenRoleAuthorized()
    {
        var filter = new PermissionEndpointFilter(Permission.ManageKnowledgeBase);
        var context = CreateFilterContext(DevelopmentOptions, demoRole: "KnowledgeManager");

        var result = await filter.InvokeAsync(context, _ => ValueTask.FromResult<object?>("handler-ran"));

        Assert.Equal("handler-ran", result);
    }

    [Fact]
    public async Task PermissionEndpointFilter_InvokesHandler_WhenRoleHasAnyAcceptedPermission()
    {
        var filter = new PermissionEndpointFilter(Permission.ViewAssignedClaims, Permission.ViewAllClaims);
        var context = CreateFilterContext(DevelopmentOptions, demoRole: "Compliance");

        var result = await filter.InvokeAsync(context, _ => ValueTask.FromResult<object?>("handler-ran"));

        Assert.Equal("handler-ran", result);
    }

    [Fact]
    public async Task PermissionEndpointFilter_ShortCircuits_WhenRoleHasNoneOfAcceptedPermissions()
    {
        var filter = new PermissionEndpointFilter(Permission.ViewAssignedClaims, Permission.ViewAllClaims);
        var context = CreateFilterContext(DevelopmentOptions, demoRole: "Client");
        var nextCalled = false;

        var result = await filter.InvokeAsync(context, _ =>
        {
            nextCalled = true;
            return ValueTask.FromResult<object?>("handler-ran");
        });

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status403Forbidden, await ExecuteStatusAsync((IResult)result!));
    }

    [Fact]
    public async Task PermissionEndpointFilter_RejectsImpersonation_OutsideDevelopmentMode()
    {
        var filter = new PermissionEndpointFilter(Permission.ManageKnowledgeBase);
        var context = CreateFilterContext(ProductionOptions, demoRole: "Admin");
        var nextCalled = false;

        var result = await filter.InvokeAsync(context, _ =>
        {
            nextCalled = true;
            return ValueTask.FromResult<object?>("handler-ran");
        });

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status403Forbidden, await ExecuteStatusAsync((IResult)result!));
    }

    [Fact]
    public void Program_ProtectsHighRiskRoutes_WithPermissionEndpointFilters()
    {
        var program = ReadProgramSource();

        AssertRouteHasPermission(
            program,
            "app.MapPost(\"/api/v1/cases\", CaseEndpoints.CreateCase)",
            "Permission.CreateClaim");
        AssertRouteHasPermission(
            program,
            "app.MapGet(\"/api/v1/cases\", CaseEndpoints.ListCases)",
            "Permission.ViewAssignedClaims, Permission.ViewAllClaims");
        AssertRouteHasPermission(
            program,
            "app.MapPost(\"/api/v1/assistant/policy-questions\", AssistantEndpoints.AskPolicyQuestion)",
            "Permission.ViewAiOutputs");
        AssertRouteHasPermission(
            program,
            "app.MapGet(\"/api/v1/command-center/metrics\", CommandCenterMetricsLoader.GetMetrics)",
            "Permission.ViewCommandCenter");
        AssertRouteHasPermission(
            program,
            "app.MapGet(\"/api/v1/provider-status\", ProviderStatusEndpoints.GetProviderStatus)",
            "Permission.ViewCommandCenter");
        AssertRouteHasPermission(
            program,
            "app.MapGet(\"/api/v1/integrations/email/health\", EmailIntegrationEndpoints.GetHealth)",
            "Permission.ConfigureIntegrations");
        AssertRouteHasPermission(
            program,
            "app.MapGet(\"/api/v1/cases/{caseId:guid}/documents/{documentId:guid}/content\", DocumentEndpoints.GetDocumentContent)",
            "Permission.ViewAssignedClaims, Permission.ViewAllClaims");
        AssertRouteHasPermission(
            program,
            "app.MapPost(\"/api/v1/cases/{caseId:guid}/documents/{documentId:guid}/correct-field\", DocumentEndpoints.CorrectExtractionField)",
            "Permission.UploadDocument");
        AssertRouteHasPermission(
            program,
            "app.MapPost(\"/api/v1/cases/{caseId:guid}/workflow/evaluate\", WorkflowEndpoints.EvaluateWorkflow)",
            "Permission.ApproveSettlementWorkflowStep");
        AssertRouteHasPermission(
            program,
            "app.MapPost(\"/api/v1/customer-updates/{updateId:guid}/send\", CustomerUpdateEndpoints.SendCustomerUpdate)",
            "Permission.SendCustomerUpdate");
        AssertRouteHasPermission(
            program,
            "app.MapGet(\"/api/v1/reports/operations-summary\", ReportsEndpoints.GetOperationsSummary)",
            "Permission.ViewCommandCenter");
        AssertRouteHasPermission(
            program,
            "app.MapGet(\"/api/v1/admin/users\", AdminEndpoints.GetUsers)",
            "Permission.ManageTenantSettings");
    }

    [Fact]
    public void Program_LeavesGraphWebhookReachableForProviderValidation()
    {
        var program = ReadProgramSource();

        var routeBlock = RouteBlock(
            program,
            "app.MapPost(\"/api/v1/integrations/email/graph/webhook\", EmailIntegrationEndpoints.GraphWebhook)");

        Assert.DoesNotContain("PermissionEndpointFilter", routeBlock);
    }

    [Fact]
    public void ProductionApiRuntime_DoesNotReferenceLocalTestDoubles()
    {
        var program = ReadProgramSource();
        var apiProject = ReadApiProjectSource();

        Assert.DoesNotContain("InsuranceIntelligence.LocalTestDoubles", program);
        Assert.DoesNotContain("LocalTestPermissionReadRepository", program);
        Assert.DoesNotContain("LocalTestAuditEventRepository", program);
        Assert.DoesNotContain("LocalTestClaimsReadRepository", program);
        Assert.DoesNotContain("LocalTestProviderHealthCheck", program);
        Assert.DoesNotContain("InsuranceIntelligence.LocalTestDoubles", apiProject);
        Assert.Contains("InsuranceIntelligence.Api cannot run with INSURANCEINTELLIGENCE_STORAGE_PROVIDER=InMemory", program);
    }

    [Fact]
    public async Task RuntimeContext_ReturnsDevelopmentRolePermissionsAndScope()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["x-demo-role"] = "ClaimsSupervisor";
        context.Request.Headers["x-demo-user-id"] = "dev-user";
        context.Request.Headers["X-Tenant-Id"] = "tenant-a";
        context.Request.Headers["X-Workspace-Id"] = "workspace-a";

        var result = await RuntimeContextEndpoints.GetMe(context, DevelopmentOptions, DevelopmentHost, EmptyWorkspaceCatalog, NoTenantContext, CancellationToken.None);

        var (status, json) = await ExecuteJsonAsync(result);
        Assert.Equal(StatusCodes.Status200OK, status);
        var root = json.RootElement;
        Assert.Equal("dev-user", root.GetProperty("userId").GetString());
        Assert.Equal("ClaimsSupervisor", root.GetProperty("role").GetString());
        Assert.Equal("Claims Manager", root.GetProperty("roleLabel").GetString());
        Assert.Equal("tenant-a", root.GetProperty("tenantId").GetString());
        Assert.Equal("workspace-a", root.GetProperty("workspaceId").GetString());
        Assert.Contains(root.GetProperty("permissions").EnumerateArray(), item => item.GetString() == "OverrideAiRecommendation");
    }

    [Fact]
    public async Task RuntimeContext_UsesFullWorkspaceAdministratorLabel()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["x-demo-role"] = "Admin";
        context.Request.Headers["x-demo-user-id"] = "dev-admin";
        context.Request.Headers["X-Tenant-Id"] = "tenant-a";
        context.Request.Headers["X-Workspace-Id"] = "workspace-a";

        var result = await RuntimeContextEndpoints.GetMe(context, DevelopmentOptions, DevelopmentHost, EmptyWorkspaceCatalog, NoTenantContext, CancellationToken.None);

        var (status, json) = await ExecuteJsonAsync(result);
        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.Equal("Workspace Administrator", json.RootElement.GetProperty("roleLabel").GetString());
    }

    [Fact]
    public async Task RuntimeContext_UsesResolvedTenantContextBeforeDevelopmentHeaders()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["x-demo-role"] = "Admin";
        context.Request.Headers["x-demo-user-id"] = "spoofed-user";
        context.Request.Headers["X-Tenant-Id"] = "tenant-spoofed";
        context.Request.Headers["X-Workspace-Id"] = "workspace-spoofed";
        var resolved = TenantContextWith("tenant-membership", "workspace-membership", "membership-runtime-user", "ClaimsSupervisor");

        var result = await RuntimeContextEndpoints.GetMe(context, DevelopmentOptions, DevelopmentHost, EmptyWorkspaceCatalog, resolved, CancellationToken.None);

        var (status, json) = await ExecuteJsonAsync(result);
        Assert.Equal(StatusCodes.Status200OK, status);
        var root = json.RootElement;
        Assert.Equal("membership-runtime-user", root.GetProperty("userId").GetString());
        Assert.Equal("ClaimsSupervisor", root.GetProperty("role").GetString());
        Assert.Equal("tenant-membership", root.GetProperty("tenantId").GetString());
        Assert.Equal("workspace-membership", root.GetProperty("workspaceId").GetString());
        Assert.Equal("DevelopmentTenantContext", root.GetProperty("source").GetString());
    }

    [Fact]
    public async Task RuntimeContext_RejectsDemoHeadersOutsideDevelopment()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["x-demo-role"] = "Admin";

        var result = await RuntimeContextEndpoints.GetMe(context, ProductionOptions, ProductionHost, EmptyWorkspaceCatalog, NoTenantContext, CancellationToken.None);

        Assert.Equal(StatusCodes.Status403Forbidden, await ExecuteStatusAsync(result));
    }

    [Fact]
    public async Task RuntimeContext_UsesMembershipStoreScopeOutsideDevelopment()
    {
        // ADR-0005: /me reflects the store-resolved membership (set by the per-request middleware),
        // NOT the token's tid/roles claims, even a token carrying different claims is ignored.
        var context = new DefaultHttpContext();
        context.RequestServices = ServicesWithOptions(ProductionDeploymentOptions);
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, "entra-user"),
            new Claim(ClaimTypes.Name, "Maya Morgan"),
            new Claim(ClaimTypes.Role, "Admin"),          // token claim, must be IGNORED
            new Claim("tenant_id", "attacker-tenant")      // token tid, must be IGNORED
        ], "Entra"));
        var resolved = TenantContextWith("tenant-a", "workspace-a", "entra-user", "ITAdmin");

        var result = await RuntimeContextEndpoints.GetMe(context, ProductionOptions, ProductionHost, EmptyWorkspaceCatalog, resolved, CancellationToken.None);

        var (status, json) = await ExecuteJsonAsync(result);
        Assert.Equal(StatusCodes.Status200OK, status);
        var root = json.RootElement;
        Assert.Equal("entra-user", root.GetProperty("userId").GetString());
        Assert.Equal("ITAdmin", root.GetProperty("role").GetString());          // from the store, not the "Admin" claim
        Assert.Equal("tenant-a", root.GetProperty("tenantId").GetString());     // not "attacker-tenant"
        Assert.Equal("workspace-a", root.GetProperty("workspaceId").GetString());
        Assert.Equal("MembershipStore", root.GetProperty("source").GetString());
    }

    [Fact]
    public async Task RuntimeContext_FailsClosedWhenNoMembership_OutsideDevelopment()
    {
        // Authenticated, but the middleware resolved no active IIP membership -> fail closed.
        var context = new DefaultHttpContext();
        context.RequestServices = ServicesWithOptions(ProductionDeploymentOptions);
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, "entra-user"),
            new Claim(ClaimTypes.Role, "ITAdmin")
        ], "Entra"));

        var result = await RuntimeContextEndpoints.GetMe(context, ProductionDeploymentOptions, ProductionHost, EmptyWorkspaceCatalog, NoTenantContext, CancellationToken.None);

        Assert.Equal(StatusCodes.Status403Forbidden, await ExecuteStatusAsync(result));
    }

    [Fact]
    public void TenantScopeReader_PrefersAuthenticatedClaimsOverHeaders()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Tenant-Id"] = "header-tenant";
        context.Request.Headers["X-Workspace-Id"] = "header-workspace";
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("tenant_id", "claim-tenant"),
            new Claim("workspace_id", "claim-workspace")
        ], "Entra"));

        var result = TenantScopeReader.TryGetFromHeadersOrQuery(context, out var scope, out var validationError);

        Assert.True(result, validationError);
        Assert.Equal("claim-tenant", scope.TenantId);
        Assert.Equal("claim-workspace", scope.WorkspaceId);
    }

    [Fact]
    public void TenantScopeReader_UsesDeploymentScopeOutsideDevelopment_WhenClaimsDoNotCarryScope()
    {
        var context = new DefaultHttpContext();
        context.RequestServices = ServicesWithOptions(ProductionDeploymentOptions);
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, "entra-user")
        ], "Entra"));

        var result = TenantScopeReader.TryGetFromHeadersOrQuery(context, out var scope, out var validationError);

        Assert.True(result, validationError);
        Assert.Equal("configured-tenant", scope.TenantId);
        Assert.Equal("configured-workspace", scope.WorkspaceId);
    }

    [Fact]
    public void TenantScopeReader_DoesNotTrustBrowserScopeOutsideDevelopment()
    {
        var context = new DefaultHttpContext();
        context.RequestServices = ServicesWithOptions(ProductionOptions);
        context.Request.Headers["X-Tenant-Id"] = "header-tenant";
        context.Request.Headers["X-Workspace-Id"] = "header-workspace";
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, "entra-user")
        ], "Entra"));

        var result = TenantScopeReader.TryGetFromHeadersOrQuery(context, out _, out _);

        Assert.False(result);
    }

    [Fact]
    public void TenantScopeReader_DoesNotUseDeploymentScopeForUnauthenticatedProtectedRequest()
    {
        var context = new DefaultHttpContext();
        context.RequestServices = ServicesWithOptions(ProductionDeploymentOptions);

        var result = TenantScopeReader.TryGetFromHeadersOrQuery(context, out _, out _);

        Assert.False(result);
    }

    [Fact]
    public async Task RuntimeHeaderGuard_RejectsScopeHeadersOutsideDevelopment()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Tenant-Id"] = "tenant-a";

        var result = RuntimeHeaderGuard.RejectBrowserAuthorityOutsideDevelopment(context, ProductionOptions);

        Assert.NotNull(result);
        Assert.Equal(StatusCodes.Status403Forbidden, await ExecuteStatusAsync(result!));
    }

    [Fact]
    public async Task RuntimeHeaderGuard_RejectsScopeQueryOutsideDevelopment()
    {
        var context = new DefaultHttpContext();
        context.Request.QueryString = new QueryString("?tenantId=tenant-a&workspaceId=workspace-a");

        var result = RuntimeHeaderGuard.RejectBrowserAuthorityOutsideDevelopment(context, ProductionOptions);

        Assert.NotNull(result);
        Assert.Equal(StatusCodes.Status403Forbidden, await ExecuteStatusAsync(result!));
    }

    [Fact]
    public void RuntimeHeaderGuard_AllowsDevelopmentScopeHeadersInDevelopment()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Tenant-Id"] = "tenant-a";
        context.Request.Headers["x-demo-role"] = "Admin";

        var result = RuntimeHeaderGuard.RejectBrowserAuthorityOutsideDevelopment(context, DevelopmentOptions);

        Assert.Null(result);
    }

    [Fact]
    public async Task RuntimeHeaderGuard_RejectsUnauthenticatedApiOutsideDevelopment()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/cases";

        var result = RuntimeHeaderGuard.RejectUnauthenticatedApiOutsideDevelopment(context, ProductionOptions);

        Assert.NotNull(result);
        Assert.Equal(StatusCodes.Status401Unauthorized, await ExecuteStatusAsync(result!));
    }

    [Fact]
    public void RuntimeHeaderGuard_AllowsAuthenticatedApiOutsideDevelopment()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/cases";
        context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "user-a")], "Entra"));

        var result = RuntimeHeaderGuard.RejectUnauthenticatedApiOutsideDevelopment(context, ProductionOptions);

        Assert.Null(result);
    }

    [Fact]
    public void RuntimeHeaderGuard_AllowsGraphWebhookOutsideDevelopment()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/integrations/email/graph/webhook";

        var result = RuntimeHeaderGuard.RejectUnauthenticatedApiOutsideDevelopment(context, ProductionOptions);

        Assert.Null(result);
    }

    [Fact]
    public void RuntimeHeaderGuard_AllowsClientPortalScopeHeadersOutsideDevelopment()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/client-portal/magic-links";
        context.Request.Headers["X-Tenant-Id"] = "configured-tenant";
        context.Request.Headers["X-Workspace-Id"] = "configured-workspace";

        Assert.Null(RuntimeHeaderGuard.RejectBrowserAuthorityOutsideDevelopment(context, ProductionOptions));
    }

    [Fact]
    public async Task RuntimeHeaderGuard_StillRejectsDemoIdentityHeadersOnClientPortalOutsideDevelopment()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/client-portal/magic-links";
        context.Request.Headers["x-demo-role"] = "Admin";

        var result = RuntimeHeaderGuard.RejectBrowserAuthorityOutsideDevelopment(context, ProductionOptions);

        Assert.NotNull(result);
        Assert.Equal(StatusCodes.Status403Forbidden, await ExecuteStatusAsync(result!));
    }

    [Theory]
    [InlineData("/api/v1/client-portal/magic-links")]
    [InlineData("/api/v1/client-portal/claims")]
    [InlineData("/api/v1/integrations/whatsapp/webhook")]
    [InlineData("/api/v1/academy/public/courses")]
    [InlineData("/api/v1/academy/public/courses/ai-agentic-insurance")]
    [InlineData("/api/v1/academy/public/certificates/IIPA-AAAA-BBBB-CCCC")]
    [InlineData("/api/v1/academy/me")]
    public void RuntimeHeaderGuard_AllowsCallersWithoutEntraTokensOnTheirOwnRoutesOutsideDevelopment(string path)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;

        Assert.Null(RuntimeHeaderGuard.RejectUnauthenticatedApiOutsideDevelopment(context, ProductionOptions));
    }

    [Theory]
    [InlineData("/api/v1/academy/learner")]
    [InlineData("/api/v1/academy/community")]
    [InlineData("/api/v1/academy/courses/ai-agentic-insurance/enrol")]
    [InlineData("/api/v1/academy/operator/finals")]
    [InlineData("/api/v1/academy/zenith/ask")]
    [InlineData("/api/v1/academy/me/extra")]
    [InlineData("/api/v1/academy/publicity")]
    public async Task RuntimeHeaderGuard_StillRequiresSignInForTheRestOfTheAcademyApi(string path)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;

        var result = RuntimeHeaderGuard.RejectUnauthenticatedApiOutsideDevelopment(context, ProductionOptions);

        Assert.NotNull(result);
        Assert.Equal(StatusCodes.Status401Unauthorized, await ExecuteStatusAsync(result!));
    }

    [Fact]
    public void ClientPortalScopeReader_PinsScopeToTheDeploymentOutsideDevelopment()
    {
        var context = new DefaultHttpContext { RequestServices = ServicesWithOptions(ProductionDeploymentOptions) };

        Assert.True(ClientPortalScopeReader.TryGetFromHeaders(context, out var scope, out _));
        Assert.Equal("configured-tenant", scope.TenantId);
        Assert.Equal("configured-workspace", scope.WorkspaceId);
    }

    [Fact]
    public void ClientPortalScopeReader_RefusesAHeaderNamingAnotherTenantOutsideDevelopment()
    {
        var context = new DefaultHttpContext { RequestServices = ServicesWithOptions(ProductionDeploymentOptions) };
        context.Request.Headers["X-Tenant-Id"] = "some-other-tenant";

        Assert.False(ClientPortalScopeReader.TryGetFromHeaders(context, out _, out var error));
        Assert.Contains("different insurer", error);
    }

    [Fact]
    public void SendCustomerUpdatePermission_IsGrantedOnlyToCustomerCommunicationRoles()
    {
        Assert.True(RolePermissionMap.HasPermission(PlatformRole.CustomerService, Permission.SendCustomerUpdate));
        Assert.True(RolePermissionMap.HasPermission(PlatformRole.ClaimsSupervisor, Permission.SendCustomerUpdate));
        Assert.False(RolePermissionMap.HasPermission(PlatformRole.Client, Permission.SendCustomerUpdate));
    }

    [Fact]
    public async Task SendCustomerUpdatePermissionFilter_BlocksClientRole()
    {
        var filter = new PermissionEndpointFilter(Permission.SendCustomerUpdate);
        var context = CreateFilterContext(DevelopmentOptions, demoRole: "Client");

        var result = await filter.InvokeAsync(context, _ => ValueTask.FromResult<object?>("handler-ran"));

        Assert.Equal(StatusCodes.Status403Forbidden, await ExecuteStatusAsync((IResult)result!));
    }

    [Fact]
    public void CustomerUpdateActionState_BlocksSendingAiDraftBeforeHumanApproval()
    {
        var update = CustomerUpdate(status: CustomerUpdateStatus.Draft, requiresApproval: true);
        var response = WorkflowMapper.ToResponse(update);

        Assert.DoesNotContain(response.AllowedActions, action => action.Action == "sendCustomerUpdate");
        Assert.Contains(response.BlockedActions, action =>
            action.Action == "sendCustomerUpdate"
            && action.Reason is not null
            && action.Reason.Contains("requires human approval", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CustomerSafeDtos_DoNotExposeInternalIntelligenceFields()
    {
        var forbiddenFragments = new[] { "risk", "fraud", "confidence", "authority", "reserve", "prompt", "audit", "provenance", "internal" };
        var dtoTypes = new[]
        {
            typeof(CustomerClaimDto),
            typeof(CustomerTimelineItemDto),
            typeof(CustomerRequiredDocumentDto),
            typeof(CustomerDocumentDto),
            typeof(CustomerMessageDto),
            typeof(CustomerSupportInsightDto)
        };

        foreach (var property in dtoTypes.SelectMany(type => type.GetProperties()))
        {
            Assert.DoesNotContain(forbiddenFragments, fragment => property.Name.Contains(fragment, StringComparison.OrdinalIgnoreCase));
        }
    }

    private static EndpointFilterInvocationContext CreateFilterContext(InfrastructureOptions options, string demoRole)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["x-demo-role"] = demoRole;
        var services = new ServiceCollection();
        services.AddSingleton(options);
        httpContext.RequestServices = services.BuildServiceProvider();
        return EndpointFilterInvocationContext.Create(httpContext);
    }

    private static DefaultHttpContext AuthenticatedContext(string role)
    {
        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, "entra-user"),
            new Claim(ClaimTypes.Role, role),
            new Claim("tenant_id", "tenant-a"),
            new Claim("workspace_id", "workspace-a")
        ], "Entra"));
        return context;
    }

    // Simulates the per-request middleware having resolved the user's effective membership from the
    // store and stashed the resulting permissions (ADR-0005).
    private static DefaultHttpContext AuthenticatedContextWithStore(params Permission[] permissions)
    {
        var context = AuthenticatedContext("ITAdmin");
        context.Items[DemoAuthorization.EffectivePermissionsItemKey] = (IReadOnlyCollection<Permission>)permissions;
        return context;
    }

    private static CustomerUpdateDraft CustomerUpdate(CustomerUpdateStatus status, bool requiresApproval) =>
        new()
        {
            UpdateId = Guid.NewGuid(),
            Scope = TenantScope.Create("tenant-a", "workspace-a"),
            CaseId = Guid.NewGuid(),
            ReasonCategory = CustomerUpdateReasonCategory.StatusUpdate,
            Channel = CommunicationChannel.Email,
            Status = status,
            MessageBody = "Customer update draft.",
            ShortMessageBody = "Customer update draft.",
            CreationMethod = CustomerUpdateCreationMethod.ManuallyWritten,
            RequiresApproval = requiresApproval,
            Sensitive = false,
            CreatedByUserId = "claims-user",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            CorrelationId = Guid.NewGuid().ToString("N")
        };

    private static ServiceProvider ServicesWithOptions(InfrastructureOptions options) =>
        new ServiceCollection()
            .AddSingleton(options)
            .BuildServiceProvider();

    private static string ReadProgramSource([CallerFilePath] string sourceFilePath = "")
    {
        return ReadRepositoryFile(Path.Combine("src", "InsuranceIntelligence.Api", "Program.cs"), sourceFilePath);
    }

    // Program.cs registers GET /api/v1/customers (CustomerProfileEndpoints.ListCustomers), and
    // Domain/CustomerEndpoints.cs maps a group on the same base path. A `group.MapGet("/")` there
    // produces a second endpoint matching the same request, so ASP.NET throws
    // AmbiguousMatchException at request time and the Customers page returns 500, invisible to a
    // build and to every handler-level test, because both registrations are individually valid.
    [Fact]
    public void CustomerGroup_DoesNotRestOnTheSamePathAsTheCustomerList()
    {
        var program = ReadProgramSource();
        Assert.Contains("app.MapGet(\"/api/v1/customers\", CustomerProfileEndpoints.ListCustomers)", program);

        // Comment lines are stripped: the fix for this bug documents the offending call in prose,
        // and the guard must assert on code rather than on the explanation of the code.
        var group = string.Join(
            '\n',
            ReadCustomerEndpointsSource()
                .Split('\n')
                .Where(line => !line.TrimStart().StartsWith("//", StringComparison.Ordinal)));

        Assert.Contains("MapGroup(\"/api/v1/customers\")", group);
        Assert.DoesNotContain("group.MapGet(\"/\"", group);
        Assert.DoesNotContain("group.MapGet(\"\"", group);
    }

    private static string ReadCustomerEndpointsSource([CallerFilePath] string sourceFilePath = "")
    {
        return ReadRepositoryFile(
            Path.Combine("src", "InsuranceIntelligence.Api", "Domain", "CustomerEndpoints.cs"),
            sourceFilePath);
    }

    private static string ReadApiProjectSource([CallerFilePath] string sourceFilePath = "")
    {
        return ReadRepositoryFile(
            Path.Combine("src", "InsuranceIntelligence.Api", "InsuranceIntelligence.Api.csproj"),
            sourceFilePath);
    }

    private static string ReadRepositoryFile(string relativePath, string sourceFilePath)
    {
        foreach (var startPath in new[]
                 {
                     Path.GetDirectoryName(sourceFilePath) ?? string.Empty,
                     Directory.GetCurrentDirectory(),
                     AppContext.BaseDirectory
                 })
        {
            if (string.IsNullOrWhiteSpace(startPath))
            {
                continue;
            }

            var directory = new DirectoryInfo(startPath);
            while (directory is not null)
            {
                var candidate = Path.Combine(directory.FullName, relativePath);
                if (File.Exists(candidate))
                {
                    return File.ReadAllText(candidate);
                }

                directory = directory.Parent;
            }
        }

        throw new FileNotFoundException($"Could not locate {relativePath} from the test runtime paths.");
    }

    private static void AssertRouteHasPermission(string program, string route, string permissionExpression)
    {
        var routeBlock = RouteBlock(program, route);

        Assert.Contains("PermissionEndpointFilter", routeBlock);
        Assert.Contains(permissionExpression, routeBlock);
    }

    private static string RouteBlock(string program, string route)
    {
        var start = program.IndexOf(route, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Route not found in Program.cs: {route}");

        var nextRoute = program.IndexOf("\r\napp.Map", start + route.Length, StringComparison.Ordinal);
        if (nextRoute < 0)
        {
            nextRoute = program.IndexOf("\napp.Map", start + route.Length, StringComparison.Ordinal);
        }

        return nextRoute < 0 ? program[start..] : program[start..nextRoute];
    }

    private static async Task<int> ExecuteStatusAsync(IResult result)
    {
        var httpContext = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection().AddLogging().AddProblemDetails().BuildServiceProvider()
        };
        httpContext.Response.Body = new MemoryStream();
        await result.ExecuteAsync(httpContext);
        return httpContext.Response.StatusCode;
    }

    private static async Task<(int StatusCode, JsonDocument Json)> ExecuteJsonAsync(IResult result)
    {
        var httpContext = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection().AddLogging().AddProblemDetails().BuildServiceProvider()
        };
        httpContext.Response.Body = new MemoryStream();
        await result.ExecuteAsync(httpContext);
        httpContext.Response.Body.Position = 0;
        return (httpContext.Response.StatusCode, await JsonDocument.ParseAsync(httpContext.Response.Body));
    }
}
