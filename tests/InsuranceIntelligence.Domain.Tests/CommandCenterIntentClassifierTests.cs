using InsuranceIntelligence.Application.CommandCenter;
using Xunit;

namespace InsuranceIntelligence.Domain.Tests;

/// <summary>
/// Guards the routing that decides whether a Command Center message is answered from
/// operational data (deterministic) or from approved knowledge via the model. Regressions
/// here are the difference between the assistant answering a policy question and silently
/// returning an unrelated workspace briefing.
/// </summary>
public sealed class CommandCenterIntentClassifierTests
{
    private static readonly DeterministicThenAiIntentClassifier Classifier = new();

    private static IntentType Classify(string message) =>
        Classifier.ClassifyAsync(message, "tenant-test", "workspace-test").GetAwaiter().GetResult().Intent;

    [Theory]
    // Vocabulary that only appears in policy-knowledge questions.
    [InlineData("What are the exclusions on the motor policy?")]
    [InlineData("Is there a waiting period for maternity?")]
    [InlineData("What is the co-payment on the Gold plan?")]
    [InlineData("How much is the premium for comprehensive cover?")]
    [InlineData("What is the grace period for life premiums?")]
    // Question-shaped messages with no operational keyword.
    [InlineData("How do we handle medical insurance?")]
    [InlineData("Explain the UAIS subsidy tiers")]
    [InlineData("What does third party only mean?")]
    public void PolicyKnowledgeQuestions_RouteToGroundedAi(string message) =>
        Assert.Equal(IntentType.AnswerPolicyQuestion, Classify(message));

    [Theory]
    [InlineData("Which claims do I need to review today?", IntentType.ShowAttentionClaims)]
    [InlineData("Show me SLA breaches", IntentType.ShowSlaBreaches)]
    [InlineData("What is missing on these claims?", IntentType.ShowMissingDocuments)]
    [InlineData("Show pending approvals", IntentType.ShowPendingApprovals)]
    [InlineData("Give me a management summary", IntentType.GenerateManagementSummary)]
    [InlineData("Draft a customer update", IntentType.PrepareCustomerUpdateDraft)]
    public void OperationalQuestions_StayDeterministic(string message, IntentType expected) =>
        Assert.Equal(expected, Classify(message));

    [Theory]
    // Greetings used to classify as Unsupported and fall through to the standing operational
    // briefing, so saying "Hey" was answered with a count of claims nobody had asked about.
    [InlineData("Hey")]
    [InlineData("hello")]
    [InlineData("Hi there")]
    [InlineData("Good morning")]
    [InlineData("How are you?")]
    [InlineData("hows it going")]
    [InlineData("How's it going?")]
    public void Greetings_AreAnsweredAsGreetings(string message) =>
        Assert.Equal(IntentType.Conversational, Classify(message));

    [Theory]
    [InlineData("thanks")]
    [InlineData("Thank you!")]
    [InlineData("thanks a lot")]
    [InlineData("much appreciated")]
    [InlineData("bye")]
    [InlineData("Goodbye")]
    [InlineData("that's all")]
    [InlineData("What can you do?")]
    [InlineData("help")]
    [InlineData("who are you")]
    public void ThanksFarewellsAndCapabilityQuestions_AreAlsoConversational(string message) =>
        Assert.Equal(IntentType.Conversational, Classify(message));

    [Theory]
    // The whole point of matching the entire message rather than looking for a greeting inside
    // it: an operational request that merely opens politely is still an operational request.
    [InlineData("hey, which claims are overdue?", IntentType.ShowSlaBreaches)]
    [InlineData("Hi, show me pending approvals", IntentType.ShowPendingApprovals)]
    [InlineData("Good morning, what needs my attention today?", IntentType.ShowAttentionClaims)]
    [InlineData("thanks, now what is missing on these claims?", IntentType.ShowMissingDocuments)]
    [InlineData("hello, what are the exclusions on the motor policy?", IntentType.AnswerPolicyQuestion)]
    public void APoliteOpenerDoesNotHijackARealRequest(string message, IntentType expected) =>
        Assert.Equal(expected, Classify(message));

    [Theory]
    // Still nothing to answer: not a greeting, not a question, not an operational request.
    [InlineData("...")]
    [InlineData("asdf")]
    [InlineData("??")]
    public void Fragments_StillFallBackToTheBriefing(string message) =>
        Assert.Equal(IntentType.Unsupported, Classify(message));

    [Fact]
    public void AGreetingNeedsNoModelOrRetrievalProvider()
    {
        // A fixed reply must never be gated behind Azure OpenAI being configured -- an insurer
        // who has not connected a model should still be greeted rather than shown "not configured".
        Assert.Empty(new ProviderRequirementResolver().GetRequiredProviders(IntentType.Conversational));
    }

    [Fact]
    public void PolicyAnswer_RequiresBothModelAndRetrievalProviders()
    {
        var required = new ProviderRequirementResolver()
            .GetRequiredProviders(IntentType.AnswerPolicyQuestion)
            .Select(p => p.ProviderCode)
            .ToArray();

        // Grounding is mandatory: answering a policy question without retrieval is forbidden.
        Assert.Contains("azure_openai", required);
        Assert.Contains("azure_ai_search", required);
    }
}
