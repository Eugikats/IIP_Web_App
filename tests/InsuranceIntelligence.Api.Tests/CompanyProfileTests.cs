using InsuranceIntelligence.Application.Organisations;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InsuranceIntelligence.Api.Tests;

public sealed class CompanyProfileTests
{
    private static readonly TenantScope Ours = new("org-a", "main");
    private static readonly TenantScope Theirs = new("org-b", "main");

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private static CompanyProfileService Service() => new(
        new EfWorkspaceRecordStore(new InsuranceIntelligenceDbContext(
            new DbContextOptionsBuilder<InsuranceIntelligenceDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options)),
        new FixedClock(new DateTimeOffset(2026, 10, 27, 9, 0, 0, TimeSpan.Zero)));

    private static CompanyProfile Mavuno() => new(
        "Mavuno Bank Limited", "Mavuno Bank", LicenceKind.BancassuranceDistributor, "IRA/BA/0042", "", "", "Aisha Namutebi", "aisha@mavuno.example", "+256 700 000 000",
        "https://mavuno.example", "Mavuno Bank serves salaried workers and small traders across Uganda and sells insurance from its partner insurers.", DateTimeOffset.UnixEpoch, "ignored");

    [Fact]
    public async Task TheProfileIsSavedTidied_AndTheRegulatorAndCountryDefaultToUganda()
    {
        var service = Service();

        var saved = await service.SaveProfileAsync(Ours, Mavuno() with { LegalName = "  Mavuno   Bank Limited ", LicenceNumber = " IRA/BA/0042 " }, "admin-1");

        Assert.Equal("Mavuno Bank Limited", saved.LegalName);
        Assert.Equal("IRA/BA/0042", saved.LicenceNumber);
        Assert.Equal("Uganda", saved.Country);
        Assert.Equal("Insurance Regulatory Authority of Uganda", saved.Regulator);
        Assert.Equal("admin-1", saved.UpdatedBy);
        Assert.Equal(saved, await service.GetProfileAsync(Ours));
        Assert.Null(await service.GetProfileAsync(Theirs));
    }

    [Theory]
    [InlineData("LegalName", "")]
    [InlineData("LicenceNumber", "??")]
    [InlineData("LicenceNumber", "")]
    [InlineData("ContactEmail", "not-an-email")]
    [InlineData("ContactName", " ")]
    [InlineData("Website", "ftp://mavuno.example")]
    [InlineData("About", "Too short")]
    public async Task APoorProfileIsRefusedWithAReasonAPersonCanAct(string field, string value)
    {
        var bad = field switch
        {
            "LegalName" => Mavuno() with { LegalName = value },
            "LicenceNumber" => Mavuno() with { LicenceNumber = value },
            "ContactEmail" => Mavuno() with { ContactEmail = value },
            "ContactName" => Mavuno() with { ContactName = value },
            "Website" => Mavuno() with { Website = value },
            _ => Mavuno() with { About = value },
        };

        var error = await Assert.ThrowsAsync<CompanyProfileException>(() => Service().SaveProfileAsync(Ours, bad, "admin-1"));

        Assert.False(string.IsNullOrWhiteSpace(error.Message));
    }

    [Fact]
    public async Task AcceptingNeedsANamedAuthorisedPerson_AndTheVersionsTheyWereShown()
    {
        var service = Service();
        const string terms = CompanyProfileService.CurrentTermsVersion;
        const string dpa = CompanyProfileService.CurrentDataProcessingVersion;

        await Assert.ThrowsAsync<CompanyProfileException>(() => service.AcceptAsync(Ours, "admin-1", "", "Managing Director", true, terms, dpa));
        await Assert.ThrowsAsync<CompanyProfileException>(() => service.AcceptAsync(Ours, "admin-1", "Aisha Namutebi", "", true, terms, dpa));
        await Assert.ThrowsAsync<CompanyProfileException>(() => service.AcceptAsync(Ours, "admin-1", "Aisha Namutebi", "Managing Director", false, terms, dpa));
        await Assert.ThrowsAsync<CompanyProfileException>(() => service.AcceptAsync(Ours, "admin-1", "Aisha Namutebi", "Managing Director", true, "2020-01-01", dpa));
        Assert.False(await service.HasAcceptedCurrentAsync(Ours));

        var accepted = await service.AcceptAsync(Ours, "admin-1", " Aisha  Namutebi ", "Managing Director", true, terms, dpa);

        Assert.Equal("Aisha Namutebi", accepted.AcceptedByName);
        Assert.Equal("admin-1", accepted.AcceptedByUserId);
        Assert.Equal(new DateTimeOffset(2026, 10, 27, 9, 0, 0, TimeSpan.Zero), accepted.AcceptedAt);
        Assert.True(await service.HasAcceptedCurrentAsync(Ours));
        Assert.False(await service.HasAcceptedCurrentAsync(Theirs));
    }

    [Fact]
    public async Task AnAcceptanceOfAnOlderVersionNoLongerCounts_ButStaysOnRecord()
    {
        var store = new EfWorkspaceRecordStore(new InsuranceIntelligenceDbContext(
            new DbContextOptionsBuilder<InsuranceIntelligenceDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options));
        var service = new CompanyProfileService(store, TimeProvider.System);
        await store.SaveAsync(Ours, CompanyProfileService.TermsKind, CompanyProfileService.Key,
            """{"termsVersion":"2025-01-01","dataProcessingVersion":"2025-01-01","acceptedByUserId":"u","acceptedByName":"Old Acceptor","acceptedByTitle":"CEO","acceptedAt":"2025-01-02T00:00:00+00:00"}""");

        Assert.False(await service.HasAcceptedCurrentAsync(Ours));
        Assert.Equal("Old Acceptor", (await service.GetAcceptanceAsync(Ours))!.AcceptedByName);
    }
}
