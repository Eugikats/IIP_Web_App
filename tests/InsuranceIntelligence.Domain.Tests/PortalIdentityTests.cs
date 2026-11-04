using InsuranceIntelligence.Domain.ClientPortal;
using InsuranceIntelligence.Domain.Common;
using Xunit;

namespace InsuranceIntelligence.Domain.Tests;

/// <summary>The short name, web address and look of a workspace's customer pages (build-loop item 29).</summary>
public sealed class PortalIdentityTests
{
    [Theory]
    [InlineData("acme", true)]
    [InlineData("acme-insurance", true)]
    [InlineData("a2b", true)]
    [InlineData("ab", false)]
    [InlineData("1acme", false)]
    [InlineData("Acme", false)]
    [InlineData("acme_insurance", false)]
    [InlineData("acme--insurance", false)]
    [InlineData("acme-", false)]
    [InlineData("admin", false)]
    [InlineData("api", false)]
    [InlineData("a b c", false)]
    [InlineData("", false)]
    public void AShortNameIsLowerCaseAndNotReserved(string slug, bool allowed)
    {
        Assert.Equal(allowed, PortalIdentity.ValidateSlug(slug) is null);
    }

    [Fact]
    public void AShortNameCanBeAtMostFortyCharacters()
    {
        Assert.Null(PortalIdentity.ValidateSlug("a" + new string('b', 39)));
        Assert.NotNull(PortalIdentity.ValidateSlug("a" + new string('b', 40)));
    }

    [Theory]
    [InlineData("claims.insurer.com", true)]
    [InlineData("insurer.co.ug", true)]
    [InlineData("claims-1.insurer.com", true)]
    [InlineData("Claims.Insurer.com", false)]
    [InlineData("https://claims.insurer.com", false)]
    [InlineData("claims.insurer.com:8443", false)]
    [InlineData("claims.insurer.com/path", false)]
    [InlineData("localhost", false)]
    [InlineData("-claims.insurer.com", false)]
    [InlineData("claims..insurer.com", false)]
    [InlineData("10.0.0.5", false)]
    public void AWebAddressIsAPlainLowerCaseHostName(string hostname, bool allowed)
    {
        Assert.Equal(allowed, PortalIdentity.ValidateHostname(hostname) is null);
    }

    [Theory]
    [InlineData("#1F5EFF", true)]
    [InlineData("#1769FF", true)]
    [InlineData("#000000", true)]
    [InlineData("#0B6B3A", true)]
    [InlineData("#FFCC00", false)]
    [InlineData("#FFFFFF", false)]
    [InlineData("#CCCCCC", false)]
    [InlineData("1F5EFF", false)]
    [InlineData("#1F5EF", false)]
    [InlineData("#GGGGGG", false)]
    [InlineData("red", false)]
    public void AButtonColourMustBeSixDigitsAndReadableWithWhiteText(string colour, bool allowed)
    {
        Assert.Equal(allowed, PortalIdentity.ValidateAccentColour(colour) is null);
    }

    [Fact]
    public void TheContrastOfWhiteWithItselfIsOneAndWithBlackIsTwentyOne()
    {
        Assert.Equal(1.0, PortalIdentity.ContrastWithWhite("#FFFFFF"), 1);
        Assert.Equal(21.0, PortalIdentity.ContrastWithWhite("#000000"), 1);
    }

    [Theory]
    [InlineData("https://cdn.insurer.com/logo.png", true)]
    [InlineData("http://cdn.insurer.com/logo.png", false)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("data:image/png;base64,AAAA", false)]
    [InlineData("https://user:pass@cdn.insurer.com/logo.png", false)]
    [InlineData("not a url", false)]
    public void ALogoIsAnHttpsAddress(string url, bool allowed)
    {
        Assert.Equal(allowed, PortalIdentity.ValidateLogoUrl(url) is null);
    }

    [Fact]
    public void ANameIsRequiredAndBounded()
    {
        Assert.Null(PortalIdentity.ValidateDisplayName("Acme Insurance"));
        Assert.NotNull(PortalIdentity.ValidateDisplayName("  "));
        Assert.NotNull(PortalIdentity.ValidateDisplayName(new string('x', 81)));
    }

    [Fact]
    public void ASecretIsKeptOnlyAsAHash_ThatIsTheSameEveryTime()
    {
        var hash = PortalIdentity.HashSecret("s3cret");

        Assert.Equal(hash, PortalIdentity.HashSecret("s3cret"));
        Assert.NotEqual(hash, PortalIdentity.HashSecret("s3cret2"));
        Assert.DoesNotContain("s3cret", hash);
        Assert.Equal(64, hash.Length);
    }

    [Fact]
    public void AWebAddressCountsOnlyOnceItIsVerified()
    {
        var scope = TenantScope.Create("t", "w");
        var identity = new PortalIdentity { Scope = scope, Slug = "acme", DisplayName = "Acme", Hostname = "claims.acme.com", UpdatedByUserId = "u", UpdatedAt = DateTimeOffset.UtcNow };

        Assert.False(identity.HostnameIsVerified);
        Assert.True((identity with { HostnameVerifiedAt = DateTimeOffset.UtcNow }).HostnameIsVerified);
        Assert.NotEqual(PortalIdentity.StorageKey(scope), PortalIdentity.StorageKey(TenantScope.Create("t", "other")));
    }
}
