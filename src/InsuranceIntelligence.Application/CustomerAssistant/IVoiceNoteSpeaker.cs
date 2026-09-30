using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Translation;

namespace InsuranceIntelligence.Application.CustomerAssistant;

/// <summary>
/// Speaks a reply so a customer who sent a voice note hears the answer in their own language. It
/// only ever speaks text Kato has already decided to send; it never composes anything itself.
/// </summary>
public interface IVoiceNoteSpeaker
{
    /// <summary>An OGG/Opus voice note of the text, or null when it cannot be spoken (no engine,
    /// engine failed). Never throws for those: the caller sends the text instead.</summary>
    Task<byte[]?> SpeakAsync(TenantScope scope, string text, SupportedLanguageCode language, CancellationToken cancellationToken = default);
}
