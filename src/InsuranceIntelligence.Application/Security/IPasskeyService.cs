using InsuranceIntelligence.Application.Security;

namespace InsuranceIntelligence.Application.Security;

/// <summary>Where a passkey may be used: the browser addresses of the portal. A passkey is tied to the address it was
/// made on, so each address has its own. Anything not listed is refused.</summary>
public sealed record PasskeyOptions(IReadOnlyList<string> AllowedOrigins, string RelyingPartyName = "Insurance Intelligence Platform");

/// <summary>Passkeys and security keys (WebAuthn). The browser asks the person's device to make or use a key pair; the
/// private key never leaves the device. The options and the replies are exchanged as JSON.</summary>
public interface IPasskeyService
{
    Task<MfaResult<string>> RegistrationOptionsAsync(string origin, string accountName, string displayName, CancellationToken cancellationToken = default);

    Task<MfaResult<bool>> RegistrationResultAsync(string origin, string responseJson, string? nickname, CancellationToken cancellationToken = default);

    Task<MfaResult<string>> AssertionOptionsAsync(string origin, CancellationToken cancellationToken = default);

    Task<MfaResult<bool>> AssertionResultAsync(string origin, string responseJson, CancellationToken cancellationToken = default);
}
