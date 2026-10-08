namespace InsuranceIntelligence.Domain.Translation;

/// <summary>
/// The resolution order for which language to answer a customer in (build plan §6.B):
///
/// 1. A digit answer to a choice prompt this assistant just sent, if one was sent.
/// 2. An explicit request in the message itself ("reply in Luganda"). This is checked ahead of
///    the remembered preference, not after it: the build plan lists the remembered preference
///    first as the default a message falls back to, but an explicit, current instruction from the
///    customer is exactly the kind of thing a "preference" exists to be overridden by, and nothing
///    in the plan says a remembered preference should out-rank the customer asking, in the moment,
///    to switch languages.
/// 3. The remembered preference, if set within the freshness window, unless a longer message
///    scores a different language with high confidence.
/// 4. The word-list scorer, on messages long enough to trust.
/// 5. Uncertain: reply in English with the choice prompt.
///
/// Pure and deterministic, mirrors ConversationContinuation's shape in the CustomerAssistant
/// namespace: every external input (the remembered preference, the detector's score) is passed in
/// already resolved, no repository or detector call happens in here.
/// </summary>
public static class LanguageResolutionOrder
{
    /// <summary>How long a remembered preference stays in force without a fresh signal.</summary>
    public static readonly TimeSpan PreferenceFreshness = TimeSpan.FromDays(90);

    /// <summary>"ok", "hello" or a bare policy number: never enough on its own to flip a
    /// customer's remembered language, and never enough to trust a detector's score either.</summary>
    private const int ShortMessageWordThreshold = 3;

    private const double DetectionConfidenceThreshold = 0.5;
    private const double OverrideRememberedConfidenceThreshold = 0.8;
    private const double LocalOverrideConfidence = 0.45;
    private const double ShortMessageOverrideConfidence = 0.66;

    public static LanguageResolution Resolve(
        string messageText,
        bool previousMessageWasChoicePrompt,
        CustomerLanguagePreference? rememberedPreference,
        LanguageResolution detected,
        DateTimeOffset now)
    {
        if (previousMessageWasChoicePrompt)
        {
            var answer = LanguageChoicePrompt.ParseAnswer(messageText);
            if (answer is not null)
            {
                return new LanguageResolution(answer.Value, LanguageResolutionSource.ExplicitRequest, 1.0);
            }
        }

        var explicitRequest = ExplicitLanguageRequestParser.Parse(messageText);
        if (explicitRequest is not null)
        {
            return new LanguageResolution(explicitRequest.Value, LanguageResolutionSource.ExplicitRequest, 1.0);
        }

        var isShortMessage = CountWords(messageText) <= ShortMessageWordThreshold;

        if (rememberedPreference is not null && now - rememberedPreference.SetAt <= PreferenceFreshness)
        {
            // A remembered language is a default, not a lock. What the customer has just written wins when it
            // clearly differs: a Luganda or Kiswahili sentence needs only moderate evidence, a short message
            // needs a strong non-English signal ("Webale nnyo"), and a switch back to English needs a long,
            // plain English message so one "ok" never flips anyone.
            var differs = detected.Language != rememberedPreference.Language;
            var toLocal = detected.Language != SupportedLanguageCode.En;
            var detectionOverrides = differs && (isShortMessage
                ? toLocal && CountWords(messageText) >= 2 && detected.Confidence >= ShortMessageOverrideConfidence
                : detected.Confidence >= (toLocal ? LocalOverrideConfidence : OverrideRememberedConfidenceThreshold));

            return detectionOverrides
                ? detected
                : new LanguageResolution(rememberedPreference.Language, LanguageResolutionSource.RememberedPreference, 1.0);
        }

        if (!isShortMessage && detected.Confidence >= DetectionConfidenceThreshold)
        {
            return detected;
        }

        return new LanguageResolution(SupportedLanguageCode.En, LanguageResolutionSource.PromptedChoice, 0);
    }

    private static int CountWords(string text) =>
        string.IsNullOrWhiteSpace(text) ? 0 : text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
}
