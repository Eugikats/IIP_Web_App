using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.CustomerAssistant;
using InsuranceIntelligence.Domain.Translation;

namespace InsuranceIntelligence.Domain.Tests;

/// <summary>What staff are told about the language of a conversation. It must say what actually
/// happened, and say nothing at all for an English conversation.</summary>
public sealed class ConversationLanguageNoteTests
{
    private static CustomerConversationTurn Turn(
        string? language, TranslationStatus? inbound = null, TranslationStatus? outbound = null, string english = "I was in an accident today") =>
        new()
        {
            TurnId = Guid.NewGuid(),
            Scope = TenantScope.Create("t", "w"),
            ThreadId = "thread",
            ContactWaId = "256772118001",
            InboundMessageText = english,
            ClassifiedIntent = CustomerIntent.NewClaim,
            Outcome = CustomerAssistantOutcome.CaseOpened,
            CustomerLanguage = language,
            InboundTranslationStatus = inbound,
            OutboundTranslationStatus = outbound,
            OccurredAt = DateTimeOffset.UtcNow,
            CorrelationId = "c",
        };

    [Fact]
    public void AnEnglishConversation_HasNothingToSay()
    {
        var result = ConversationLanguageNote.For(Turn(language: null));

        Assert.Null(result.Language);
        Assert.Null(result.Note);
    }

    [Fact]
    public void NoTurnAtAll_HasNothingToSay()
    {
        var result = ConversationLanguageNote.For(null);

        Assert.Null(result.Language);
        Assert.Null(result.Note);
    }

    [Fact]
    public void ATranslatedMessageAndReply_SayWhatWasUnderstoodAndThatAComputerTranslatedTheReply()
    {
        var result = ConversationLanguageNote.For(Turn("lg", TranslationStatus.Translated, TranslationStatus.Translated));

        Assert.Equal("Luganda", result.Language);
        Assert.Contains("Understood as: \"I was in an accident today\"", result.Note);
        Assert.Contains("Reply sent in Luganda", result.Note);
        Assert.Contains("translated by a computer", result.Note);
        Assert.Contains("not been checked by a native speaker", result.Note);
    }

    [Fact]
    public void AReplyThatFellBackToEnglish_SaysSo()
    {
        var result = ConversationLanguageNote.For(Turn("sw", TranslationStatus.Translated, TranslationStatus.FellBackToEnglish));

        Assert.Equal("Kiswahili", result.Language);
        Assert.Contains("Reply sent in English because a translation could not be shown", result.Note);
    }

    [Fact]
    public void AMessageThatCouldNotBeTranslated_TellsAPersonToReadTheCustomersOwnWords()
    {
        var result = ConversationLanguageNote.For(Turn("lg", TranslationStatus.FellBackToEnglish, TranslationStatus.FellBackToEnglish));

        Assert.Contains("could not be translated", result.Note);
        Assert.Contains("a person needs to read the customer's own words", result.Note);
        Assert.DoesNotContain("Understood as", result.Note);
    }

    [Fact]
    public void ALongUnderstoodMessage_IsShortenedToOneLine()
    {
        var long_ = string.Join(' ', Enumerable.Repeat("accident", 60)) + "\nsecond line";

        var result = ConversationLanguageNote.For(Turn("sw", TranslationStatus.Translated, null, long_));

        Assert.EndsWith("...\".", result.Note!.Split(" Reply", StringSplitOptions.None)[0]);
        Assert.DoesNotContain("\n", result.Note);
    }

    [Fact]
    public void AnUnknownLanguageCode_IsShownAsTheCodeRatherThanHidden()
    {
        var result = ConversationLanguageNote.For(Turn("xx", TranslationStatus.NotNeeded, TranslationStatus.NotNeeded));

        Assert.Equal("xx", result.Language);
        Assert.Null(result.Note);
    }
}
