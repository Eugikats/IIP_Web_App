using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Communications;
using InsuranceIntelligence.Domain.Integrations;
using InsuranceIntelligence.Domain.Security;
using InsuranceIntelligence.Domain.Workflow;

namespace InsuranceIntelligence.Application.Security;

/// <summary>Tunable limits for the second step. Defaults follow CIP's platform MFA, with shorter email and SMS codes.</summary>
public sealed record PlatformMfaOptions
{
    public string Issuer { get; init; } = "Insurance Intelligence Platform";
    public TimeSpan CodeLifetime { get; init; } = TimeSpan.FromMinutes(10);
    public TimeSpan ResendCooldown { get; init; } = TimeSpan.FromSeconds(60);
    public int MaxCodeAttempts { get; init; } = 5;

    /// <summary>How long a person stays verified after proving it is them. Signing out ends it sooner.</summary>
    public TimeSpan VerifiedFor { get; init; } = TimeSpan.FromHours(12);

    /// <summary>This many wrong codes inside the window lock verification for the lock time.</summary>
    public int MaxFailures { get; init; } = 5;
    public TimeSpan FailureWindow { get; init; } = TimeSpan.FromMinutes(15);
    public TimeSpan LockTime { get; init; } = TimeSpan.FromMinutes(15);

    public TimeSpan SnoozeFor { get; init; } = TimeSpan.FromHours(24);
    public int MaxSnoozes { get; init; } = 7;
    public int BackupCodeCount { get; init; } = 8;
    public TimeSpan PolicyCacheFor { get; init; } = TimeSpan.FromSeconds(20);
}

public sealed record MfaResult<T>(bool Ok, T? Value, string? Code, string? Message)
{
    public static MfaResult<T> Success(T value) => new(true, value, null, null);

    public static MfaResult<T> Fail(string code, string message) => new(false, default, code, message);
}

public sealed record MfaCredentialView(string Method, Guid Id, string Label, DateTimeOffset CreatedAt, DateTimeOffset? LastUsedAt);

public sealed record MfaStatus(
    bool PolicyRequired,
    bool AllowSnooze,
    int MinMethods,
    IReadOnlyList<string> OfferedMethods,
    IReadOnlyList<string> EnrolledMethods,
    int UnusedBackupCodes,
    bool Satisfied,
    bool Verified,
    DateTimeOffset? VerifiedUntil,
    bool Snoozed,
    DateTimeOffset? SnoozeUntil,
    bool CanSnooze,
    bool ShouldPrompt,
    bool MustVerify,
    bool LockedOut,
    DateTimeOffset? LockedUntil,
    string? SmsNumberMasked,
    IReadOnlyList<MfaCredentialView> Credentials);

public sealed record TotpEnrolment(Guid Id, string Secret, string ProvisioningUri);

public sealed record MfaCodeSent(string Channel, string DestinationMasked, DateTimeOffset ExpiresAt);

public sealed record MfaMetrics(int PeopleWithAMethod, int TotpPeople, int PasskeyPeople, int EmailPeople, int SmsPeople);

/// <summary>What the API should do with a signed-in request.</summary>
public enum MfaGate
{
    Allowed,
    EnrolRequired,
    VerifyRequired,
}

public interface IPlatformMfaService
{
    Task<MfaStatus> GetStatusAsync(CancellationToken cancellationToken = default);

    /// <summary>The decision for one signed-in request. Cheap when the organisation has not turned the policy on.</summary>
    Task<MfaGate> EvaluateGateAsync(TenantScope scope, string userId, CancellationToken cancellationToken = default);

    Task<MfaResult<TotpEnrolment>> BeginTotpAsync(string label, string accountName, CancellationToken cancellationToken = default);

    Task<MfaResult<bool>> ConfirmTotpAsync(Guid id, string code, CancellationToken cancellationToken = default);

    Task<MfaResult<bool>> RemoveCredentialAsync(string method, Guid id, CancellationToken cancellationToken = default);

    Task<MfaResult<IReadOnlyList<string>>> RotateBackupCodesAsync(CancellationToken cancellationToken = default);

    Task<MfaResult<MfaCodeSent>> SendCodeAsync(string channel, string purpose, string? destination, string? accountEmail, CancellationToken cancellationToken = default);

    Task<MfaResult<bool>> ConfirmCodeEnrolmentAsync(string channel, string code, CancellationToken cancellationToken = default);

    Task<MfaResult<bool>> DisableChannelAsync(string channel, CancellationToken cancellationToken = default);

    Task<MfaResult<bool>> VerifyAsync(string method, string code, CancellationToken cancellationToken = default);

    Task<MfaResult<bool>> SnoozeAsync(CancellationToken cancellationToken = default);

    Task SignOutAsync(CancellationToken cancellationToken = default);

    Task<MfaPolicy> GetPolicyAsync(CancellationToken cancellationToken = default);

    Task<MfaResult<MfaPolicy>> SetPolicyAsync(bool required, bool allowSnooze, int minMethods, IReadOnlyList<string> enabledMethods, CancellationToken cancellationToken = default);

    Task<MfaResult<bool>> ResetUserAsync(string userId, CancellationToken cancellationToken = default);

