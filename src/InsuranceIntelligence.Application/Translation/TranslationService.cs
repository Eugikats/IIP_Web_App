using System.Security.Cryptography;
using System.Text;
using InsuranceIntelligence.Domain.Translation;

namespace InsuranceIntelligence.Application.Translation;

public interface ITranslationService
{
    /// <summary>Never throws on a bad model output and never returns an unvalidated candidate
    /// (build plan §4, non-negotiable 4): every path ends in either a judged-clean translation or
    /// the original text marked as a fallback. Callers that need the reason a fallback happened
    /// read <see cref="TranslationOutcome.Failure"/>; they never need to catch anything.</summary>
    Task<TranslationOutcome> TranslateAsync(
        string text,
        SupportedLanguageCode from,
        SupportedLanguageCode to,
        CancellationToken cancellationToken = default);
}

/// <summary>Cleans, validates, retries once and falls back, per sentence past the split
/// threshold, with protected tokens masked out before the model ever sees them. Ports the
/// reference app's flow (build plan §5) onto <see cref="TranslationQualityJudge"/> and
/// <see cref="TranslationSentenceSplitter"/>.</summary>
public sealed class TranslationService(ITranslationProvider provider, ITranslationCache cache) : ITranslationService
{
    /// <summary>Included in every cache key so a new quant or a different model file never reuses
    /// a translation produced by the one it replaced (build plan §5).</summary>
    public const string ModelTag = "afrislm-0.8b-q4_k_m.gguf#671088640";

    public async Task<TranslationOutcome> TranslateAsync(
        string text, SupportedLanguageCode from, SupportedLanguageCode to, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text) || from == to)
        {
            return TranslationOutcome.NotNeeded(text);
        }

        if (!provider.IsConfigured)
        {
            return TranslationOutcome.Fallback(text, TranslationFailureReason.ProviderNotConfigured);
        }

        var cacheKey = BuildCacheKey(from, to, text);
        var cached = await cache.GetAsync(cacheKey, cancellationToken);
        if (cached is not null)
        {
            return TranslationOutcome.Success(cached, fromCache: true);
        }

        var sentences = TranslationSentenceSplitter.Split(text);
        var translatedPieces = new List<string>(sentences.Count);

        foreach (var sentence in sentences)
        {
            var (masked, tokens) = ProtectedTokenMasker.Mask(sentence);
            var (translatedText, failure) = await TranslateSentenceAsync(masked, from, to, cancellationToken);
            if (failure is not null)
            {
                // Keep successful pieces only conceptually: one rejected sentence means the whole
                // reply falls back to English, a half-translated customer message is worse than
                // an honest, fully-English one.
                return TranslationOutcome.Fallback(text, failure.Value);
            }

            translatedPieces.Add(ProtectedTokenMasker.Unmask(translatedText!, tokens));
        }

        var result = string.Join(' ', translatedPieces);
        await cache.SetAsync(cacheKey, result, cancellationToken);
        return TranslationOutcome.Success(result, fromCache: false);
    }

    private async Task<(string? Text, TranslationFailureReason? Failure)> TranslateSentenceAsync(
        string sentence, SupportedLanguageCode from, SupportedLanguageCode to, CancellationToken cancellationToken)
    {
        TranslationFailureReason lastFailure = TranslationFailureReason.Empty;

        for (var attempt = 0; attempt < 2; attempt++)
        {
            var raw = await provider.TranslateAsync(sentence, from, to, cancellationToken);
            var cleaned = TranslationQualityJudge.Clean(raw);

            // "Strict English check on the first attempt only" (build plan §5): a retry that still
            // reads as English is accepted rather than guaranteed a second rejection, everything
            // else the judge checks still applies on both attempts. Never applies when translating
            // into English, where reading as English is the point.
            var candidateShouldNotBeEnglish = attempt == 0 && to != SupportedLanguageCode.En;
            var reason = TranslationQualityJudge.Judge(sentence, cleaned, candidateShouldNotBeEnglish);
            if (reason is null)
            {
                return (cleaned, null);
            }

            lastFailure = reason.Value;
        }

        return (null, lastFailure);
    }

    public static string BuildCacheKey(SupportedLanguageCode from, SupportedLanguageCode to, string text)
    {
        var normalized = text.Trim().ToLowerInvariant();
        var payload = $"{ModelTag}|{from}|{to}|{normalized}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash);
    }
}
