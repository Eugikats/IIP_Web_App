using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Translation;

namespace InsuranceIntelligence.Application.Translation;

/// <summary>The adapter boundary for the translation engine (outcome G). Returns the model's raw
/// output, uncleaned and unjudged, that is <see cref="TranslationQualityJudge"/>'s job, not the
/// provider's; a provider that started cleaning its own output would hide failures the judge is
/// supposed to catch. Implementations are resolved per tenant scope: a tenant's own dashboard-
/// configured connection (e.g. Sunbird AI), falling back to the deployment's environment-configured
/// engine, mirroring how ISmsProvider/IEmailSendingProvider already resolve per tenant.</summary>
public interface ITranslationProvider
{
    /// <summary>False when no engine is reachable for this tenant (build plan §"six states", "not
    /// configured"); <see cref="Application.Translation.TranslationService"/> falls back to English
    /// rather than calling a provider that cannot serve the request.</summary>
    Task<bool> IsConfiguredAsync(TenantScope scope, CancellationToken cancellationToken = default);

    /// <summary>Identifies which model actually served (or would serve) this tenant, folded into
    /// the translation cache key so a tenant switching engines never has a stale translation from
    /// the previous one served back under the new one's key.</summary>
    Task<string> GetModelTagAsync(TenantScope scope, CancellationToken cancellationToken = default);

    Task<string> TranslateAsync(
        string text,
        SupportedLanguageCode from,
        SupportedLanguageCode to,
        TenantScope scope,
        CancellationToken cancellationToken = default);
}
