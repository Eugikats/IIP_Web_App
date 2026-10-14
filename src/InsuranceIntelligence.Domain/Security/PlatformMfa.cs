using System.Security.Cryptography;
using System.Text;
using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Domain.Security;

/// <summary>The ways a person can prove it is them, after signing in with Microsoft or Google. IIP's own
/// second step, separate from whatever the sign-in provider already asks for. Modelled on CIP's platform MFA.</summary>
public static class MfaMethods
{
    public const string Totp = "totp";
    public const string Passkey = "passkey";
    public const string EmailOtp = "email_otp";
    public const string SmsOtp = "sms_otp";
    public const string BackupCodes = "backup_codes";

    public static readonly IReadOnlyList<string> All = [Totp, Passkey, EmailOtp, SmsOtp, BackupCodes];

    public static bool IsKnown(string? method) => method is not null && All.Contains(method, StringComparer.Ordinal);
}

/// <summary>
/// One organisation's rules for the second step. Off by default: nothing changes for anyone until an
/// administrator turns it on. When on, people without a method are asked to set one up (and may postpone once a
/// day while the policy allows it), and people with one must verify before the API answers.
/// </summary>
public sealed record MfaPolicy
{
    public required TenantScope Scope { get; init; }

    /// <summary>When true, the API refuses protected routes until the person has verified.</summary>
    public bool Required { get; init; }

    /// <summary>Whether a person without a method may postpone setting one up, 24 hours at a time.</summary>
    public bool AllowSnooze { get; init; } = true;

    /// <summary>How many different methods a person must have set up. At least 1.</summary>
    public int MinMethods { get; init; } = 1;

    /// <summary>The methods the organisation offers. An administrator can switch a method off.</summary>
    public IReadOnlyList<string> EnabledMethods { get; init; } = MfaMethods.All;

    public DateTimeOffset? UpdatedAt { get; init; }

    public string? UpdatedBy { get; init; }

    public static MfaPolicy Default(TenantScope scope) => new() { Scope = scope };

    public bool Offers(string method) => EnabledMethods.Contains(method, StringComparer.Ordinal);

    public static Guid StorageKey(TenantScope scope) =>
        new(MD5.HashData(Encoding.UTF8.GetBytes($"{scope.TenantId}:{scope.WorkspaceId}:mfa-policy")));
}

/// <summary>An authenticator app (Microsoft Authenticator, Google Authenticator or any standard TOTP app).
/// The secret is encrypted at rest and never returned after set-up.</summary>
public sealed record TotpAuthenticator
{
    public required Guid Id { get; init; }
    public required string Label { get; init; }
    public required string ProtectedSecret { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? ConfirmedAt { get; init; }

    /// <summary>The time step of the last accepted code, so one code cannot be used twice.</summary>
    public long LastUsedStep { get; init; }
}

/// <summary>A one-time recovery code. Only a hash is kept.</summary>
public sealed record BackupCode
{
    public required string Hash { get; init; }
    public DateTimeOffset? UsedAt { get; init; }
}

/// <summary>A passkey or security key. The private key never leaves the person's device.</summary>
public sealed record PasskeyCredential
{
    public required Guid Id { get; init; }
    public required string Nickname { get; init; }
    public required string CredentialId { get; init; }
    public required string PublicKey { get; init; }
    public required uint SignCount { get; init; }
    public string? UserHandle { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? LastUsedAt { get; init; }
}

/// <summary>A code sent by email or SMS, waiting to be typed back. Only a hash of the code is kept.</summary>
public sealed record MfaCodeChallenge
{
    /// <summary>"email" or "sms".</summary>
    public required string Channel { get; init; }

    /// <summary>"enroll" while setting the method up, "login" when verifying.</summary>
    public required string Purpose { get; init; }

    public required string CodeHash { get; init; }
    public required string DestinationMasked { get; init; }

    /// <summary>For an SMS being enrolled, the number to save once the code is confirmed (encrypted).</summary>
    public string? ProtectedDestination { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }
    public required DateTimeOffset ExpiresAt { get; init; }
    public int Attempts { get; init; }
    public int MaxAttempts { get; init; } = 5;
}

/// <summary>Everything about one person's second step, in one document so a change is always consistent.</summary>
public sealed record UserSecurityProfile
{
    public required TenantScope Scope { get; init; }
    public required string UserId { get; init; }

    public IReadOnlyList<TotpAuthenticator> Authenticators { get; init; } = [];
    public IReadOnlyList<BackupCode> BackupCodes { get; init; } = [];
    public IReadOnlyList<PasskeyCredential> Passkeys { get; init; } = [];

    public bool EmailEnrolled { get; init; }
    public bool SmsEnrolled { get; init; }
    public string? ProtectedSmsNumber { get; init; }
    public string? SmsNumberMasked { get; init; }

    public MfaCodeChallenge? PendingChallenge { get; init; }

    /// <summary>A passkey ceremony in progress: the options sent to the browser, kept to check the reply.</summary>
    public string? PendingPasskeyOptionsJson { get; init; }
    public string? PendingPasskeyPurpose { get; init; }
    public DateTimeOffset? PendingPasskeyExpiresAt { get; init; }

    /// <summary>The person postponed set-up until this time.</summary>
    public DateTimeOffset? SnoozeUntil { get; init; }
    public int SnoozeCount { get; init; }

    /// <summary>The person has verified, and stays verified until this time or until they sign out.</summary>
    public DateTimeOffset? VerifiedUntil { get; init; }
    public DateTimeOffset? LastVerifiedAt { get; init; }

    public int FailedAttempts { get; init; }
    public DateTimeOffset? FirstFailureAt { get; init; }
    public DateTimeOffset? LockedUntil { get; init; }

    public DateTimeOffset UpdatedAt { get; init; }

    public static UserSecurityProfile Empty(TenantScope scope, string userId, DateTimeOffset now) =>
        new() { Scope = scope, UserId = userId, UpdatedAt = now };

    public IReadOnlyList<TotpAuthenticator> ConfirmedAuthenticators => Authenticators.Where(a => a.ConfirmedAt is not null).ToArray();

    public int UnusedBackupCodes => BackupCodes.Count(code => code.UsedAt is null);

    /// <summary>The methods this person has set up and can verify with.</summary>
    public IReadOnlyList<string> EnrolledMethods
    {
        get
        {
            var methods = new List<string>();
            if (ConfirmedAuthenticators.Count > 0) methods.Add(MfaMethods.Totp);
            if (Passkeys.Count > 0) methods.Add(MfaMethods.Passkey);
            if (EmailEnrolled) methods.Add(MfaMethods.EmailOtp);
            if (SmsEnrolled) methods.Add(MfaMethods.SmsOtp);
            if (UnusedBackupCodes > 0) methods.Add(MfaMethods.BackupCodes);
            return methods;
        }
    }

    public static Guid StorageKey(TenantScope scope, string userId) =>
        new(MD5.HashData(Encoding.UTF8.GetBytes($"{scope.TenantId}:{scope.WorkspaceId}:user-security:{userId.ToLowerInvariant()}")));
}

public interface IUserSecurityRepository
{
    Task<UserSecurityProfile?> GetAsync(TenantScope scope, string userId, CancellationToken cancellationToken = default);

    Task SaveAsync(UserSecurityProfile profile, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UserSecurityProfile>> ListAsync(TenantScope scope, CancellationToken cancellationToken = default);
}

public interface IMfaPolicyRepository
{
    Task<MfaPolicy?> GetAsync(TenantScope scope, CancellationToken cancellationToken = default);

    Task SaveAsync(MfaPolicy policy, CancellationToken cancellationToken = default);
}
