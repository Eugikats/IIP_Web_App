namespace InsuranceIntelligence.Domain.Translation;

/// <summary>How the assistant decided which language a customer speaks (build plan §6.B). The
/// resolution order itself (remembered preference, explicit request, word-list detection, prompt
/// and save) is outcome B's job; this record is the shared result shape every step of that order
/// hands back.</summary>
public enum LanguageResolutionSource
{
    RememberedPreference,
    ExplicitRequest,
    Detected,
    PromptedChoice,
}

public sealed record LanguageResolution(SupportedLanguageCode Language, LanguageResolutionSource Source, double Confidence);
