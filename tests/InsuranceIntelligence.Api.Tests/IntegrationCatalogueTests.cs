using InsuranceIntelligence.Application.Integrations;

namespace InsuranceIntelligence.Api.Tests;

/// <summary>Every card shows one of four states, each earned by a check. These pin what earns each.</summary>
public sealed class IntegrationCatalogueTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 14, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void StateRules_FollowTheOrder_PlatformThenConnectionThenChecks()
    {
        IntegrationCheck[] failing = [new("c", false, "bad")];
        IntegrationCheck[] softFailing = [new("c", false, "meh", Blocking: false)];

        Assert.Equal(IntegrationCardState.NotConfigured, IntegrationCardRules.Decide(false, true, []));
        Assert.Equal(IntegrationCardState.NotConnected, IntegrationCardRules.Decide(true, false, failing));
        Assert.Equal(IntegrationCardState.NeedsAttention, IntegrationCardRules.Decide(true, true, failing));
        Assert.Equal(IntegrationCardState.Connected, IntegrationCardRules.Decide(true, true, softFailing));
        Assert.Equal(IntegrationCardState.Connected, IntegrationCardRules.Decide(true, true, []));
    }

    [Fact]
    public void WhatsApp_WithNothingSaved_IsNotConnected_NeverConnected()
    {
        var card = Build(WhatsApp(configured: false)).Single(c => c.Key == "whatsapp");
        Assert.Equal(IntegrationCardState.NotConnected, card.State);
        Assert.Empty(card.Checks);
    }

    [Fact]
    public void WhatsApp_ThatIsConfiguredAndHearingFromCustomers_IsConnected_AndSaysWhenTheLastMessageCame()
    {
        var card = Build(WhatsApp(lastAccepted: Now.AddMinutes(-30), lastReceipt: Now.AddMinutes(-30))).Single(c => c.Key == "whatsapp");
        Assert.Equal(IntegrationCardState.Connected, card.State);
        Assert.Contains("30 minutes ago", card.Summary);
    }

    [Fact]
    public void WhatsApp_ThatNeverReceivedAMessage_IsStillConnected_ButSaysSo()
    {
        var card = Build(WhatsApp()).Single(c => c.Key == "whatsapp");
        Assert.Equal(IntegrationCardState.Connected, card.State);
        Assert.Contains("No customer message has arrived yet", card.Summary);
    }

    [Fact]
    public void WhatsApp_WhoseCallsMetaMakesAreRefused_NeedsAttention()
    {
        // The 12 October failure shape: calls arrive and nothing is accepted.
        var card = Build(WhatsApp(lastReceipt: Now.AddHours(-1), refusedInLastDay: 4)).Single(c => c.Key == "whatsapp");
        Assert.Equal(IntegrationCardState.NeedsAttention, card.State);
        Assert.Contains("refused", card.Checks.Single(c => c.Blocking && !c.Passed).Note);
        Assert.DoesNotContain("refused", card.Summary);
    }

    [Fact]
    public void WhatsApp_RefusalsFollowedByAnAcceptedCall_AreNoLongerAProblem()
    {
        var card = Build(WhatsApp(lastAccepted: Now.AddMinutes(-5), lastReceipt: Now.AddMinutes(-5), refusedInLastDay: 2)).Single(c => c.Key == "whatsapp");
        Assert.Equal(IntegrationCardState.Connected, card.State);
    }

    [Fact]
    public void Email_WithNoSavedConnectionAndNoPlatformProvider_IsNotConnected_AndSaysWhereToSetItUp()
    {
        var card = Build().Single(c => c.Key == "email");
        Assert.Equal(IntegrationCardState.NotConnected, card.State);
        Assert.Contains("Settings", card.Summary);
    }

    [Fact]
    public void Email_SavedAndAcceptedByTheProvider_IsConnected()
    {
        var facts = Facts() with { Email = new ConnectionFacts(false, true, true, "Success", Now, "Graph") };
        Assert.Equal(IntegrationCardState.Connected, IntegrationCatalogueBuilder.Build(facts).Single(c => c.Key == "email").State);
    }

    [Theory]
    [InlineData("Failed", true)]
    [InlineData("Success", false)]
    public void Email_SavedButFailingOrSwitchedOff_NeedsAttention(string status, bool active)
    {
        var facts = Facts() with { Email = new ConnectionFacts(false, true, active, status, Now, "Graph") };
        Assert.Equal(IntegrationCardState.NeedsAttention, IntegrationCatalogueBuilder.Build(facts).Single(c => c.Key == "email").State);
    }

    [Fact]
    public void Sms_ThroughThePlatformProviderWithoutASavedConnection_IsConnected()
    {
        var facts = Facts() with { Sms = new ConnectionFacts(true, false, false, null, null, null) };
        Assert.Equal(IntegrationCardState.Connected, IntegrationCatalogueBuilder.Build(facts).Single(c => c.Key == "sms").State);
    }

    [Fact]
    public void Teams_FollowsThePlatformProvider()
    {
        Assert.Equal(IntegrationCardState.NotConfigured, Build().Single(c => c.Key == "teams").State);
        Assert.Equal(IntegrationCardState.Connected, IntegrationCatalogueBuilder.Build(Facts() with { TeamsProviderAvailable = true }).Single(c => c.Key == "teams").State);
    }

    [Fact]
    public void Files_AreNotConfiguredUntilThePlatformHasAnAppRegistration_ThenPerPerson()
    {
        var none = new FileProviderFacts(false, false, null, "Google sign-in is not set up on this platform yet.");
        var ready = new FileProviderFacts(true, false, null, null);
        var mine = new FileProviderFacts(true, true, "aisha@example.test", null);

        var card = IntegrationCatalogueBuilder.Build(Facts() with { GoogleDrive = none }).Single(c => c.Key == "google-drive");
        Assert.Equal(IntegrationCardState.NotConfigured, card.State);
        Assert.Contains("not set up", card.Summary);

        Assert.Equal(IntegrationCardState.NotConnected, IntegrationCatalogueBuilder.Build(Facts() with { GoogleDrive = ready }).Single(c => c.Key == "google-drive").State);

        var connected = IntegrationCatalogueBuilder.Build(Facts() with { Microsoft = mine }).Single(c => c.Key == "microsoft-files");
        Assert.Equal(IntegrationCardState.Connected, connected.State);
        Assert.Contains("your own connection", connected.Summary);
    }

    [Fact]
    public void OnlyIntegrationsThatExistAreListed() =>
        Assert.Equal(["whatsapp", "email", "sms", "teams", "google-drive", "microsoft-files"], Build().Select(c => c.Key));

    [Fact]
    public void NoCardSummaryContainsAnEmDashOrAnEmoji()
    {
        foreach (var card in Build())
        {
            Assert.DoesNotContain('—', card.Summary);
            Assert.All(card.Summary, ch => Assert.True(ch < 0x1F000, "no emoji"));
        }
    }

    private static IReadOnlyList<IntegrationCard> Build(WhatsAppFacts? whatsApp = null) =>
        IntegrationCatalogueBuilder.Build(whatsApp is null ? Facts() : Facts() with { WhatsApp = whatsApp });

    private static IntegrationFacts Facts() => new(
        Now,
        WhatsApp(configured: false),
        new ConnectionFacts(false, false, false, null, null, null),
        new ConnectionFacts(false, false, false, null, null, null),
        false,
        new FileProviderFacts(false, false, null, null),
        new FileProviderFacts(false, false, null, null));

    private static WhatsAppFacts WhatsApp(bool configured = true, DateTimeOffset? lastAccepted = null, DateTimeOffset? lastReceipt = null, int refusedInLastDay = 0) =>
        new(configured, "tenant", lastReceipt, lastAccepted, refusedInLastDay, true);
}
