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
    [InlineData("Hey")]
    [InlineData("hello")]
    [InlineData("...")]
    public void GreetingsAndFragments_FallBackToBriefing(string message) =>
        Assert.Equal(IntentType.Unsupported, Classify(message));

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
