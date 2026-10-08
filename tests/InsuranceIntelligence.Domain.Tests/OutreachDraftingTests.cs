using InsuranceIntelligence.Domain.Acquisition;

namespace InsuranceIntelligence.Domain.Tests;

public sealed class OutreachDraftingTests
{
    [Fact]
    public void An_introduction_greets_by_first_name_names_the_insurer_and_offers_a_way_out()
    {
        var text = OutreachDrafting.Introduction("Mavuno Bank", "Brian Okello", "As a boda boda rider, this cover may suit you.", "Boda Rider Accident pays UGX 50,000 a day.", "Nile Shield Assurance");

        Assert.StartsWith("Hello Brian,", text);
        Assert.Contains("Mavuno Bank's insurance team", text);
        Assert.Contains("provided by Nile Shield Assurance", text);
        Assert.Contains("As a boda boda rider", text);
        Assert.EndsWith(OutreachDrafting.OptOut, text);
    }

    [Fact]
    public void Without_a_reason_the_message_still_reads_naturally()
    {
        var text = OutreachDrafting.Introduction("Mavuno Bank", "Aisha", null, "Cover explained.", "Mpingo Life Assurance");

        Assert.DoesNotContain("  ", text);
        Assert.Contains("Cover explained.", text);
    }

    [Theory]
    [InlineData("Works as boda boda rider, which this cover is for (boda)", "As a boda boda rider, this cover may suit you.")]
    [InlineData("Works as boutique owner, boutique in Entebbe, which this cover is for (boutique)", "As a boutique owner, this cover may suit you.")]
    public void A_work_reason_becomes_a_line_that_speaks_to_the_customer(string reason, string expected) =>
        Assert.Equal(expected, OutreachDrafting.CustomerReason([reason]));

    [Fact]
    public void Only_work_and_banking_reasons_are_used_never_family_situation()
    {
        Assert.Null(OutreachDrafting.CustomerReason(["Family situation: 3 children", "Said they want it"]));
        Assert.Equal("As someone who banks with us, this cover may suit you.", OutreachDrafting.CustomerReason(["Family situation: 3 children", "Holds Savings account with the bank (savings)"]));
    }

    [Fact]
    public void A_missing_name_falls_back_to_there() =>
        Assert.Equal("there", OutreachDrafting.FirstName("  "));

    [Fact]
    public void A_follow_up_and_an_ending_policy_message_carry_the_opt_out()
    {
        Assert.EndsWith(OutreachDrafting.OptOut, OutreachDrafting.QuietFollowUp("Mavuno Bank", "Esther Nakato", "Market Stall Stock", "Nile Shield Assurance"));
        var ending = OutreachDrafting.PolicyEnding("Mavuno Bank", "Esther Nakato", "Market Stall Stock", new DateOnly(2026, 11, 3));
        Assert.Contains("3 November 2026", ending);
        Assert.EndsWith(OutreachDrafting.OptOut, ending);
    }
}
