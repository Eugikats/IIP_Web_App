using InsuranceIntelligence.Domain.Acquisition;

namespace InsuranceIntelligence.Domain.Tests;

public sealed class ProspectsTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 7, 9, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("sells shoes", "I sell shoes in Kalerwe")]
    [InlineData("Kalerwe", "I sell shoes in Kalerwe")]
    [InlineData("UGX 300,000 a month", "I make about 300k every month")]
    [InlineData("2 children", "I have 2 kids")]
    public void A_fact_is_grounded_when_the_customer_wrote_it(string value, string customerSaid) =>
        Assert.True(ProspectFacts.IsGroundedIn(value, [customerSaid]));

    [Theory]
    [InlineData("boda boda rider", "I sell shoes in Kalerwe")]
    [InlineData("UGX 500,000", "I make about 300k every month")]
    [InlineData("the", "the shop")]
    public void A_fact_the_customer_never_said_is_not_grounded(string value, string customerSaid) =>
        Assert.False(ProspectFacts.IsGroundedIn(value, [customerSaid]));

    [Fact]
    public void Nothing_is_grounded_in_an_empty_conversation() =>
        Assert.False(ProspectFacts.IsGroundedIn("shoes", [null, ""]));

    [Theory]
    [InlineData("occupation", true)]
    [InlineData("Monthly Budget", true)]
    [InlineData("religion", false)]
    [InlineData("tribe", false)]
    [InlineData("health_condition", false)]
    public void Only_listed_fields_can_be_recorded(string key, bool allowed) =>
        Assert.Equal(allowed, ProspectFacts.Find(key) is not null);

    [Fact]
    public void A_new_value_replaces_the_old_one_for_the_same_field()
    {
        var merged = ProspectFacts.Merge(
            [new ProspectFact("area", "Kalerwe", ProspectFactSource.Customer, Now)],
            [new ProspectFact("area", "Ntinda", ProspectFactSource.Customer, Now.AddMinutes(5)), new ProspectFact("occupation", "shop owner", ProspectFactSource.Customer, Now)]);

        Assert.Equal(2, merged.Count);
        Assert.Equal("Ntinda", merged.Single(fact => fact.Key == "area").Value);
    }

    [Fact]
    public void Missing_lists_the_unanswered_discovery_questions()
    {
        var missing = ProspectFacts.Missing([new ProspectFact("occupation", "shop owner", ProspectFactSource.Customer, Now)]);

        Assert.DoesNotContain(missing, field => field.Key == "occupation");
        Assert.Contains(missing, field => field.Key == "monthly_budget");
        Assert.DoesNotContain(missing, field => field.Key == "email");
    }

    [Fact]
    public void Stage_is_new_with_nothing_recorded() =>
        Assert.Equal(LeadStage.New, LeadStages.Derive([], [], applied: false, withAPerson: false));

    [Fact]
    public void Stage_follows_the_strongest_interest_and_how_much_is_known()
    {
        var facts = new[]
        {
            new ProspectFact("occupation", "shop owner", ProspectFactSource.Customer, Now),
            new ProspectFact("area", "Kalerwe", ProspectFactSource.Customer, Now),
            new ProspectFact("monthly_budget", "20,000", ProspectFactSource.Customer, Now),
        };
        ProspectInterest Interest(InterestStrength strength) => new("Small Business Shop Cover", strength, "", Now);

        Assert.Equal(LeadStage.GettingToKnow, LeadStages.Derive(facts, [], false, false));
        Assert.Equal(LeadStage.Interested, LeadStages.Derive(facts[..1], [Interest(InterestStrength.Interested)], false, false));
        Assert.Equal(LeadStage.Considering, LeadStages.Derive(facts, [Interest(InterestStrength.Interested)], false, false));
        Assert.Equal(LeadStage.ReadyToApply, LeadStages.Derive(facts, [Interest(InterestStrength.WantsIt)], false, false));
        Assert.Equal(LeadStage.NotNow, LeadStages.Derive(facts, [Interest(InterestStrength.NotNow)], false, false));
        Assert.Equal(LeadStage.Applied, LeadStages.Derive(facts, [Interest(InterestStrength.WantsIt)], applied: true, withAPerson: false));
        Assert.Equal(LeadStage.WithAPerson, LeadStages.Derive(facts, [], applied: true, withAPerson: true));
    }

    [Theory]
    [InlineData("+256776678989", "25***89")]
    [InlineData("256 700 000 101", "25***01")]
    [InlineData("123", "Hidden")]
    [InlineData(null, "Hidden")]
    public void Contacts_are_masked_for_lists(string? phone, string expected) =>
        Assert.Equal(expected, ContactMask.Mask(phone));
}
