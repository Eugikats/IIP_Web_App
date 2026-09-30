using InsuranceIntelligence.Application.Translation;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Translation;

namespace InsuranceIntelligence.LocalTestDoubles;

/// <summary>Deterministic stand-in for the AfriSLM container (build plan §6.A), for Development
/// and Demo only, never registered in Staging or Production. Produces a clearly-labelled,
/// judge-passing translation so tests and demos can exercise the whole pipeline without a live
/// container; it never simulates any of the model's documented failure modes on its own; tests
/// that need a rejection path construct one directly against <see cref="TranslationQualityJudge"/>
/// or a purpose-built fake <see cref="ITranslationProvider"/> instead.</summary>
public sealed class FakeTranslationProvider : ITranslationProvider
{
    public const string ModelTag = "fake-translator";

    public bool IsConfigured { get; init; } = true;

    public Task<bool> IsConfiguredAsync(TenantScope scope, CancellationToken cancellationToken = default) => Task.FromResult(IsConfigured);

    public Task<string> GetModelTagAsync(TenantScope scope, CancellationToken cancellationToken = default) => Task.FromResult(ModelTag);

    public Task<string> TranslateAsync(
        string text, SupportedLanguageCode from, SupportedLanguageCode to, TenantScope scope, CancellationToken cancellationToken = default)
    {
        var target = SupportedLanguage.For(to);
        return Task.FromResult($"[{target.Iso}] {text}");
    }
}
