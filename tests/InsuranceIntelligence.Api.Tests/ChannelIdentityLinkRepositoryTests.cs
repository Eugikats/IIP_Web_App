using InsuranceIntelligence.Domain.Channels;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InsuranceIntelligence.Api.Tests;

public sealed class ChannelIdentityLinkRepositoryTests
{
    [Fact]
    public async Task SaveGetAndListRemainTenantAndWorkspaceScoped()
    {
        var options = new DbContextOptionsBuilder<InsuranceIntelligenceDbContext>()
            .UseInMemoryDatabase($"channel-identities-{Guid.NewGuid():N}")
            .Options;
        var scope = TenantScope.Create("insurer-a", "claims");
        var otherScope = TenantScope.Create("insurer-a", "sales");
        var link = new ChannelIdentityLink
        {
            LinkId = Guid.NewGuid(), Scope = scope, CustomerId = Guid.NewGuid().ToString(),
            Channel = "x", AccountId = "x-business-1", ExternalRecipientId = "x-recipient-9",
            VerificationMethod = ChannelIdentityVerificationMethod.StaffVerifiedCustomerChallenge,
            EvidenceReference = "interaction-16", ConsentPurpose = ChannelIdentityConsentPurposes.ForChannel("x"),
            LinkedByUserId = "staff-1", LinkedAt = DateTimeOffset.UtcNow,
        };

        await using var db = new InsuranceIntelligenceDbContext(options);
        var repository = new EfChannelIdentityLinkRepository(db);
        await repository.SaveAsync(link);

        var loaded = await repository.GetAsync(scope, link.LinkId);
        Assert.Equal(link, loaded);
        Assert.Null(await repository.GetAsync(otherScope, link.LinkId));
        Assert.Single(await repository.ListForCustomerAsync(scope, link.CustomerId));
        Assert.Empty(await repository.ListForCustomerAsync(otherScope, link.CustomerId));

        var revoked = link.Revoke("staff-2", DateTimeOffset.UtcNow);
        await repository.SaveAsync(revoked);
        Assert.False((await repository.GetAsync(scope, link.LinkId))!.IsActive);
    }
}
