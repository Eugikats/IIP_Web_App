using InsuranceIntelligence.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace InsuranceIntelligence.Api.Tests;

public sealed class MembershipLookupTests
{
    private static InsuranceIntelligenceDbContext Db() => new(
        new DbContextOptionsBuilder<InsuranceIntelligenceDbContext>().UseInMemoryDatabase($"membership-{Guid.NewGuid():N}").Options);

    private static UserTenantMembershipEntity Row(string tenant, string workspace, string user, bool active = true) =>
        new() { Id = Guid.NewGuid(), TenantId = tenant, WorkspaceId = workspace, UserId = user, IsActive = active };

    [Fact]
    public async Task One_membership_resolves()
    {
        await using var db = Db();
        db.UserTenantMemberships.Add(Row("tenant", "workspace-operations", "user-1"));
        await db.SaveChangesAsync();

        var membership = await new EfPermissionReadRepository(db, checkOrganisationStatus: false).GetEffectiveMembershipAsync("user-1", "tenant", null);

        Assert.NotNull(membership);
        Assert.Equal("workspace-operations", membership!.WorkspaceId);
    }

    [Fact]
    public async Task The_same_workspace_recorded_twice_is_still_one_membership()
    {
        await using var db = Db();
        db.UserTenantMemberships.Add(Row("tenant", "workspace-operations", "user-1"));
        db.UserTenantMemberships.Add(Row("tenant", "workspace-operations", "user-1"));
        await db.SaveChangesAsync();

        var membership = await new EfPermissionReadRepository(db, checkOrganisationStatus: false).GetEffectiveMembershipAsync("user-1", "tenant", null);

        Assert.NotNull(membership);
        Assert.Equal("workspace-operations", membership!.WorkspaceId);
    }

    [Fact]
    public async Task Two_different_workspaces_without_a_hint_are_still_refused()
    {
        await using var db = Db();
        db.UserTenantMemberships.Add(Row("tenant", "workspace-operations", "user-1"));
        db.UserTenantMemberships.Add(Row("tenant", "other", "user-1"));
        await db.SaveChangesAsync();

        Assert.Null(await new EfPermissionReadRepository(db, checkOrganisationStatus: false).GetEffectiveMembershipAsync("user-1", "tenant", null));
    }

    [Fact]
    public async Task An_inactive_membership_is_refused_and_another_person_is_never_matched()
    {
        await using var db = Db();
        db.UserTenantMemberships.Add(Row("tenant", "workspace-operations", "user-1", active: false));
        db.UserTenantMemberships.Add(Row("tenant", "workspace-operations", "user-2"));
        await db.SaveChangesAsync();

        Assert.Null(await new EfPermissionReadRepository(db, checkOrganisationStatus: false).GetEffectiveMembershipAsync("user-1", "tenant", null));
    }
}
