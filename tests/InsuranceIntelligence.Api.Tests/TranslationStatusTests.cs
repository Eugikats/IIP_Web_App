using InsuranceIntelligence.Api.Agents;
using InsuranceIntelligence.Application.Translation;

namespace InsuranceIntelligence.Api.Tests;

/// <summary>What the staff screen tells the person running the workspace about local-language
/// replies. It must never say things are working when the engine cannot be reached.</summary>
public sealed class TranslationStatusTests
{
    [Fact]
    public void WithNoLanguageSwitchedOn_ItSaysOnlyEnglishAndNeverClaimsAnythingElse()
    {
        var text = TranslationStatusEndpoints.Summarise(TranslationEngineStatus.Healthy, enabledLanguages: 0, allowUnreviewed: true);

        Assert.Contains("Only English", text);
        Assert.DoesNotContain("working", text);
    }

    [Fact]
    public void AnEngineThatCannotBeReached_IsNeverReportedAsWorking()
    {
        var text = TranslationStatusEndpoints.Summarise(TranslationEngineStatus.Failing, enabledLanguages: 2, allowUnreviewed: true);

        Assert.Contains("cannot be reached", text);
        Assert.DoesNotContain("is working", text);
        Assert.Contains("handed to a person", text);
    }

    [Fact]
    public void AMissingEngine_SaysNoEngineIsConnected()
    {
        var text = TranslationStatusEndpoints.Summarise(TranslationEngineStatus.NotConfigured, enabledLanguages: 2, allowUnreviewed: true);

        Assert.Contains("No translation engine is connected", text);
    }

    [Fact]
    public void AHealthyEngineWithUnreviewedRepliesOff_SaysRepliesStayInEnglish()
    {
        var text = TranslationStatusEndpoints.Summarise(TranslationEngineStatus.Healthy, enabledLanguages: 2, allowUnreviewed: false);

        Assert.Contains("Replies go out in English", text);
        Assert.Contains("native speaker", text);
    }

    [Fact]
    public void AHealthyEngineWithTranslatedRepliesOn_SaysTheyAreMachineTranslatedAndUnreviewed()
    {
        var text = TranslationStatusEndpoints.Summarise(TranslationEngineStatus.Healthy, enabledLanguages: 2, allowUnreviewed: true);

        Assert.Contains("machine translated and say so", text);
        Assert.Contains("no native speaker has reviewed", text);
    }

    [Theory]
    [InlineData("AfriSlm", "AfriSLM translation model")]
    [InlineData("Mock", "Test translator (not real)")]
    [InlineData("Disabled", "None")]
    [InlineData("anything else", "None")]
    public void TheEngineNameNeverRevealsAnAddressOrAKey(string provider, string expected) =>
        Assert.Equal(expected, TranslationStatusEndpoints.EngineName(provider));
}
