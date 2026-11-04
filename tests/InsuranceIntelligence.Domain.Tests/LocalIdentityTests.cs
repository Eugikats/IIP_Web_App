using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Identity;
using Xunit;

namespace InsuranceIntelligence.Domain.Tests;

/// <summary>The rules under the platform's own sign-in (build-loop item 30): passwords, how they are kept, and the secrets in email links.</summary>
public sealed class LocalIdentityTests
{
    [Theory]
    [InlineData("correct horse battery staple", true)]
    [InlineData("a-long-enough-phrase-7", true)]
    [InlineData("twelve chars!", true)]
    [InlineData("short1!", false)]
    [InlineData("elevenchars", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("password1234", false)]
    [InlineData("PASSWORD1234", false)]
    [InlineData("aaaaaaaaaaaaaaaa", false)]
    [InlineData("121212121212121", false)]
    public void APasswordMustBeLongAndNotTheOnesEveryoneTries(string? password, bool allowed)
    {
        Assert.Equal(allowed, PasswordPolicy.Check(password) is null);
    }

    [Fact]
    public void APasswordCannotBeBuiltFromTheOwnersEmailOrCompany()
    {
        Assert.NotNull(PasswordPolicy.Check("amina.okello-2026-xx", "amina.okello@acme.test"));
        Assert.NotNull(PasswordPolicy.Check("we-love-Acme Insurance-2026", "x@y.test", "Acme Insurance"));
        Assert.Null(PasswordPolicy.Check("a-different-phrase-entirely", "amina.okello@acme.test", "Acme Insurance"));
    }

    [Fact]
    public void APasswordCanBeAtMostOneHundredTwentyEightCharacters()
    {
        var varied = string.Concat(Enumerable.Repeat("correct horse-", 10))[..128];
        Assert.Null(PasswordPolicy.Check(varied));
        Assert.NotNull(PasswordPolicy.Check(varied + "x"));
    }

    [Fact]
    public void APasswordIsKeptOnlyAsASaltedHash_ThatChecksOutOnlyForTheRightPassword()
    {
        var (hash, salt, iterations) = PasswordHasher.Hash("a-long-enough-phrase-7");

        Assert.DoesNotContain("long-enough", hash);
        Assert.True(PasswordHasher.Verify("a-long-enough-phrase-7", hash, salt, iterations));
        Assert.False(PasswordHasher.Verify("a-long-enough-phrase-8", hash, salt, iterations));
        Assert.False(PasswordHasher.Verify(string.Empty, hash, salt, iterations));
        Assert.Equal(PasswordHasher.CurrentIterations, iterations);
    }

    [Fact]
    public void TheSamePasswordGetsADifferentSaltAndHashEveryTime()
    {
        var first = PasswordHasher.Hash("a-long-enough-phrase-7");
        var second = PasswordHasher.Hash("a-long-enough-phrase-7");

        Assert.NotEqual(first.Salt, second.Salt);
        Assert.NotEqual(first.Hash, second.Hash);
    }

    [Fact]
    public void AnOlderPasswordWithFewerRoundsStillChecksOut()
    {
        var salt = Convert.ToBase64String(new byte[16]);
        var oldHash = Convert.ToBase64String(System.Security.Cryptography.Rfc2898DeriveBytes.Pbkdf2(
            System.Text.Encoding.UTF8.GetBytes("a-long-enough-phrase-7"), new byte[16], 100_000, System.Security.Cryptography.HashAlgorithmName.SHA256, 32));

        Assert.True(PasswordHasher.Verify("a-long-enough-phrase-7", oldHash, salt, 100_000));
    }

    [Fact]
    public void EmailLinkSecretsAreLongUniqueAndOnlyTheirFingerprintIsKept()
    {
        var a = IdentityTokens.NewSecret();
        var b = IdentityTokens.NewSecret();

        Assert.NotEqual(a, b);
        Assert.True(a.Length >= 40);
        Assert.Matches("^[A-Za-z0-9_-]+$", a);
        Assert.Equal(IdentityTokens.Hash(a), IdentityTokens.Hash(a));
        Assert.DoesNotContain(a, IdentityTokens.Hash(a));
        Assert.NotEqual(IdentityTokens.KeyFor("signup", IdentityTokens.Hash(a)), IdentityTokens.KeyFor("reset", IdentityTokens.Hash(a)));
        Assert.NotEqual(IdentityTokens.KeyFor("signup", IdentityTokens.Hash(a)), IdentityTokens.KeyFor("signup", IdentityTokens.Hash(b)));
    }

    [Fact]
    public void AnEmailIsComparedWithoutRegardToCaseOrSpaces()
    {
        Assert.Equal("amina@acme.test", LocalCredential.NormaliseEmail("  Amina@Acme.Test "));
        Assert.Equal(LocalCredential.StorageKey("amina@acme.test"), LocalCredential.StorageKey(LocalCredential.NormaliseEmail("AMINA@acme.test")));
    }

    [Fact]
    public void AnInvitationIsPendingUntilAcceptedRevokedOrExpired()
    {
        var now = DateTimeOffset.UtcNow;
        var invitation = new StaffInvitation
        {
            InvitationId = Guid.NewGuid(), Scope = TenantScope.Create("t", "w"), TokenHash = "h", Email = "a@b.test", Role = "ClaimsOfficer",
            InvitedByUserId = "u", CreatedAt = now, ExpiresAt = now.AddDays(7),
        };

        Assert.Equal("Pending", invitation.Status(now));
        Assert.Equal("Expired", invitation.Status(now.AddDays(8)));
        Assert.Equal("Accepted", (invitation with { AcceptedAt = now }).Status(now));
        Assert.Equal("Revoked", (invitation with { RevokedAt = now }).Status(now));
    }

    [Fact]
    public void ACredentialIsLockedOnlyUntilTheLockEnds()
    {
        var now = DateTimeOffset.UtcNow;
        var credential = new LocalCredential
        {
            UserId = "u", Email = "a@b.test", PasswordHash = "h", PasswordSalt = "s", Iterations = 1, CreatedAt = now, PasswordChangedAt = now, LockedUntil = now.AddMinutes(15),
        };

        Assert.True(credential.IsLocked(now));
        Assert.False(credential.IsLocked(now.AddMinutes(16)));
        Assert.False((credential with { LockedUntil = null }).IsLocked(now));
    }
}
