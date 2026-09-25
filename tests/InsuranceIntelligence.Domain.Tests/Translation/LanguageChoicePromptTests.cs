using InsuranceIntelligence.Domain.Translation;

namespace InsuranceIntelligence.Domain.Tests.Translation;

public sealed class LanguageChoicePromptTests
{
    [Theory]
    [InlineData("1", SupportedLanguageCode.En)]
    [InlineData(" 2 ", SupportedLanguageCode.Lg)]
    [InlineData("3", SupportedLanguageCode.Sw)]
    public void Each_digit_maps_to_its_language(string reply, SupportedLanguageCode expected)
    {
        Assert.Equal(expected, LanguageChoicePrompt.ParseAnswer(reply));
    }

    [Fact]
    public void Anything_else_is_not_a_valid_answer()
    {
        Assert.Null(LanguageChoicePrompt.ParseAnswer("4"));
        Assert.Null(LanguageChoicePrompt.ParseAnswer("Luganda please"));
        Assert.Null(LanguageChoicePrompt.ParseAnswer(""));
    }
}
