using System.Security.Cryptography;
using System.Text;
using InsuranceIntelligence.Application.Onboarding;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Organisations;

namespace InsuranceIntelligence.Application.Organisations;

public sealed record ApiKeyCreated(Guid KeyId, string Name, string Prefix, string Key, DateTimeOffset? ExpiresAt);

public sealed record ApiKeyView(Guid KeyId, string Name, string Prefix, DateTimeOffset CreatedAt, DateTimeOffset? ExpiresAt, DateTimeOffset? LastUsedAt, DateTimeOffset? RevokedAt);

/// <summary>
/// Keys for an insurer's own systems (ERP, core insurance system, reporting). A key belongs to one
/// organisation, acts only in it, with the narrow Integration role, and can be revoked at once.
/// Whoever holds it can do what the role allows: treat it like a password.
/// </summary>
public sealed class ApiKeyService(
    IApiKeyRepository keys,
    IOrganisationRepository organisations,
    ITenantMembershipWriter membershipWriter,
    IAuditRepository auditRepository)
{
    public const int MaxActiveKeysPerOrganisation = 10;
    public static readonly TimeSpan LastUsedGranularity = TimeSpan.FromMinutes(5);

    public async Task<ApiKeyCreated> CreateAsync(
        string organisationId, string actorUserId, string name, int? expiresInDays, string correlationId, CancellationToken cancellationToken = default)
    {
        var organisation = await organisations.GetAsync(organisationId, cancellationToken);
        if (organisation is not { IsActive: true })
        {
            throw OrganisationException.NotFound("No such organisation.");
        }

        var cleanName = string.Join(' ', (name ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries)).Trim();
        if (cleanName.Length is < 2 or > 80)
        {
            throw OrganisationException.Invalid("Give the key a name of 2 to 80 characters, for example the system that will use it.");
        }

        if (expiresInDays is < 1 or > 730)
        {
            throw OrganisationException.Invalid("A key can last from 1 to 730 days.");
        }

        var now = DateTimeOffset.UtcNow;
        var existing = await keys.ListAsync(organisationId, cancellationToken);
        if (existing.Count(k => k.IsUsable(now)) >= MaxActiveKeysPerOrganisation)
        {
            throw OrganisationException.Conflict($"An organisation can have {MaxActiveKeysPerOrganisation} active keys. Revoke one first.");
        }

        var keyId = Guid.NewGuid();
        var raw = ApiKeyRecord.KeyPrefix + Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).Replace('+', '-').Replace('/', '_').TrimEnd('=');
        var record = new ApiKeyRecord(
            keyId, organisationId, cleanName, raw[..8], Hash(raw), $"{ApiKeyRecord.UserPrefix}{keyId:N}", actorUserId, now,
            expiresInDays is { } days ? now.AddDays(days) : null);
        await keys.AddAsync(record, cancellationToken);
        await membershipWriter.GrantMembershipAsync(
            organisationId, organisation.DefaultWorkspaceId, record.UserId, nameof(PlatformRole.Integration), cancellationToken);
        await AuditAsync(organisation.Scope, actorUserId, "organisation.api_key_created", record, $"API key '{cleanName}' created.", correlationId, cancellationToken);
        return new ApiKeyCreated(keyId, cleanName, record.Prefix, raw, record.ExpiresAt);
    }

    public async Task<IReadOnlyList<ApiKeyView>> ListAsync(string organisationId, CancellationToken cancellationToken = default) =>
        (await keys.ListAsync(organisationId, cancellationToken))
            .OrderByDescending(k => k.CreatedAt)
            .Select(k => new ApiKeyView(k.KeyId, k.Name, k.Prefix, k.CreatedAt, k.ExpiresAt, k.LastUsedAt, k.RevokedAt))
            .ToArray();

    public async Task RevokeAsync(string organisationId, string actorUserId, Guid keyId, string correlationId, CancellationToken cancellationToken = default)
    {
        var key = await keys.GetAsync(organisationId, keyId, cancellationToken)
            ?? throw OrganisationException.NotFound("No such key.");
        if (key.RevokedAt is not null)
        {
            return;
        }

        var revoked = key with { RevokedAt = DateTimeOffset.UtcNow };
        await keys.UpdateAsync(revoked, cancellationToken);
        var organisation = await organisations.GetAsync(organisationId, cancellationToken);
        if (organisation is not null)
        {
            await AuditAsync(organisation.Scope, actorUserId, "organisation.api_key_revoked", key, $"API key '{key.Name}' revoked.", correlationId, cancellationToken);
        }
    }

    /// <summary>Who a presented key belongs to, or null. Revoked, expired and unknown keys, and keys of a
    /// suspended organisation, all give the same answer.</summary>
    public async Task<ApiKeyRecord?> AuthenticateAsync(string? presentedKey, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(presentedKey) || !presentedKey.StartsWith(ApiKeyRecord.KeyPrefix, StringComparison.Ordinal) || presentedKey.Length > 200)
        {
            return null;
        }

        var key = await keys.GetByHashAsync(Hash(presentedKey.Trim()), cancellationToken);
        if (key is null || !key.IsUsable(now))
        {
            return null;
        }

        if (await organisations.GetAsync(key.OrganisationId, cancellationToken) is not { IsActive: true })
        {
            return null;
        }

        // A write on every request would make every call slower; once in a while is enough to see a key is alive.
        if (key.LastUsedAt is null || now - key.LastUsedAt >= LastUsedGranularity)
        {
            await keys.UpdateAsync(key with { LastUsedAt = now }, cancellationToken);
        }

        return key;
    }

    public static string Hash(string key) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)));

    private Task AuditAsync(TenantScope scope, string userId, string action, ApiKeyRecord key, string reason, string correlationId, CancellationToken cancellationToken) =>
        auditRepository.WriteAsync(
            new AuditEvent(
                Guid.NewGuid(), scope, null, null, userId, ActorType.User, action, DateTimeOffset.UtcNow,
                null, null, [key.KeyId.ToString()], "Platform", null, "recorded", null, reason, correlationId),
            cancellationToken);
}
