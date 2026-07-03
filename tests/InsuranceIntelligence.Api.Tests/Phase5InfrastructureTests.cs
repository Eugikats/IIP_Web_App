using System.Text.Json;
using InsuranceIntelligence.Api.Admin;
using InsuranceIntelligence.Api.Compliance;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Documents;
using InsuranceIntelligence.Domain.Knowledge;
using InsuranceIntelligence.Domain.Workflow;
using InsuranceIntelligence.Infrastructure.Configuration;
using InsuranceIntelligence.Infrastructure.Persistence;
using InsuranceIntelligence.Infrastructure.Simulated;
using InsuranceIntelligence.LocalTestDoubles;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace InsuranceIntelligence.Api.Tests;

public sealed class Phase5InfrastructureTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task EfRepository_CreatesRetrievesAndIsolatesCase()
    {
        await using var fixture = await EfFixture.CreateAsync();
        var repository = new EfCaseRepository(fixture.DbContext);
        var scope = TenantScope.Create("tenant-a", "workspace-a");
        var claimCase = Case(scope);
        await repository.SaveAsync(claimCase);

        var sameScope = await repository.ListAsync(scope);
        var otherScope = await repository.ListAsync(TenantScope.Create("tenant-b", "workspace-a"));

        Assert.Single(sameScope);
        Assert.Empty(otherScope);
        Assert.Equal(claimCase.CaseId, (await repository.GetAsync(claimCase.CaseId))!.CaseId);
    }

    [Fact]
    public async Task EfRepository_PreservesAuditEvents()
    {
        await using var fixture = await EfFixture.CreateAsync();
        var repository = new EfAuditRepository(fixture.DbContext);
        var claimCase = Case(TenantScope.Create("tenant-a", "workspace-a"));
        await repository.WriteAsync(AuditEvent.CaseCreated(claimCase.ToAuditSnapshot(), "user", "corr"));

        var events = await repository.ListAsync(claimCase.Scope);

        Assert.Single(events);
        Assert.Equal("case.created", events.First().ActionName);
    }

    [Fact]
    public async Task AdminTenantConfigUpdate_WorksForAdmin()
    {
        var repository = new InMemoryTenantConfigurationRepository();
        var auditRepository = new InMemoryAuditRepository();
        const string auditReason = "Tenant administrator approved the insurer configuration update.";
        var context = ScopedContext(role: "Admin", auditReason: auditReason);
        var request = TenantConfigurationRequest();

        var result = await AdminEndpoints.UpdateTenantConfiguration(request, repository, auditRepository, Options(), context, CancellationToken.None);
        var (status, body) = await ExecuteAsync<TenantConfiguration>(result);
        var auditEvents = await auditRepository.ListAsync(TenantScope.Create("tenant-demo", "workspace-claims"));

        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.Equal("Updated Insurer", body!.InsurerName);
        Assert.Contains(auditEvents, auditEvent =>
            auditEvent.ActionName == "admin.tenant_configuration.updated"
            && auditEvent.UserId == "local-admin"
            && auditEvent.AuditReason == auditReason
            && auditEvent.EvidenceIds.Contains("insurerProfile")
            && auditEvent.EvidenceIds.Contains("auditRetention"));
    }

    [Fact]
    public async Task AdminTenantConfigFirstUpdate_AuditsOnboardingTransition()
    {
        var repository = new InMemoryTenantConfigurationRepository();
        var auditRepository = new InMemoryAuditRepository();
        var scope = TenantScope.Create("tenant-demo", "workspace-claims");
        await repository.SaveAsync(TenantConfiguration.Unconfigured(scope));

        var result = await AdminEndpoints.UpdateTenantConfiguration(
            TenantConfigurationRequest(),
            repository,
            auditRepository,
            Options(),
            ScopedContext(role: "Admin", auditReason: "Configure insurer profile during governed tenant onboarding."),
            CancellationToken.None);
        var (status, _) = await ExecuteAsync<TenantConfiguration>(result);
        var auditEvent = Assert.Single(await auditRepository.ListAsync(scope));

        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.Equal("NotConfigured", auditEvent.BeforeStatus);
        Assert.Equal("Configured", auditEvent.AfterStatus);
    }

    [Fact]
    public async Task AdminTenantConfigUpdate_RejectsMissingReasonBeforePersistence()
    {
        var repository = new InMemoryTenantConfigurationRepository();
        var auditRepository = new InMemoryAuditRepository();
        var scope = TenantScope.Create("tenant-demo", "workspace-claims");
        var before = await repository.GetAsync(scope);

        var result = await AdminEndpoints.UpdateTenantConfiguration(
            TenantConfigurationRequest(),
            repository,
            auditRepository,
            Options(),
            ScopedContext(role: "Admin"),
            CancellationToken.None);
        var (status, body) = await ExecuteAsync<JsonElement>(result);
        var after = await repository.GetAsync(scope);

        Assert.Equal(StatusCodes.Status400BadRequest, status);
        Assert.True(body.GetProperty("errors").TryGetProperty("auditReason", out _));
        Assert.Equal(before.InsurerName, after.InsurerName);
        Assert.Equal(before.AuditRetentionDays, after.AuditRetentionDays);
        Assert.Empty(await auditRepository.ListAsync(scope));
    }

    [Fact]
    public async Task AdminTenantConfigUpdate_RejectsNoOpMutation()
    {
        var repository = new InMemoryTenantConfigurationRepository();
        var auditRepository = new InMemoryAuditRepository();
        var current = await repository.GetAsync(TenantScope.Create("tenant-demo", "workspace-claims"));
        var request = new UpdateTenantConfigurationRequest(
            current.InsurerName,
            current.LogoUrl,
            current.Country,
            current.Region,
            current.BusinessLines,
            current.Channels,
            current.ApprovalThresholds,
            current.Checklists,
            current.AuditRetentionDays);

        var result = await AdminEndpoints.UpdateTenantConfiguration(
            request,
            repository,
            auditRepository,
            Options(),
            ScopedContext(role: "Admin", auditReason: "Attempt no-op tenant update."),
            CancellationToken.None);
        var (status, _) = await ExecuteAsync<JsonElement>(result);

        Assert.Equal(StatusCodes.Status409Conflict, status);
    }

    [Fact]
    public async Task AdminUserRoleUpdate_PersistsAuditReasonHeader()
    {
        var userRepository = new InMemoryUserAccountRepository();
        var auditRepository = new InMemoryAuditRepository();
        var scope = TenantScope.Create("tenant-demo", "workspace-claims");
        await userRepository.SaveAsync(new InsuranceUserAccount
        {
            UserId = "user-claims-1",
            Scope = scope,
            Name = "Claims Handler",
            Email = "claims@example.test",
            Phone = null,
            Role = PlatformRole.ClaimsOfficer,
            Department = "Claims",
            Branch = "Head Office"
        });

        const string auditReason = "Tenant admin approved role escalation for claims supervision coverage.";
        var result = await AdminEndpoints.UpdateUserRole(
            "user-claims-1",
            new UpdateUserRoleRequest("ClaimsSupervisor"),
            userRepository,
            auditRepository,
            Options(),
            ScopedContext(role: "Admin", auditReason: auditReason),
            CancellationToken.None);
        var (status, body) = await ExecuteAsync<AdminUserResponse>(result);
        var auditEvents = await auditRepository.ListAsync(scope, CancellationToken.None);

        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.Equal("ClaimsSupervisor", body!.Role);
        Assert.Contains(body.AllowedActions, action => action.Action == "updateUserRole");
        Assert.Contains(auditEvents, audit =>
            audit.ActionName == "admin.user_role.updated"
            && audit.BeforeStatus == "ClaimsOfficer"
            && audit.AfterStatus == "ClaimsSupervisor"
            && audit.AuditReason == auditReason
            && audit.UserId == "local-admin");
    }

    [Fact]
    public async Task AdminUserRoleUpdate_RejectsMissingAuditReasonBeforeMutation()
    {
        var userRepository = new InMemoryUserAccountRepository();
        var auditRepository = new InMemoryAuditRepository();
        var scope = TenantScope.Create("tenant-demo", "workspace-claims");
        await userRepository.SaveAsync(new InsuranceUserAccount
        {
            UserId = "user-claims-1",
            Scope = scope,
            Name = "Claims Handler",
            Email = "claims@example.test",
            Role = PlatformRole.ClaimsOfficer
        });

        var result = await AdminEndpoints.UpdateUserRole(
            "user-claims-1",
            new UpdateUserRoleRequest("ClaimsSupervisor"),
            userRepository,
            auditRepository,
            Options(),
            ScopedContext(role: "Admin"),
            CancellationToken.None);
        var (status, body) = await ExecuteAsync<JsonElement>(result);
        var persisted = await userRepository.GetAsync("user-claims-1", scope, CancellationToken.None);
        var auditEvents = await auditRepository.ListAsync(scope, CancellationToken.None);

        Assert.Equal(StatusCodes.Status400BadRequest, status);
        Assert.True(body.GetProperty("errors").TryGetProperty("auditReason", out _));
        Assert.Equal(PlatformRole.ClaimsOfficer, persisted!.Role);
        Assert.Empty(auditEvents);
    }

    [Fact]
    public async Task AdminUserRoleUpdate_BlocksDemotionOfLastActiveAdmin()
    {
        var userRepository = new InMemoryUserAccountRepository();
        var auditRepository = new InMemoryAuditRepository();
        var scope = TenantScope.Create("tenant-demo", "workspace-claims");
        await userRepository.SaveAsync(new InsuranceUserAccount
        {
            UserId = "last-admin",
            Scope = scope,
            Name = "Last Administrator",
            Email = "admin@example.test",
            Role = PlatformRole.Admin,
            Status = "Active"
        });

        var result = await AdminEndpoints.UpdateUserRole(
            "last-admin",
            new UpdateUserRoleRequest("ClaimsOfficer"),
            userRepository,
            auditRepository,
            Options(),
            ScopedContext(role: "Admin", auditReason: "Attempt tenant administrator demotion."),
            CancellationToken.None);
        var (status, body) = await ExecuteAsync<JsonElement>(result);
        var persisted = await userRepository.GetAsync("last-admin", scope, CancellationToken.None);

        Assert.Equal(StatusCodes.Status409Conflict, status);
        Assert.Equal("updateUserRole", body.GetProperty("blockedAction").GetString());
        Assert.Equal(PlatformRole.Admin, persisted!.Role);
    }

    [Fact]
    public async Task AdminRoles_AreDeterministicAndDoNotExposeDemoTenantScope()
    {
        var first = AdminEndpoints.GetRoles(Options(), ScopedContext(role: "Admin"));
        var second = AdminEndpoints.GetRoles(Options(), ScopedContext(role: "Admin"));
        var (firstStatus, firstBody) = await ExecuteAsync<AdminRoleResponse[]>(first);
        var (secondStatus, secondBody) = await ExecuteAsync<AdminRoleResponse[]>(second);
        var serialized = JsonSerializer.Serialize(firstBody);

        Assert.Equal(StatusCodes.Status200OK, firstStatus);
        Assert.Equal(StatusCodes.Status200OK, secondStatus);
        Assert.Equal(JsonSerializer.Serialize(firstBody), JsonSerializer.Serialize(secondBody));
        Assert.DoesNotContain("tenant-demo", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("workspace-claims", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.All(firstBody!, role => Assert.NotEmpty(role.Permissions));
    }

    [Fact]
    public async Task RoleWithoutPermission_GetsForbidden()
    {
        var result = AdminEndpoints.GetProviderModes(Options(), ScopedContext(role: "Client"));
        var (status, _) = await ExecuteAsync<JsonElement>(result);

        Assert.Equal(StatusCodes.Status403Forbidden, status);
    }

    [Fact]
    public async Task AuditPack_ExcludesUnsafeContent()
    {
        var scope = TenantScope.Create("tenant-demo", "workspace-claims");
        var claimCase = Case(scope);
        var caseRepository = new InMemoryCaseRepository();
        var auditRepository = new InMemoryAuditRepository();
        var documentRepository = new InMemoryCaseDocumentRepository();
        var actionRepository = new InMemoryAgentActionLogRepository();
        var approvalRepository = new InMemoryApprovalRepository();
        var updateRepository = new InMemoryCustomerUpdateRepository();
        await caseRepository.SaveAsync(claimCase);
        await auditRepository.WriteAsync(AuditEvent.CaseCreated(claimCase.ToAuditSnapshot(), "user", "corr"));
        await actionRepository.SaveAsync(new AgentActionLog
        {
            AgentActionLogId = Guid.NewGuid(),
            Scope = scope,
            CaseId = claimCase.CaseId,
            UserId = "user",
            AgentName = "PolicyQnA",
            AssistantMode = AssistantMode.PolicyQnA,
            InputSummary = "raw prompt",
            RetrievedSources = [],
            ModelOutput = "system message",
            ConfidenceScore = AiConfidenceScore.Create(0.5m),
            RiskFlag = "needs_review",
            HumanOverride = false,
            CreatedAt = DateTimeOffset.UtcNow,
            CorrelationId = "corr"
        });

        var result = await ComplianceEndpoints.GetAuditPack(claimCase.CaseId, caseRepository, auditRepository, documentRepository, actionRepository, approvalRepository, updateRepository, Options(), ScopedContext(), CancellationToken.None);
        var (status, body) = await ExecuteAsync<JsonElement>(result);

        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.Empty(body.GetProperty("assistantActions").EnumerateArray());
    }

    [Fact]
    public async Task EfTenantConfigurationRepository_MissingRecordReturnsUnconfiguredTenant()
    {
        await using var fixture = await EfFixture.CreateAsync();
        var repository = new EfTenantConfigurationRepository(fixture.DbContext);
        var scope = TenantScope.Create("tenant-production", "workspace-onboarding");

        var configuration = await repository.GetAsync(scope);

        Assert.True(configuration.Scope.Matches(scope));
        Assert.Equal(string.Empty, configuration.InsurerName);
        Assert.Empty(configuration.BusinessLines);
        Assert.Empty(configuration.Channels);
        Assert.Equal("NotConfigured", configuration.ProviderModes.StorageProvider);
        Assert.Equal("NotConfigured", configuration.ProviderModes.AuthMode);
        Assert.False(configuration.ProviderModes.SeedDemoData);
    }

    [Fact]
    public async Task SeedData_CreatesExpectedDemoRecords()
    {
        await using var fixture = await EfFixture.CreateAsync();
        var services = new ServiceCollection()
            .AddSingleton<ITenantConfigurationRepository>(new EfTenantConfigurationRepository(fixture.DbContext))
            .AddSingleton<IUserAccountRepository>(new EfUserAccountRepository(fixture.DbContext))
            .AddSingleton<ICaseRepository>(new EfCaseRepository(fixture.DbContext))
            .AddSingleton<IKnowledgeDocumentRepository>(new EfKnowledgeDocumentRepository(fixture.DbContext))
            .AddSingleton<IApprovalRepository>(new EfApprovalRepository(fixture.DbContext))
            .AddSingleton<ICustomerUpdateRepository>(new EfCustomerUpdateRepository(fixture.DbContext))
            .AddSingleton<IAuditRepository>(new EfAuditRepository(fixture.DbContext))
            .BuildServiceProvider();

        var scope = TenantScope.Create("tenant-demo", "workspace-claims");
        await DemoDataSeeder.SeedAsync(services);
        var cases = await services.GetRequiredService<ICaseRepository>().ListAsync(scope);
        var users = await services.GetRequiredService<IUserAccountRepository>().ListAsync(scope);
        var approvals = await services.GetRequiredService<IApprovalRepository>().ListAsync(scope);
        var updates = await services.GetRequiredService<ICustomerUpdateRepository>().ListAsync(scope);
        var audits = await services.GetRequiredService<IAuditRepository>().ListAsync(scope);

        // Enriched demo spread: multiple claims across SLA states, pending approvals,
        // draft customer updates, and audit trail — enough for every Command Center block.
        Assert.True(cases.Count > 1);
        Assert.Contains(users, user => user.Role == PlatformRole.Admin);
        Assert.Contains(approvals, approval => approval.Status == ApprovalStatus.Pending);
        Assert.Contains(updates, update => update.Status == CustomerUpdateStatus.Draft && update.RequiresApproval);
        Assert.Contains(audits, evt => evt.ActionName == "case.created");
    }

    private static InfrastructureOptions Options() => new() { AuthMode = "Development", StorageProvider = "InMemory" };

    private static UpdateTenantConfigurationRequest TenantConfigurationRequest() =>
        new(
            "Updated Insurer",
            null,
            "Uganda",
            "East Africa",
            [new BusinessLineConfiguration("Motor", true)],
            [new ChannelConfiguration("Email", true)],
            [new ApprovalThresholdConfiguration("Motor", 1000m, "ClaimsSupervisor")],
            [new ChecklistConfiguration("Motor", ["ClaimForm"])],
            365);

    private static DefaultHttpContext ScopedContext(string role = "Admin", string? auditReason = null)
    {
        var context = new DefaultHttpContext { TraceIdentifier = "trace" };
        context.Request.Headers["X-Tenant-Id"] = "tenant-demo";
        context.Request.Headers["X-Workspace-Id"] = "workspace-claims";
        context.Request.Headers["x-demo-role"] = role;
        context.Request.Headers["x-demo-user-id"] = "local-admin";
        if (auditReason is not null)
        {
            context.Request.Headers["X-Audit-Reason"] = auditReason;
        }
        context.RequestServices = new ServiceCollection().AddLogging().AddProblemDetails().BuildServiceProvider();
        return context;
    }

    private static ClaimCase Case(TenantScope scope) =>
        new()
        {
            CaseId = Guid.NewGuid(),
            Scope = scope,
            CustomerName = "Amina",
            CustomerEmail = "amina@example.test",
            CustomerPhone = "+256700000000",
            PolicyNumber = "POL-1",
            ProductLine = "Motor",
            ClaimType = "Accident",
            Channel = "WebPortal",
            IncidentDate = DateOnly.FromDateTime(DateTime.UtcNow.Date),
            IncidentDescription = "Demo",
            Status = CaseStatus.New,
            CompletenessScore = 0m,
            CreatedByUserId = "user",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            CorrelationId = "corr"
        };

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

    private sealed class EfFixture : IAsyncDisposable
    {
        public InsuranceIntelligenceDbContext DbContext { get; }

        private EfFixture(InsuranceIntelligenceDbContext dbContext)
        {
            DbContext = dbContext;
        }

        public static Task<EfFixture> CreateAsync()
        {
            var options = new DbContextOptionsBuilder<InsuranceIntelligenceDbContext>()
                .UseInMemoryDatabase($"insuranceintelligence-tests-{Guid.NewGuid():N}")
                .Options;
            var dbContext = new InsuranceIntelligenceDbContext(options);
            return Task.FromResult(new EfFixture(dbContext));
        }

        public async ValueTask DisposeAsync()
        {
            await DbContext.DisposeAsync();
        }
    }
}
