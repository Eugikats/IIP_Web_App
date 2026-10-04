using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace InsuranceIntelligence.Domain.Tests;

/// <summary>
/// Many organisations share one database. These pin the rules that keep them apart at the lowest
/// level, where a mistake in one endpoint can no longer be caught by another.
/// </summary>
public sealed class TenantIsolationTests : IDisposable
{
    private static readonly TenantScope Acme = TenantScope.Create("org-acme", "main");
    private static readonly TenantScope Baraka = TenantScope.Create("org-baraka", "main");

    private readonly InsuranceIntelligenceDbContext db = new(
        new DbContextOptionsBuilder<InsuranceIntelligenceDbContext>().UseInMemoryDatabase($"isolation-{Guid.NewGuid():N}").Options);

    public void Dispose() => db.Dispose();

    // Tables that are not organisation data: the organisations themselves, the platform's own
    // bookkeeping that is keyed by organisation id, and the sign-in membership store. Anything
    // new must either carry a tenant id or be added here on purpose, with its reason.
    private static readonly Dictionary<string, string> PlatformTables = new(StringComparer.Ordinal)
    {
        ["Organisations"] = "the organisations themselves",
        ["OrganisationInvitations"] = "keyed by organisation id; removed when it closes",
        ["BrokerLinks"] = "the relationship between two organisations, keyed by both ids",
        ["UsageCounters"] = "carries TenantId; listed for clarity only",
        ["Permissions"] = "the catalogue of permission names, the same for everyone",
        ["ApiKeys"] = "keyed by OrganisationId; a key acts only in its own organisation and only a hash is stored",
    };

    [Fact]
    public void EveryTableCarriesATenantId_OrIsListedHereOnPurposeAsPlatformData()
    {
        var unscoped = db.Model.GetEntityTypes()
            .Where(entity => entity.ClrType.GetProperty("TenantId") is null)
            .Select(entity => entity.GetTableName() ?? entity.ClrType.Name)
            .Where(table => !PlatformTables.ContainsKey(table))
            .OrderBy(table => table)
            .ToArray();

        Assert.True(unscoped.Length == 0, "Tables with no TenantId and no entry in PlatformTables: " + string.Join(", ", unscoped));
    }

    [Fact]
    public async Task AnIdThatBelongsToAnotherOrganisation_CannotBeOverwrittenOrTaken()
    {
        var shared = Guid.NewGuid();
        await new EfCaseRepository(db).SaveAsync(Claim(shared, Acme, "Acme original claim"));

        await Assert.ThrowsAsync<TenantIsolationException>(() => new EfCaseRepository(db).SaveAsync(Claim(shared, Baraka, "Baraka overwrites")));
        // Same organisation, different workspace, is a different place too.
        await Assert.ThrowsAsync<TenantIsolationException>(
            () => new EfCaseRepository(db).SaveAsync(Claim(shared, TenantScope.Create("org-acme", "sandbox"), "Sandbox overwrites")));

        var row = await db.Cases.AsNoTracking().SingleAsync(c => c.Id == shared);
        Assert.Equal("org-acme", row.TenantId);
        Assert.Contains("Acme original claim", row.PayloadJson);

        // The owner can still update its own record.
        await new EfCaseRepository(db).SaveAsync(Claim(shared, Acme, "Acme updates"));
        Assert.Contains("Acme updates", (await db.Cases.AsNoTracking().SingleAsync(c => c.Id == shared)).PayloadJson);
    }

    [Fact]
    public async Task ARequestInsideOneOrganisation_CannotReadAnotherOrganisationsClaimByItsId()
    {
        var claimId = Guid.NewGuid();
        await new EfCaseRepository(db).SaveAsync(Claim(claimId, Acme, "Private to Acme"));

        var asBaraka = new EfCaseRepository(db, new FixedTenant(Baraka));
        var asAcme = new EfCaseRepository(db, new FixedTenant(Acme));
        var asAcmeSandbox = new EfCaseRepository(db, new FixedTenant(TenantScope.Create("org-acme", "sandbox")));
        var background = new EfCaseRepository(db); // a job or webhook with no signed-in organisation

        Assert.Null(await asBaraka.GetAsync(claimId));
        Assert.Null(await asAcmeSandbox.GetAsync(claimId));
        Assert.NotNull(await asAcme.GetAsync(claimId));
        Assert.NotNull(await background.GetAsync(claimId));
        Assert.Empty(await asBaraka.ListAsync(Baraka));
        Assert.Single(await asAcme.ListAsync(Acme));
    }

    [Fact]
    public async Task ListingNeverCrossesOrganisations()
    {
        var repository = new EfCaseRepository(db);
        await repository.SaveAsync(Claim(Guid.NewGuid(), Acme, "a1"));
        await repository.SaveAsync(Claim(Guid.NewGuid(), Acme, "a2"));
        await repository.SaveAsync(Claim(Guid.NewGuid(), Baraka, "b1"));

        Assert.Equal(2, (await repository.ListAsync(Acme)).Count);
        Assert.Single(await repository.ListAsync(Baraka));
        Assert.Empty(await repository.ListAsync(TenantScope.Create("org-nobody", "main")));
    }

    private static ClaimCase Claim(Guid id, TenantScope scope, string description) => new()
    {
        CaseId = id,
        Scope = scope,
        CustomerName = "Client",
        PolicyNumber = "POL-1",
        ProductLine = "Motor",
        ClaimType = "Accident",
        Channel = "Portal",
        IncidentDate = DateOnly.FromDateTime(DateTime.UtcNow),
        IncidentDescription = description,
        Status = CaseStatus.New,
        CompletenessScore = 0,
        CreatedByUserId = "officer",
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow,
        CorrelationId = "c",
    };

    private sealed class FixedTenant(TenantScope scope) : ITenantContextAccessor
    {
        public bool HasCurrent => true;
        public TenantContext Current => new(scope.TenantId, scope.WorkspaceId, "user", []);
    }
}
