using InsuranceIntelligence.Application.Organisations;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Organisations;
using InsuranceIntelligence.Infrastructure.Persistence;
using InsuranceIntelligence.LocalTestDoubles;
using Microsoft.EntityFrameworkCore;

namespace InsuranceIntelligence.Api.Tests;

/// <summary>Sign-up, invitations, and the broker/insurer link, against the real storage code.</summary>
public sealed class OrganisationTests : IDisposable
{
    private readonly InsuranceIntelligenceDbContext db = new(
        new DbContextOptionsBuilder<InsuranceIntelligenceDbContext>().UseInMemoryDatabase($"orgs-{Guid.NewGuid():N}").Options);
    private readonly InMemoryCaseRepository cases = new();
    private readonly OrganisationService service;
    private readonly EfPermissionReadRepository permissions;

    public OrganisationTests()
    {
        service = new OrganisationService(new EfOrganisationRepository(db), new EfTenantMembershipWriter(db), new InMemoryAuditRepository(), cases);
        permissions = new EfPermissionReadRepository(db);
    }

    public void Dispose() => db.Dispose();

    private Task<Organisation> NewInsurerAsync(string owner = "owner-insurer", string name = "Acme Assurance") =>
        service.CreateAsync(owner, name, OrganisationType.Insurer, "corr");

    private Task<Organisation> NewBrokerAsync(string owner = "owner-broker", string name = "Kampala Brokers") =>
        service.CreateAsync(owner, name, OrganisationType.Broker, "corr");

    [Fact]
    public async Task SigningUp_CreatesTheOrganisation_AndMakesTheFounderItsAdmin()
    {
        var organisation = await NewInsurerAsync();

        Assert.StartsWith("org-", organisation.OrganisationId);
        var membership = await permissions.GetEffectiveMembershipAsync("owner-insurer", null, null);
        Assert.Equal(organisation.OrganisationId, membership!.TenantId);
        Assert.Contains("Admin", membership.Roles);
        Assert.Contains(Permission.ManageOrganisation, membership.Permissions);
    }

    [Fact]
    public async Task ANameThatIsTooShort_OrAnAccountThatKeepsCreatingOrganisations_IsRefused()
    {
        await Assert.ThrowsAsync<OrganisationException>(() => service.CreateAsync("u", "A", OrganisationType.Broker, "c"));

        for (var i = 0; i < OrganisationService.MaxOrganisationsCreatedPerUser; i++)
        {
            await service.CreateAsync("busy", $"Organisation {i}", OrganisationType.Broker, "c");
        }

        var error = await Assert.ThrowsAsync<OrganisationException>(() => service.CreateAsync("busy", "One too many", OrganisationType.Broker, "c"));
        Assert.Equal("conflict", error.Code);
    }

    [Fact]
    public async Task AnInvitation_IsUsableOnce_ByThePersonItWasSentTo_AndGrantsTheRoleOffered()
    {
        var insurer = await NewInsurerAsync();
        var invitation = await service.InviteAsync(insurer.OrganisationId, "owner-insurer", "Amina@Acme.example", "ClaimsOfficer", "c");

        var wrongPerson = await Assert.ThrowsAsync<OrganisationException>(
            () => service.AcceptInvitationAsync("someone-else", "other@acme.example", invitation.Token, "c"));
        var noEmail = await Assert.ThrowsAsync<OrganisationException>(
            () => service.AcceptInvitationAsync("amina", null, invitation.Token, "c"));
        await service.AcceptInvitationAsync("amina", "amina@acme.example", invitation.Token, "c");
        var again = await Assert.ThrowsAsync<OrganisationException>(
            () => service.AcceptInvitationAsync("amina", "amina@acme.example", invitation.Token, "c"));

        Assert.Equal("forbidden", wrongPerson.Code);
        Assert.Equal("forbidden", noEmail.Code);
        Assert.Equal("not_found", again.Code);
        var membership = await permissions.GetEffectiveMembershipAsync("amina", insurer.OrganisationId, null);
        Assert.Equal(["ClaimsOfficer"], membership!.Roles);
        Assert.DoesNotContain(Permission.ManageOrganisation, membership.Permissions);
    }

