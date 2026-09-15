using InsuranceIntelligence.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace InsuranceIntelligence.Domain.Tests;

/// <summary>
/// The membership store is what the per-request lookup reads to decide whether someone may use a
/// workspace at all. Adding a user in the admin console used to write only the user directory, so
/// it never granted access; these tests pin the write that now does.
/// </summary>
public sealed class TenantMembershipWriterTests
{
    private const string Tenant = "tenant-a";
    private const string Workspace = "workspace-a";

    [Fact]
    public async Task Granting_creates_an_active_membership_with_the_role()
    {
        await using var db = CreateDbContext();

        await new EfTenantMembershipWriter(db).GrantMembershipAsync(Tenant, Workspace, "user-1", "Admin");

        var membership = Assert.Single(db.UserTenantMemberships);
        Assert.True(membership.IsActive);
        Assert.Equal("Admin", Assert.Single(db.RoleAssignments).RoleCode);
    }

    [Fact]
    public async Task Granting_a_different_role_replaces_the_old_one()
    {
        await using var db = CreateDbContext();
        var writer = new EfTenantMembershipWriter(db);

        await writer.GrantMembershipAsync(Tenant, Workspace, "user-1", "ClaimsOfficer");
        await writer.GrantMembershipAsync(Tenant, Workspace, "user-1", "ClaimsSupervisor");

        Assert.Single(db.UserTenantMemberships);
        Assert.Equal("ClaimsSupervisor", Assert.Single(db.RoleAssignments).RoleCode);
    }

    [Fact]
    public async Task Granting_twice_does_not_duplicate_anything()
    {
        await using var db = CreateDbContext();
        var writer = new EfTenantMembershipWriter(db);

        await writer.GrantMembershipAsync(Tenant, Workspace, "user-1", "Admin");
        await writer.GrantMembershipAsync(Tenant, Workspace, "user-1", "Admin");

        Assert.Single(db.UserTenantMemberships);
        Assert.Single(db.RoleAssignments);
    }

    [Fact]
    public async Task Granting_reactivates_a_deactivated_membership()
    {
        await using var db = CreateDbContext();
        db.UserTenantMemberships.Add(new UserTenantMembershipEntity
        {
            Id = Guid.NewGuid(), TenantId = Tenant, WorkspaceId = Workspace, UserId = "user-1", IsActive = false,
        });
        await db.SaveChangesAsync();

        await new EfTenantMembershipWriter(db).GrantMembershipAsync(Tenant, Workspace, "user-1", "Admin");

        Assert.True(Assert.Single(db.UserTenantMemberships).IsActive);
    }

    [Fact]
    public async Task Granting_in_one_workspace_leaves_other_workspaces_untouched()
    {
        await using var db = CreateDbContext();
        var writer = new EfTenantMembershipWriter(db);

        await writer.GrantMembershipAsync(Tenant, "workspace-b", "user-1", "ClaimsOfficer");
        await writer.GrantMembershipAsync(Tenant, Workspace, "user-1", "Admin");

        Assert.Equal(2, db.UserTenantMemberships.Count());
        Assert.Contains(db.RoleAssignments, r => r.WorkspaceId == "workspace-b" && r.RoleCode == "ClaimsOfficer");
        Assert.Contains(db.RoleAssignments, r => r.WorkspaceId == Workspace && r.RoleCode == "Admin");
    }

    private static InsuranceIntelligenceDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<InsuranceIntelligenceDbContext>()
            .UseInMemoryDatabase($"membership-{Guid.NewGuid():N}")
            .Options);
}
