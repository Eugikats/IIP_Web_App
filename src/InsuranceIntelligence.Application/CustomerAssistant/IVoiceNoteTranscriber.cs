using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Translation;

namespace InsuranceIntelligence.Application.CustomerAssistant;

/// <summary>
/// Turns a WhatsApp voice note into text so Kato can answer it in writing. Voice in, text out:
/// nothing here speaks back. A transcript is only ever what the customer said, run through the same
/// understanding, confirmation and human-decision steps as a typed message.
/// </summary>
public interface IVoiceNoteTranscriber
{
    /// <summary>True when this tenant has a speech engine connected, so Kato may invite voice notes.</summary>
    Task<bool> IsAvailableAsync(TenantScope scope, CancellationToken cancellationToken = default);

    /// <summary>The words in the voice note, or null when it could not be understood (no engine,
    /// download failed, engine failed, silence). Never throws for those: the caller answers in text.</summary>
    Task<string?> TranscribeAsync(
        TenantScope scope, string mediaId, SupportedLanguageCode? preferredLanguage, CancellationToken cancellationToken = default);
}
