using System.Text.Json;
using InsuranceIntelligence.Application.Organisations;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Organisations;
using InsuranceIntelligence.Domain.Usage;
using InsuranceIntelligence.Infrastructure.Persistence;
using InsuranceIntelligence.LocalTestDoubles;
using Microsoft.EntityFrameworkCore;

namespace InsuranceIntelligence.Api.Tests;

/// <summary>Plan limits, and taking an organisation's data out or removing it entirely.</summary>
public sealed class OrganisationLifecycleTests : IDisposable
{
    private readonly InsuranceIntelligenceDbContext db = new(
        new DbContextOptionsBuilder<InsuranceIntelligenceDbContext>().UseInMemoryDatabase($"org-life-{Guid.NewGuid():N}").Options);
    private readonly OrganisationService service;
    private readonly EfUsageMeter meter;

    public OrganisationLifecycleTests()
    {
        var repository = new EfOrganisationRepository(db);
        service = new OrganisationService(repository, new EfTenantMembershipWriter(db), new InMemoryAuditRepository(), new InMemoryCaseRepository());
        meter = new EfUsageMeter(db, repository);
    }

    public void Dispose() => db.Dispose();

    [Fact]
    public async Task APlanCeiling_StopsTheCostlyWork_ForThatOrganisationOnly()
    {
        var busy = await service.CreateAsync("owner-1", "Busy Insurer", OrganisationType.Insurer, "c");
        var quiet = await service.CreateAsync("owner-2", "Quiet Insurer", OrganisationType.Insurer, "c");

        for (var i = 0; i < PlanCatalog.Trial.AssistantTurnsPerMonth; i++)
        {
            Assert.True((await meter.TryConsumeAsync(busy.Scope, UsageKind.AssistantTurn)).Allowed);
        }

        var over = await meter.TryConsumeAsync(busy.Scope, UsageKind.AssistantTurn);
        var other = await meter.TryConsumeAsync(quiet.Scope, UsageKind.AssistantTurn);

        Assert.False(over.Allowed);
        Assert.Equal(PlanCatalog.Trial.AssistantTurnsPerMonth, over.Used);
        Assert.True(other.Allowed);
        var snapshot = await meter.GetAsync(busy.Scope);
        Assert.Equal(PlanCatalog.Trial.AssistantTurnsPerMonth, snapshot!.Used[UsageKind.AssistantTurn]);
        Assert.Equal(0, snapshot.Used[UsageKind.InboundMessage]);
    }

    [Fact]
    public async Task ADeploymentWithNoOrganisations_IsNotMetered_AndAnUnknownPlanGetsTheSmallestOne()
    {
        var managedApp = await meter.TryConsumeAsync(TenantScope.Create("tenant-demo", "workspace-operations"), UsageKind.AssistantTurn);
        Assert.True(managedApp.Allowed);
        Assert.Null(await meter.GetAsync(TenantScope.Create("tenant-demo", "workspace-operations")));
        Assert.Equal("trial", PlanCatalog.Find("platinum-typo").PlanId);
    }

    [Fact]
    public async Task ExportingAnOrganisation_GivesItsOwnRecordsOnly_AndNeverItsStoredCredentials()
    {
        var acme = await service.CreateAsync("owner-acme", "Acme Assurance", OrganisationType.Insurer, "c");
        var baraka = await service.CreateAsync("owner-baraka", "Baraka Insurance", OrganisationType.Insurer, "c");
        AddRecord(acme.OrganisationId, "acme-secret-claim");
        AddRecord(baraka.OrganisationId, "baraka-private-claim");
        db.WhatsAppChannelConfigurations.Add(new PersistedWhatsAppChannelConfiguration
        {
            Id = Guid.NewGuid(), TenantId = acme.OrganisationId, WorkspaceId = "main", PayloadJson = """{"EncryptedAccessToken":"protected:token"}""",
        });
        await db.SaveChangesAsync();

        var export = await new TenantDataService(db).ExportAsync(acme.OrganisationId);

        Assert.Contains("acme-secret-claim", export);
        Assert.DoesNotContain("baraka-private-claim", export);
        Assert.DoesNotContain("protected:token", export);
        Assert.DoesNotContain("WhatsAppChannelConfigurations", export);
        Assert.Equal(acme.OrganisationId, JsonDocument.Parse(export).RootElement.GetProperty("organisationId").GetString());
    }

    [Fact]
    public async Task ClosingAnOrganisation_NeedsItsExactName_RemovesItsDataAndAccess_AndLeavesOthersAlone()
    {
        var acme = await service.CreateAsync("owner-acme", "Acme Assurance", OrganisationType.Insurer, "c");
        var baraka = await service.CreateAsync("owner-baraka", "Baraka Insurance", OrganisationType.Insurer, "c");
        AddRecord(acme.OrganisationId, "acme-claim");
        AddRecord(baraka.OrganisationId, "baraka-claim");
        await db.SaveChangesAsync();

        await service.InviteAsync(acme.OrganisationId, "owner-acme", "someone@acme.example", "ClaimsOfficer", "c");
        await Assert.ThrowsAsync<OrganisationException>(() => service.CloseAsync(acme.OrganisationId, "owner-acme", "acme", "c"));
        await service.CloseAsync(acme.OrganisationId, "owner-acme", "Acme Assurance", "c");
        var removed = await new TenantDataService(db).PurgeAsync(acme.OrganisationId);

        Assert.True(removed >= 3); // the record, the membership, the role
        Assert.DoesNotContain(db.TenantOnboardings, r => r.TenantId == acme.OrganisationId);
        Assert.DoesNotContain(db.UserTenantMemberships, m => m.TenantId == acme.OrganisationId);
        Assert.Empty(db.OrganisationInvitations.Where(i => i.OrganisationId == acme.OrganisationId));
        Assert.Contains(db.TenantOnboardings, r => r.TenantId == baraka.OrganisationId);
        Assert.Contains(db.UserTenantMemberships, m => m.TenantId == baraka.OrganisationId);
        Assert.Empty(await service.ListMineAsync("owner-acme"));
        Assert.Single(await service.ListMineAsync("owner-baraka"));
    }

    private void AddRecord(string tenantId, string text) =>
        db.TenantOnboardings.Add(new PersistedTenantOnboarding
        {
            Id = Guid.NewGuid(), TenantId = tenantId, WorkspaceId = "main", PayloadJson = $"{{\"note\":\"{text}\"}}",
        });
}
