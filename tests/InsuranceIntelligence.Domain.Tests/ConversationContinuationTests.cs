using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.CustomerAssistant;

namespace InsuranceIntelligence.Domain.Tests;

public sealed class ConversationContinuationTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 25, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Bare_policy_number_after_we_asked_for_it_continues_the_claim()
    {
        var previous = Turn(CustomerIntent.NewClaim, CustomerAssistantOutcome.AwaitingMoreInformation, "I had an accident");

        var result = ConversationContinuation.Resolve(CustomerIntent.Unknown, messageHasPolicyNumber: true, previous, Now);

        Assert.Equal(CustomerIntent.NewClaim, result.Intent);
        Assert.Equal("I had an accident", result.EarlierMessageText);
    }

    [Fact]
    public void Request_expires_after_the_window()
    {
        var previous = Turn(CustomerIntent.NewClaim, CustomerAssistantOutcome.AwaitingMoreInformation, "I had an accident", Now - ConversationContinuation.Window - TimeSpan.FromMinutes(1));

        var result = ConversationContinuation.Resolve(CustomerIntent.Unknown, messageHasPolicyNumber: true, previous, Now);

        Assert.Equal(CustomerIntent.Unknown, result.Intent);
        Assert.Null(result.EarlierMessageText);
    }

    [Fact]
    public void A_new_explicit_intent_is_never_overridden()
    {
        var previous = Turn(CustomerIntent.NewClaim, CustomerAssistantOutcome.AwaitingMoreInformation, "I had an accident");

        var result = ConversationContinuation.Resolve(CustomerIntent.HumanRequested, messageHasPolicyNumber: false, previous, Now);

        Assert.Equal(CustomerIntent.HumanRequested, result.Intent);
    }

    [Fact]
    public void A_follow_up_question_without_a_policy_number_stays_a_question()
    {
        var previous = Turn(CustomerIntent.NewClaim, CustomerAssistantOutcome.AwaitingMoreInformation, "I had an accident");

        var result = ConversationContinuation.Resolve(CustomerIntent.GeneralQuestion, messageHasPolicyNumber: false, previous, Now);

        Assert.Equal(CustomerIntent.GeneralQuestion, result.Intent);
    }

    [Fact]
    public void A_completed_turn_is_not_continued()
    {
        var previous = Turn(CustomerIntent.NewClaim, CustomerAssistantOutcome.CaseOpened, "I had an accident, NSA/MO/2026/00009");

        var result = ConversationContinuation.Resolve(CustomerIntent.Unknown, messageHasPolicyNumber: true, previous, Now);

        Assert.Equal(CustomerIntent.Unknown, result.Intent);
    }

    [Theory]
    [InlineData("Hi", true)]
    [InlineData("hello!", true)]
    [InlineData("Good morning", true)]
    [InlineData("Oli otya", true)]
    [InlineData("Habari", true)]
    [InlineData("history of my policy", false)]
    [InlineData("Hi, I had an accident on the highway near Mukono yesterday", false)]
    public void Recognises_short_greetings_only(string text, bool expected) =>
        Assert.Equal(expected, ConversationContinuation.IsGreeting(text));

    private static CustomerConversationTurn Turn(CustomerIntent intent, CustomerAssistantOutcome outcome, string text, DateTimeOffset? at = null) => new()
    {
        TurnId = Guid.NewGuid(),
        Scope = TenantScope.Create("tenant-a", "workspace-a"),
        ThreadId = "thread-a",
        ContactWaId = "256700000000",
        InboundMessageText = text,
        ClassifiedIntent = intent,
        Outcome = outcome,
        OccurredAt = at ?? Now.AddMinutes(-2),
        CorrelationId = "corr-a",
    };
}
