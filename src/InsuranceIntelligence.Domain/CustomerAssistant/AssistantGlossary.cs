using InsuranceIntelligence.Domain.Translation;

namespace InsuranceIntelligence.Domain.CustomerAssistant;

/// <summary>
/// Insurance words that are better left in English when writing to a customer, per language.
///
/// Measured against the model: asked to put "your claim on your Motor policy" into Luganda it wrote
/// "your case on your plot of the car", and "claims officer" became "debt trader". Keeping "claim",
/// "policy" and "claims officer" in English removed those errors, and is how Luganda is written in
/// practice. The same words left in English made Kiswahili worse ("Motor yako policy"), because the
/// model already handles them there, so the list is per language and Kiswahili has none.
///
/// Longest first, so "claims officer" is protected before "claims" and "claim" can split it.
/// </summary>
public static class AssistantGlossary
{
    private static readonly string[] Luganda = ["claims officer", "claims", "claim", "policies", "policy"];

    public static IReadOnlyList<string> KeptInEnglish(SupportedLanguageCode language) =>
        language == SupportedLanguageCode.Lg ? Luganda : [];
}
