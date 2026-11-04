using System.Security.Cryptography;
using System.Text;
using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Domain.Identity;

/// <summary>
/// What makes a password acceptable (build-loop item 30). Length matters more than rules about symbols, so the policy is a long
/// minimum, a refusal of the passwords everyone tries first, and a refusal of a password built from the person's own email or
/// company name. A check against lists of passwords that have leaked elsewhere needs an outside service and is not built.
/// </summary>
public static class PasswordPolicy
{
    public const int MinLength = 12;
    public const int MaxLength = 128;

    private static readonly HashSet<string> Common = new(StringComparer.OrdinalIgnoreCase)
    {
        "password1234", "password12345", "passw0rd1234", "123456789012", "1234567890123", "qwertyuiop12", "qwerty123456", "letmein12345",
        "welcome12345", "administrator", "iloveyou1234", "changeme1234", "insurance123", "insurance1234", "claims123456", "abcdefghijkl",
        "111111111111", "000000000000", "aaaaaaaaaaaa", "password!234", "p@ssword1234", "p@ssw0rd1234", "adminadmin12", "welcome@1234",
    };

    /// <summary>Why this password cannot be used, in words a person can act on, or null when it can.</summary>
    public static string? Check(string? password, string? email = null, string? companyName = null)
    {
        if (string.IsNullOrEmpty(password) || password.Length < MinLength)
        {
            return $"Use at least {MinLength} characters. A few unrelated words make a strong, memorable password.";
        }

        if (password.Length > MaxLength)
        {
            return $"Use at most {MaxLength} characters.";
        }

        if (Common.Contains(password) || password.Distinct().Count() <= 3)
        {
            return "That password is too easy to guess. Choose something less common.";
        }

        var local = email?.Split('@')[0];
        if (local is { Length: >= 4 } && password.Contains(local, StringComparison.OrdinalIgnoreCase))
        {
            return "The password should not contain your email name.";
        }

        var company = new string((companyName ?? string.Empty).Where(char.IsLetterOrDigit).ToArray());
        if (company.Length >= 5 && new string(password.Where(char.IsLetterOrDigit).ToArray()).Contains(company, StringComparison.OrdinalIgnoreCase))
        {
            return "The password should not contain your company name.";
        }

        return null;
    }
}

/// <summary>PBKDF2 with SHA-256, a fresh random salt for each password and a work factor that can be raised later: a stored hash says
/// how many rounds made it, so older passwords keep working and are upgraded when their owner next signs in.</summary>
public static class PasswordHasher
{
    public const int CurrentIterations = 600_000;
    private const int SaltBytes = 16;
    private const int KeyBytes = 32;

    public static (string Hash, string Salt, int Iterations) Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltBytes);
        return (Convert.ToBase64String(Derive(password, salt, CurrentIterations)), Convert.ToBase64String(salt), CurrentIterations);
    }

    public static bool Verify(string password, string hash, string salt, int iterations) =>
        CryptographicOperations.FixedTimeEquals(Derive(password, Convert.FromBase64String(salt), iterations), Convert.FromBase64String(hash));

    /// <summary>Does the same work as a real check, for an email nobody has, so how long a sign-in takes does not say whether the account exists.</summary>
    public static void BurnTime(string password) => Derive(password, new byte[SaltBytes], CurrentIterations);

    private static byte[] Derive(string password, byte[] salt, int iterations) =>
        Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, iterations, HashAlgorithmName.SHA256, KeyBytes);
}

