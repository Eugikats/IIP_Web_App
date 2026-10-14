using InsuranceIntelligence.Domain.Integrations;

namespace InsuranceIntelligence.Api.Tests;

/// <summary>Where does Meta send this number's messages? Covers the facts as they were on 12 October 2026, when IIP
/// looked healthy while messages went nowhere, and the other ways delivery goes wrong.</summary>
public sealed class WhatsAppDeliveryAnalysisTests
{
    private const string Ours = "https://app-api-dev.example.net/api/v1/integrations/whatsapp/webhook";
    private const string Elsewhere = "https://cashflow.example.com/webhook";

    [Fact]
    public void The_setup_seen_on_12_October_is_reported_as_pointing_at_IIP_with_the_app_still_to_check()
    {
        // The account-level address is IIP's, the app's own default address is another service, and two apps are
        // subscribed to the account. That was the real state: IIP was right, and the cause was outside it.
        var facts = new WhatsAppMetaFacts(null, "CONNECTED", "LIVE", null, Ours, Elsewhere,
            [new WhatsAppSubscribedApp("1", "Otic Group", Ours), new WhatsAppSubscribedApp("2", "Otic", null)]);

        var findings = WhatsAppDeliveryAnalysis.Analyse(Ours, facts);

        Assert.DoesNotContain(findings, f => f.Level == "problem");
        Assert.Contains(findings, f => f.Level == "ok" && f.Text.Contains("sends this number's messages to IIP", StringComparison.OrdinalIgnoreCase)
                                       || f.Text.Contains("set to send this number's messages to IIP"));
        Assert.Contains(findings, f => f.Text.Contains("cashflow.example.com") && f.Text.Contains("overrides it"));
        Assert.Contains(findings, f => f.Level == "warning" && f.Text.Contains("Otic"));
        Assert.Contains(findings, f => f.Level == "warning" && f.Text.Contains("published"));
    }

    [Fact]
    public void A_setting_on_the_number_itself_that_points_elsewhere_is_a_problem_and_names_where()
    {
        var facts = new WhatsAppMetaFacts(null, "CONNECTED", "LIVE", Elsewhere, Ours, null, [new WhatsAppSubscribedApp("1", "Otic Group", Ours)]);

        var findings = WhatsAppDeliveryAnalysis.Analyse(Ours, facts);

        var problem = Assert.Single(findings, f => f.Level == "problem");
        Assert.Contains("cashflow.example.com", problem.Text);
        Assert.Contains("outranks", problem.Text);
    }

    [Fact]
    public void Nothing_pointing_at_IIP_is_a_problem_with_the_fix()
    {
        var facts = new WhatsAppMetaFacts(null, "CONNECTED", "LIVE", null, null, Elsewhere, [new WhatsAppSubscribedApp("2", "Otic", null)]);

        var findings = WhatsAppDeliveryAnalysis.Analyse(Ours, facts);

        Assert.Contains(findings, f => f.Level == "problem" && f.Text.Contains("Save the WhatsApp connection again"));
    }

    [Fact]
    public void A_number_that_is_not_connected_or_in_sandbox_is_called_out()
    {
        var disconnected = WhatsAppDeliveryAnalysis.Analyse(Ours, new WhatsAppMetaFacts(null, "DISCONNECTED", "LIVE", null, Ours, null, []));
        Assert.Contains(disconnected, f => f.Level == "problem" && f.Text.Contains("DISCONNECTED"));

        var sandbox = WhatsAppDeliveryAnalysis.Analyse(Ours, new WhatsAppMetaFacts(null, "CONNECTED", "SANDBOX", null, Ours, null, []));
        Assert.Contains(sandbox, f => f.Level == "warning" && f.Text.Contains("sandbox"));
    }

    [Fact]
    public void When_Meta_cannot_be_asked_the_reason_is_the_only_finding()
    {
        var findings = WhatsAppDeliveryAnalysis.Analyse(Ours, new WhatsAppMetaFacts("The access token has expired.", null, null, null, null, null, []));

        var only = Assert.Single(findings);
        Assert.Equal("problem", only.Level);
        Assert.Contains("access token has expired", only.Text);
    }

    [Fact]
    public void Without_a_public_address_of_its_own_IIP_says_it_cannot_tell()
    {
        var findings = WhatsAppDeliveryAnalysis.Analyse(null, new WhatsAppMetaFacts(null, "CONNECTED", "LIVE", null, Elsewhere, null, []));

        Assert.Contains(findings, f => f.Level == "warning" && f.Text.Contains("cannot tell"));
    }

    [Fact]
    public void Addresses_are_compared_without_regard_to_case_or_a_trailing_slash()
    {
        var facts = new WhatsAppMetaFacts(null, "CONNECTED", "LIVE", null, Ours.ToUpperInvariant() + "/", null, []);

        Assert.DoesNotContain(WhatsAppDeliveryAnalysis.Analyse(Ours, facts), f => f.Level == "problem");
    }
}
