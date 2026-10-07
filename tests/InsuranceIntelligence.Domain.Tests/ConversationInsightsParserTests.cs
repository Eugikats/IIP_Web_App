using InsuranceIntelligence.Application.CustomerAssistant;
using InsuranceIntelligence.Application.SalesLeads;
using InsuranceIntelligence.Domain.Acquisition;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.CustomerAssistant;

namespace InsuranceIntelligence.Domain.Tests;

/// <summary>
/// The rules that stand between the model and a prospect's record: a point must cite a real customer
/// message, a fact must use an allowed field and echo the customer's own words, and nothing sensitive
/// is kept, whatever the model returns.
/// </summary>
public sealed class ConversationInsightsParserTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 7, 9, 0, 0, TimeSpan.Zero);
    private static readonly TenantScope Scope = new("tenant-a", "workspace-a");

    private static readonly CustomerConversationTurn[] Window =
    [
        Turn("Hello, I have a shoe shop in Kalerwe and thieves broke into my neighbour's shop last week"),
        Turn("I make about 300k a month and I can pay maybe 15,000 for cover. Is it worth it?"),
    ];

    private static readonly InsightSource[] Sources =
    [
        new("M1", Window[0].TurnId, Now, "Hello, I have a shoe shop in Kalerwe"),
        new("M2", Window[1].TurnId, Now, "I make about 300k a month"),
    ];

    [Fact]
    public void Cited_points_and_grounded_facts_are_kept()
    {
        var result = ConversationInsightsParser.Parse("""
            {
              "summary": {"text": "Shoe shop owner in Kalerwe worried about theft.", "refs": ["M1","M2"]},
              "facts": [
                {"field": "occupation", "value": "shoe shop owner", "refs": ["M1"]},
                {"field": "area", "value": "Kalerwe", "refs": ["M1"]},
                {"field": "income_band", "value": "about UGX 300,000 a month", "refs": ["M2"]}
              ],
              "interests": [{"product": "Small Business Shop Cover", "strength": "interested", "text": "Worried about break-ins.", "refs": ["M1","M2"]}],
              "problems": [{"text": "A neighbour's shop was broken into.", "refs": ["M1"]}],
              "objections": [{"text": "Unsure cover is worth the price.", "refs": ["M2"]}],
              "tone": "curious",
              "next_step": {"text": "Show what the cover pays for a break-in.", "refs": ["M2"]}
            }
            """, Sources, Window, Now);

        Assert.Equal(0, result.Dropped);
        Assert.Equal(["occupation", "area", "income_band"], result.Facts.Select(fact => fact.Key));
        Assert.All(result.Facts, fact => Assert.Equal(ProspectFactSource.Customer, fact.Source));
        Assert.Equal(Window[0].TurnId, result.Facts[0].TurnId);
        var interest = Assert.Single(result.Interests);
        Assert.Equal(MicroinsuranceCatalog.ShopCover.Name, interest.Product);
        Assert.Equal(InterestStrength.Interested, interest.Strength);
        Assert.NotNull(result.Insights.Summary);
        Assert.NotNull(result.Insights.NextStep);
        Assert.Single(result.Insights.Problems);
        Assert.Single(result.Insights.Objections);
    }

    [Fact]
    public void A_point_citing_no_real_message_is_dropped()
    {
        var result = ConversationInsightsParser.Parse("""
            {
              "summary": {"text": "Owns three shops.", "refs": ["M9"]},
              "problems": [{"text": "Was robbed twice.", "refs": []}],
              "next_step": {"text": "Call them.", "refs": ["M2"]}
            }
            """, Sources, Window, Now);

        Assert.Null(result.Insights.Summary);
        Assert.Empty(result.Insights.Problems);
        Assert.NotNull(result.Insights.NextStep);
        Assert.Equal(2, result.Dropped);
    }

    [Fact]
    public void A_fact_the_customer_did_not_say_in_the_cited_message_is_dropped()
    {
        var result = ConversationInsightsParser.Parse("""
            {"facts": [
              {"field": "occupation", "value": "boda boda rider", "refs": ["M1"]},
              {"field": "area", "value": "Kalerwe", "refs": ["M2"]}
            ]}
            """, Sources, Window, Now);

        Assert.Empty(result.Facts);
        Assert.Equal(2, result.Dropped);
    }

    [Fact]
    public void Fields_off_the_list_and_sensitive_content_are_never_kept()
    {
        var result = ConversationInsightsParser.Parse("""
            {
              "facts": [{"field": "religion", "value": "shop", "refs": ["M1"]}],
              "problems": [{"text": "Seems to belong to an opposition party member family.", "refs": ["M1"]}],
              "tone": "religious"
            }
            """, Sources, Window, Now);

        Assert.Empty(result.Facts);
        Assert.Empty(result.Insights.Problems);
        Assert.Null(result.Insights.Tone);
    }

    [Fact]
    public void An_unknown_product_is_kept_only_when_the_customer_named_it()
    {
        var result = ConversationInsightsParser.Parse("""
            {"interests": [
              {"product": "Travel insurance", "strength": "asked", "text": "", "refs": ["M1"]},
              {"product": "shoe shop", "strength": "asked", "text": "", "refs": ["M1"]}
            ]}
            """, Sources, Window, Now);

        var interest = Assert.Single(result.Interests);
        Assert.Equal("shoe shop", interest.Product);
        Assert.Equal(InterestStrength.Mentioned, interest.Strength);
    }

    [Fact]
    public void A_reply_that_is_not_json_yields_no_points_and_no_facts()
    {
        var result = ConversationInsightsParser.Parse("Sorry, I cannot help with that.", Sources, Window, Now);

        Assert.Null(result.Insights.Summary);
        Assert.Empty(result.Facts);
        Assert.Empty(result.Interests);
    }

    [Fact]
    public void Asking_about_a_cover_makes_someone_a_prospect_and_not_now_alone_does_not()
    {
        ProspectInterest Interest(InterestStrength strength) => new("Small Business Shop Cover", strength, "", Now);

        Assert.False(ConversationInsightsService.IsProspect([]));
        Assert.False(ConversationInsightsService.IsProspect([Interest(InterestStrength.NotNow)]));
        Assert.True(ConversationInsightsService.IsProspect([Interest(InterestStrength.Mentioned)]));
        Assert.True(ConversationInsightsService.IsProspect([Interest(InterestStrength.NotNow), Interest(InterestStrength.WantsIt)]));
    }

    private static CustomerConversationTurn Turn(string text) => new()
    {
        TurnId = Guid.NewGuid(),
        Scope = Scope,
        ThreadId = "thread-1",
        ContactWaId = "256776678989",
        InboundMessageText = text,
        ClassifiedIntent = CustomerIntent.GeneralQuestion,
        Outcome = CustomerAssistantOutcome.AutoReplied,
        OccurredAt = Now,
        CorrelationId = "c",
    };
}
