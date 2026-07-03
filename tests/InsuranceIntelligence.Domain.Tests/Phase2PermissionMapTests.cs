using InsuranceIntelligence.Domain.Administration;

namespace InsuranceIntelligence.Domain.Tests;

public sealed class Phase2PermissionMapTests
{
    [Fact]
    public void Admin_InheritsAllPhase2ReadPermissions()
    {
        Assert.True(RolePermissionMap.HasPermission(PlatformRole.Admin, Permission.ViewChannels));
        Assert.True(RolePermissionMap.HasPermission(PlatformRole.Admin, Permission.ViewCustomers));
        Assert.True(RolePermissionMap.HasPermission(PlatformRole.Admin, Permission.ViewSalesLeads));
    }

    [Fact]
    public void OperationalRoles_CanReadChannelsAndCustomers()
    {
        Assert.True(RolePermissionMap.HasPermission(PlatformRole.ClaimsOfficer, Permission.ViewChannels));
        Assert.True(RolePermissionMap.HasPermission(PlatformRole.ClaimsOfficer, Permission.ViewCustomers));
        Assert.True(RolePermissionMap.HasPermission(PlatformRole.ClaimsSupervisor, Permission.ViewChannels));
        Assert.True(RolePermissionMap.HasPermission(PlatformRole.ClaimsSupervisor, Permission.ViewCustomers));
        Assert.True(RolePermissionMap.HasPermission(PlatformRole.CustomerService, Permission.ViewChannels));
        Assert.True(RolePermissionMap.HasPermission(PlatformRole.CustomerService, Permission.ViewCustomers));
    }

    [Fact]
    public void SalesLeadRead_StaysNarrowAtPhase2Start()
    {
        Assert.True(RolePermissionMap.HasPermission(PlatformRole.CustomerService, Permission.ViewSalesLeads));
        Assert.False(RolePermissionMap.HasPermission(PlatformRole.ClaimsOfficer, Permission.ViewSalesLeads));
        Assert.False(RolePermissionMap.HasPermission(PlatformRole.Broker, Permission.ViewSalesLeads));
        Assert.False(RolePermissionMap.HasPermission(PlatformRole.Client, Permission.ViewSalesLeads));
    }

    [Fact]
    public void CustomerFacingAndBrokerRoles_DoNotGainInternalPhase2ReadPermissions()
    {
        Assert.False(RolePermissionMap.HasPermission(PlatformRole.Client, Permission.ViewChannels));
        Assert.False(RolePermissionMap.HasPermission(PlatformRole.Client, Permission.ViewCustomers));
        Assert.False(RolePermissionMap.HasPermission(PlatformRole.Broker, Permission.ViewChannels));
        Assert.False(RolePermissionMap.HasPermission(PlatformRole.Broker, Permission.ViewCustomers));
    }
}