    [Fact]
    public async Task AnInvitation_ThatIsRevoked_OrGuessed_OrForTheWrongKindOfOrganisation_DoesNothing()
    {
        var insurer = await NewInsurerAsync();
        var revoked = await service.InviteAsync(insurer.OrganisationId, "owner-insurer", "x@acme.example", "ClaimsOfficer", "c");
        await service.RevokeInvitationAsync(insurer.OrganisationId, "owner-insurer", revoked.InvitationId, "c");

        await Assert.ThrowsAsync<OrganisationException>(() => service.AcceptInvitationAsync("x", "x@acme.example", revoked.Token, "c"));
        await Assert.ThrowsAsync<OrganisationException>(() => service.AcceptInvitationAsync("x", "x@acme.example", "guess", "c"));
        // A broker role cannot be handed out inside an insurer: brokers come in through a link.
        await Assert.ThrowsAsync<OrganisationException>(() => service.InviteAsync(insurer.OrganisationId, "owner-insurer", "y@acme.example", "Broker", "c"));
        await Assert.ThrowsAsync<OrganisationException>(() => service.InviteAsync(insurer.OrganisationId, "owner-insurer", "not-an-email", "ClaimsOfficer", "c"));
    }

    [Fact]
    public async Task ABrokerReachesAnInsurerOnlyThroughALinkBothSidesAgreedTo_AndOnlyAsABroker()
    {
        var insurer = await NewInsurerAsync();
        var broker = await NewBrokerAsync();

        // No link: no access, however the broker asks.
        Assert.Null(await permissions.GetEffectiveMembershipAsync("owner-broker", insurer.OrganisationId, null));

        var link = await service.RequestLinkAsync(broker.OrganisationId, "owner-broker", insurer.OrganisationId, "c");
        Assert.Null(await permissions.GetEffectiveMembershipAsync("owner-broker", insurer.OrganisationId, null)); // asked, not agreed

        await Assert.ThrowsAsync<OrganisationException>(
            () => service.DecideLinkAsync(broker.OrganisationId, "owner-broker", link.LinkId, approve: true, "c")); // cannot approve your own request
        await service.DecideLinkAsync(insurer.OrganisationId, "owner-insurer", link.LinkId, approve: true, "c");

        var access = await permissions.GetEffectiveMembershipAsync("owner-broker", insurer.OrganisationId, null);
        Assert.Equal(insurer.OrganisationId, access!.TenantId);
        Assert.Equal(["Broker"], access.Roles);
        Assert.Contains(Permission.CreateClaim, access.Permissions);
        Assert.DoesNotContain(Permission.ViewAllClaims, access.Permissions);
        Assert.DoesNotContain(Permission.ManageOrganisation, access.Permissions);
        Assert.DoesNotContain(Permission.ConfigureIntegrations, access.Permissions);

        // A person with no tie to the broker gets nothing from the same link.
        Assert.Null(await permissions.GetEffectiveMembershipAsync("stranger", insurer.OrganisationId, null));
    }

    [Fact]
    public async Task EndingTheLink_EndsTheBrokersAccessOnTheVeryNextRequest()
    {
        var insurer = await NewInsurerAsync();
        var broker = await NewBrokerAsync();
        var link = await service.RequestLinkAsync(insurer.OrganisationId, "owner-insurer", broker.OrganisationId, "c");
        await service.DecideLinkAsync(broker.OrganisationId, "owner-broker", link.LinkId, approve: true, "c");
        Assert.NotNull(await permissions.GetEffectiveMembershipAsync("owner-broker", insurer.OrganisationId, null));

        await service.RevokeLinkAsync(insurer.OrganisationId, "owner-insurer", link.LinkId, "c");

        Assert.Null(await new EfPermissionReadRepository(db).GetEffectiveMembershipAsync("owner-broker", insurer.OrganisationId, null));
    }

