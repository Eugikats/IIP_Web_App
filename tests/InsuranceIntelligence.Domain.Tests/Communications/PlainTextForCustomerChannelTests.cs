using InsuranceIntelligence.Domain.Communications;

namespace InsuranceIntelligence.Domain.Tests.Communications;

/// <summary>
/// This text goes to a real policyholder over SMS/WhatsApp, where no markdown renders, so the
/// only acceptable outcome is that no markdown syntax survives the conversion.
/// </summary>
public sealed class PlainTextForCustomerChannelTests
{
    [Theory]
    [InlineData("**bold**", "bold")]
    [InlineData("*italic*", "italic")]
    [InlineData("_italic_", "italic")]
    [InlineData("`code`", "code")]
    [InlineData("## Heading", "Heading")]
    [InlineData("no markup at all", "no markup at all")]
    public void Convert_StripsInlineMarkersAndKeepsTheWords(string input, string expected)
    {
        Assert.Equal(expected, PlainTextForCustomerChannel.Convert(input));
    }

    [Fact]
    public void Convert_KeepsBoldTextWhenItSitsInsideASentence()
    {
        var result = PlainTextForCustomerChannel.Convert("A **completed claim form** is required.");

        Assert.Equal("A completed claim form is required.", result);
    }

    [Fact]
    public void Convert_TurnsBulletsIntoReadableLinesRatherThanARunOn()
    {
        var result = PlainTextForCustomerChannel.Convert("Required:\n- A claim form\n- A police report");

        Assert.Equal("Required:\n• A claim form\n• A police report", result);
    }

    [Fact]
    public void Convert_KeepsNumberingOnOrderedLists_BecauseTheOrderCarriesMeaning()
    {
        var result = PlainTextForCustomerChannel.Convert("1. Report the loss\n2. Submit the form");

        Assert.Equal("1. Report the loss\n2. Submit the form", result);
    }

    [Fact]
    public void Convert_CollapsesRunsOfBlankLinesButKeepsParagraphSeparation()
    {
        var result = PlainTextForCustomerChannel.Convert("First para.\n\n\n\nSecond para.");

        Assert.Equal("First para.\n\nSecond para.", result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Convert_EmptyInput_ReturnsEmptyRatherThanThrowing(string? input)
    {
        Assert.Equal(string.Empty, PlainTextForCustomerChannel.Convert(input));
    }

    [Fact]
    public void Convert_RealAssistantAnswer_LeavesNoMarkdownSyntaxForTheCustomerToSee()
    {
        // The exact shape reported from the deployed app, which was being sent verbatim.
        const string answer = """
            The approved source does not clearly state **what "fire" covers**, so **human review is required**.

            - A **completed claim form** and **photographic evidence of the loss** are required in every case.
            - For **fire claims**, a **fire brigade or police report** is required.
            """;

        var result = PlainTextForCustomerChannel.Convert(answer);

        Assert.DoesNotContain("**", result);
        Assert.DoesNotContain("- ", result);
        Assert.Contains("completed claim form", result);
        Assert.Contains("• ", result);
    }
}
