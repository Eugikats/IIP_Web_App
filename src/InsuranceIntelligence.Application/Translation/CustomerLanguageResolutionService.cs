using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Translation;

namespace InsuranceIntelligence.Application.Translation;

/// <summary>Wires the pure <see cref="LanguageResolutionOrder"/> up to the repository and the
/// detector, and does the one piece of I/O the resolution order itself cannot: saving a language
/// the customer explicitly asked for (build plan §6.B, "save the answer as the preference"),
/// whether it arrived as a phrase ("reply in Luganda") or as the digit answer to a choice prompt.</summary>
public interface ICustomerLanguageResolutionService
{
    Task<LanguageResolution> ResolveAsync(
        TenantScope scope,
        string contactWaId,
        string messageText,
        bool previousMessageWasChoicePrompt,
        CancellationToken cancellationToken = default);

    /// <summary>Remembers the language the customer has just clearly written in, so a short reply
    /// that follows ("1", "yes", a policy number) stays in that language.</summary>
    Task RememberAsync(TenantScope scope, string contactWaId, SupportedLanguageCode language, CancellationToken cancellationToken = default);
}

public sealed class CustomerLanguageResolutionService(
    ICustomerLanguagePreferenceRepository preferenceRepository,
    ILanguageDetector detector) : ICustomerLanguageResolutionService
{
    public async Task<LanguageResolution> ResolveAsync(
        TenantScope scope, string contactWaId, string messageText, bool previousMessageWasChoicePrompt,
        CancellationToken cancellationToken = default)
    {
        var remembered = await preferenceRepository.GetCurrentAsync(scope, contactWaId, cancellationToken);
        var detected = detector.Detect(messageText);

        var resolution = LanguageResolutionOrder.Resolve(
            messageText, previousMessageWasChoicePrompt, remembered, detected, DateTimeOffset.UtcNow);

        if (resolution.Source == LanguageResolutionSource.ExplicitRequest)
        {
            await preferenceRepository.SaveAsync(new CustomerLanguagePreference
            {
                PreferenceId = Guid.NewGuid(),
                Scope = scope,
                ContactWaId = contactWaId,
                Language = resolution.Language,
                SetAt = DateTimeOffset.UtcNow,
            }, cancellationToken);
        }

        return resolution;
    }

    public async Task RememberAsync(TenantScope scope, string contactWaId, SupportedLanguageCode language, CancellationToken cancellationToken = default)
    {
        var remembered = await preferenceRepository.GetCurrentAsync(scope, contactWaId, cancellationToken);
        if (remembered?.Language == language)
        {
            return;
        }

        await preferenceRepository.SaveAsync(new CustomerLanguagePreference
        {
            PreferenceId = Guid.NewGuid(),
            Scope = scope,
            ContactWaId = contactWaId,
            Language = language,
            SetAt = DateTimeOffset.UtcNow,
        }, cancellationToken);
    }
}
