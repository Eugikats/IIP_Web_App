using InsuranceIntelligence.Domain.Common;
using Xunit;

namespace InsuranceIntelligence.Domain.Tests;

/// <summary>
/// Every model completion passes through DisplayText before a person reads it. Dash characters are
/// built from code points here, as in the implementation, so no repository-wide clean-up can quietly
/// change what these tests check.
/// </summary>
public sealed class DisplayTextTests
{
    private static readonly string Em = ((char)0x2014).ToString();
    private static readonly string En = ((char)0x2013).ToString();

    [Fact]
    public void A_spaced_em_dash_becomes_a_comma()
    {
        Assert.Equal(
            "The claim is complete, a person still decides.",
            DisplayText.WithoutEmDashes($"The claim is complete {Em} a person still decides."));
    }

    [Fact]
    public void A_spaced_en_dash_used_as_an_em_dash_becomes_a_comma()
    {
        Assert.Equal("Documents received, review pending.", DisplayText.WithoutEmDashes($"Documents received {En} review pending."));
    }

    [Fact]
    public void An_unspaced_em_dash_becomes_a_hyphen()
    {
        Assert.Equal("well-known", DisplayText.WithoutEmDashes($"well{Em}known"));
    }

    [Fact]
    public void Ordinary_hyphens_and_number_ranges_are_left_alone()
    {
        // The regression this guards against: with the em dash constant rewritten to a hyphen,
        // " - " became ", " and a range such as 2020-2021 was untouched while real em dashes leaked.
        const string text = "Claim CLM-726E3445 - see note; cover 2020-2021";
        Assert.Equal(text, DisplayText.WithoutEmDashes(text));
        Assert.Equal($"2020{En}2021", DisplayText.WithoutEmDashes($"2020{En}2021"));
    }

    [Fact]
    public void No_em_dash_survives()
    {
        var result = DisplayText.WithoutEmDashes($"{Em} Overview {Em} first {Em}second{Em} last {Em}");

        Assert.DoesNotContain(Em, result);
    }

    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    public void Empty_input_is_returned_as_empty(string? input, string expected) =>
        Assert.Equal(expected, DisplayText.WithoutEmDashes(input));
}
