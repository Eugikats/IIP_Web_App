using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Infrastructure.Configuration;
using InsuranceIntelligence.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InsuranceIntelligence.Domain.Tests;

public sealed class CorePortsEfImplTests
{
    [Fact]
    public void TenantContextAccessorEf_RequiresServerResolvedContext()
    {
        var accessor = new TenantContextAccessorEf();

        Assert.Throws<InvalidOperationException>(() => accessor.Current);
        Assert.Throws<InvalidOperationException>(() =>
            accessor.SetCurrent(new TenantContext("", "workspace-a", "user-1", [])));
        Assert.Throws<InvalidOperationException>(() =>
            accessor.SetCurrent(new TenantContext("tenant-a", "", "user-1", [])));
        Assert.Throws<InvalidOperationException>(() =>
            accessor.SetCurrent(new TenantContext("tenant-a", "workspace-a", "", [])));

        var context = new TenantContext("tenant-a", "workspace-a", "user-1", [PlatformRole.ClaimsOfficer.ToString()]);
        accessor.SetCurrent(context);

        Assert.Same(context, accessor.Current);
    }

    [Fact]
    public async Task PermissionServiceEf_UsesTenantScopedPersistedPermissions()
    {
        await using var dbContext = CreateDbContext();
        dbContext.UserTenantMemberships.Add(new UserTenantMembershipEntity
        {
            Id = Guid.NewGuid(),
            TenantId = "tenant-a",
            WorkspaceId = "workspace-a",
            UserId = "user-1",
            IsActive = true
        });
        dbContext.RoleAssignments.Add(new RoleAssignmentEntity
        {
            Id = Guid.NewGuid(),
            TenantId = "tenant-a",
            WorkspaceId = "workspace-a",
            UserId = "user-1",
            RoleCode = "CustomClaimsReader"
        });
        dbContext.RolePermissions.Add(new RolePermissionEntity
        {
            Id = Guid.NewGuid(),
            TenantId = "tenant-a",
            WorkspaceId = "workspace-a",
            RoleCode = "CustomClaimsReader",
            PermissionCode = Permission.ViewAssignedClaims.ToString()
        });
        dbContext.RolePermissions.Add(new RolePermissionEntity
        {
            Id = Guid.NewGuid(),
            TenantId = "tenant-b",
            WorkspaceId = "workspace-a",
            RoleCode = "CustomClaimsReader",
            PermissionCode = Permission.ManageTenantSettings.ToString()
        });
        await dbContext.SaveChangesAsync();

        var contextAccessor = CurrentContext("tenant-a", "workspace-a", "user-1");
        var service = new PermissionServiceEf(dbContext, contextAccessor);

        Assert.True(await service.HasPermissionAsync(Permission.ViewAssignedClaims));
        Assert.False(await service.HasPermissionAsync(Permission.ManageTenantSettings));
        await Assert.ThrowsAsync<PermissionDeniedException>(() =>
            service.RequirePermissionAsync(Permission.ManageTenantSettings));
    }

    [Fact]
    public async Task PermissionServiceEf_DeniesInactiveMembership()
    {
        await using var dbContext = CreateDbContext();
        dbContext.UserTenantMemberships.Add(new UserTenantMembershipEntity
        {
            Id = Guid.NewGuid(),
            TenantId = "tenant-a",
            WorkspaceId = "workspace-a",
            UserId = "user-1",
            IsActive = false
        });
        dbContext.RoleAssignments.Add(new RoleAssignmentEntity
        {
            Id = Guid.NewGuid(),
            TenantId = "tenant-a",
            WorkspaceId = "workspace-a",
            UserId = "user-1",
            RoleCode = PlatformRole.Admin.ToString()
        });
        await dbContext.SaveChangesAsync();

        var service = new PermissionServiceEf(dbContext, CurrentContext("tenant-a", "workspace-a", "user-1"));

        Assert.False(await service.HasPermissionAsync(Permission.ManageTenantSettings));
    }

    [Fact]
    public async Task AuditWriterEf_PersistsOnlyCurrentTenantScope()
    {
        await using var dbContext = CreateDbContext();
        var writer = new AuditWriterEf(dbContext, CurrentContext("tenant-a", "workspace-a", "user-1"));
        var auditEvent = AuditEventFor(TenantScope.Create("tenant-a", "workspace-a"));

        await writer.WriteAsync(auditEvent);

        var persisted = Assert.Single(dbContext.AuditEvents);
        Assert.Equal(auditEvent.EventId, persisted.Id);
        Assert.Equal("tenant-a", persisted.TenantId);
        Assert.Equal("workspace-a", persisted.WorkspaceId);
        Assert.Contains("core_port.test", persisted.PayloadJson, StringComparison.Ordinal);

        var otherTenantEvent = AuditEventFor(TenantScope.Create("tenant-b", "workspace-a"));
        await Assert.ThrowsAsync<AuditScopeException>(() => writer.WriteAsync(otherTenantEvent));
    }

    [Fact]
    public async Task ProviderHealthCheckEf_ReportsSqlProviderStateWithoutSimulation()
    {
        await using var dbContext = CreateDbContext();
        var notConfigured = new ProviderHealthCheckEf(dbContext, new InfrastructureOptions
        {
            StorageProvider = "InMemory"
        });

        var notConfiguredStatus = await notConfigured.CheckAsync("SqlServer");

        Assert.Equal("not_configured", notConfiguredStatus.Status);

        var missingConnection = new ProviderHealthCheckEf(dbContext, new InfrastructureOptions
        {
            StorageProvider = "SqlServer"
        });

        var missingStatus = await missingConnection.CheckAsync("AzureSql");

        Assert.Equal("missing", missingStatus.Status);
        Assert.Contains("connection string", missingStatus.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ProviderHealthCheckEf_DelegatesConfiguredAzureProviderChecks()
    {
        await using var dbContext = CreateDbContext();
        var healthCheck = new ProviderHealthCheckEf(dbContext, new InfrastructureOptions
        {
            AiProvider = "AzureOpenAI",
            AzureOpenAI = new AzureOpenAiOptions
            {
                Endpoint = "https://iip-openai.openai.azure.com",
                ChatDeployment = "gpt-4o-mini"
            }
        });

        var status = await healthCheck.CheckAsync("azure_openai");

        Assert.Equal("ready", status.Status);
    }

    private static TenantContextAccessorEf CurrentContext(string tenantId, string workspaceId, string userId)
    {
        var accessor = new TenantContextAccessorEf();
        accessor.SetCurrent(new TenantContext(tenantId, workspaceId, userId, []));
        return accessor;
    }

    private static AuditEvent AuditEventFor(TenantScope scope) =>
        new(
            Guid.NewGuid(),
            scope,
            CaseId: null,
            DocumentId: null,
            UserId: "user-1",
            ActorType.User,
            "core_port.test",
            DateTimeOffset.UtcNow,
            BeforeStatus: null,
            AfterStatus: null,
            EvidenceIds: [],
            ModelProvider: null,
            Confidence: null,
            ValidationResult: "accepted",
            ApprovalId: null,
            AuditReason: null,
            CorrelationId: "corr-1");

    private static InsuranceIntelligenceDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<InsuranceIntelligenceDbContext>()
            .UseInMemoryDatabase($"core-ports-ef-{Guid.NewGuid():N}")
            .Options;

        return new InsuranceIntelligenceDbContext(options);
    }
}
