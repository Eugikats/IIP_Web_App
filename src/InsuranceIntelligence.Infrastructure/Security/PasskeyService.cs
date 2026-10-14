using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Fido2NetLib;
using Fido2NetLib.Objects;
using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Application.Security;
using InsuranceIntelligence.Domain.Security;
using Microsoft.Extensions.Logging;

namespace InsuranceIntelligence.Infrastructure.Security;

/// <summary>
/// Passkeys through the same Fido2 library CIP uses. A person's keys live in their security profile; the options sent
/// to the browser are kept there for a few minutes to check the reply against. The relying party is the portal's own
/// address (taken from the browser's Origin header, and only when it is on the allow-list).
/// </summary>
public sealed class PasskeyService(
    IPlatformMfaService mfa,
    ITenantContextAccessor tenantContextAccessor,
    PasskeyOptions options,
    ILogger<PasskeyService> logger) : IPasskeyService
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<MfaResult<string>> RegistrationOptionsAsync(string origin, string accountName, string displayName, CancellationToken cancellationToken = default)
    {
        if (!TryFido2(origin, out var fido2, out var refusal)) return MfaResult<string>.Fail("origin_not_allowed", refusal);
        var profile = await mfa.GetProfileAsync(cancellationToken);
        var exclude = profile.Passkeys.Select(p => new PublicKeyCredentialDescriptor(Base64Url.Decode(p.CredentialId))).ToList();
        var user = new Fido2User
        {
            Id = UserHandle(),
            Name = string.IsNullOrWhiteSpace(accountName) ? "account" : accountName,
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? accountName : displayName,
        };
        var created = fido2.RequestNewCredential(
            user, exclude,
            new AuthenticatorSelection { RequireResidentKey = false, UserVerification = UserVerificationRequirement.Preferred },
            AttestationConveyancePreference.None, extensions: null);
        var optionsJson = JsonSerializer.Serialize(created, Json);
        var stored = await mfa.BeginPasskeyCeremonyAsync("register", optionsJson, cancellationToken);
        return stored.Ok ? MfaResult<string>.Success(optionsJson) : MfaResult<string>.Fail(stored.Code!, stored.Message!);
    }

    public async Task<MfaResult<bool>> RegistrationResultAsync(string origin, string responseJson, string? nickname, CancellationToken cancellationToken = default)
    {
        if (!TryFido2(origin, out var fido2, out var refusal)) return MfaResult<bool>.Fail("origin_not_allowed", refusal);
        var pending = await mfa.TakePendingPasskeyCeremonyAsync("register", cancellationToken);
        if (pending is null) return MfaResult<bool>.Fail("no_ceremony", "That passkey set-up expired. Start again.");

        try
        {
            var createOptions = JsonSerializer.Deserialize<CredentialCreateOptions>(pending, Json) ?? throw new InvalidOperationException("Empty options.");
            var response = JsonSerializer.Deserialize<AuthenticatorAttestationRawResponse>(responseJson, Json) ?? throw new InvalidOperationException("Empty reply.");
            var made = await fido2.MakeNewCredentialAsync(response, createOptions, (_, _) => Task.FromResult(true), requestTokenBindingId: null, cancellationToken);
            if (made.Result is null) return MfaResult<bool>.Fail("verification_failed", "The passkey could not be verified.");

            var name = string.IsNullOrWhiteSpace(nickname) ? "Passkey" : nickname.Trim();
            return await mfa.AddPasskeyAsync(new PasskeyCredential
            {
                Id = Guid.NewGuid(),
                Nickname = name[..Math.Min(name.Length, 60)],
                CredentialId = Base64Url.Encode(made.Result.CredentialId),
                PublicKey = Convert.ToBase64String(made.Result.PublicKey),
                SignCount = made.Result.Counter,
                UserHandle = Base64Url.Encode(createOptions.User.Id),
                CreatedAt = DateTimeOffset.UtcNow,
            }, cancellationToken);
        }
        catch (Exception exception) when (exception is Fido2VerificationException or JsonException or InvalidOperationException)
        {
            logger.LogInformation(exception, "Passkey registration could not be verified");
            return MfaResult<bool>.Fail("verification_failed", "The passkey could not be verified.");
        }
    }

    public async Task<MfaResult<string>> AssertionOptionsAsync(string origin, CancellationToken cancellationToken = default)
    {
        if (!TryFido2(origin, out var fido2, out var refusal)) return MfaResult<string>.Fail("origin_not_allowed", refusal);
        var profile = await mfa.GetProfileAsync(cancellationToken);
        if (profile.Passkeys.Count == 0) return MfaResult<string>.Fail("not_enrolled", "You have not set up a passkey.");
        var allowed = profile.Passkeys.Select(p => new PublicKeyCredentialDescriptor(Base64Url.Decode(p.CredentialId))).ToList();
        var assertion = fido2.GetAssertionOptions(allowed, UserVerificationRequirement.Preferred, extensions: null);
        var optionsJson = JsonSerializer.Serialize(assertion, Json);
        var stored = await mfa.BeginPasskeyCeremonyAsync("assert", optionsJson, cancellationToken);
        return stored.Ok ? MfaResult<string>.Success(optionsJson) : MfaResult<string>.Fail(stored.Code!, stored.Message!);
    }

    public async Task<MfaResult<bool>> AssertionResultAsync(string origin, string responseJson, CancellationToken cancellationToken = default)
    {
        if (!TryFido2(origin, out var fido2, out var refusal)) return MfaResult<bool>.Fail("origin_not_allowed", refusal);
        var pending = await mfa.TakePendingPasskeyCeremonyAsync("assert", cancellationToken);
        if (pending is null) return MfaResult<bool>.Fail("no_ceremony", "That sign-in expired. Try again.");

        try
        {
            var assertionOptions = JsonSerializer.Deserialize<AssertionOptions>(pending, Json) ?? throw new InvalidOperationException("Empty options.");
            var response = JsonSerializer.Deserialize<AuthenticatorAssertionRawResponse>(responseJson, Json) ?? throw new InvalidOperationException("Empty reply.");
            var credentialId = Base64Url.Encode(response.RawId ?? response.Id);
            var profile = await mfa.GetProfileAsync(cancellationToken);
            var stored = profile.Passkeys.FirstOrDefault(p => p.CredentialId == credentialId);
            if (stored is null) return MfaResult<bool>.Fail("not_found", "That passkey is not registered for you.");

            var result = await fido2.MakeAssertionAsync(
                response, assertionOptions, Convert.FromBase64String(stored.PublicKey), stored.SignCount,
                (_, _) => Task.FromResult(true), requestTokenBindingId: null, cancellationToken);
            return await mfa.CompletePasskeyVerificationAsync(credentialId, result.Counter, cancellationToken);
        }
        catch (Exception exception) when (exception is Fido2VerificationException or JsonException or InvalidOperationException)
        {
            logger.LogInformation(exception, "Passkey sign-in could not be verified");
            return MfaResult<bool>.Fail("verification_failed", "The passkey could not be verified.");
        }
    }

    private bool TryFido2(string origin, out Fido2 fido2, out string refusal)
    {
        fido2 = null!;
        var match = options.AllowedOrigins.FirstOrDefault(allowed => string.Equals(allowed.TrimEnd('/'), origin?.TrimEnd('/'), StringComparison.OrdinalIgnoreCase));
        if (match is null || !Uri.TryCreate(match, UriKind.Absolute, out var uri))
        {
            refusal = "Passkeys are not available from this address.";
            return false;
        }

        fido2 = new Fido2(new Fido2Configuration
        {
            ServerDomain = uri.Host,
            ServerName = options.RelyingPartyName,
            Origins = new HashSet<string>([match.TrimEnd('/')]),
            TimestampDriftTolerance = 300_000,
        });
        refusal = string.Empty;
        return true;
    }

    /// <summary>An identifier for the person that carries no personal detail: a hash of their scoped user id.</summary>
    private byte[] UserHandle()
    {
        var context = tenantContextAccessor.Current;
        return SHA256.HashData(Encoding.UTF8.GetBytes($"{context.TenantId}:{context.WorkspaceId}:{context.UserId.ToLowerInvariant()}"));
    }
}
