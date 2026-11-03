using InsuranceIntelligence.Domain.Translation;

namespace InsuranceIntelligence.Domain.Tests.Translation;

public sealed class ExplicitLanguageRequestParserTests
{
    [Fact]
    public void Reply_in_luganda_is_recognised()
    {
        Assert.Equal(SupportedLanguageCode.Lg, ExplicitLanguageRequestParser.Parse("Please reply in Luganda"));
    }

    [Fact]
    public void Kiswahili_tafadhali_is_recognised()
    {
        Assert.Equal(SupportedLanguageCode.Sw, ExplicitLanguageRequestParser.Parse("Kiswahili tafadhali"));
    }

    [Fact]
    public void Swahili_without_the_ki_prefix_is_also_recognised()
    {
        Assert.Equal(SupportedLanguageCode.Sw, ExplicitLanguageRequestParser.Parse("can we talk in swahili"));
    }

    [Theory]
    [InlineData("Please reply in Acholi", SupportedLanguageCode.Ach)]
    [InlineData("Can we use Kinyarwanda?", SupportedLanguageCode.Kin)]
    [InlineData("Reply in Runyankole", SupportedLanguageCode.Nyn)]
    public void A_language_from_the_extended_catalog_is_recognised(string text, SupportedLanguageCode expected)
    {
        Assert.Equal(expected, ExplicitLanguageRequestParser.Parse(text));
    }

    [Fact]
    public void A_request_for_english_is_recognised()
    {
        Assert.Equal(SupportedLanguageCode.En, ExplicitLanguageRequestParser.Parse("switch to English please"));
    }

    [Fact]
    public void A_message_naming_no_language_returns_null()
    {
        Assert.Null(ExplicitLanguageRequestParser.Parse("I had an accident yesterday"));
    }

    [Fact]
    public void A_language_name_inside_another_word_is_not_treated_as_a_request()
    {
        Assert.Null(ExplicitLanguageRequestParser.Parse("Habari"));
    }

    [Fact]
    public void Blank_text_returns_null()
    {
        Assert.Null(ExplicitLanguageRequestParser.Parse("   "));
    }
}
