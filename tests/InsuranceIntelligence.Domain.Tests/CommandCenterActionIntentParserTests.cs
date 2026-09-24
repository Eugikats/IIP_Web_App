using InsuranceIntelligence.Application.CommandCenter;
using Xunit;

namespace InsuranceIntelligence.Domain.Tests;

/// <summary>
/// The chat box accepts three instructions. These tests pin both halves of that: the phrasings a
/// person actually uses reach the right action with the right target, and the operational questions
/// that share words with them ("summarise claims missing documents", "message the team") still reach
/// the query intents they always did.
/// </summary>
public sealed class CommandCenterActionIntentParserTests
{
    private static readonly DeterministicThenAiIntentClassifier Classifier = new();

    private static IntentClassificationResult Classify(string message) =>
        Classifier.ClassifyAsync(message, "tenant-test", "workspace-test").GetAwaiter().GetResult();

    [Theory]
    // The exact phrase from the live demo that used to return the claims briefing.
    [InlineData("send a message to eugene", "eugene", "")]
    [InlineData("Send a message to Eugene Katusiime saying we received his documents.", "Eugene Katusiime", "we received his documents")]
    [InlineData("message Aisha Kato about the police abstract", "Aisha Kato", "the police abstract")]
    [InlineData("let Brian know his claim form arrived", "Brian", "his claim form arrived")]
    [InlineData("draft an update for CLM-726E3445", "CLM-726E3445", "")]
    public void Contacting_a_customer_becomes_a_draft_message_action(string message, string target, string instruction)
    {
        var result = Classify(message);

        Assert.Equal(IntentType.DraftCustomerMessage, result.Intent);
        Assert.Equal(target, result.Parameters.GetStringOrDefault(CommandCenterActionIntentParser.TargetKey));
        Assert.Equal(instruction, result.Parameters.GetStringOrDefault(CommandCenterActionIntentParser.InstructionKey));
    }

    [Theory]
    [InlineData("assign CLM-726E3445 to me", "CLM-726E3445", "me")]
    [InlineData("assign Aisha Kato's claim to Brian Mugisha", "Aisha Kato", "Brian Mugisha")]
    [InlineData("please assign the claim for Eugene to Sarah", "Eugene", "Sarah")]
    public void Assigning_a_claim_becomes_an_assign_action(string message, string target, string assignee)
    {
        var result = Classify(message);

        Assert.Equal(IntentType.AssignClaim, result.Intent);
        Assert.Equal(target, result.Parameters.GetStringOrDefault(CommandCenterActionIntentParser.TargetKey));
        Assert.Equal(assignee, result.Parameters.GetStringOrDefault(CommandCenterActionIntentParser.AssigneeKey));
    }

    [Theory]
    [InlineData("summarise the claim for Aisha Kato", "Aisha Kato")]
    [InlineData("review CLM-726E3445", "CLM-726E3445")]
    [InlineData("summarize Eugene's claim", "Eugene")]
    public void Asking_for_a_claim_review_becomes_a_summarize_action(string message, string target)
    {
        var result = Classify(message);

        Assert.Equal(IntentType.SummarizeClaim, result.Intent);
        Assert.Equal(target, result.Parameters.GetStringOrDefault(CommandCenterActionIntentParser.TargetKey));
    }

    [Theory]
    // Questions that share words with the instructions must keep their existing meaning.
    [InlineData("Which claims do I need to review today?", IntentType.ShowAttentionClaims)]
    [InlineData("Draft a customer update", IntentType.PrepareCustomerUpdateDraft)]
    [InlineData("Give me a management summary", IntentType.GenerateManagementSummary)]
    [InlineData("summarise claims missing documents", IntentType.ShowMissingDocuments)]
    [InlineData("tell me which claims are overdue", IntentType.ShowSlaBreaches)]
    public void Operational_questions_are_not_mistaken_for_instructions(string message, IntentType expected) =>
        Assert.Equal(expected, Classify(message).Intent);

    [Theory]
    [InlineData("message the team")]
    [InlineData("send a message to everyone")]
    [InlineData("assign all overdue claims to me")]
    public void Instructions_without_a_single_customer_or_claim_are_not_actions(string message)
    {
        var intent = Classify(message).Intent;

        Assert.NotEqual(IntentType.DraftCustomerMessage, intent);
        Assert.NotEqual(IntentType.AssignClaim, intent);
        Assert.NotEqual(IntentType.SummarizeClaim, intent);
    }
}
