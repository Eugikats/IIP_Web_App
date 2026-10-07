using InsuranceIntelligence.Application.CustomerAssistant;

namespace InsuranceIntelligence.Domain.Tests;

public sealed class KatoBrandTests
{
    [Fact]
    public void Unconfigured_Kato_is_Nile_Shield_and_sells_only_its_covers()
    {
        var brand = KatoBrand.FromSettings(null, null, null);

        Assert.False(brand.IsBancassurance);
        Assert.All(brand.Products, product => Assert.Equal(MicroinsuranceCatalog.NileShield, product.Insurer));
        Assert.DoesNotContain(MicroinsuranceCatalog.LoanProtector, brand.Products);
    }

    [Fact]
    public void A_bank_sells_its_partner_insurers_covers_and_each_names_its_insurer()
    {
        var brand = KatoBrand.FromSettings("Mavuno Bank", "Mavuno Bank", "Nile Shield Assurance, Mpingo Life Assurance, Kifaru General Insurance");

        Assert.True(brand.IsBancassurance);
        Assert.Contains(MicroinsuranceCatalog.LoanProtector, brand.Products);
        Assert.Contains(MicroinsuranceCatalog.HomeContents, brand.Products);
        Assert.Contains(MicroinsuranceCatalog.ShopCover, brand.Products);
        Assert.Equal(3, brand.Products.Select(product => product.Insurer).Distinct().Count());
    }

    [Fact]
    public void A_bank_with_one_partner_sells_only_that_partners_covers()
    {
        var brand = KatoBrand.FromSettings("Mavuno Bank", null, "Mpingo Life Assurance");

        Assert.All(brand.Products, product => Assert.Equal(MicroinsuranceCatalog.Mpingo, product.Insurer));
        Assert.Equal("Mavuno Bank", brand.ShortName);
    }
}
