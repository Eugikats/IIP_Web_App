using InsuranceIntelligence.Api.Organisations;
using InsuranceIntelligence.Infrastructure.Configuration;

namespace InsuranceIntelligence.Api.Tests;

public sealed class SignUpGateTests
{
    [Fact]
    public void Sign_up_is_shut_in_a_single_tenant_deployment() =>
        Assert.NotNull(OrganisationEndpoints.SignUpClosed(new InfrastructureOptions { AuthMode = "Entra", MultiTenant = false }));

    [Fact]
    public void Sign_up_is_open_once_multi_tenant_mode_is_on() =>
        Assert.Null(OrganisationEndpoints.SignUpClosed(new InfrastructureOptions { AuthMode = "Entra", MultiTenant = true }));

    [Fact]
    public void Local_development_can_test_sign_up() =>
        Assert.Null(OrganisationEndpoints.SignUpClosed(new InfrastructureOptions { AuthMode = "Development", MultiTenant = false }));
}
