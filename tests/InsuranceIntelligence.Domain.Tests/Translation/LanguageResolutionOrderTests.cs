using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Translation;

namespace InsuranceIntelligence.Domain.Tests.Translation;

public sealed class LanguageResolutionOrderTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 25, 10, 0, 0, TimeSpan.Zero);
    private static readonly TenantScope Scope = TenantScope.Create("tenant-a", "workspace-a");
    private static readonly LanguageResolution NoSignal = new(SupportedLanguageCode.En, LanguageResolutionSource.Detected, 0);

    private static CustomerLanguagePreference Preference(SupportedLanguageCode language, DateTimeOffset setAt) => new()
    {
        PreferenceId = Guid.NewGuid(),
        Scope = Scope,
        ContactWaId = "256772000000",
        Language = language,
        SetAt = setAt,
    };

    [Fact]
    public void Step_1_a_fresh_remembered_preference_wins_with_no_other_signal()
    {
        var remembered = Preference(SupportedLanguageCode.Sw, Now - TimeSpan.FromDays(10));

        var result = LanguageResolutionOrder.Resolve("Habari, nina swali", previousMessageWasChoicePrompt: false, remembered, NoSignal, Now);

        Assert.Equal(SupportedLanguageCode.Sw, result.Language);
        Assert.Equal(LanguageResolutionSource.RememberedPreference, result.Source);
    }

    [Fact]
    public void An_expired_preference_is_not_used()
    {
        var remembered = Preference(SupportedLanguageCode.Sw, Now - LanguageResolutionOrder.PreferenceFreshness - TimeSpan.FromDays(1));

        var result = LanguageResolutionOrder.Resolve("I had an accident yesterday on Jinja Road", previousMessageWasChoicePrompt: false, remembered, NoSignal, Now);

        Assert.NotEqual(LanguageResolutionSource.RememberedPreference, result.Source);
    }

    [Fact]
    public void Step_2_an_explicit_request_beats_a_remembered_preference()
    {
        var remembered = Preference(SupportedLanguageCode.Sw, Now - TimeSpan.FromDays(1));

        var result = LanguageResolutionOrder.Resolve("Please reply in Luganda from now on", previousMessageWasChoicePrompt: false, remembered, NoSignal, Now);

        Assert.Equal(SupportedLanguageCode.Lg, result.Language);
        Assert.Equal(LanguageResolutionSource.ExplicitRequest, result.Source);
    }

    [Fact]
    public void Step_3_a_confident_detection_is_used_when_nothing_else_applies()
    {
        var detected = new LanguageResolution(SupportedLanguageCode.Sw, LanguageResolutionSource.Detected, 0.9);

        var result = LanguageResolutionOrder.Resolve(
            "Habari, nilikuwa na ajali njiani", previousMessageWasChoicePrompt: false, rememberedPreference: null, detected, Now);

        Assert.Equal(SupportedLanguageCode.Sw, result.Language);
        Assert.Equal(LanguageResolutionSource.Detected, result.Source);
    }

    [Fact]
    public void A_low_confidence_detection_is_not_trusted_on_its_own()
    {
        var weaklyDetected = new LanguageResolution(SupportedLanguageCode.Sw, LanguageResolutionSource.Detected, 0.2);

        var result = LanguageResolutionOrder.Resolve(
            "Something ambiguous here today", previousMessageWasChoicePrompt: false, rememberedPreference: null, weaklyDetected, Now);

        Assert.Equal(LanguageResolutionSource.PromptedChoice, result.Source);
    }

    [Fact]
    public void Step_4_nothing_resolves_it_so_the_assistant_must_ask()
    {
        var result = LanguageResolutionOrder.Resolve(
            "hmm", previousMessageWasChoicePrompt: false, rememberedPreference: null, NoSignal, Now);

        Assert.Equal(LanguageResolutionSource.PromptedChoice, result.Source);
        Assert.Equal(SupportedLanguageCode.En, result.Language);
    }

    [Fact]
    public void A_short_reply_never_flips_a_remembered_language_even_with_a_strong_detection()
    {
        var remembered = Preference(SupportedLanguageCode.Sw, Now - TimeSpan.FromDays(1));
        var strongOppositeDetection = new LanguageResolution(SupportedLanguageCode.Lg, LanguageResolutionSource.Detected, 0.95);

        // "ok" carries a stray Luganda-list token in isolation only by coincidence in a longer
        // message; as a short reply it must never override the remembered language.
        var result = LanguageResolutionOrder.Resolve("ok", previousMessageWasChoicePrompt: false, remembered, strongOppositeDetection, Now);

        Assert.Equal(SupportedLanguageCode.Sw, result.Language);
        Assert.Equal(LanguageResolutionSource.RememberedPreference, result.Source);
    }

    [Fact]
    public void A_bare_policy_number_never_flips_a_remembered_language()
    {
        var remembered = Preference(SupportedLanguageCode.En, Now - TimeSpan.FromDays(1));
        var strongOppositeDetection = new LanguageResolution(SupportedLanguageCode.Lg, LanguageResolutionSource.Detected, 0.95);

        var result = LanguageResolutionOrder.Resolve("NSA/MO/2026/00033", previousMessageWasChoicePrompt: false, remembered, strongOppositeDetection, Now);

        Assert.Equal(SupportedLanguageCode.En, result.Language);
        Assert.Equal(LanguageResolutionSource.RememberedPreference, result.Source);
    }

    [Fact]
    public void A_long_message_with_high_confidence_detection_can_override_a_remembered_preference()
    {
        var remembered = Preference(SupportedLanguageCode.En, Now - TimeSpan.FromDays(1));
        var strongDetection = new LanguageResolution(SupportedLanguageCode.Sw, LanguageResolutionSource.Detected, 0.9);

        var result = LanguageResolutionOrder.Resolve(
            "Habari, nilikuwa na ajali njiani leo asubuhi", previousMessageWasChoicePrompt: false, remembered, strongDetection, Now);

        Assert.Equal(SupportedLanguageCode.Sw, result.Language);
    }

    [Fact]
    public void A_digit_answer_to_our_own_prompt_is_resolved_and_treated_as_explicit()
    {
        var result = LanguageResolutionOrder.Resolve("2", previousMessageWasChoicePrompt: true, rememberedPreference: null, NoSignal, Now);

        Assert.Equal(SupportedLanguageCode.Lg, result.Language);
        Assert.Equal(LanguageResolutionSource.ExplicitRequest, result.Source);
    }

    [Fact]
    public void A_non_digit_reply_to_our_prompt_falls_through_to_the_normal_order()
    {
        var remembered = Preference(SupportedLanguageCode.Sw, Now - TimeSpan.FromDays(1));

        var result = LanguageResolutionOrder.Resolve("what do you mean", previousMessageWasChoicePrompt: true, remembered, NoSignal, Now);

        Assert.Equal(LanguageResolutionSource.RememberedPreference, result.Source);
    }
}
