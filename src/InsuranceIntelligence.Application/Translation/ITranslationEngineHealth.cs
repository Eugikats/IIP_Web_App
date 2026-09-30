using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Application.Translation;

public enum TranslationEngineStatus
{
    NotConfigured,
    Healthy,
    Failing,
}

/// <summary>Whether the translation engine can be reached right now, for the staff screen that
/// says whether local-language replies are actually working. Never called on a customer's path.
/// Scoped per tenant so the status screen reports on whichever engine that tenant actually has
/// active (its own connection, or the deployment fallback), never a deployment-wide assumption.</summary>
public interface ITranslationEngineHealth
{
    Task<TranslationEngineStatus> CheckAsync(TenantScope scope, CancellationToken cancellationToken = default);
}
