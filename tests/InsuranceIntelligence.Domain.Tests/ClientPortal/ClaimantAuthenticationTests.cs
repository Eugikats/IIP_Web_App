using InsuranceIntelligence.Domain.ClientPortal;
using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Domain.Tests.ClientPortal;

public sealed class ClaimantAuthenticationTests
{
    private static readonly TenantScope Scope = TenantScope.Create("tenant-demo", "workspace-claims");

    [Fact]
    public void MagicLinkTokenIsRedeemableBeforeExpiryAndBeforeConsumption()
    {
        var token = Token(consumedAt: null, expiresAt: DateTimeOffset.UtcNow.AddMinutes(10));

        Assert.True(token.IsRedeemableAt(DateTimeOffset.UtcNow));
    }

    [Fact]
    public void MagicLinkTokenIsNotRedeemableAfterExpiry()
    {
        var token = Token(consumedAt: null, expiresAt: DateTimeOffset.UtcNow.AddMinutes(-1));

        Assert.False(token.IsRedeemableAt(DateTimeOffset.UtcNow));
    }

    [Fact]
    public void MagicLinkTokenIsNotRedeemableOnceConsumedEvenBeforeExpiry()
    {
        var now = DateTimeOffset.UtcNow;
        var token = Token(consumedAt: now.AddSeconds(-1), expiresAt: now.AddMinutes(10));

        Assert.False(token.IsRedeemableAt(now));
    }

    [Fact]
    public void PortalSessionIsValidBeforeExpiryAndWhenNotRevoked()
    {
        var session = Session(revokedAt: null, expiresAt: DateTimeOffset.UtcNow.AddHours(1));

        Assert.True(session.IsValidAt(DateTimeOffset.UtcNow));
    }

    [Fact]
    public void PortalSessionIsNotValidAfterExpiry()
    {
        var session = Session(revokedAt: null, expiresAt: DateTimeOffset.UtcNow.AddSeconds(-1));

        Assert.False(session.IsValidAt(DateTimeOffset.UtcNow));
    }

    [Fact]
    public void PortalSessionIsNotValidOnceRevokedEvenBeforeExpiry()
    {
        var now = DateTimeOffset.UtcNow;
        var session = Session(revokedAt: now.AddSeconds(-1), expiresAt: now.AddHours(1));

        Assert.False(session.IsValidAt(now));
    }

    [Fact]
    public void TokenHashIsDeterministicForTheSameRawTokenAndDiffersAcrossRandomTokens()
    {
        var raw = ClaimantTokenGenerator.GenerateRawToken();

        Assert.Equal(ClaimantTokenGenerator.Hash(raw), ClaimantTokenGenerator.Hash(raw));
        Assert.NotEqual(ClaimantTokenGenerator.Hash(raw), ClaimantTokenGenerator.Hash(ClaimantTokenGenerator.GenerateRawToken()));
    }

    [Theory]
    [InlineData("Sarah@Example.com", "sarah@example.com", true)]
    [InlineData("+256 700 000 000", "+256700000000", true)]
    [InlineData("sarah@example.com", "attacker@example.com", false)]
    public void ContactMatcherNormalizesBeforeComparing(string candidate, string onFile, bool expectedMatch)
    {
        Assert.Equal(expectedMatch, ClaimantContactMatcher.Matches(candidate, onFile));
    }

    [Theory]
    [InlineData("sarah@example.com", ClaimantContactChannel.Email)]
    [InlineData("+256700000000", ClaimantContactChannel.Sms)]
    public void ContactMatcherClassifiesChannelByPresenceOfAt(string value, ClaimantContactChannel expected)
    {
        Assert.Equal(expected, ClaimantContactMatcher.ClassifyChannel(value));
    }

    private static ClaimantMagicLinkToken Token(DateTimeOffset? consumedAt, DateTimeOffset expiresAt) => new()
    {
        TokenId = Guid.NewGuid(),
        Scope = Scope,
        CaseId = Guid.NewGuid(),
        ContactValue = "sarah@example.com",
        Channel = ClaimantContactChannel.Email,
        TokenHash = ClaimantTokenGenerator.Hash("raw-token"),
        ExpiresAt = expiresAt,
        ConsumedAt = consumedAt,
        CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-1),
        CorrelationId = "corr-test"
    };

    private static ClaimantPortalSession Session(DateTimeOffset? revokedAt, DateTimeOffset expiresAt) => new()
    {
        SessionId = Guid.NewGuid(),
        Scope = Scope,
        CaseId = Guid.NewGuid(),
        ContactValue = "sarah@example.com",
        SessionTokenHash = ClaimantTokenGenerator.Hash("raw-session-token"),
        ExpiresAt = expiresAt,
        RevokedAt = revokedAt,
        CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-1),
        CorrelationId = "corr-test"
    };
}
