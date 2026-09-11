using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Compliance;

namespace InsuranceIntelligence.Domain.Tests.Compliance;

public sealed class ConsentEnforcementGuardTests
{
    private static readonly TenantScope Scope = TenantScope.Create("tenant-consent", "workspace-claims");

    [Fact]
    public void IsPermitted_NoRecord_ReturnsFalse()
    {
        Assert.False(ConsentEnforcementGuard.IsPermitted(null));
    }

    [Fact]
    public void IsPermitted_ActiveGrant_ReturnsTrue()
    {
        var record = Grant(ConsentBasis.CustomerInitiatedConversation);

        Assert.True(ConsentEnforcementGuard.IsPermitted(record));
    }

    [Fact]
    public void IsPermitted_WithdrawnAtSet_ReturnsFalse()
    {
        var record = Grant(ConsentBasis.CustomerInitiatedConversation) with { WithdrawnAt = DateTimeOffset.UtcNow };

        Assert.False(ConsentEnforcementGuard.IsPermitted(record));
    }

    [Fact]
    public void IsPermitted_BasisIsWithdrawn_ReturnsFalse()
    {
        var record = Grant(ConsentBasis.Withdrawn);

        Assert.False(ConsentEnforcementGuard.IsPermitted(record));
    }

    [Theory]
    [InlineData(ConsentBasis.CustomerInitiatedConversation)]
    [InlineData(ConsentBasis.ExplicitOptIn)]
    [InlineData(ConsentBasis.ContractualNecessity)]
    public void IsPermitted_EveryNonWithdrawnBasis_ReturnsTrueWhenNotWithdrawn(ConsentBasis basis)
    {
        Assert.True(ConsentEnforcementGuard.IsPermitted(Grant(basis)));
    }

    private static ConsentRecord Grant(ConsentBasis basis) => new()
    {
        RecordId = Guid.NewGuid(),
        Scope = Scope,
        CustomerIdentifier = "256700111222",
        Basis = basis,
        Purpose = "whatsapp_messaging",
        Channel = "WhatsApp",
        RecordedByUserId = "system:customer-assistant",
        RecordedAt = DateTimeOffset.UtcNow,
        CorrelationId = "corr-consent-test",
    };
}
