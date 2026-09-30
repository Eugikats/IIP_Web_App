using InsuranceIntelligence.Domain.Customers;
using InsuranceIntelligence.Domain.CustomerAssistant;

namespace InsuranceIntelligence.Domain.Tests;

public sealed class CustomerMessageSignalsTests
{
    [Theory]
    [InlineData("256776678989", "+256 776 678989", true)]
    [InlineData("256776678989", "0776678989", true)]
    [InlineData("0776678989", "+256776678989", true)]
    [InlineData("256776678989", "256776678980", false)]
    [InlineData("256776678989", "254776678989", false)] // same national digits, different country
    [InlineData("256776678989", null, false)]
    [InlineData("256776678989", "", false)]
    public void Phone_numbers_match_across_the_ways_they_are_written(string first, string? second, bool expected) =>
        Assert.Equal(expected, PhoneNumbers.SameNumber(first, second));

    [Theory]
    [InlineData("Thanks", true)]
    [InlineData("ok thank you so much", true)]
    [InlineData("Webale nnyo", true)]
    [InlineData("Asante sana", true)]
    [InlineData("ok I had an accident", false)]
    [InlineData("thanks, what does my policy cover?", false)]
    public void Recognises_a_thanks_or_goodbye_only_when_that_is_all_it_is(string text, bool expected) =>
        Assert.Equal(expected, CustomerMessageSignals.IsThanksOrClose(text));

    [Theory]
    [InlineData("Am I talking to a bot?", true)]
    [InlineData("are you a real person", true)]
    [InlineData("I want to speak to a real person", false)]
    public void Recognises_a_question_about_whether_the_assistant_is_automated(string text, bool expected) =>
        Assert.Equal(expected, CustomerMessageSignals.IsAskingIfAutomated(text));

    [Theory]
    [InlineData("send me the link", true)]
    [InlineData("How do I upload my documents?", true)]
    [InlineData("I had an accident", false)]
    public void Recognises_a_request_for_the_upload_link(string text, bool expected) =>
        Assert.Equal(expected, CustomerMessageSignals.IsAccessLinkRequest(text));

    [Theory]
    [InlineData("My car was hit at the roundabout", "Motor")]
    [InlineData("the medical one", "Medical")]
    [InlineData("I was admitted to hospital last night", null)] // Medical and Hospital Cash both fit, so the customer is asked
    [InlineData("my father passed away", "Life")]
    [InlineData("I had an accident", "Boda Rider Accident")]
    [InlineData("the car crashed into the house", "Motor")]
    public void Reads_which_cover_a_message_points_at(string text, string? expected) =>
        Assert.Equal(expected, CustomerMessageSignals.ProductLineHint(text));

    [Theory]
    [InlineData("2", 2)]
    [InlineData("option 1", 1)]
    [InlineData("12", null)]
    [InlineData("motor", null)]
    public void Reads_a_numbered_choice(string text, int? expected) =>
        Assert.Equal(expected, CustomerMessageSignals.ChoiceNumber(text));

    [Theory]
    [InlineData("I had an accident", false)]
    [InlineData("I had an accident on Jinja Road this morning", true)]
    [InlineData("accident NSA/MO/2026/00009 NSA/MO/2026/00010", false)]
    public void Asks_for_more_before_opening_a_claim_on_a_thin_description(string text, bool expected) =>
        Assert.Equal(expected, CustomerMessageSignals.HasEnoughIncidentDetail(text));

    [Fact]
    public void Reads_the_incident_date_the_customer_implies()
    {
        var thursday = new DateOnly(2026, 9, 24);

        Assert.Equal(thursday, CustomerMessageSignals.IncidentDate("this morning on Jinja Road", thursday));
        Assert.Equal(thursday.AddDays(-1), CustomerMessageSignals.IncidentDate("it happened last night", thursday));
        Assert.Equal(thursday.AddDays(-2), CustomerMessageSignals.IncidentDate("the day before yesterday", thursday));
        Assert.Equal(new DateOnly(2026, 9, 21), CustomerMessageSignals.IncidentDate("on Monday evening", thursday));
    }

    [Fact]
    public void Formats_the_document_list_as_a_sentence()
    {
        Assert.Equal("a", ClaimDocumentChecklist.AsSentenceList(["a"]));
        Assert.Equal("a and b", ClaimDocumentChecklist.AsSentenceList(["a", "b"]));
        Assert.Equal("a, b and c", ClaimDocumentChecklist.AsSentenceList(["a", "b", "c"]));
    }
}
