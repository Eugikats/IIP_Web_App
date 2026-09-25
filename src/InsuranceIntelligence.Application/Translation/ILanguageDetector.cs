using InsuranceIntelligence.Domain.Translation;

namespace InsuranceIntelligence.Application.Translation;

/// <summary>Step 3 of the language-resolution order (build plan §6.B): a word-list scorer for
/// English, Luganda and Kiswahili, used only after a remembered preference and an explicit
/// in-message request have both come back empty. Outcome B implements this; outcome A only
/// defines the boundary so <see cref="Application.Translation.TranslationService"/>'s dependents
/// can be wired without waiting on it.</summary>
public interface ILanguageDetector
{
    LanguageResolution Detect(string text);
}
