using System.Text.Json;
using System.Text.Json.Serialization;
using InsuranceIntelligence.Application.Audit;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Communications;
using InsuranceIntelligence.Domain.Customers;
using InsuranceIntelligence.Domain.Policies;
using InsuranceIntelligence.Domain.Workflow;
using InsuranceIntelligence.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InsuranceIntelligence.Domain.Tests;

// Verifies the W2.5 read-side adapters that back the Phase 2 query services.
public sealed class Phase2ReadRepositoryTests
{
    // Mirrors Infrastructure.Persistence.JsonSerializerSettings.Options (which is internal).
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter() }
    };

    private static readonly TenantScope ScopeA = TenantScope.Create("tenant-a", "workspace-a");

    [Fact]
    public async Task Audit_ListEvents_IsTenantScopedFilteredAndPaged()
    {
        await using var dbContext = CreateDbContext();
        var caseId = Guid.NewGuid();

        SeedAudit(dbContext, ScopeA, caseId, "claim.decision_made", "user-1", DateTimeOffset.UtcNow);
        SeedAudit(dbContext, ScopeA, caseId, "document.uploaded", "user-2", DateTimeOffset.UtcNow.AddMinutes(-5));
        // Different tenant workspace — must never appear.
        SeedAudit(dbContext, TenantScope.Create("tenant-b", "workspace-a"), Guid.NewGuid(), "claim.decision_made", "user-1", DateTimeOffset.UtcNow);
        await dbContext.SaveChangesAsync();

        var repository = new EfAuditReadRepository(dbContext);

        var all = await repository.ListEventsAsync("tenant-a", "workspace-a", new AuditEventListQuery());
        Assert.Equal(2, all.Count);
        Assert.Equal("claim.decision_made", all[0].EventType); // newest first
        Assert.Equal("case", all[0].TargetType);
        Assert.Equal(caseId.ToString(), all[0].TargetId);

        var byType = await repository.ListEventsAsync(
            "tenant-a", "workspace-a", new AuditEventListQuery(EventType: "document.uploaded"));
        Assert.Equal("user-2", Assert.Single(byType).ActorUserId);

        var firstPage = await repository.ListEventsAsync(
            "tenant-a", "workspace-a", new AuditEventListQuery(Page: 1, PageSize: 1));
        Assert.Single(firstPage);
        Assert.Equal("claim.decision_made", firstPage[0].EventType);
    }

    [Fact]
    public async Task Customer_Profile_AggregatesOnlyThisCustomersRecords()
    {
        await using var dbContext = CreateDbContext();
        var matchingCaseId = Guid.NewGuid();

        SeedJson(dbContext.Customers, ScopeA, Guid.NewGuid(),
            new Customer("cust-1", "tenant-a", "workspace-a", "Jane Doe", "jane@example.com", "+1 555 0100"));

        SeedJson(dbContext.Policies, ScopeA, Guid.NewGuid(),
            new Policy("pol-1", "tenant-a", "workspace-a", "POL-1", "Motor", "Comprehensive", "cust-1", null,
                new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31)));
        // Policy for another customer — excluded by FK.
        SeedJson(dbContext.Policies, ScopeA, Guid.NewGuid(),
            new Policy("pol-2", "tenant-a", "workspace-a", "POL-2", "Home", null, "cust-2", null, null, null));

        SeedJson(dbContext.Cases, ScopeA, matchingCaseId, Claim(matchingCaseId, "Jane Doe", "jane@example.com"));
        // Case for a different customer (different email) — must not leak in.
        SeedJson(dbContext.Cases, ScopeA, Guid.NewGuid(), Claim(Guid.NewGuid(), "John Other", "other@example.com"));

        SeedJson(dbContext.Communications, ScopeA, Guid.NewGuid(),
            new Communication
            {
                CommunicationId = Guid.NewGuid(),
                Scope = ScopeA,
                CaseId = matchingCaseId,
                CustomerId = "cust-1",
                Channel = CommunicationChannel.WhatsApp,
                Direction = "inbound",
                MessageBody = "Hello, I need to update my claim.",
                SentBy = "customer",
                DeliveryStatus = "delivered",
                CreatedAt = DateTimeOffset.UtcNow,
                CorrelationId = "corr-1"
            },
            entity => entity.CaseId = matchingCaseId);

        SeedJson(dbContext.AuditEvents, ScopeA, Guid.NewGuid(),
            new AuditEvent(Guid.NewGuid(), ScopeA, matchingCaseId, null, "user-1", ActorType.User,
                "claim.decision_made", DateTimeOffset.UtcNow, null, null, Array.Empty<string>(),
                null, null, "accepted", null, "Customer profile viewed.", "corr-1"),
            entity => entity.CaseId = matchingCaseId);

        await dbContext.SaveChangesAsync();

        var repository = new EfCustomerReadRepository(dbContext);
        var profile = await repository.GetCustomerProfileAsync("tenant-a", "workspace-a", "cust-1");

        Assert.NotNull(profile);
        Assert.Equal("cust-1", profile!.CustomerId);
        Assert.Equal("Jane Doe", profile.Identity["fullName"]);
        Assert.Equal("jane@example.com", profile.Identity["email"]);
        Assert.Equal("POL-1", Assert.Single(profile.Policies)["policyNumber"]);
        Assert.Equal(matchingCaseId.ToString(), Assert.Single(profile.Claims)["caseId"]);
        Assert.Single(profile.Interactions);
        Assert.Single(profile.AuditHistory);
        // Sources with no data model yet must be empty, never simulated.
        Assert.Empty(profile.Consent);
        Assert.Empty(profile.Payments);
        Assert.Empty(profile.Opportunities);
    }

    [Fact]
    public async Task Customer_Profile_ReturnsNullWhenNotFound()
    {
        await using var dbContext = CreateDbContext();
        var repository = new EfCustomerReadRepository(dbContext);

        Assert.Null(await repository.GetCustomerProfileAsync("tenant-a", "workspace-a", "does-not-exist"));
    }

    [Fact]
    public async Task Channels_Overview_IsEmptyUntilIngestionConfigured()
    {
        var repository = new EfChannelReadRepository();

        Assert.Empty(await repository.GetOverviewAsync("tenant-a", "workspace-a"));
    }

    [Fact]
    public async Task SalesLead_Create_Then_Get_And_List_RoundTripsTenantScoped()
    {
        await using var dbContext = CreateDbContext();
        var repository = new EfSalesLeadRepository(dbContext);

        // Unknown lead is null (no fabrication).
        Assert.Null(await repository.GetLeadDetailAsync("tenant-a", "workspace-a", "lead-x"));

        var lead = new InsuranceIntelligence.Application.SalesLeads.SalesLeadDetailReadModel(
            "LEAD-1", "Manual capture", "Motor cover", "Captured by staff", "Motor comprehensive", "New",
            ["Confirm details"], ["Contact customer"], "officer-1", null, "Jane Doe", "jane@example.test",
            DateTimeOffset.UtcNow, "user-1");
        await repository.CreateAsync("tenant-a", "workspace-a", lead);
        // Different tenant workspace — must never see it.
        await repository.CreateAsync("tenant-b", "workspace-a",
            lead with { LeadId = "LEAD-2", CustomerName = "Other Tenant" });

        var fetched = await repository.GetLeadDetailAsync("tenant-a", "workspace-a", "LEAD-1");
        Assert.NotNull(fetched);
        Assert.Equal("Jane Doe", fetched!.CustomerName);

        var list = await repository.ListLeadsAsync("tenant-a", "workspace-a");
        Assert.Single(list);
        Assert.Equal("LEAD-1", list[0].LeadId);
    }

    [Fact]
    public async Task WorkspaceCatalog_ListsPerTenant_AndIsTenantIsolated()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Workspaces.Add(ScopedJson("tenant-a", "workspace-claims",
            new InsuranceIntelligence.Application.Tenants.WorkspaceSummaryDto("workspace-claims", "Motor Claims", "af-east")));
        dbContext.Workspaces.Add(ScopedJson("tenant-a", "workspace-life",
            new InsuranceIntelligence.Application.Tenants.WorkspaceSummaryDto("workspace-life", "Life Claims", "af-east")));
        dbContext.Workspaces.Add(ScopedJson("tenant-b", "workspace-claims",
            new InsuranceIntelligence.Application.Tenants.WorkspaceSummaryDto("workspace-claims", "Other Tenant", "af-east")));
        await dbContext.SaveChangesAsync();

        var repository = new EfWorkspaceCatalogRepository(dbContext);
        var workspaces = await repository.ListForTenantAsync("tenant-a");

        Assert.Equal(2, workspaces.Count);
        Assert.DoesNotContain(workspaces, w => w.DisplayName == "Other Tenant");
    }

    [Fact]
    public async Task EffectiveMembership_ResolvesFromStore_AndFailsClosed()
    {
        await using var dbContext = CreateDbContext();
        // tenant-a/workspace-a: user-1 is an active member with role "ClaimsOfficer" granting ViewAssignedClaims.
        dbContext.UserTenantMemberships.Add(new InsuranceIntelligence.Infrastructure.Persistence.UserTenantMembershipEntity
        { Id = Guid.NewGuid(), TenantId = "tenant-a", WorkspaceId = "workspace-a", UserId = "oid-user-1", IsActive = true });
        dbContext.RoleAssignments.Add(new InsuranceIntelligence.Infrastructure.Persistence.RoleAssignmentEntity
        { Id = Guid.NewGuid(), TenantId = "tenant-a", WorkspaceId = "workspace-a", UserId = "oid-user-1", RoleCode = "ClaimsOfficer" });
        dbContext.RolePermissions.Add(new InsuranceIntelligence.Infrastructure.Persistence.RolePermissionEntity
        { Id = Guid.NewGuid(), TenantId = "tenant-a", WorkspaceId = "workspace-a", RoleCode = "ClaimsOfficer", PermissionCode = "ViewAssignedClaims" });
        // Inactive membership in another tenant — must never resolve.
        dbContext.UserTenantMemberships.Add(new InsuranceIntelligence.Infrastructure.Persistence.UserTenantMembershipEntity
        { Id = Guid.NewGuid(), TenantId = "tenant-b", WorkspaceId = "workspace-a", UserId = "oid-user-1", IsActive = false });
        await dbContext.SaveChangesAsync();

        var repo = new EfPermissionReadRepository(dbContext);

        var resolved = await repo.GetEffectiveMembershipAsync("oid-user-1", tenantHint: null, workspaceHint: null);
        Assert.NotNull(resolved);
        Assert.Equal("tenant-a", resolved!.TenantId);
        Assert.Equal("workspace-a", resolved.WorkspaceId);
        Assert.Contains(InsuranceIntelligence.Domain.Administration.Permission.ViewAssignedClaims, resolved.Permissions);

        // Fail closed: unknown user, and inactive-only membership.
        Assert.Null(await repo.GetEffectiveMembershipAsync("oid-unknown", null, null));
        Assert.Null(await repo.GetEffectiveMembershipAsync("oid-user-1", tenantHint: "tenant-b", workspaceHint: null));
    }

    [Fact]
    public async Task EffectiveMembership_MultipleMembershipsWithoutHint_FailsClosed_ButHintDisambiguates()
    {
        await using var dbContext = CreateDbContext();
        foreach (var t in new[] { "tenant-a", "tenant-b" })
        {
            dbContext.UserTenantMemberships.Add(new InsuranceIntelligence.Infrastructure.Persistence.UserTenantMembershipEntity
            { Id = Guid.NewGuid(), TenantId = t, WorkspaceId = "workspace-a", UserId = "oid-user-2", IsActive = true });
        }
        await dbContext.SaveChangesAsync();
        var repo = new EfPermissionReadRepository(dbContext);

        Assert.Null(await repo.GetEffectiveMembershipAsync("oid-user-2", null, null)); // ambiguous -> fail closed
        var resolved = await repo.GetEffectiveMembershipAsync("oid-user-2", tenantHint: "tenant-b", workspaceHint: null);
        Assert.NotNull(resolved);
        Assert.Equal("tenant-b", resolved!.TenantId);
    }

    [Fact]
    public async Task Onboarding_UpsertGet_And_ConsentBootstrap_MakesAdminResolvable()
    {
        await using var dbContext = CreateDbContext();
        var onboarding = new EfTenantOnboardingRepository(dbContext);
        var record = new InsuranceIntelligence.Application.Onboarding.TenantOnboardingRecord(
            "ONB-1", "Insurer X", "contoso.onmicrosoft.com", "admin@contoso.test",
            ["workspace-claims"], ["sub-123"], "tenant-x", "state-1",
            InsuranceIntelligence.Application.Onboarding.TenantOnboardingStatus.Pending,
            null, null, "operator-1", DateTimeOffset.UtcNow, null);
        await onboarding.UpsertAsync(record);

        Assert.Equal("ONB-1", (await onboarding.GetAsync("ONB-1"))!.OnboardingId);
        Assert.Equal("ONB-1", (await onboarding.GetByStateAsync("state-1"))!.OnboardingId);
        Assert.Single(await onboarding.ListAsync());

        // Bootstrap the consenting admin's membership, then confirm the runtime resolver resolves it
        // with Admin permissions from the code-defined map (no per-tenant RolePermissions seeding).
        var writer = new EfTenantMembershipWriter(dbContext);
        await writer.CreateAdminMembershipAsync("tenant-x", "workspace-claims", "admin@contoso.test");
        await writer.CreateAdminMembershipAsync("tenant-x", "workspace-claims", "admin@contoso.test"); // idempotent

        var membership = await new EfPermissionReadRepository(dbContext)
            .GetEffectiveMembershipAsync("admin@contoso.test", "tenant-x", "workspace-claims");
        Assert.NotNull(membership);
        Assert.Equal("tenant-x", membership!.TenantId);
        Assert.Contains(InsuranceIntelligence.Domain.Administration.Permission.ManageTenantSettings, membership.Permissions);
    }

    private static PersistedWorkspace ScopedJson(string tenantId, string workspaceId, InsuranceIntelligence.Application.Tenants.WorkspaceSummaryDto payload) =>
        new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            WorkspaceId = workspaceId,
            PayloadJson = JsonSerializer.Serialize(payload, Json)
        };

    private static ClaimCase Claim(Guid caseId, string customerName, string customerEmail) =>
        new()
        {
            CaseId = caseId,
            Scope = ScopeA,
            CustomerName = customerName,
            CustomerEmail = customerEmail,
            PolicyNumber = "POL-1",
            ProductLine = "Motor",
            ClaimType = "Collision",
            Channel = "whatsapp",
            IncidentDate = new DateOnly(2026, 1, 1),
            IncidentDescription = "Rear-ended at an intersection.",
            Status = CaseStatus.MissingDocuments,
            CompletenessScore = 40m,
            CreatedByUserId = "user-1",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            CorrelationId = "corr-1"
        };

    private static void SeedAudit(
        InsuranceIntelligenceDbContext dbContext,
        TenantScope scope,
        Guid caseId,
        string actionName,
        string userId,
        DateTimeOffset timestamp) =>
        SeedJson(dbContext.AuditEvents, scope, Guid.NewGuid(),
            new AuditEvent(Guid.NewGuid(), scope, caseId, null, userId, ActorType.User,
                actionName, timestamp, null, null, Array.Empty<string>(),
                null, null, "accepted", null, null, "corr-1"),
            entity => entity.CaseId = caseId);

    private static void SeedJson<TEntity>(
        DbSet<TEntity> set,
        TenantScope scope,
        Guid id,
        object payload,
        Action<TEntity>? configure = null)
        where TEntity : PersistedScopedJson, new()
    {
        var entity = new TEntity
        {
            Id = id,
            TenantId = scope.TenantId,
            WorkspaceId = scope.WorkspaceId,
            PayloadJson = JsonSerializer.Serialize(payload, Json),
            UpdatedAt = DateTimeOffset.UtcNow
        };

        configure?.Invoke(entity);
        set.Add(entity);
    }

    private static InsuranceIntelligenceDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<InsuranceIntelligenceDbContext>()
            .UseInMemoryDatabase($"phase2-read-{Guid.NewGuid():N}")
            .Options;

        return new InsuranceIntelligenceDbContext(options);
    }
}
