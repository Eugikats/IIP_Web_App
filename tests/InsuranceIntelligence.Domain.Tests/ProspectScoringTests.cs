using InsuranceIntelligence.Domain.Acquisition;

namespace InsuranceIntelligence.Domain.Tests;

public sealed class ProspectScoringTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 7, 9, 0, 0, TimeSpan.Zero);

    private static readonly ProductTargetProfile Boda = new("Boda Accident", "Boda Rider Accident", "Nile Shield", 3_000m, ["boda", "rider"], [], [], ["accident"]);
    private static readonly ProductTargetProfile Funeral = new("Family Funeral", "Family Funeral", "Mpingo", 5_000m, [], ["savings"], ["children", "parents"], ["funeral"], 15);
    private static readonly ProductTargetProfile Hospital = new("Hospital Cash", "Hospital Cash", "Nile Shield", 1_000m, [], ["salary"], ["family"], ["hospital"], 30, "Almost anyone.");
    private static readonly ProductTargetProfile[] Profiles = [Boda, Funeral, Hospital];

    private static ProspectFact Fact(string key, string value) => new(key, value, ProspectFactSource.Customer, Now);

    private static ProspectInputs Inputs(
        IReadOnlyList<ProspectFact>? facts = null,
        IReadOnlyList<ProspectInterest>? interests = null,
        ContactConsentStatus consent = ContactConsentStatus.NotRecorded,
        bool? bank = null,
        int messages = 0) =>
        new(facts ?? [], interests ?? [], bank, false, false, consent, true, Now.AddDays(-1), messages, false);

    [Fact]
    public void A_boda_rider_who_wants_cover_is_qualified_and_gets_the_boda_cover_first()
    {
        var triage = ProspectScoring.Score(
            Inputs(
                [Fact("occupation", "boda rider"), Fact("monthly_budget", "UGX 10,000 a month"), Fact("worries", "accident on the road")],
                [new ProspectInterest("Boda Accident", InterestStrength.WantsIt, "asked", Now)],
                ContactConsentStatus.Agreed,
                bank: true,
                messages: 6),
            Profiles,
            Now);

        Assert.Equal("Boda Accident", triage.Fits[0].Product);
        Assert.Contains(triage.Fits[0].Reasons, reason => reason.Contains("boda rider", StringComparison.OrdinalIgnoreCase));
        Assert.True(triage.Score >= 80, $"score was {triage.Score}");
        Assert.Equal(ReadinessBand.HighPriority, triage.Band);
        Assert.Equal("High", triage.Confidence);
    }

    [Fact]
    public void Only_two_covers_are_recommended_and_each_has_a_reason()
    {
        var triage = ProspectScoring.Score(
            Inputs([Fact("occupation", "boda rider"), Fact("dependants", "3 children"), Fact("bank_products", "savings account")]),
            Profiles,
            Now);

        Assert.Equal(2, triage.Fits.Count);
        Assert.All(triage.Fits, fit => Assert.NotEmpty(fit.Reasons));
    }

    [Fact]
    public void Reasons_quote_what_is_recorded_not_what_is_guessed()
    {
        var triage = ProspectScoring.Score(Inputs([Fact("dependants", "2 children"), Fact("bank_products", "savings account")]), Profiles, Now);

        var funeral = triage.Fits.Single(fit => fit.Product == "Family Funeral");
        Assert.Contains(funeral.Reasons, reason => reason.Contains("2 children"));
        Assert.Contains(funeral.Reasons, reason => reason.Contains("savings account"));
        Assert.DoesNotContain(triage.Fits, fit => fit.Product == "Boda Accident");
    }

    [Fact]
    public void Someone_who_said_not_now_stays_cold()
    {
        var triage = ProspectScoring.Score(
            Inputs([Fact("occupation", "boda rider")], [new ProspectInterest("Boda Accident", InterestStrength.NotNow, "later", Now)], bank: true),
            Profiles,
            Now);

        Assert.True(triage.Score <= 20);
        Assert.Equal(ReadinessBand.Cold, triage.Band);
    }

    [Theory]
    [InlineData(ContactConsentStatus.Declined)]
    [InlineData(ContactConsentStatus.Withdrawn)]
    public void Someone_who_does_not_want_contact_is_never_ranked_for_outreach(ContactConsentStatus consent)
    {
        var triage = ProspectScoring.Score(
            Inputs([Fact("occupation", "boda rider")], [new ProspectInterest("Boda Accident", InterestStrength.WantsIt, "asked", Now)], consent, bank: true, messages: 8),
            Profiles,
            Now);

        Assert.True(triage.Score <= 44);
        Assert.Contains("Does not want to be contacted", triage.Signals);
    }

    [Fact]
    public void A_budget_below_the_monthly_price_lowers_capacity()
    {
        var enough = ProspectScoring.Score(Inputs([Fact("occupation", "boda rider"), Fact("monthly_budget", "5,000")]), Profiles, Now);
        var short_ = ProspectScoring.Score(Inputs([Fact("occupation", "boda rider"), Fact("monthly_budget", "1,000")]), Profiles, Now);

        Assert.True(enough.Score > short_.Score);
    }

    [Fact]
    public void The_same_record_always_gets_the_same_score()
    {
        var input = Inputs([Fact("occupation", "boda rider")], [new ProspectInterest("Boda Accident", InterestStrength.Interested, "asked", Now)]);

        Assert.Equal(ProspectScoring.Score(input, Profiles, Now).Score, ProspectScoring.Score(input, Profiles, Now).Score);
    }

    [Fact]
    public void A_record_with_nothing_in_it_has_low_confidence_and_lists_what_is_missing()
    {
        var triage = ProspectScoring.Score(Inputs(), Profiles, Now);

        Assert.Equal("Low", triage.Confidence);
        Assert.NotEmpty(triage.Missing);
    }

    [Theory]
    [InlineData("UGX 10,000 a month", 10_000)]
    [InlineData("300k", 300_000)]
    [InlineData("about 1.5 million", 1_500_000)]
    [InlineData("nothing yet", null)]
    public void Amounts_are_read_in_shillings(string text, int? expected) =>
        Assert.Equal(expected is null ? null : (decimal?)expected, ProspectScoring.ParseUgx(text));

    [Theory]
    [InlineData(80, ReadinessBand.HighPriority)]
    [InlineData(65, ReadinessBand.Qualified)]
    [InlineData(45, ReadinessBand.Warm)]
    [InlineData(25, ReadinessBand.Early)]
    [InlineData(24, ReadinessBand.Cold)]
    public void Scores_map_to_bands(int score, ReadinessBand band) =>
        Assert.Equal(band, ProspectScoring.BandFor(score));
}
