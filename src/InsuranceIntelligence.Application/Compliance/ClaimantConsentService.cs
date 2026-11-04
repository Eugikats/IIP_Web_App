using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Compliance;

namespace InsuranceIntelligence.Application.Compliance;

/// <summary>A claimant's consent as the portal and staff see it. The notice text is included so the
/// page shows exactly the words being agreed to.</summary>
public sealed record ClaimantConsentStatusDto(
    string State,
    int NoticeVersion,
    string NoticeText,
    bool NoticeIsDefault,
    DateTimeOffset? RecordedAt,
    string? ConsentedVersion);

public sealed class NoticeChangedException(string message) : InvalidOperationException(message);

public interface IClaimantConsentService
{
    Task<ClaimantConsentStatusDto> GetStatusAsync(TenantScope scope, string contactValue, CancellationToken cancellationToken = default);

    /// <summary>Records agreement to the notice in force. Refused with <see cref="NoticeChangedException"/>
    /// when the version the claimant read is no longer the one in force.</summary>
    Task<ClaimantConsentStatusDto> GrantAsync(TenantScope scope, string contactValue, string noticeVersion, Guid? caseId, string? interactionReference, string correlationId, CancellationToken cancellationToken = default);

    Task<ClaimantConsentStatusDto> WithdrawAsync(TenantScope scope, string contactValue, Guid? caseId, string? interactionReference, string correlationId, CancellationToken cancellationToken = default);

    /// <summary>True only when the claimant has agreed to the notice that is in force now.</summary>
    Task<bool> HasConsentAsync(TenantScope scope, string contactValue, CancellationToken cancellationToken = default);

    /// <summary>For staff: the consent of whoever filed this claim, found by the contacts on file.</summary>
    Task<ClaimantConsentStatusDto> GetStatusForCaseAsync(ClaimCase claimCase, CancellationToken cancellationToken = default);
}

/// <summary>
/// Consent to how a claimant's details and documents are processed, asked before the first upload
/// (build-loop item 9). Append-only on the existing consent store: agreeing, and withdrawing, each
/// add a row naming the notice version, so the history of what was agreed to and when is never
/// rewritten. Withdrawing stops new documents being accepted from the portal until the claimant
/// agrees again; it does not delete what has been sent (retention and erasure are item 34).
/// </summary>
public sealed class ClaimantConsentService(
    IConsentRepository consentRepository,
    ITenantSettingsRepository settingsRepository,
    IAuditRepository auditRepository) : IClaimantConsentService
{
    public async Task<ClaimantConsentStatusDto> GetStatusAsync(TenantScope scope, string contactValue, CancellationToken cancellationToken = default)
    {
        var notice = PrivacyNotices.For(await settingsRepository.GetAsync(scope, cancellationToken));
        var current = await consentRepository.GetCurrentAsync(scope, ClaimantConsent.Identifier(contactValue), ConsentPurposes.ClaimDataProcessing, cancellationToken);
        return ToDto(current, notice);
    }

    public async Task<ClaimantConsentStatusDto> GrantAsync(
        TenantScope scope, string contactValue, string noticeVersion, Guid? caseId, string? interactionReference, string correlationId, CancellationToken cancellationToken = default)
    {
        var notice = PrivacyNotices.For(await settingsRepository.GetAsync(scope, cancellationToken));
        if (!string.Equals(noticeVersion?.Trim(), notice.VersionKey, StringComparison.Ordinal))
        {
            throw new NoticeChangedException("The privacy notice has changed. Please read the current notice and agree again.");
        }

        var record = Record(scope, contactValue, ConsentBasis.ExplicitOptIn, null, notice.VersionKey, interactionReference, correlationId);
        await consentRepository.SaveAsync(record, cancellationToken);
        await auditRepository.WriteAsync(AuditEvent.ClaimantConsentChanged(scope, caseId, "claimant.consent.given", notice.VersionKey, correlationId), cancellationToken);
        return ToDto(record, notice);
    }

    public async Task<ClaimantConsentStatusDto> WithdrawAsync(
        TenantScope scope, string contactValue, Guid? caseId, string? interactionReference, string correlationId, CancellationToken cancellationToken = default)
    {
        var notice = PrivacyNotices.For(await settingsRepository.GetAsync(scope, cancellationToken));
        var record = Record(scope, contactValue, ConsentBasis.Withdrawn, DateTimeOffset.UtcNow, notice.VersionKey, interactionReference, correlationId);
        await consentRepository.SaveAsync(record, cancellationToken);
        await auditRepository.WriteAsync(AuditEvent.ClaimantConsentChanged(scope, caseId, "claimant.consent.withdrawn", notice.VersionKey, correlationId), cancellationToken);
        return ToDto(record, notice);
    }

    public async Task<bool> HasConsentAsync(TenantScope scope, string contactValue, CancellationToken cancellationToken = default) =>
        (await GetStatusAsync(scope, contactValue, cancellationToken)).State == nameof(ClaimantConsentState.Given);

    public async Task<ClaimantConsentStatusDto> GetStatusForCaseAsync(ClaimCase claimCase, CancellationToken cancellationToken = default)
    {
        var notice = PrivacyNotices.For(await settingsRepository.GetAsync(claimCase.Scope, cancellationToken));
        ConsentRecord? latest = null;
        foreach (var contact in new[] { claimCase.CustomerEmail, claimCase.CustomerPhone }.Where(value => !string.IsNullOrWhiteSpace(value)))
        {
            var current = await consentRepository.GetCurrentAsync(claimCase.Scope, ClaimantConsent.Identifier(contact!), ConsentPurposes.ClaimDataProcessing, cancellationToken);
            if (current is not null && (latest is null || current.RecordedAt > latest.RecordedAt))
            {
                latest = current;
            }
        }

        return ToDto(latest, notice);
    }

    private static ConsentRecord Record(
        TenantScope scope, string contactValue, ConsentBasis basis, DateTimeOffset? withdrawnAt, string noticeVersion, string? interactionReference, string correlationId) =>
        new()
        {
            RecordId = Guid.NewGuid(),
            Scope = scope,
            CustomerIdentifier = ClaimantConsent.Identifier(contactValue),
            Basis = basis,
            Purpose = ConsentPurposes.ClaimDataProcessing,
            Channel = ClaimantConsent.Channel,
            WithdrawnAt = withdrawnAt,
            // The claimant is the actor; the record names the identifier, never the raw contact.
            RecordedByUserId = ClaimantConsent.Identifier(contactValue),
            RecordedAt = DateTimeOffset.UtcNow,
            CorrelationId = correlationId,
            InteractionReference = interactionReference,
            NoticeVersion = noticeVersion,
        };

    private static ClaimantConsentStatusDto ToDto(ConsentRecord? current, PrivacyNotice notice) =>
        new(
            ClaimantConsent.StateOf(current, notice).ToString(),
            notice.Version,
            notice.Text,
            notice.IsDefault,
            current?.RecordedAt,
            current?.NoticeVersion);
}
