using InsuranceIntelligence.Domain.Translation;

namespace InsuranceIntelligence.Domain.Tests.Translation;

public sealed class TranslationQualityJudgeTests
{
    [Fact]
    public void Empty_output_is_rejected()
    {
        var reason = TranslationQualityJudge.Judge("Oli otya", "   ", candidateShouldNotBeEnglish: true);

        Assert.Equal(TranslationFailureReason.Empty, reason);
    }

    [Fact]
    public void Output_identical_to_the_source_is_rejected_as_unchanged()
    {
        var reason = TranslationQualityJudge.Judge("Nina bima", "Nina bima", candidateShouldNotBeEnglish: true);

        Assert.Equal(TranslationFailureReason.Unchanged, reason);
    }

    [Fact]
    public void Echoed_instruction_commentary_is_rejected()
    {
        var reason = TranslationQualityJudge.Judge(
            "Nina bima",
            "You are a professional Swahili to English translator producing the translation",
            candidateShouldNotBeEnglish: false);

        Assert.Equal(TranslationFailureReason.EchoedPrompt, reason);
    }

    [Fact]
    public void A_prefix_of_commentary_is_stripped_before_judging_the_rest()
    {
        var cleaned = TranslationQualityJudge.Clean("Here is the translation: Nkwagala nnyo");

        Assert.Equal("Nkwagala nnyo", cleaned);
    }

    [Fact]
    public void A_looping_phrase_is_rejected_as_a_repetition_loop()
    {
        var reason = TranslationQualityJudge.Judge(
            "Please share your policy number",
            "tuma nambari tuma nambari tuma nambari tuma nambari",
            candidateShouldNotBeEnglish: true);

        Assert.Equal(TranslationFailureReason.RepetitionLoop, reason);
    }

    [Fact]
    public void An_output_many_times_longer_than_the_source_is_rejected_as_a_length_blowup()
    {
        var source = "hello there";
        var blownUp = string.Join(' ', Enumerable.Range(0, 50).Select(index => $"neno{index}"));

        var reason = TranslationQualityJudge.Judge(source, blownUp, candidateShouldNotBeEnglish: true);

        Assert.Equal(TranslationFailureReason.LengthBlowup, reason);
    }

    [Fact]
    public void An_output_that_still_reads_as_english_is_rejected_when_translating_out_of_english()
    {
        var reason = TranslationQualityJudge.Judge(
            "Nina bima",
            "Thank you for your claim and please have this policy with our team",
            candidateShouldNotBeEnglish: true);

        Assert.Equal(TranslationFailureReason.StillEnglish, reason);
    }

    [Fact]
    public void A_short_reply_never_trips_the_still_english_check()
    {
        var reason = TranslationQualityJudge.Judge("hi", "the end", candidateShouldNotBeEnglish: true);

        Assert.Null(reason);
    }

    [Fact]
    public void The_still_english_check_is_skipped_on_retry()
    {
        var reason = TranslationQualityJudge.Judge(
            "Nina bima",
            "Thank you for your claim and please have this policy with our team",
            candidateShouldNotBeEnglish: false);

        Assert.Null(reason);
    }

    [Fact]
    public void Non_latin_output_for_a_latin_script_language_is_rejected_as_garbled()
    {
        var reason = TranslationQualityJudge.Judge("nina bima", "яимаб анин яэпми", candidateShouldNotBeEnglish: true);

        Assert.Equal(TranslationFailureReason.GarbledScript, reason);
    }

    [Fact]
    public void A_clean_plausible_translation_passes()
    {
        var reason = TranslationQualityJudge.Judge("I had an accident", "Nalina akabenje", candidateShouldNotBeEnglish: true);

        Assert.Null(reason);
    }
}