    Task<MfaMetrics> GetMetricsAsync(CancellationToken cancellationToken = default);

    // Passkeys: the browser ceremony runs in the infrastructure service; these keep the person's record.
    Task<UserSecurityProfile> GetProfileAsync(CancellationToken cancellationToken = default);

    Task<MfaResult<bool>> BeginPasskeyCeremonyAsync(string purpose, string optionsJson, CancellationToken cancellationToken = default);

    Task<string?> TakePendingPasskeyCeremonyAsync(string purpose, CancellationToken cancellationToken = default);

    Task<MfaResult<bool>> AddPasskeyAsync(PasskeyCredential credential, CancellationToken cancellationToken = default);

    Task<MfaResult<bool>> CompletePasskeyVerificationAsync(string credentialId, uint signCount, CancellationToken cancellationToken = default);
}

public sealed class PlatformMfaService(
    ITenantContextAccessor tenantContextAccessor,
    IPermissionService permissionService,
    IUserSecurityRepository profiles,
    IMfaPolicyRepository policies,
    IConnectionSecretProtector protector,
    ISmsProvider smsProvider,
    IEmailSendingProvider emailProvider,
    IAuditWriter auditWriter,
    PlatformMfaOptions options,
    TimeProvider clock) : IPlatformMfaService
{
    private static readonly ConcurrentDictionary<string, (MfaPolicy Policy, DateTimeOffset At)> PolicyCache = new();
    private const string BackupAlphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

    private TenantContext Context => tenantContextAccessor.Current;
    private DateTimeOffset Now => clock.GetUtcNow();

    // ---------------------------------------------------------------- status and the gate

    public async Task<MfaStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var context = Context;
        var policy = await ReadPolicyAsync(context.Scope, cancellationToken);
        var profile = await LoadAsync(context, cancellationToken);
        return BuildStatus(policy, profile);
    }

    public async Task<MfaGate> EvaluateGateAsync(TenantScope scope, string userId, CancellationToken cancellationToken = default)
    {
        var policy = await ReadPolicyAsync(scope, cancellationToken);
        if (!policy.Required) return MfaGate.Allowed;
        var profile = await profiles.GetAsync(scope, userId, cancellationToken) ?? UserSecurityProfile.Empty(scope, userId, Now);
        return Decide(policy, profile);
    }

    private MfaGate Decide(MfaPolicy policy, UserSecurityProfile profile)
    {
        if (!policy.Required) return MfaGate.Allowed;
        var enrolled = Offered(policy, profile.EnrolledMethods);
        if (enrolled.Count < Math.Max(1, policy.MinMethods))
        {
            return IsSnoozed(profile) ? MfaGate.Allowed : MfaGate.EnrolRequired;
        }

        return IsVerified(profile) ? MfaGate.Allowed : MfaGate.VerifyRequired;
    }

    private MfaStatus BuildStatus(MfaPolicy policy, UserSecurityProfile profile)
    {
        var enrolled = Offered(policy, profile.EnrolledMethods);
        var minimum = Math.Max(1, policy.MinMethods);
        var satisfied = enrolled.Count >= minimum;
        var snoozed = IsSnoozed(profile);
        var gate = Decide(policy, profile);
        var credentials = profile.ConfirmedAuthenticators
            .Select(a => new MfaCredentialView(MfaMethods.Totp, a.Id, a.Label, a.CreatedAt, null))
            .Concat(profile.Passkeys.Select(p => new MfaCredentialView(MfaMethods.Passkey, p.Id, p.Nickname, p.CreatedAt, p.LastUsedAt)))
            .ToArray();
        var canSnooze = policy.Required && policy.AllowSnooze && !satisfied && profile.SnoozeCount < options.MaxSnoozes;
        var locked = profile.LockedUntil is { } until && until > Now;
        return new MfaStatus(
            policy.Required, policy.AllowSnooze, minimum, policy.EnabledMethods, enrolled, profile.UnusedBackupCodes,
            satisfied, IsVerified(profile), profile.VerifiedUntil is { } v && v > Now ? v : null,
            snoozed, snoozed ? profile.SnoozeUntil : null, canSnooze,
            policy.Required && !satisfied && !snoozed, gate == MfaGate.VerifyRequired,
            locked, locked ? profile.LockedUntil : null, profile.SmsNumberMasked, credentials);
    }

    private static IReadOnlyList<string> Offered(MfaPolicy policy, IReadOnlyList<string> enrolled) =>
        enrolled.Where(policy.Offers).ToArray();

    private bool IsVerified(UserSecurityProfile profile) => profile.VerifiedUntil is { } until && until > Now;

    private bool IsSnoozed(UserSecurityProfile profile) => profile.SnoozeUntil is { } until && until > Now;

    // ---------------------------------------------------------------- authenticator app

    public async Task<MfaResult<TotpEnrolment>> BeginTotpAsync(string label, string accountName, CancellationToken cancellationToken = default)
    {
        var context = Context;
        var policy = await ReadPolicyAsync(context.Scope, cancellationToken);
        if (!policy.Offers(MfaMethods.Totp)) return MfaResult<TotpEnrolment>.Fail("not_offered", "Authenticator apps are switched off for this organisation.");
        var profile = await LoadAsync(context, cancellationToken);
        if (NeedsVerification(profile)) return MfaResult<TotpEnrolment>.Fail("needs_verification", "Verify it is you before adding another method.");

        var secret = Totp.NewSecret();
        var authenticator = new TotpAuthenticator
        {
            Id = Guid.NewGuid(),
            Label = string.IsNullOrWhiteSpace(label) ? "Authenticator app" : label.Trim()[..Math.Min(label.Trim().Length, 60)],
            ProtectedSecret = protector.Protect(secret),
            CreatedAt = Now,
        };
        // A set-up that was started and never confirmed is replaced, so abandoned attempts do not pile up.
        var kept = profile.Authenticators.Where(a => a.ConfirmedAt is not null).Append(authenticator).ToArray();
        await profiles.SaveAsync(profile with { Authenticators = kept, UpdatedAt = Now }, cancellationToken);
        return MfaResult<TotpEnrolment>.Success(new TotpEnrolment(authenticator.Id, secret, Totp.ProvisioningUri(options.Issuer, accountName, secret)));
    }

    public async Task<MfaResult<bool>> ConfirmTotpAsync(Guid id, string code, CancellationToken cancellationToken = default)
    {
        var context = Context;
        var profile = await LoadAsync(context, cancellationToken);
        if (IsLocked(profile)) return Locked();
        var authenticator = profile.Authenticators.FirstOrDefault(a => a.Id == id && a.ConfirmedAt is null);
        if (authenticator is null) return MfaResult<bool>.Fail("not_found", "That set-up was not found. Start again.");

        var step = TryVerifyTotp(authenticator, code);
        if (step is null) return await FailAsync(context, profile, "totp_enrol", cancellationToken);

        var hadAMethod = profile.EnrolledMethods.Count > 0;
        var updated = profile with
        {
            Authenticators = profile.Authenticators.Select(a => a.Id == id ? a with { ConfirmedAt = Now, LastUsedStep = step.Value } : a).ToArray(),
            UpdatedAt = Now,
        };
        // Proving the code works is also proving it is them, so the person is not asked again straight away.
        updated = ClearFailures(hadAMethod ? updated : MarkVerified(updated));
        await profiles.SaveAsync(updated, cancellationToken);
        await AuditAsync(context, "security.mfa.enrolled", "totp", cancellationToken);
        return MfaResult<bool>.Success(true);
    }

    // ---------------------------------------------------------------- removing, backup codes

    public async Task<MfaResult<bool>> RemoveCredentialAsync(string method, Guid id, CancellationToken cancellationToken = default)
    {
        var context = Context;
        var profile = await LoadAsync(context, cancellationToken);
        if (NeedsVerification(profile)) return MfaResult<bool>.Fail("needs_verification", "Verify it is you before removing a method.");

        UserSecurityProfile updated;
        if (method == MfaMethods.Totp && profile.Authenticators.Any(a => a.Id == id))
        {
            updated = profile with { Authenticators = profile.Authenticators.Where(a => a.Id != id).ToArray(), UpdatedAt = Now };
        }
        else if (method == MfaMethods.Passkey && profile.Passkeys.Any(p => p.Id == id))
        {
            updated = profile with { Passkeys = profile.Passkeys.Where(p => p.Id != id).ToArray(), UpdatedAt = Now };
        }
        else
        {
            return MfaResult<bool>.Fail("not_found", "That method was not found.");
        }

        await profiles.SaveAsync(updated, cancellationToken);
        await AuditAsync(context, "security.mfa.removed", method, cancellationToken);
        return MfaResult<bool>.Success(true);
    }

    public async Task<MfaResult<IReadOnlyList<string>>> RotateBackupCodesAsync(CancellationToken cancellationToken = default)
    {
        var context = Context;
        var policy = await ReadPolicyAsync(context.Scope, cancellationToken);
        if (!policy.Offers(MfaMethods.BackupCodes)) return MfaResult<IReadOnlyList<string>>.Fail("not_offered", "Backup codes are switched off for this organisation.");
        var profile = await LoadAsync(context, cancellationToken);
        // Backup codes stand in for the other methods, so they need one of them to exist first.
        if (profile.EnrolledMethods.All(m => m == MfaMethods.BackupCodes))
        {
            return MfaResult<IReadOnlyList<string>>.Fail("needs_method", "Set up an authenticator app, passkey, email or SMS first. Backup codes are for when you cannot use it.");
        }

        if (NeedsVerification(profile)) return MfaResult<IReadOnlyList<string>>.Fail("needs_verification", "Verify it is you before making new backup codes.");

        var codes = Enumerable.Range(0, options.BackupCodeCount).Select(_ => NewBackupCode()).ToArray();
        var stored = codes.Select(code => new BackupCode { Hash = HashBackupCode(context, code) }).ToArray();
        await profiles.SaveAsync(profile with { BackupCodes = stored, UpdatedAt = Now }, cancellationToken);
        await AuditAsync(context, "security.mfa.backup_codes_rotated", MfaMethods.BackupCodes, cancellationToken);
        return MfaResult<IReadOnlyList<string>>.Success(codes);
    }

    // ---------------------------------------------------------------- email and SMS codes

    public async Task<MfaResult<MfaCodeSent>> SendCodeAsync(string channel, string purpose, string? destination, string? accountEmail, CancellationToken cancellationToken = default)
    {
        var context = Context;
        if (channel is not ("email" or "sms")) return MfaResult<MfaCodeSent>.Fail("not_found", "Unknown channel.");
        if (purpose is not ("enroll" or "login")) return MfaResult<MfaCodeSent>.Fail("not_found", "Unknown purpose.");
        var method = channel == "email" ? MfaMethods.EmailOtp : MfaMethods.SmsOtp;
        var policy = await ReadPolicyAsync(context.Scope, cancellationToken);
        if (!policy.Offers(method)) return MfaResult<MfaCodeSent>.Fail("not_offered", $"{(channel == "email" ? "Email" : "SMS")} codes are switched off for this organisation.");

        var profile = await LoadAsync(context, cancellationToken);
        if (IsLocked(profile)) return MfaResult<MfaCodeSent>.Fail("locked", LockedMessage(profile));
        if (purpose == "enroll" && NeedsVerification(profile)) return MfaResult<MfaCodeSent>.Fail("needs_verification", "Verify it is you before adding another method.");
        if (profile.PendingChallenge is { } pending && Now - pending.CreatedAt < options.ResendCooldown)
        {
            return MfaResult<MfaCodeSent>.Fail("cooldown", "A code was just sent. Wait a minute before asking for another.");
        }

        string recipient;
        string? protectedDestination = null;
        if (channel == "email")
        {
            if (string.IsNullOrWhiteSpace(accountEmail)) return MfaResult<MfaCodeSent>.Fail("no_destination", "Your sign-in has no email address, so email codes cannot be sent.");
            recipient = accountEmail.Trim();
        }
        else if (purpose == "enroll")
        {
            var number = NormalisePhone(destination);
            if (number is null) return MfaResult<MfaCodeSent>.Fail("bad_number", "Enter the number with its country code, for example +256700000000.");
            recipient = number;
            protectedDestination = protector.Protect(number);
        }
        else
        {
            if (!profile.SmsEnrolled || profile.ProtectedSmsNumber is null) return MfaResult<MfaCodeSent>.Fail("not_enrolled", "SMS codes are not set up for you.");
            var saved = TryUnprotect(profile.ProtectedSmsNumber);
            if (saved is null) return MfaResult<MfaCodeSent>.Fail("not_enrolled", "Your saved number cannot be read. Ask an administrator to reset your methods.");
            recipient = saved;
        }

        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        var message = $"{options.Issuer}: your verification code is {code}. It expires in {(int)options.CodeLifetime.TotalMinutes} minutes. Do not share it.";
        var request = new CommunicationSendRequest(
            context.Scope, null, channel == "email" ? CommunicationChannel.Email : CommunicationChannel.SMS, recipient, message,
            context.UserId, Approved: true, LowRisk: true, CorrelationId: Guid.NewGuid().ToString("N"));
        var result = channel == "email" ? await emailProvider.SendEmailAsync(request, cancellationToken) : await smsProvider.SendAsync(request, cancellationToken);
        if (string.Equals(result.DeliveryStatus, "ConfigurationRequired", StringComparison.OrdinalIgnoreCase))
        {
            return MfaResult<MfaCodeSent>.Fail("not_configured", $"{(channel == "email" ? "Email" : "SMS")} is not connected for this organisation yet.");
        }

        if (!string.IsNullOrEmpty(result.SafeError) && !IsAccepted(result.DeliveryStatus))
        {
            return MfaResult<MfaCodeSent>.Fail("send_failed", "The code could not be sent. Try again in a moment.");
        }

        var challenge = new MfaCodeChallenge
        {
            Channel = channel,
            Purpose = purpose,
            CodeHash = HashCode(context, code),
            DestinationMasked = Mask(recipient, channel),
            ProtectedDestination = protectedDestination,
            CreatedAt = Now,
            ExpiresAt = Now + options.CodeLifetime,
            MaxAttempts = options.MaxCodeAttempts,
        };
        await profiles.SaveAsync(profile with { PendingChallenge = challenge, UpdatedAt = Now }, cancellationToken);
        return MfaResult<MfaCodeSent>.Success(new MfaCodeSent(channel, challenge.DestinationMasked, challenge.ExpiresAt));
    }

    private static bool IsAccepted(string status) =>
        status.Contains("sent", StringComparison.OrdinalIgnoreCase) || status.Contains("accepted", StringComparison.OrdinalIgnoreCase)
        || status.Contains("queued", StringComparison.OrdinalIgnoreCase) || status.Contains("delivered", StringComparison.OrdinalIgnoreCase);

    public async Task<MfaResult<bool>> ConfirmCodeEnrolmentAsync(string channel, string code, CancellationToken cancellationToken = default)
    {
        var context = Context;
        var profile = await LoadAsync(context, cancellationToken);
        if (IsLocked(profile)) return Locked();
        var check = await CheckChallengeAsync(context, profile, channel, "enroll", code, cancellationToken);
        if (!check.Ok) return MfaResult<bool>.Fail(check.Code!, check.Message!);

        var challenge = profile.PendingChallenge!;
        var hadAMethod = profile.EnrolledMethods.Count > 0;
        var updated = channel == "email"
            ? profile with { EmailEnrolled = true, PendingChallenge = null, UpdatedAt = Now }
            : profile with { SmsEnrolled = true, ProtectedSmsNumber = challenge.ProtectedDestination, SmsNumberMasked = challenge.DestinationMasked, PendingChallenge = null, UpdatedAt = Now };
        updated = ClearFailures(hadAMethod ? updated : MarkVerified(updated));
        await profiles.SaveAsync(updated, cancellationToken);
        await AuditAsync(context, "security.mfa.enrolled", channel == "email" ? MfaMethods.EmailOtp : MfaMethods.SmsOtp, cancellationToken);
        return MfaResult<bool>.Success(true);
    }

    public async Task<MfaResult<bool>> DisableChannelAsync(string channel, CancellationToken cancellationToken = default)
    {
        var context = Context;
        var profile = await LoadAsync(context, cancellationToken);
        if (NeedsVerification(profile)) return MfaResult<bool>.Fail("needs_verification", "Verify it is you before removing a method.");
        UserSecurityProfile updated;
        if (channel == "email" && profile.EmailEnrolled) updated = profile with { EmailEnrolled = false, UpdatedAt = Now };
        else if (channel == "sms" && profile.SmsEnrolled) updated = profile with { SmsEnrolled = false, ProtectedSmsNumber = null, SmsNumberMasked = null, UpdatedAt = Now };
        else return MfaResult<bool>.Fail("not_found", "That method was not set up.");
        await profiles.SaveAsync(updated, cancellationToken);
        await AuditAsync(context, "security.mfa.removed", channel == "email" ? MfaMethods.EmailOtp : MfaMethods.SmsOtp, cancellationToken);
        return MfaResult<bool>.Success(true);
    }

    private async Task<MfaResult<bool>> CheckChallengeAsync(
        TenantContext context, UserSecurityProfile profile, string channel, string purpose, string code, CancellationToken cancellationToken)
    {
        var challenge = profile.PendingChallenge;
        if (challenge is null || challenge.Channel != channel || challenge.Purpose != purpose)
        {
            return MfaResult<bool>.Fail("no_code", "Ask for a code first.");
        }

        if (challenge.ExpiresAt <= Now)
        {
            await profiles.SaveAsync(profile with { PendingChallenge = null, UpdatedAt = Now }, cancellationToken);
            return MfaResult<bool>.Fail("expired", "That code has expired. Ask for a new one.");
        }

        var attempts = challenge.Attempts + 1;
        var matches = CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(HashCode(context, new string((code ?? string.Empty).Where(char.IsDigit).ToArray()))),
            Encoding.ASCII.GetBytes(challenge.CodeHash));
        if (matches) return MfaResult<bool>.Success(true);

        if (attempts >= challenge.MaxAttempts)
        {
            await profiles.SaveAsync(profile with { PendingChallenge = null, UpdatedAt = Now }, cancellationToken);
            await RegisterFailureAsync(context, profile with { PendingChallenge = null }, channel, cancellationToken);
            return MfaResult<bool>.Fail("expired", "Too many wrong codes. Ask for a new one.");
        }

        await profiles.SaveAsync(profile with { PendingChallenge = challenge with { Attempts = attempts }, UpdatedAt = Now }, cancellationToken);
        await RegisterFailureAsync(context, profile with { PendingChallenge = challenge with { Attempts = attempts } }, channel, cancellationToken);
        return MfaResult<bool>.Fail("invalid_code", "That code is not right.");
    }

    // ---------------------------------------------------------------- verifying

    public async Task<MfaResult<bool>> VerifyAsync(string method, string code, CancellationToken cancellationToken = default)
    {
        var context = Context;
        var policy = await ReadPolicyAsync(context.Scope, cancellationToken);
        if (!MfaMethods.IsKnown(method) || method == MfaMethods.Passkey) return MfaResult<bool>.Fail("not_found", "Unknown method.");
        if (!policy.Offers(method)) return MfaResult<bool>.Fail("not_offered", "That method is switched off for this organisation.");
        var profile = await LoadAsync(context, cancellationToken);
        if (IsLocked(profile)) return Locked();

        switch (method)
        {
            case MfaMethods.Totp:
            {
                foreach (var authenticator in profile.ConfirmedAuthenticators)
                {
                    var step = TryVerifyTotp(authenticator, code);
                    if (step is null) continue;
                    var updated = MarkVerified(ClearFailures(profile with
                    {
                        Authenticators = profile.Authenticators.Select(a => a.Id == authenticator.Id ? a with { LastUsedStep = step.Value } : a).ToArray(),
                    }));
                    await profiles.SaveAsync(updated, cancellationToken);
                    await AuditAsync(context, "security.mfa.verified", method, cancellationToken);
                    return MfaResult<bool>.Success(true);
                }

                return await FailAsync(context, profile, method, cancellationToken);
            }
            case MfaMethods.BackupCodes:
            {
                var hash = HashBackupCode(context, code);
                var match = profile.BackupCodes.FirstOrDefault(b => b.UsedAt is null && CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(b.Hash), Encoding.ASCII.GetBytes(hash)));
                if (match is null) return await FailAsync(context, profile, method, cancellationToken);
                var updated = MarkVerified(ClearFailures(profile with
                {
                    BackupCodes = profile.BackupCodes.Select(b => ReferenceEquals(b, match) ? b with { UsedAt = Now } : b).ToArray(),
                }));
                await profiles.SaveAsync(updated, cancellationToken);
                await AuditAsync(context, "security.mfa.verified", method, cancellationToken);
                return MfaResult<bool>.Success(true);
            }
            default:
            {
                var channel = method == MfaMethods.EmailOtp ? "email" : "sms";
                if (channel == "email" ? !profile.EmailEnrolled : !profile.SmsEnrolled) return MfaResult<bool>.Fail("not_enrolled", "That method is not set up for you.");
                var check = await CheckChallengeAsync(context, profile, channel, "login", code, cancellationToken);
                if (!check.Ok) return MfaResult<bool>.Fail(check.Code!, check.Message!);
                await profiles.SaveAsync(MarkVerified(ClearFailures(profile with { PendingChallenge = null })), cancellationToken);
                await AuditAsync(context, "security.mfa.verified", method, cancellationToken);
                return MfaResult<bool>.Success(true);
            }
        }
    }

    public async Task SignOutAsync(CancellationToken cancellationToken = default)
    {
        var context = Context;
        var profile = await profiles.GetAsync(context.Scope, context.UserId, cancellationToken);
        if (profile?.VerifiedUntil is null) return;
        await profiles.SaveAsync(profile with { VerifiedUntil = null, UpdatedAt = Now }, cancellationToken);
    }

    public async Task<MfaResult<bool>> SnoozeAsync(CancellationToken cancellationToken = default)
    {
        var context = Context;
        var policy = await ReadPolicyAsync(context.Scope, cancellationToken);
        var profile = await LoadAsync(context, cancellationToken);
        if (!policy.Required || !policy.AllowSnooze) return MfaResult<bool>.Fail("not_allowed", "Your organisation does not allow postponing this.");
        if (Offered(policy, profile.EnrolledMethods).Count >= Math.Max(1, policy.MinMethods)) return MfaResult<bool>.Fail("not_needed", "You have already set up verification.");
        if (profile.SnoozeCount >= options.MaxSnoozes) return MfaResult<bool>.Fail("not_allowed", "You have postponed this as many times as allowed. Set up a method now.");
        await profiles.SaveAsync(profile with { SnoozeUntil = Now + options.SnoozeFor, SnoozeCount = profile.SnoozeCount + 1, UpdatedAt = Now }, cancellationToken);
        await AuditAsync(context, "security.mfa.snoozed", "setup", cancellationToken);
        return MfaResult<bool>.Success(true);
    }

    // ---------------------------------------------------------------- policy and administration

    public async Task<MfaPolicy> GetPolicyAsync(CancellationToken cancellationToken = default) =>
        await ReadPolicyAsync(Context.Scope, cancellationToken);

    public async Task<MfaResult<MfaPolicy>> SetPolicyAsync(
        bool required, bool allowSnooze, int minMethods, IReadOnlyList<string> enabledMethods, CancellationToken cancellationToken = default)
    {
        await permissionService.RequirePermissionAsync(Permission.ManageTenantSettings, cancellationToken);
        var context = Context;
        var enabled = enabledMethods.Where(MfaMethods.IsKnown).Distinct().ToArray();
        if (minMethods is < 1 or > 3) return MfaResult<MfaPolicy>.Fail("invalid", "Choose between 1 and 3 methods.");
        if (enabled.Length < minMethods) return MfaResult<MfaPolicy>.Fail("invalid", "Switch on at least as many methods as people must set up.");

        var candidate = new MfaPolicy
        {
            Scope = context.Scope, Required = required, AllowSnooze = allowSnooze, MinMethods = minMethods, EnabledMethods = enabled,
        };
        if (required)
        {
            // Whoever switches this on must already satisfy it, or they lock themselves out of their own organisation.
            var self = await LoadAsync(context, cancellationToken);
            if (Offered(candidate, self.EnrolledMethods).Count < minMethods)
            {
                return MfaResult<MfaPolicy>.Fail("enrol_first", "Set up your own verification method first, then require it for everyone.");
            }
        }

        var saved = candidate with { UpdatedAt = Now, UpdatedBy = context.UserId };
        await policies.SaveAsync(saved, cancellationToken);
        PolicyCache.TryRemove(CacheKey(context.Scope), out _);
        await AuditAsync(context, "security.mfa.policy_changed", $"required={required}; min={minMethods}; methods={string.Join(",", enabled)}", cancellationToken);
        return MfaResult<MfaPolicy>.Success(saved);
    }

    public async Task<MfaResult<bool>> ResetUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        await permissionService.RequirePermissionAsync(Permission.ManageTenantSettings, cancellationToken);
        var context = Context;
        if (string.Equals(userId, context.UserId, StringComparison.OrdinalIgnoreCase))
        {
            return MfaResult<bool>.Fail("not_allowed", "Ask another administrator to reset your methods.");
        }

        var profile = await profiles.GetAsync(context.Scope, userId, cancellationToken);
        if (profile is null) return MfaResult<bool>.Fail("not_found", "That person has not set up any method.");
        await profiles.SaveAsync(UserSecurityProfile.Empty(context.Scope, profile.UserId, Now), cancellationToken);
        await AuditAsync(context, "security.mfa.user_reset", $"target={profile.UserId}", cancellationToken);
        return MfaResult<bool>.Success(true);
    }

    public async Task<MfaMetrics> GetMetricsAsync(CancellationToken cancellationToken = default)
    {
        await permissionService.RequirePermissionAsync(Permission.ManageTenantSettings, cancellationToken);
        var all = await profiles.ListAsync(Context.Scope, cancellationToken);
        return new MfaMetrics(
            all.Count(p => p.EnrolledMethods.Count > 0),
            all.Count(p => p.EnrolledMethods.Contains(MfaMethods.Totp)),
            all.Count(p => p.EnrolledMethods.Contains(MfaMethods.Passkey)),
            all.Count(p => p.EmailEnrolled),
            all.Count(p => p.SmsEnrolled));
    }

    // ---------------------------------------------------------------- passkeys (the ceremony itself is in infrastructure)

    public Task<UserSecurityProfile> GetProfileAsync(CancellationToken cancellationToken = default) => LoadAsync(Context, cancellationToken);

    public async Task<MfaResult<bool>> BeginPasskeyCeremonyAsync(string purpose, string optionsJson, CancellationToken cancellationToken = default)
    {
        var context = Context;
        var policy = await ReadPolicyAsync(context.Scope, cancellationToken);
        if (!policy.Offers(MfaMethods.Passkey)) return MfaResult<bool>.Fail("not_offered", "Passkeys are switched off for this organisation.");
        var profile = await LoadAsync(context, cancellationToken);
        if (IsLocked(profile)) return Locked();
        if (purpose == "register" && NeedsVerification(profile)) return MfaResult<bool>.Fail("needs_verification", "Verify it is you before adding another method.");
        await profiles.SaveAsync(profile with { PendingPasskeyOptionsJson = optionsJson, PendingPasskeyPurpose = purpose, PendingPasskeyExpiresAt = Now.AddMinutes(5), UpdatedAt = Now }, cancellationToken);
        return MfaResult<bool>.Success(true);
    }

    public async Task<string?> TakePendingPasskeyCeremonyAsync(string purpose, CancellationToken cancellationToken = default)
    {
        var context = Context;
        var profile = await LoadAsync(context, cancellationToken);
        var json = profile.PendingPasskeyPurpose == purpose && profile.PendingPasskeyExpiresAt > Now ? profile.PendingPasskeyOptionsJson : null;
        if (profile.PendingPasskeyOptionsJson is not null)
        {
            await profiles.SaveAsync(profile with { PendingPasskeyOptionsJson = null, PendingPasskeyPurpose = null, PendingPasskeyExpiresAt = null, UpdatedAt = Now }, cancellationToken);
        }

        return json;
    }

    public async Task<MfaResult<bool>> AddPasskeyAsync(PasskeyCredential credential, CancellationToken cancellationToken = default)
    {
        var context = Context;
        var profile = await LoadAsync(context, cancellationToken);
        var hadAMethod = profile.EnrolledMethods.Count > 0;
        var updated = profile with { Passkeys = profile.Passkeys.Append(credential).ToArray(), UpdatedAt = Now };
        await profiles.SaveAsync(hadAMethod ? updated : MarkVerified(updated), cancellationToken);
        await AuditAsync(context, "security.mfa.enrolled", MfaMethods.Passkey, cancellationToken);
        return MfaResult<bool>.Success(true);
    }

    public async Task<MfaResult<bool>> CompletePasskeyVerificationAsync(string credentialId, uint signCount, CancellationToken cancellationToken = default)
    {
        var context = Context;
        var profile = await LoadAsync(context, cancellationToken);
        if (profile.Passkeys.All(p => p.CredentialId != credentialId)) return MfaResult<bool>.Fail("not_found", "That passkey is not registered for you.");
        var updated = MarkVerified(ClearFailures(profile with
        {
            Passkeys = profile.Passkeys.Select(p => p.CredentialId == credentialId ? p with { SignCount = signCount, LastUsedAt = Now } : p).ToArray(),
        }));
        await profiles.SaveAsync(updated, cancellationToken);
        await AuditAsync(context, "security.mfa.verified", MfaMethods.Passkey, cancellationToken);
        return MfaResult<bool>.Success(true);
    }

    // ---------------------------------------------------------------- helpers

    /// <summary>A saved secret that can no longer be read (the encryption key was lost or replaced) is treated as
    /// "does not match", never as a crash. The person falls back to a backup code, or an administrator resets them.</summary>
    private string? TryUnprotect(string protectedValue)
    {
        try
        {
            return protector.Unprotect(protectedValue);
        }
        catch (Exception exception) when (exception is CryptographicException or FormatException or InvalidOperationException)
        {
            return null;
        }
    }

    private long? TryVerifyTotp(TotpAuthenticator authenticator, string code) =>
        TryUnprotect(authenticator.ProtectedSecret) is { } secret ? Totp.Verify(secret, code, Now, authenticator.LastUsedStep) : null;

    private async Task<UserSecurityProfile> LoadAsync(TenantContext context, CancellationToken cancellationToken) =>
        await profiles.GetAsync(context.Scope, context.UserId, cancellationToken) ?? UserSecurityProfile.Empty(context.Scope, context.UserId, Now);

    private async Task<MfaPolicy> ReadPolicyAsync(TenantScope scope, CancellationToken cancellationToken)
    {
        var key = CacheKey(scope);
        if (PolicyCache.TryGetValue(key, out var cached) && Now - cached.At < options.PolicyCacheFor) return cached.Policy;
        var policy = await policies.GetAsync(scope, cancellationToken) ?? MfaPolicy.Default(scope);
        PolicyCache[key] = (policy, Now);
        return policy;
    }

    private static string CacheKey(TenantScope scope) => $"{scope.TenantId}|{scope.WorkspaceId}";

    /// <summary>Changing methods needs a person who already has one to have proved it recently.</summary>
    private bool NeedsVerification(UserSecurityProfile profile) => profile.EnrolledMethods.Count > 0 && !IsVerified(profile);

    private UserSecurityProfile MarkVerified(UserSecurityProfile profile) =>
        profile with { VerifiedUntil = Now + options.VerifiedFor, LastVerifiedAt = Now, UpdatedAt = Now };

    private static UserSecurityProfile ClearFailures(UserSecurityProfile profile) =>
        profile with { FailedAttempts = 0, FirstFailureAt = null, LockedUntil = null };

    private bool IsLocked(UserSecurityProfile profile) => profile.LockedUntil is { } until && until > Now;

    private MfaResult<bool> Locked() => MfaResult<bool>.Fail("locked", "Too many wrong codes. Try again in a few minutes.");

    private string LockedMessage(UserSecurityProfile profile) =>
        profile.LockedUntil is { } until ? $"Too many wrong codes. Try again after {until.ToUniversalTime():HH:mm} UTC." : "Too many wrong codes. Try again in a few minutes.";

    private async Task<MfaResult<bool>> FailAsync(TenantContext context, UserSecurityProfile profile, string method, CancellationToken cancellationToken)
    {
        await RegisterFailureAsync(context, profile, method, cancellationToken);
        return MfaResult<bool>.Fail("invalid_code", "That code is not right.");
    }

    private async Task RegisterFailureAsync(TenantContext context, UserSecurityProfile profile, string method, CancellationToken cancellationToken)
    {
        var fresh = profile.FirstFailureAt is { } first && Now - first <= options.FailureWindow;
        var failures = (fresh ? profile.FailedAttempts : 0) + 1;
        var locked = failures >= options.MaxFailures;
        await profiles.SaveAsync(profile with
        {
            FailedAttempts = locked ? 0 : failures,
            FirstFailureAt = locked ? null : fresh ? profile.FirstFailureAt : Now,
            LockedUntil = locked ? Now + options.LockTime : profile.LockedUntil,
            UpdatedAt = Now,
        }, cancellationToken);
        await AuditAsync(context, locked ? "security.mfa.locked" : "security.mfa.failed", method, cancellationToken);
    }

    private async Task AuditAsync(TenantContext context, string action, string detail, CancellationToken cancellationToken) =>
        await auditWriter.WriteAsync(
            new AuditEvent(
                Guid.NewGuid(), context.Scope, CaseId: null, DocumentId: null, UserId: context.UserId, ActorType.User, action,
                Now, BeforeStatus: null, AfterStatus: null, EvidenceIds: [], ModelProvider: "Security", Confidence: null,
                ValidationResult: "recorded", ApprovalId: null, AuditReason: detail, CorrelationId: Guid.NewGuid().ToString("N")),
            cancellationToken);

    private static string HashCode(TenantContext context, string code) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{context.TenantId}:{context.WorkspaceId}:{context.UserId.ToLowerInvariant()}:code:{code}")));

    private static string HashBackupCode(TenantContext context, string code)
    {
        var normalised = new string((code ?? string.Empty).Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{context.TenantId}:{context.WorkspaceId}:{context.UserId.ToLowerInvariant()}:backup:{normalised}")));
    }

    private static string NewBackupCode()
    {
        var chars = Enumerable.Range(0, 10).Select(_ => BackupAlphabet[RandomNumberGenerator.GetInt32(BackupAlphabet.Length)]).ToArray();
        return $"{new string(chars, 0, 5)}-{new string(chars, 5, 5)}";
    }

    /// <summary>A number with its country code, digits only after the plus. Null when it does not look like one.</summary>
    public static string? NormalisePhone(string? input)
    {
        var digits = new string((input ?? string.Empty).Where(char.IsDigit).ToArray());
        if (digits.Length is < 9 or > 15) return null;
        return "+" + digits;
    }

    private static string Mask(string recipient, string channel)
    {
        if (channel == "sms") return recipient.Length > 6 ? $"{recipient[..4]}{new string('*', recipient.Length - 6)}{recipient[^2..]}" : "****";
        var at = recipient.IndexOf('@');
        if (at <= 1) return "****" + (at >= 0 ? recipient[at..] : string.Empty);
        return $"{recipient[0]}{new string('*', Math.Min(at - 1, 6))}{recipient[at..]}";
    }
}
