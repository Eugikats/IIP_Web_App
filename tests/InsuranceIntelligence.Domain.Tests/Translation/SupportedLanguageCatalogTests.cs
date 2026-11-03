using InsuranceIntelligence.Domain.Translation;

namespace InsuranceIntelligence.Domain.Tests.Translation;

public sealed class SupportedLanguageCatalogTests
{
    [Fact]
    public void Catalog_codes_and_provider_codes_are_unique_and_round_trip()
    {
        Assert.Equal(SupportedLanguage.All.Count, SupportedLanguage.All.Select(language => language.Code).Distinct().Count());
        Assert.Equal(SupportedLanguage.All.Count, SupportedLanguage.All.Select(language => language.Iso).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal(SupportedLanguage.All.Count, SupportedLanguage.All.Select(language => language.SunbirdModelCode).Distinct(StringComparer.OrdinalIgnoreCase).Count());

        foreach (var language in SupportedLanguage.All)
        {
            Assert.Equal(language, SupportedLanguage.FromIso(language.Iso));
            Assert.Equal(language, SupportedLanguage.FromIso(language.SunbirdModelCode));
        }
    }

    [Fact]
    public void Catalog_covers_the_published_text_set_and_keeps_voice_separate()
    {
        Assert.Equal(70, SupportedLanguage.All.Count);
        Assert.Equal(TranslationQualityTier.Good, SupportedLanguage.For(SupportedLanguageCode.Lg).TranslationTier);
        Assert.Equal(TranslationQualityTier.Moderate, SupportedLanguage.For(SupportedLanguageCode.Ach).TranslationTier);
        Assert.Equal(TranslationQualityTier.Basic, SupportedLanguage.For(SupportedLanguageCode.Kab).TranslationTier);
        Assert.Equal(2, SupportedLanguage.All.Count(language => language.VoiceInputAvailable && language.VoiceOutputAvailable));
    }
}
