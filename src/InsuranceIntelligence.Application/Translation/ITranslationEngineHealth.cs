namespace InsuranceIntelligence.Application.Translation;

public enum TranslationEngineStatus
{
    NotConfigured,
    Healthy,
    Failing,
}

/// <summary>Whether the translation engine can be reached right now, for the staff screen that
/// says whether local-language replies are actually working. Never called on a customer's path.</summary>
public interface ITranslationEngineHealth
{
    Task<TranslationEngineStatus> CheckAsync(CancellationToken cancellationToken = default);
}
