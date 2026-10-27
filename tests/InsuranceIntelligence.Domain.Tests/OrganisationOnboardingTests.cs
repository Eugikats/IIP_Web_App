using InsuranceIntelligence.Application.DemoSeeding;
using InsuranceIntelligence.Application.Organisations;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Organisations;
using InsuranceIntelligence.Infrastructure.Persistence;
using InsuranceIntelligence.LocalTestDoubles;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace InsuranceIntelligence.Domain.Tests;

public sealed class OrganisationOnboardingTests : IDisposable
{
    private readonly InsuranceIntelligenceDbContext db = new(
        new DbContextOptionsBuilder<InsuranceIntelligenceDbContext>().UseInMemoryDatabase($"onboarding-{Guid.NewGuid():N}").Options);
    private readonly InMemoryCaseRepository cases = new();
    private readonly InMemoryWhatsAppChannelConfigurationRepository whatsApp = new();
    private readonly DemoSeedInMemoryProductRepository products = new();
    private readonly OrganisationService organisations;
    private readonly OrganisationOnboardingService onboarding;

    public OrganisationOnboardingTests()
    {
        var repository = new EfOrganisationRepository(db);
        var membership = new EfTenantMembershipWriter(db);
        var audit = new InMemoryAuditRepository();
        organisations = new OrganisationService(repository, membership, audit, cases);
        var seeder = new DemoSeedService(
            cases, new InMemoryCaseDocumentRepository(), new InMemoryApprovalRepository(), audit, new InMemoryCustomerRepository(),
            new InMemoryPolicyRepository(), new InMemoryCoverageRepository(), new InMemoryBranchRepository(), new InMemoryProducerRepository(), products);
        onboarding = new OrganisationOnboardingService(
            repository, whatsApp, products, new InMemoryKnowledgeDocumentRepository(), cases, membership, seeder);
    }

    public void Dispose() => db.Dispose();

    [Fact]
    public async Task TheChecklist_ReflectsWhatActuallyExists_NotWhatSomeoneTicked()
    {
        var acme = await organisations.CreateAsync("owner", "Acme Assurance", OrganisationType.Insurer, "c");

        var fresh = await onboarding.ChecklistAsync(acme.OrganisationId);
        Assert.Equal(1, fresh.Completed); // only the sign-up itself
        Assert.Equal(6, fresh.Total);

        await whatsApp.SaveAsync(new InsuranceIntelligence.Domain.Integrations.WhatsAppChannelConfiguration
        {
            Scope = acme.Scope, PhoneNumberId = "p", EncryptedAccessToken = "a", EncryptedAppSecret = "s", EncryptedVerifyToken = "v",
            ConfiguredByUserId = "owner", ConfiguredAt = DateTimeOffset.UtcNow,
        });
        var token = (await organisations.InviteAsync(acme.OrganisationId, "owner", "amina@acme.example", "ClaimsOfficer", "c")).Token;
        await organisations.AcceptInvitationAsync("amina", "amina@acme.example", token, "c");

        var progressed = await onboarding.ChecklistAsync(acme.OrganisationId);
        Assert.Equal(3, progressed.Completed);
        Assert.True(progressed.Steps.Single(s => s.Key == "whatsapp").Done);
        Assert.True(progressed.Steps.Single(s => s.Key == "team").Done);
        Assert.False(progressed.Steps.Single(s => s.Key == "products").Done);
    }

    [Fact]
    public async Task WithTheProfileService_TheChecklistAsksForTheProfileAndTheTermsRightAfterSignUp_AndTicksThemOnlyWhenTheyExist()
    {
        var profile = new CompanyProfileService(new EfWorkspaceRecordStore(db), TimeProvider.System);
        var repository = new EfOrganisationRepository(db);
        var seeder = new DemoSeedService(
            cases, new InMemoryCaseDocumentRepository(), new InMemoryApprovalRepository(), new InMemoryAuditRepository(), new InMemoryCustomerRepository(),
            new InMemoryPolicyRepository(), new InMemoryCoverageRepository(), new InMemoryBranchRepository(), new InMemoryProducerRepository(), products);
        var withProfile = new OrganisationOnboardingService(
            repository, whatsApp, products, new InMemoryKnowledgeDocumentRepository(), cases, new EfTenantMembershipWriter(db), seeder, profile);
        var acme = await organisations.CreateAsync("owner", "Acme Assurance", OrganisationType.Insurer, "c");

        var fresh = await withProfile.ChecklistAsync(acme.OrganisationId);

        Assert.Equal(["organisation", "profile", "terms"], fresh.Steps.Take(3).Select(step => step.Key).ToArray());
        Assert.Equal(8, fresh.Total);
        Assert.False(fresh.Steps.Single(step => step.Key == "profile").Done);
        Assert.False(fresh.Steps.Single(step => step.Key == "terms").Done);

        await profile.SaveProfileAsync(
            acme.Scope,
            new CompanyProfile("Acme Assurance Limited", null, LicenceKind.Insurer, "IRA/INS/0001", "", "", "Owner", "owner@acme.example", null, null,
                "Acme Assurance sells motor and medical cover to businesses in Kampala.", DateTimeOffset.UnixEpoch, "x"),
            "owner");
        await profile.AcceptAsync(acme.Scope, "owner", "Owner Person", "Director", true, CompanyProfileService.CurrentTermsVersion, CompanyProfileService.CurrentDataProcessingVersion);

        var done = await withProfile.ChecklistAsync(acme.OrganisationId);
        Assert.True(done.Steps.Single(step => step.Key == "profile").Done);
        Assert.True(done.Steps.Single(step => step.Key == "terms").Done);
    }

    [Fact]
    public async Task TheSandbox_IsASeparateWorkspaceFilledOnce_AndNeverVisitedByScheduledJobs()
    {
        var acme = await organisations.CreateAsync("owner", "Acme Assurance", OrganisationType.Insurer, "c");

        var first = await onboarding.EnableSandboxAsync(acme.OrganisationId, "owner");
        var seeded = (await cases.ListAsync(first)).Count;
        var second = await onboarding.EnableSandboxAsync(acme.OrganisationId, "owner");

        Assert.Equal("sandbox", first.WorkspaceId);
        Assert.Equal(first, second);
        Assert.True(seeded > 10);
        Assert.Equal(seeded, (await cases.ListAsync(second)).Count); // not filled twice
        Assert.Empty(await cases.ListAsync(acme.Scope)); // live workspace untouched
        Assert.True((await onboarding.ChecklistAsync(acme.OrganisationId)).Steps.Single(s => s.Key == "sandbox").Done);

        var visited = await new EfWorkspaceDirectory(db).ListActiveWorkspacesAsync();
        Assert.Contains(acme.Scope, visited);
        Assert.DoesNotContain(visited, scope => scope.WorkspaceId == "sandbox");
    }

    [Fact]
    public async Task ASuspendedOrganisation_IsNotVisitedByScheduledJobs_OrOpenedAsASandbox()
    {
        var acme = await organisations.CreateAsync("owner", "Acme Assurance", OrganisationType.Insurer, "c");
        await new EfOrganisationRepository(db).UpdateOrganisationAsync(acme with { Status = Organisation.SuspendedStatus });

        Assert.Empty(await new EfWorkspaceDirectory(db).ListActiveWorkspacesAsync());
        await Assert.ThrowsAsync<OrganisationException>(() => onboarding.EnableSandboxAsync(acme.OrganisationId, "owner"));
    }
}
