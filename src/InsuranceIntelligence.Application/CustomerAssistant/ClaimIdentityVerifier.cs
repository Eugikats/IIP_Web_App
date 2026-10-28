using System.Security.Cryptography;
using System.Text.Json;
using InsuranceIntelligence.Application.Workspaces;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Communications;
using InsuranceIntelligence.Domain.Compliance;
using InsuranceIntelligence.Domain.Workflow;

namespace InsuranceIntelligence.Application.CustomerAssistant;

public enum ClaimIdentityResult { Verified, CodeSent, InvalidCode, Expired, Locked, Cooldown, NotConfigured, SendFailed }

public interface IClaimIdentityVerifier
{
    Task<bool> IsVerifiedAsync(TenantScope scope, string phone, CancellationToken cancellationToken = default);
    Task<ClaimIdentityResult> SendAsync(TenantScope scope, string phone, string correlationId, CancellationToken cancellationToken = default);
    Task<ClaimIdentityResult> VerifyAsync(TenantScope scope, string phone, string code, string correlationId, CancellationToken cancellationToken = default);
}

/// <summary>SMS possession challenge for WhatsApp claim access. Only salted hashes are stored.</summary>
public sealed class ClaimIdentityVerifier(
    IWorkspaceRecordStore records,
    ISmsProvider sms,
    IAuditRepository audit,
    TimeProvider clock) : IClaimIdentityVerifier
{
    private const string Kind = "whatsapp-claim-identity";
    private sealed record Challenge(string Hash, string Salt, DateTimeOffset SentAt, DateTimeOffset ExpiresAt,
        int Attempts, DateTimeOffset? VerifiedUntil);

    public async Task<bool> IsVerifiedAsync(TenantScope scope, string phone, CancellationToken cancellationToken = default)
    {
        var challenge = await ReadAsync(scope, phone, cancellationToken);
        return challenge?.VerifiedUntil > clock.GetUtcNow();
    }

    public async Task<ClaimIdentityResult> SendAsync(TenantScope scope, string phone, string correlationId, CancellationToken cancellationToken = default)
    {
        var now = clock.GetUtcNow();
        var existing = await ReadAsync(scope, phone, cancellationToken);
        if (existing is { Attempts: >= 5 } && existing.ExpiresAt > now)
            return ClaimIdentityResult.Locked;
        if (existing is not null && now - existing.SentAt < TimeSpan.FromMinutes(1))
            return ClaimIdentityResult.Cooldown;

        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        var salt = RandomNumberGenerator.GetBytes(16);
        var challenge = new Challenge(Hash(code, salt), Convert.ToBase64String(salt), now, now.AddMinutes(10), 0, null);
        var result = await sms.SendAsync(new CommunicationSendRequest(scope, null, CommunicationChannel.SMS,
            $"+{phone.TrimStart('+')}", $"Your claim verification code is {code}. It expires in 10 minutes.",
            CustomerAssistantService.SystemActorId, true, true, correlationId), cancellationToken);
        if (!string.Equals(result.DeliveryStatus, "Sent", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(result.DeliveryStatus, "Success", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(result.DeliveryStatus, "Queued", StringComparison.OrdinalIgnoreCase))
        {
            // Only a genuinely unconfigured SMS service is reported as "not configured"; a declined or failed send is not.
            var notConfigured = string.Equals(result.DeliveryStatus, "ConfigurationRequired", StringComparison.OrdinalIgnoreCase);
            await WriteAuditAsync(scope, $"claim.identity_code_unavailable.{result.DeliveryStatus}", correlationId, cancellationToken);
            return notConfigured ? ClaimIdentityResult.NotConfigured : ClaimIdentityResult.SendFailed;
        }

        await records.SaveAsync(scope, Kind, phone, JsonSerializer.Serialize(challenge), cancellationToken);
        await WriteAuditAsync(scope, "claim.identity_code_sent", correlationId, cancellationToken);
        return ClaimIdentityResult.CodeSent;
    }

    public async Task<ClaimIdentityResult> VerifyAsync(TenantScope scope, string phone, string code, string correlationId, CancellationToken cancellationToken = default)
    {
        for (var retry = 0; retry < 3; retry++)
        {
            var now = clock.GetUtcNow();
            var record = await records.GetAsync(scope, Kind, phone, cancellationToken);
            if (record is null) return ClaimIdentityResult.Expired;
            var challenge = JsonSerializer.Deserialize<Challenge>(record.PayloadJson);
            if (challenge is null || challenge.ExpiresAt <= now) return ClaimIdentityResult.Expired;
            if (challenge.Attempts >= 5) return ClaimIdentityResult.Locked;
            if (challenge.VerifiedUntil > now) return ClaimIdentityResult.Verified;
            var correct = code.Length == 6 && code.All(char.IsAsciiDigit)
                && CryptographicOperations.FixedTimeEquals(
                    Convert.FromHexString(challenge.Hash), Convert.FromHexString(Hash(code, Convert.FromBase64String(challenge.Salt))));
            var updated = correct
                ? challenge with { Hash = "", Salt = "", VerifiedUntil = now.AddMinutes(15) }
                : challenge with { Attempts = challenge.Attempts + 1 };
            if (!await records.TryReplaceAsync(scope, Kind, phone, record.PayloadJson,
                    JsonSerializer.Serialize(updated), cancellationToken)) continue;
            await WriteAuditAsync(scope, correct ? "claim.identity_verified" : "claim.identity_code_rejected", correlationId, cancellationToken);
            return correct ? ClaimIdentityResult.Verified
                : updated.Attempts >= 5 ? ClaimIdentityResult.Locked : ClaimIdentityResult.InvalidCode;
        }
        return ClaimIdentityResult.Cooldown;
    }

    private async Task<Challenge?> ReadAsync(TenantScope scope, string phone, CancellationToken cancellationToken)
    {
        var record = await records.GetAsync(scope, Kind, phone, cancellationToken);
        return record is null ? null : JsonSerializer.Deserialize<Challenge>(record.PayloadJson);
    }

    private static string Hash(string code, byte[] salt) =>
        Convert.ToHexString(Rfc2898DeriveBytes.Pbkdf2(code, salt, 120_000, HashAlgorithmName.SHA256, 32));

    private Task WriteAuditAsync(TenantScope scope, string action, string correlationId, CancellationToken cancellationToken) =>
        audit.WriteAsync(new AuditEvent(Guid.NewGuid(), scope, null, null, CustomerAssistantService.SystemActorId,
            ActorType.Integration, action, clock.GetUtcNow(), null, null, [], "CustomerAssistant", null,
            "recorded", null, "Claim identity challenge metadata; code and phone omitted.", correlationId), cancellationToken);
}

/// <summary>
/// Used only when a Demo-profile deployment is explicitly set to skip the SMS check for a presentation. Every number
/// counts as verified. Registered nowhere else, and never in Staging or Production.
/// </summary>
public sealed class DemoSkippedClaimIdentityVerifier : IClaimIdentityVerifier
{
    public Task<bool> IsVerifiedAsync(TenantScope scope, string phone, CancellationToken cancellationToken = default) => Task.FromResult(true);

    public Task<ClaimIdentityResult> SendAsync(TenantScope scope, string phone, string correlationId, CancellationToken cancellationToken = default) =>
        Task.FromResult(ClaimIdentityResult.Verified);

    public Task<ClaimIdentityResult> VerifyAsync(TenantScope scope, string phone, string code, string correlationId, CancellationToken cancellationToken = default) =>
        Task.FromResult(ClaimIdentityResult.Verified);
}
