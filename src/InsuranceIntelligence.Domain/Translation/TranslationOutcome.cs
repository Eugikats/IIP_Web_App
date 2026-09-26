namespace InsuranceIntelligence.Domain.Translation;

/// <summary>Every rejection <see cref="TranslationQualityJudge"/> can hand back, matching the
/// AfriSLM model's documented failure modes (build plan §5): it does not fail loudly, it invents,
/// so each of these is something the judge caught, not something the model reported.</summary>
public enum TranslationFailureReason
{
    Empty,
    Unchanged,
    EchoedPrompt,
    RepetitionLoop,
    LengthBlowup,
    StillEnglish,
    GarbledScript,
    ProviderNotConfigured,

    /// <summary>The engine could not be reached or answered with an error. Appended last so stored
    /// values keep their meaning.</summary>
    ProviderUnavailable,

    /// <summary>The engine dropped or altered a protected value (policy number, link, reference).
    /// Refused rather than repaired, because a translation that lost the customer's policy number
    /// is worse than an English one.</summary>
    ProtectedTokenLost,
}

/// <summary>What a translation attempt actually produced. <see cref="Translated"/> is the only
/// field a caller needs to decide whether <see cref="Text"/> is a model translation or a
/// passthrough of the original; a passthrough can never be mistaken for a translation because
/// <see cref="Translated"/> is false in every passthrough case, whether the text was never sent to
/// the model (<see cref="NotNeeded"/>) or the model's output was rejected (<see cref="Fallback"/>).</summary>
public sealed record TranslationOutcome(string Text, bool Translated, bool FromCache, TranslationFailureReason? Failure)
{
    public static TranslationOutcome Success(string text, bool fromCache) => new(text, true, fromCache, null);

    /// <summary>Source and target language were the same, or the text was blank; nothing to
    /// translate and nothing failed.</summary>
    public static TranslationOutcome NotNeeded(string text) => new(text, false, false, null);

    /// <summary>The model's output was rejected, or no provider is configured; <paramref
    /// name="originalText"/> is returned unchanged, marked with why.</summary>
    public static TranslationOutcome Fallback(string originalText, TranslationFailureReason reason) => new(originalText, false, false, reason);
}

/// <summary>What happened to one side of a conversation turn, so a person reading it can tell a
/// machine translation from a human-written reply from plain English. Stored, never inferred.
/// The first three are what the assistant produces today; the last two are reserved for the
/// human-reviewed template layer, and appended so stored values keep their meaning.</summary>
public enum TranslationStatus
{
    /// <summary>The turn was in English, or the text had nothing to translate.</summary>
    NotNeeded,

    /// <summary>The model translated it and the result passed every check.</summary>
    Translated,

    /// <summary>A translation was wanted but could not be shown, so the English went out instead.</summary>
    FellBackToEnglish,

    /// <summary>A human-reviewed translation was used.</summary>
    HumanTemplate,

    /// <summary>A human-written translation that has not been reviewed by a native speaker was used.</summary>
    DraftTemplate,
}
