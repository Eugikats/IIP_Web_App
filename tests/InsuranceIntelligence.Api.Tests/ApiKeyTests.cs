using InsuranceIntelligence.Application.Organisations;
using InsuranceIntelligence.Domain.Organisations;
using InsuranceIntelligence.Infrastructure.Persistence;
using InsuranceIntelligence.LocalTestDoubles;
using Microsoft.EntityFrameworkCore;

namespace InsuranceIntelligence.Api.Tests;

/// <summary>Keys for an insurer's own systems: shown once, stored as a hash, scoped to one organisation, revocable.</summary>
public sealed class ApiKeyTests : IDisposable
{
    private readonly InsuranceIntelligenceDbContext db = new(
        new DbContextOptionsBuilder<InsuranceIntelligenceDbContext>().UseInMemoryDatabase($"apikeys-{Guid.NewGuid():N}").Options);
    private readonly OrganisationService organisations;
    private readonly ApiKeyService keys;
    private readonly EfOrganisationRepository repository;

    public ApiKeyTests()
    {
        repository = new EfOrganisationRepository(db);
        var writer = new EfTenantMembershipWriter(db);
        var audit = new InMemoryAuditRepository();
        organisations = new OrganisationService(repository, writer, audit, new InMemoryCaseRepository());
        keys = new ApiKeyService(new EfApiKeyRepository(db), repository, writer, audit);
    }

    public void Dispose() => db.Dispose();

    [Fact]
    public async Task AKey_IsShownOnce_AndOnlyItsHashIsKept()
    {
        var org = await organisations.CreateAsync("owner-1", "Acme Assurance", OrganisationType.Insurer, "c");

        var created = await keys.CreateAsync(org.OrganisationId, "owner-1", "Core system", null, "c");

        Assert.StartsWith("iip_", created.Key);
        var stored = await db.ApiKeys.SingleAsync();
        Assert.DoesNotContain(created.Key, stored.KeyHash);
        Assert.Equal(ApiKeyService.Hash(created.Key), stored.KeyHash);
        var listed = Assert.Single(await keys.ListAsync(org.OrganisationId));
        Assert.Equal(created.Prefix, listed.Prefix);
        Assert.DoesNotContain(created.Key, System.Text.Json.JsonSerializer.Serialize(listed));
    }

    [Fact]
    public async Task AValidKey_SignsInAsAMachineInItsOwnOrganisation_AndAWrongOneDoesNot()
    {
        var org = await organisations.CreateAsync("owner-1", "Acme Assurance", OrganisationType.Insurer, "c");
        var created = await keys.CreateAsync(org.OrganisationId, "owner-1", "Core system", null, "c");

        var ok = await keys.AuthenticateAsync(created.Key, DateTimeOffset.UtcNow);
        var wrong = await keys.AuthenticateAsync(created.Key + "x", DateTimeOffset.UtcNow);
        var none = await keys.AuthenticateAsync(null, DateTimeOffset.UtcNow);
        var notAKey = await keys.AuthenticateAsync("Bearer abc", DateTimeOffset.UtcNow);

        Assert.NotNull(ok);
        Assert.Equal(org.OrganisationId, ok!.OrganisationId);
        Assert.True(ApiKeyRecord.IsMachineUser(ok.UserId));
        Assert.Null(wrong);
        Assert.Null(none);
        Assert.Null(notAKey);
    }

    [Fact]
    public async Task ARevokedKey_StopsWorkingAtOnce_AndAnExpiredOneToo()
    {
        var org = await organisations.CreateAsync("owner-1", "Acme Assurance", OrganisationType.Insurer, "c");
        var revoked = await keys.CreateAsync(org.OrganisationId, "owner-1", "Old system", null, "c");
        var shortLived = await keys.CreateAsync(org.OrganisationId, "owner-1", "Temp import", 1, "c");

        await keys.RevokeAsync(org.OrganisationId, "owner-1", revoked.KeyId, "c");

        Assert.Null(await keys.AuthenticateAsync(revoked.Key, DateTimeOffset.UtcNow));
        Assert.NotNull(await keys.AuthenticateAsync(shortLived.Key, DateTimeOffset.UtcNow));
        Assert.Null(await keys.AuthenticateAsync(shortLived.Key, DateTimeOffset.UtcNow.AddDays(2)));
    }

    [Fact]
    public async Task AKeyOfAnotherOrganisation_CannotBeRevokedFromThisOne()
    {
        var acme = await organisations.CreateAsync("owner-1", "Acme Assurance", OrganisationType.Insurer, "c");
        var baraka = await organisations.CreateAsync("owner-2", "Baraka Insurance", OrganisationType.Insurer, "c");
        var barakaKey = await keys.CreateAsync(baraka.OrganisationId, "owner-2", "Their system", null, "c");

        await Assert.ThrowsAsync<OrganisationException>(() => keys.RevokeAsync(acme.OrganisationId, "owner-1", barakaKey.KeyId, "c"));

        Assert.NotNull(await keys.AuthenticateAsync(barakaKey.Key, DateTimeOffset.UtcNow));
    }

    [Fact]
    public async Task AKeyIdentity_IsNeverListedAsATeamMember_AndTheNumberOfActiveKeysIsLimited()
    {
        var org = await organisations.CreateAsync("owner-1", "Acme Assurance", OrganisationType.Insurer, "c");
        for (var i = 0; i < ApiKeyService.MaxActiveKeysPerOrganisation; i++)
        {
            await keys.CreateAsync(org.OrganisationId, "owner-1", $"System {i}", null, "c");
        }

        var members = await repository.ListMemberUserIdsAsync(org.OrganisationId);

        Assert.DoesNotContain(members, m => ApiKeyRecord.IsMachineUser(m));
        Assert.Contains("owner-1", members);
        await Assert.ThrowsAsync<OrganisationException>(() => keys.CreateAsync(org.OrganisationId, "owner-1", "One too many", null, "c"));
    }

    [Fact]
    public async Task AKey_NeedsAName()
    {
        var org = await organisations.CreateAsync("owner-1", "Acme Assurance", OrganisationType.Insurer, "c");
        await Assert.ThrowsAsync<OrganisationException>(() => keys.CreateAsync(org.OrganisationId, "owner-1", " ", null, "c"));
    }
}
