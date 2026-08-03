using InsuranceIntelligence.Application.Claims;
using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Documents;
using InsuranceIntelligence.Domain.Workflow;
using InsuranceIntelligence.Infrastructure.Configuration;
using InsuranceIntelligence.Infrastructure.Persistence;
using InsuranceIntelligence.Infrastructure.Providers;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace InsuranceIntelligence.Api.Tests;

public sealed class CorePortsAndClaimsQueryTests
{
    [Fact]
    public async Task PermissionService_UsesPersistedTenantScopedRolePermissions()
    {
        await using var db = Db();
        db.UserTenantMemberships.Add(new UserTenantMembershipEntity
        {
            Id = Guid.NewGuid(),
            TenantId = "tenant-a",
            WorkspaceId = "claims",
            UserId = "user-1",
            IsActive = true
        });
        db.RoleAssignments.Add(new RoleAssignmentEntity
        {
            Id = Guid.NewGuid(),
            TenantId = "tenant-a",
            WorkspaceId = "claims",
            UserId = "user-1",
            RoleCode = "ClaimsReader"
        });
        db.RolePermissions.Add(new RolePermissionEntity
        {
            Id = Guid.NewGuid(),
            TenantId = "tenant-a",
            WorkspaceId = "claims",
            RoleCode = "ClaimsReader",
            PermissionCode = Permission.ViewAssignedClaims.ToString()
        });
        await db.SaveChangesAsync();

        var context = new TenantContextAccessor();
        context.SetCurrent(new TenantContext("tenant-a", "claims", "user-1", []));
        var service = new PermissionService(context, new EfPermissionReadRepository(db));

        Assert.True(await service.HasPermissionAsync(Permission.ViewAssignedClaims));
        Assert.False(await service.HasPermissionAsync(Permission.ManageTenantSettings));
    }

