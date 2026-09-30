using InsuranceIntelligence.Application.Translation;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Translation;

namespace InsuranceIntelligence.Api.Local;

/// <summary>Development and Demo only: AzureProviderStartupValidator refuses
/// INSURANCEINTELLIGENCE_TRANSLATION_PROVIDER=Mock under every deployed profile. Deterministic and
/// clearly labelled, so a demo reply is never mistaken for a real AfriSLM translation. Lives here
/// rather than in InsuranceIntelligence.LocalTestDoubles because InsuranceIntelligence.Api does not
/// reference that project, the same reason LocalMockDocumentStorageProvider and
/// MockMessageQueueDispatcher live here too: it is a compile-time guarantee a test double can never
/// reach a real deployment, not just a runtime check.</summary>
public sealed class MockTranslationProvider : ITranslationProvider, ITranslationEngineHealth
{
    public const string ModelTag = "mock-translator";

    public Task<bool> IsConfiguredAsync(TenantScope scope, CancellationToken cancellationToken = default) => Task.FromResult(true);

    public Task<string> GetModelTagAsync(TenantScope scope, CancellationToken cancellationToken = default) => Task.FromResult(ModelTag);

    public Task<TranslationEngineStatus> CheckAsync(TenantScope scope, CancellationToken cancellationToken = default) =>
        Task.FromResult(TranslationEngineStatus.Healthy);

    public Task<string> TranslateAsync(string text, SupportedLanguageCode from, SupportedLanguageCode to, TenantScope scope, CancellationToken cancellationToken = default) =>
        Task.FromResult($"[{SupportedLanguage.For(to).Iso}] {text}");
}