/// <summary>A person's sign-in on the platform's own identity, for companies without Microsoft 365. One per email address, across the
/// platform: the same person can be a member of several workspaces.</summary>
public sealed record LocalCredential
{
    public required string UserId { get; init; }
    public required string Email { get; init; }
    public required string PasswordHash { get; init; }
    public required string PasswordSalt { get; init; }
    public required int Iterations { get; init; }
    public int FailedAttempts { get; init; }
    public DateTimeOffset? LockedUntil { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required DateTimeOffset PasswordChangedAt { get; init; }

    public static string NormaliseEmail(string email) => email.Trim().ToLowerInvariant();

    public static Guid StorageKey(string normalisedEmail) =>
        new(MD5.HashData(Encoding.UTF8.GetBytes($"local-credential|{normalisedEmail}")));

    public bool IsLocked(DateTimeOffset now) => LockedUntil is { } until && until > now;
}

/// <summary>A company that has asked to join, waiting for its administrator to prove the email address is theirs.</summary>
public sealed record SignupRequest
{
    public required string TokenHash { get; init; }
    public required string CompanyName { get; init; }
    public required string AdminName { get; init; }
    public required string Email { get; init; }
    public required string PasswordHash { get; init; }
    public required string PasswordSalt { get; init; }
    public required int Iterations { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required DateTimeOffset ExpiresAt { get; init; }
    public DateTimeOffset? ConsumedAt { get; init; }
}

/// <summary>An invitation for one person to join a workspace in a role. Only a fingerprint of the invitation's secret is kept.</summary>
public sealed record StaffInvitation
{
    public required Guid InvitationId { get; init; }
    public required TenantScope Scope { get; init; }
    public required string TokenHash { get; init; }
    public required string Email { get; init; }
    public required string Role { get; init; }
    public required string InvitedByUserId { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required DateTimeOffset ExpiresAt { get; init; }
    public DateTimeOffset? AcceptedAt { get; init; }
    public DateTimeOffset? RevokedAt { get; init; }

    public string Status(DateTimeOffset now) =>
        AcceptedAt is not null ? "Accepted" : RevokedAt is not null ? "Revoked" : ExpiresAt <= now ? "Expired" : "Pending";
}

public sealed record PasswordReset
{
    public required string TokenHash { get; init; }
    public required string UserId { get; init; }
    public required string Email { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required DateTimeOffset ExpiresAt { get; init; }
    public DateTimeOffset? ConsumedAt { get; init; }
}

public static class IdentityTokens
{
    /// <summary>An unguessable secret for a link in an email.</summary>
    public static string NewSecret() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).Replace('+', '-').Replace('/', '_').TrimEnd('=');

    public static string Hash(string secret) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(secret))).ToLowerInvariant();

    /// <summary>The row an email link points at, derived from the secret's fingerprint so it can be found without searching.</summary>
    public static Guid KeyFor(string kind, string tokenHash) =>
        new(SHA256.HashData(Encoding.UTF8.GetBytes($"{kind}|{tokenHash}")).AsSpan(0, 16));
}

/// <summary>Where sign-in and sign-up records are kept. Credentials, sign-up requests and password resets live at the platform level (a
/// person is found by email, a link by its secret); invitations belong to the workspace that sent them.</summary>
public interface ILocalIdentityStore
{
    Task<LocalCredential?> GetCredentialAsync(string normalisedEmail, CancellationToken cancellationToken = default);

    Task SaveCredentialAsync(LocalCredential credential, CancellationToken cancellationToken = default);

    Task<SignupRequest?> GetSignupAsync(string tokenHash, CancellationToken cancellationToken = default);

    Task SaveSignupAsync(SignupRequest request, CancellationToken cancellationToken = default);

    Task<PasswordReset?> GetResetAsync(string tokenHash, CancellationToken cancellationToken = default);

    Task SaveResetAsync(PasswordReset reset, CancellationToken cancellationToken = default);

    Task<StaffInvitation?> GetInvitationByTokenAsync(string tokenHash, CancellationToken cancellationToken = default);

    Task<StaffInvitation?> GetInvitationAsync(TenantScope scope, Guid invitationId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StaffInvitation>> ListInvitationsAsync(TenantScope scope, CancellationToken cancellationToken = default);

    Task SaveInvitationAsync(StaffInvitation invitation, CancellationToken cancellationToken = default);

    /// <summary>Every workspace the person with this id is an active member of. Used at sign-in to choose one.</summary>
    Task<IReadOnlyList<TenantScope>> ListMembershipsAsync(string userId, CancellationToken cancellationToken = default);
}
