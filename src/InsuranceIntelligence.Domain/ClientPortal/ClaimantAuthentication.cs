using System.Security.Cryptography;
using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Domain.ClientPortal;

/// <summary>
/// A short-lived, single-use credential sent to a claimant's email or phone so they can access
/// their own claim without a platform account. Only the SHA-256 hash of the token is ever
/// persisted — the raw value exists only in the outbound message and the claimant's browser, the
/// same way a password reset token would be handled. Redeeming a valid token issues a
/// <see cref="ClaimantPortalSession"/>; the magic link itself is never reused as a bearer
/// credential (a mail-scanning bot or link-preview fetch would otherwise burn a claimant's only
/// token before they click it).
/// </summary>
public sealed record ClaimantMagicLinkToken
{
    public required Guid TokenId { get; init; }
    public required TenantScope Scope { get; init; }

    /// <summary>Null means this token authenticates a verified policyholder who has no claim yet
    /// — redeeming it issues a session that can only be used to submit a new claim on
    /// <see cref="PolicyNumber"/>, never to read or modify anyone's existing case.</summary>
    public Guid? CaseId { get; init; }

    /// <summary>The policy this token was verified against. Always set — even for an
    /// existing-case token — so redemption can re-verify identity independently of the case
    /// record.</summary>
    public required string PolicyNumber { get; init; }

    /// <summary>The normalized email or phone the token was issued to — checked again at
    /// redemption so a token can never authenticate a different contact channel than it was sent
    /// to.</summary>
    public required string ContactValue { get; init; }
    public required ClaimantContactChannel Channel { get; init; }
    public required string TokenHash { get; init; }
    public required DateTimeOffset ExpiresAt { get; init; }
    public DateTimeOffset? ConsumedAt { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required string CorrelationId { get; init; }

    /// <summary>What actually happened when this token's link was sent — "Sent", "Failed", or
    /// "ConfigurationRequired". Never shown to the anonymous requester (the endpoint's response is
    /// deliberately identical either way, to preserve anti-enumeration); it exists so a failure is
    /// visible somewhere — logs, and here for future support tooling — instead of silently
    /// discarded. See ClaimantAuthenticationEndpoints.IssueAndSendAsync.</summary>
    public string? DeliveryStatus { get; init; }
    public string? DeliverySafeError { get; init; }

    public bool IsRedeemableAt(DateTimeOffset now) => ConsumedAt is null && now < ExpiresAt;
}

/// <summary>
/// The credential a claimant's browser holds after redeeming a magic link. Scoped to exactly one
/// case — a session for one claim never grants access to another, even for the same contact
/// value, so a compromised session has the smallest possible blast radius. A session with no
/// <see cref="CaseId"/> yet can only submit a new claim on <see cref="PolicyNumber"/>; once that
/// claim exists the session is upgraded in place (see ClaimantAuthenticationEndpoints.CreateClaim)
/// so the claimant never needs a second link to see what they just submitted.
/// </summary>
public sealed record ClaimantPortalSession
{
    public required Guid SessionId { get; init; }
    public required TenantScope Scope { get; init; }
    public Guid? CaseId { get; init; }
    public required string PolicyNumber { get; init; }
    public required string ContactValue { get; init; }
    public required string SessionTokenHash { get; init; }
    public required DateTimeOffset ExpiresAt { get; init; }
    public DateTimeOffset? RevokedAt { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required string CorrelationId { get; init; }

    public bool IsValidAt(DateTimeOffset now) => RevokedAt is null && now < ExpiresAt;
}

public enum ClaimantContactChannel
{
    Email,
    Sms
}

/// <summary>
/// Normalizes and matches a claimant-supplied contact value (email or phone) against the values
/// on file for a case. Shared by magic-link issuance and the legacy header-based portal identity
/// check so both apply exactly the same rule.
/// </summary>
public static class ClaimantContactMatcher
{
    public static bool Matches(string candidate, params string?[] valuesOnFile) =>
        !string.IsNullOrWhiteSpace(candidate)
        && valuesOnFile.Any(value => !string.IsNullOrWhiteSpace(value) && Normalize(candidate) == Normalize(value));

    public static ClaimantContactChannel ClassifyChannel(string contactValue) =>
        contactValue.Contains('@', StringComparison.Ordinal) ? ClaimantContactChannel.Email : ClaimantContactChannel.Sms;

    private static string Normalize(string value) =>
        new(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
}

public interface IClaimantMagicLinkTokenRepository
{
    Task SaveAsync(ClaimantMagicLinkToken token, CancellationToken cancellationToken = default);

    Task<ClaimantMagicLinkToken?> GetByTokenHashAsync(string tokenHash, TenantScope scope, CancellationToken cancellationToken = default);
}

public interface IClaimantPortalSessionRepository
{
    Task SaveAsync(ClaimantPortalSession session, CancellationToken cancellationToken = default);

    Task<ClaimantPortalSession?> GetByTokenHashAsync(string tokenHash, TenantScope scope, CancellationToken cancellationToken = default);
}

/// <summary>
/// Generates opaque bearer tokens and hashes them for storage. Never persist a raw token — only
/// its hash, so a database read (backup, replica, insider) can never be replayed as a live
/// credential.
/// </summary>
public static class ClaimantTokenGenerator
{
    public static string GenerateRawToken() => Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

    public static string Hash(string rawToken) => Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(rawToken)));

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
