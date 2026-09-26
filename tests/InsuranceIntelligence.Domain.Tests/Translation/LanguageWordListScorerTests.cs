using InsuranceIntelligence.Domain.Translation;

namespace InsuranceIntelligence.Domain.Tests.Translation;

public sealed class LanguageWordListScorerTests
{
    [Fact]
    public void Luganda_vocabulary_scores_as_luganda_with_real_confidence()
    {
        var result = LanguageWordListScorer.Score("Oli otya, nina akabenje, nkwagala okumanya ensimbi");

        Assert.Equal(SupportedLanguageCode.Lg, result.Language);
        Assert.True(result.Confidence > 0.3, $"expected meaningful confidence, got {result.Confidence}");
    }

    [Fact]
    public void Kiswahili_vocabulary_scores_as_kiswahili_with_real_confidence()
    {
        var result = LanguageWordListScorer.Score("Habari, nilikuwa na ajali, naomba msaada wa haraka tafadhali");

        Assert.Equal(SupportedLanguageCode.Sw, result.Language);
        Assert.True(result.Confidence > 0.3, $"expected meaningful confidence, got {result.Confidence}");
    }

    [Fact]
    public void Plain_english_scores_as_english()
    {
        var result = LanguageWordListScorer.Score("Thank you for your help with our claim and policy");

        Assert.Equal(SupportedLanguageCode.En, result.Language);
        Assert.True(result.Confidence > 0, "an unmistakably English sentence should score above zero");
    }

    [Fact]
    public void Text_with_no_recognisable_signal_scores_zero_confidence()
    {
        var result = LanguageWordListScorer.Score("xk qq zz vv");

        Assert.Equal(SupportedLanguageCode.En, result.Language);
        Assert.Equal(0, result.Confidence);
    }

    [Fact]
    public void Empty_text_scores_zero_confidence()
    {
        var result = LanguageWordListScorer.Score("");

        Assert.Equal(0, result.Confidence);
    }

    [Theory]
    [InlineData("Nimepata ajali na gari langu leo asubuhi kwenye barabara ya Jinja, sera yangu ni NSA/MO/2026/00009")]
    [InlineData("Naomba kujua hali ya madai yangu ya bima")]
    [InlineData("Je, bima yangu inalipia kioo cha gari kilichovunjika")]
    public void A_realistic_swahili_claim_sentence_is_detected_with_confidence(string text)
    {
        var result = LanguageWordListScorer.Score(text);

        Assert.Equal(SupportedLanguageCode.Sw, result.Language);
        Assert.True(result.Confidence >= 0.5, $"confidence was {result.Confidence}");
    }

    [Theory]
    [InlineData("Nfunye akabenje ku motoka yange leero ku makya")]
    [InlineData("Nfunye akabenje leero nsaba mumbeeko policy yange NSA/MO/2026/00009")]
    public void A_realistic_luganda_claim_sentence_is_detected_with_confidence(string text)
    {
        var result = LanguageWordListScorer.Score(text);

        Assert.Equal(SupportedLanguageCode.Lg, result.Language);
        Assert.True(result.Confidence >= 0.5, $"confidence was {result.Confidence}");
    }

    [Theory]
    [InlineData("I was in an accident today, my policy is NSA/MO/2026/00009 please help")]
    [InlineData("What is the status of my claim on my motor policy")]
    [InlineData("Is windscreen damage covered by my comprehensive policy")]
    public void An_english_claim_sentence_is_never_mistaken_for_a_local_language(string text)
    {
        var result = LanguageWordListScorer.Score(text);

        Assert.Equal(SupportedLanguageCode.En, result.Language);
    }
}