    [Fact]
    public async Task ALinkJoinsOneBrokerAndOneInsurer_AndOutsidersCannotTouchIt()
    {
        var insurer = await NewInsurerAsync();
        var otherInsurer = await NewInsurerAsync("owner-2", "Other Assurance");
        var broker = await NewBrokerAsync();

        await Assert.ThrowsAsync<OrganisationException>(() => service.RequestLinkAsync(insurer.OrganisationId, "owner-insurer", otherInsurer.OrganisationId, "c"));
        var link = await service.RequestLinkAsync(broker.OrganisationId, "owner-broker", insurer.OrganisationId, "c");
        var duplicate = await Assert.ThrowsAsync<OrganisationException>(() => service.RequestLinkAsync(insurer.OrganisationId, "owner-insurer", broker.OrganisationId, "c"));

        Assert.Equal("conflict", duplicate.Code);
        var outsider = await Assert.ThrowsAsync<OrganisationException>(
            () => service.DecideLinkAsync(otherInsurer.OrganisationId, "owner-2", link.LinkId, approve: true, "c"));
        Assert.Equal("not_found", outsider.Code);
    }

    [Fact]
    public async Task ABrokerWorkingWithTwoInsurers_SeesBothInOneList_AndOneDashboardOfOnlyItsOwnClaims()
    {
        var acme = await NewInsurerAsync("owner-acme", "Acme Assurance");
        var baraka = await NewInsurerAsync("owner-baraka", "Baraka Insurance");
        var broker = await NewBrokerAsync();
        await service.InviteAsync(broker.OrganisationId, "owner-broker", "juma@brokers.example", "Broker", "c")
            .ContinueWith(t => service.AcceptInvitationAsync("juma", "juma@brokers.example", t.Result.Token, "c")).Unwrap();
        foreach (var insurer in new[] { acme, baraka })
        {
            var link = await service.RequestLinkAsync(broker.OrganisationId, "owner-broker", insurer.OrganisationId, "c");
            await service.DecideLinkAsync(insurer.OrganisationId, insurer.CreatedByUserId, link.LinkId, approve: true, "c");
        }

        await cases.SaveAsync(Claim(acme, "juma"), CancellationToken.None);
        await cases.SaveAsync(Claim(acme, "juma", CaseStatus.Settled), CancellationToken.None);
        await cases.SaveAsync(Claim(acme, "someone-at-acme"), CancellationToken.None);   // not the broker's
        await cases.SaveAsync(Claim(baraka, "owner-broker"), CancellationToken.None);

        var mine = await service.ListMineAsync("juma");
        var dashboard = await service.BrokerDashboardAsync(broker.OrganisationId);

        Assert.Equal(["Acme Assurance", "Baraka Insurance", "Kampala Brokers"], mine.Select(m => m.Name).ToArray());
        Assert.Equal(2, dashboard.InsurerCount);
        Assert.Equal(3, dashboard.ClaimsSubmitted); // the insurer's own staff claim is not counted
        Assert.Equal(2, dashboard.OpenClaims);
        Assert.Equal(2, dashboard.Insurers.Single(i => i.InsurerName == "Acme Assurance").ClaimsSubmitted);
    }

    [Fact]
    public async Task OnlyABrokerHasABrokerDashboard()
    {
        var insurer = await NewInsurerAsync();
        var error = await Assert.ThrowsAsync<OrganisationException>(() => service.BrokerDashboardAsync(insurer.OrganisationId));
        Assert.Equal("invalid", error.Code);
    }

    private static ClaimCase Claim(Organisation insurer, string createdBy, CaseStatus status = CaseStatus.New) => new()
    {
        CaseId = Guid.NewGuid(),
        Scope = insurer.Scope,
        CustomerName = "Client",
        PolicyNumber = "POL-1",
        ProductLine = "Motor",
        ClaimType = "Accident",
        Channel = "Broker",
        IncidentDate = DateOnly.FromDateTime(DateTime.UtcNow),
        IncidentDescription = "Collision",
        Status = status,
        CompletenessScore = 0,
        CreatedByUserId = createdBy,
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow,
        CorrelationId = "c",
    };
}