    [Fact]
    public async Task AuditWriter_RejectsCrossTenantEvents()
    {
        await using var db = Db();
        var context = new TenantContextAccessor();
        context.SetCurrent(new TenantContext("tenant-a", "claims", "user-1", [PlatformRole.Admin.ToString()]));
        var writer = new AuditWriter(context, new EfAuditEventRepository(db), NoOpOperationalTelemetry.Instance);

        var otherScope = TenantScope.Create("tenant-b", "claims");
        var auditEvent = new AuditEvent(
            Guid.NewGuid(),
            otherScope,
            CaseId: null,
            DocumentId: null,
            UserId: "user-1",
            ActorType.User,
            "test.action",
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

        await Assert.ThrowsAsync<AuditScopeException>(() => writer.WriteAsync(auditEvent));
    }

    [Fact]
    public async Task ClaimsQueryService_ReturnsOnlyTenantScopedClaims()
    {
        await using var db = Db();
        var scope = TenantScope.Create("tenant-a", "claims");
        var otherScope = TenantScope.Create("tenant-b", "claims");
        var claim = Claim(Guid.NewGuid(), scope, "Alice Motor", 50m);
        var otherClaim = Claim(Guid.NewGuid(), otherScope, "Other Tenant", 20m);

        db.Cases.Add(Persisted(claim));
        db.Cases.Add(Persisted(otherClaim));
        await db.SaveChangesAsync();

        var context = new TenantContextAccessor();
        context.SetCurrent(new TenantContext("tenant-a", "claims", "user-1", [PlatformRole.ClaimsOfficer.ToString()]));
        var service = new ClaimsQueryService(
            new EfClaimsReadRepository(db, context),
            new PermissionService(context, new EfPermissionReadRepository(db)));

        var claims = await service.ListAsync(new ClaimsQuery(Take: 10));

        var item = Assert.Single(claims);
        Assert.Equal(claim.CaseId, item.ClaimId);
    }

    [Fact]
    public async Task ProviderHealthCheck_ReportsMissingAzureBlobConfiguration()
    {
        var check = new AzureProviderHealthCheck(new InfrastructureOptions
        {
            BlobProvider = "AzureBlob",
            AzureBlob = new AzureBlobOptions()
        });

        var status = await check.CheckAsync("AzureBlob");

        Assert.Equal("missing", status.Status);
        Assert.Contains("missing", status.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ProviderHealthCheck_ReportsReadyAzureOpenAiConfiguration()
    {
        var check = new AzureProviderHealthCheck(new InfrastructureOptions
        {
            AiProvider = "AzureOpenAI",
            AzureOpenAI = new AzureOpenAiOptions
            {
                Endpoint = "https://iip-openai.openai.azure.com",
                ChatDeployment = "gpt-4o-mini",
                EmbeddingDeployment = "text-embedding-3-small"
            }
        });

        var status = await check.CheckAsync("azure_openai");

        Assert.Equal("ready", status.Status);
        Assert.DoesNotContain("not configured", status.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ProviderHealthCheck_ReportsMissingAzureOpenAiConfiguration()
    {
        var check = new AzureProviderHealthCheck(new InfrastructureOptions
        {
            AiProvider = "AzureOpenAI",
            AzureOpenAI = new AzureOpenAiOptions
            {
                Endpoint = "https://iip-openai.openai.azure.com"
            }
        });

        var status = await check.CheckAsync("azure_openai");

        Assert.Equal("missing", status.Status);
        Assert.Contains("AzureOpenAI:ChatDeployment", status.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProviderHealthCheck_ReportsReadyAzureAiSearchConfiguration()
    {
        var check = new AzureProviderHealthCheck(new InfrastructureOptions
        {
            RetrievalProvider = "AzureAiSearch",
            AzureAiSearch = new AzureAiSearchOptions
            {
                Endpoint = "https://iip-search.search.windows.net",
                IndexName = "knowledge"
            }
        });

        var status = await check.CheckAsync("azure_ai_search");

        Assert.Equal("ready", status.Status);
        Assert.DoesNotContain("not configured", status.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ProviderHealthCheck_ReportsMissingAzureAiSearchConfiguration()
    {
        var check = new AzureProviderHealthCheck(new InfrastructureOptions
        {
            RetrievalProvider = "AzureAiSearch",
            AzureAiSearch = new AzureAiSearchOptions
            {
                Endpoint = "https://iip-search.search.windows.net"
            }
        });

        var status = await check.CheckAsync("azure_ai_search");

        Assert.Equal("missing", status.Status);
        Assert.Contains("AzureAiSearch:IndexName", status.Detail, StringComparison.Ordinal);
    }

    private static InsuranceIntelligenceDbContext Db()
    {
        var options = new DbContextOptionsBuilder<InsuranceIntelligenceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new InsuranceIntelligenceDbContext(options);
    }

    private static PersistedAggregate Persisted(ClaimCase claim) =>
        new()
        {
            Id = claim.CaseId,
            TenantId = claim.Scope.TenantId,
            WorkspaceId = claim.Scope.WorkspaceId,
            PayloadJson = JsonSerializer.Serialize(claim, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
            UpdatedAt = claim.UpdatedAt
        };

    private static ClaimCase Claim(Guid id, TenantScope scope, string customerName, decimal completeness) =>
        new()
        {
            CaseId = id,
            Scope = scope,
            CustomerName = customerName,
            PolicyNumber = $"POL-{id:N}"[..12],
            ProductLine = "Motor",
            ClaimType = "Accident",
            Channel = "Email",
            IncidentDate = DateOnly.FromDateTime(DateTime.UtcNow),
            IncidentDescription = "Minor vehicle damage.",
            Status = completeness < 100m ? CaseStatus.MissingDocuments : CaseStatus.ReadyForReview,
            CompletenessScore = completeness,
            RiskLevel = "Medium",
            Priority = CasePriority.High,
            SlaDueAt = DateTimeOffset.UtcNow.AddHours(2),
            AssignedUserId = "user-1",
            AssignedTeam = "Claims",
            CreatedByUserId = "user-1",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            CorrelationId = "corr-1"
        };
}
