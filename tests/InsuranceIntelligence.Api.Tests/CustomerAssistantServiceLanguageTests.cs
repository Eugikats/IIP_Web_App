using InsuranceIntelligence.Application.Translation;
using InsuranceIntelligence.Domain.CustomerAssistant;
using InsuranceIntelligence.Domain.Translation;

namespace InsuranceIntelligence.Api.Tests;

/// <summary>
/// The WhatsApp assistant in Luganda and Kiswahili. Translation goes around the claim process, never
/// into it: the customer's message is translated to English at the door, the existing English flow
/// runs unchanged, and the reply is translated on the way out. These tests use a scripted engine so
/// they prove the pipeline's behaviour, not the model's quality (that is what the review sheet is for).
/// </summary>
public sealed partial class CustomerAssistantServiceTests
{
    private const string LugandaClaim = "Nfunye akabenje leero nsaba mumbeeko policy yange NSA/MO/2026/00009";
    private const string EnglishOfLugandaClaim = "I was in an accident today, please help me, my policy is [TKN0]";

    private static ScriptedTranslator LugandaClaimTranslator() => new()
    {
        Script = (text, from, to) => to == SupportedLanguageCode.En && text.Contains("akabenje") ? EnglishOfLugandaClaim : null,
    };

    [Fact]
    public async Task AnEnglishCustomer_IsAnsweredExactlyAsBefore_AndTheEngineIsNeverCalled()
    {
        var translator = new ScriptedTranslator();
        var fixture = Fixture.Create(translator: translator);
        fixture.SeedPolicyholder("Eugene Katusiime", "+256772118001", ("Motor", ValidPolicyNumber));

        await fixture.HandleAsync($"I was in an accident, my policy is {ValidPolicyNumber}");

        Assert.Empty(translator.Calls);
        var turn = Assert.Single(fixture.Conversations.Saved);
        Assert.Equal(CustomerAssistantOutcome.AwaitingClaimConfirmation, turn.Outcome);
        Assert.Null(turn.CustomerLanguage);
        Assert.Null(turn.OriginalInboundText);
        Assert.Null(turn.OutboundLocalisedText);
        var sent = Assert.Single(fixture.Outbound.SentRequests);
        Assert.Equal("en", sent.Language);
        Assert.DoesNotContain("translated by a computer", sent.CustomerSafeBody);
    }

    [Fact]
    public async Task ALugandaClaimMessage_OpensTheClaimAndTheReplyComesBackInLuganda()
    {
        var translator = LugandaClaimTranslator();
        var fixture = Fixture.Create(translator: translator);
        fixture.SeedPolicyholder("Eugene Katusiime", "+256772118001", ("Motor", ValidPolicyNumber));

        await fixture.HandleAsync(LugandaClaim);

        var turn = Assert.Single(fixture.Conversations.Saved);
        Assert.Equal(CustomerIntent.NewClaim, turn.ClassifiedIntent);
        Assert.Equal(CustomerAssistantOutcome.AwaitingClaimConfirmation, turn.Outcome); // read back before anything is opened
        Assert.Null(turn.CaseId);
        Assert.Equal("lg", turn.CustomerLanguage);
        Assert.Equal(TranslationStatus.Translated, turn.InboundTranslationStatus);
        Assert.Equal(TranslationStatus.Translated, turn.OutboundTranslationStatus);

        // The record is English, next to what the customer actually wrote and received.
        Assert.Contains("accident", turn.InboundMessageText);
        Assert.Contains(ValidPolicyNumber, turn.InboundMessageText);
        Assert.Equal(LugandaClaim, turn.OriginalInboundText);
        Assert.Contains("Is that right?", turn.OutboundReplyText);
        Assert.StartsWith("[lg]", turn.OutboundLocalisedText);

        var sent = Assert.Single(fixture.Outbound.SentRequests);
        Assert.Equal("lg", sent.Language);
        Assert.StartsWith("[lg]", sent.CustomerSafeBody);
        Assert.Contains("translated by a computer", sent.CustomerSafeBody.Replace("[lg] ", string.Empty), StringComparison.OrdinalIgnoreCase);

        var opened = await fixture.HandleAsync("Yee");
        var claim = await fixture.Cases.GetAsync(Assert.Single(opened));
        Assert.Equal(ValidPolicyNumber, claim!.PolicyNumber);
    }

    [Fact]
    public async Task ThePolicyNumberInATranslatedMessageIsNeverLost()
    {
        var fixture = Fixture.Create(translator: LugandaClaimTranslator());
        fixture.SeedPolicyholder("Eugene Katusiime", "+256772118001", ("Motor", ValidPolicyNumber));

        await fixture.HandleAsync(LugandaClaim);
        await fixture.HandleAsync("Yee");

        Assert.Equal(ValidPolicyNumber, Assert.Single(await fixture.Cases.ListAsync(fixture.Scope)).PolicyNumber);
    }

    [Fact]
    public async Task AClaimOpenedFromATranslation_KeepsTheCustomersOwnWordsAndLanguage()
    {
        var fixture = Fixture.Create(translator: LugandaClaimTranslator());
        fixture.SeedPolicyholder("Eugene Katusiime", "+256772118001", ("Motor", ValidPolicyNumber));

        await fixture.HandleAsync(LugandaClaim);
        await fixture.HandleAsync("Yee");

        var claim = Assert.Single(await fixture.Cases.ListAsync(fixture.Scope));
        Assert.Contains("accident", claim.IncidentDescription); // the English description
        Assert.Equal(LugandaClaim, claim.OriginalIncidentText); // what they actually wrote
        Assert.Equal("lg", claim.OriginalIncidentLanguage);
    }

    [Fact]
    public async Task AnEnglishClaim_HasNoSeparateOriginalText()
    {
        var fixture = Fixture.Create(translator: new ScriptedTranslator());
        fixture.SeedPolicyholder("Eugene Katusiime", "+256772118001", ("Motor", ValidPolicyNumber));

        await fixture.HandleAsync($"I was in an accident on Jinja Road this morning, my policy is {ValidPolicyNumber}");
        await fixture.HandleAsync("yes");

        var claim = Assert.Single(await fixture.Cases.ListAsync(fixture.Scope));
        Assert.Null(claim.OriginalIncidentText);
        Assert.Null(claim.OriginalIncidentLanguage);
    }

    [Fact]
    public async Task AClaimBuiltFromTwoLugandaMessages_KeepsBothOfTheCustomersOwnMessages()
    {
        var translator = new ScriptedTranslator
        {
            Script = (text, from, to) => to == SupportedLanguageCode.En
                ? text.Contains("akabenje") ? "Accident, my policy [TKN0]" : text.Contains("ekyaliwo", StringComparison.OrdinalIgnoreCase) ? "it happened yesterday on the Jinja road near the roundabout with a taxi" : null
                : null,
        };
        var fixture = Fixture.Create(translator: translator);
        fixture.SeedPolicyholder("Eugene Katusiime", "+256772118001", ("Motor", ValidPolicyNumber));
        const string first = "Nfunye akabenje leero nsaba mumbeeko yange NSA/MO/2026/00009";
        const string second = "Ekyaliwo jjo ku luguudo lwa Jinja okumpi ne kalannamba wa nnyo nnyo leero";

        await fixture.HandleAsync(first);
        await fixture.HandleAsync(second);
        await fixture.HandleAsync("Yee");

        var claim = Assert.Single(await fixture.Cases.ListAsync(fixture.Scope));
        Assert.Contains(first, claim.OriginalIncidentText);
        Assert.Contains(second, claim.OriginalIncidentText);
        Assert.Equal("lg", claim.OriginalIncidentLanguage);
    }

    [Fact]
    public async Task ThePolicyNumberAndReferenceInTheReplyReachTheCustomerUntouched()
    {
        var fixture = Fixture.Create(translator: LugandaClaimTranslator());
        fixture.SeedPolicyholder("Eugene Katusiime", "+256772118001", ("Motor", ValidPolicyNumber));

        await fixture.HandleAsync(LugandaClaim);
        await fixture.HandleAsync("Yee");

        var claim = Assert.Single(await fixture.Cases.ListAsync(fixture.Scope));
        var sent = fixture.Outbound.SentRequests[^1];
        Assert.Contains(InsuranceIntelligence.Domain.Cases.ClaimReference.For(claim.CaseId), sent.CustomerSafeBody);
        Assert.DoesNotContain("TKN", sent.CustomerSafeBody);
        Assert.All(fixture.Outbound.SentRequests, r => Assert.DoesNotContain("TKN", r.CustomerSafeBody));
    }

    [Fact]
    public async Task AnEngineThatIsDown_EscalatesTheMessageToAPersonWithTheOriginalWords()
    {
        var fixture = Fixture.Create(translator: new ScriptedTranslator { Throw = true });
        fixture.SeedPolicyholder("Eugene Katusiime", "+256772118001", ("Motor", ValidPolicyNumber));

        await fixture.HandleAsync(LugandaClaim);

        var turn = Assert.Single(fixture.Conversations.Saved);
        Assert.Equal(CustomerAssistantOutcome.Escalated, turn.Outcome);
        Assert.Equal(LugandaClaim, turn.InboundMessageText); // nothing was understood, so nothing was guessed
        Assert.Equal(TranslationStatus.FellBackToEnglish, turn.InboundTranslationStatus);
        Assert.Empty(await fixture.Cases.ListAsync(fixture.Scope));
        var sent = Assert.Single(fixture.Outbound.SentRequests);
        Assert.Contains("someone from our team", sent.CustomerSafeBody);
        Assert.Equal("en", sent.Language);
        var audit = Assert.Single(fixture.Audit.Written);
        Assert.Equal("customer_assistant.translation_failed", audit.ActionName);
        Assert.DoesNotContain("akabenje", audit.AuditReason!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task WhereUnreviewedTranslationsAreNotAllowed_TheReplyIsEnglishAndSaysSo()
    {
        var fixture = Fixture.Create(translator: LugandaClaimTranslator(), allowUnreviewedTranslations: false);
        fixture.SeedPolicyholder("Eugene Katusiime", "+256772118001", ("Motor", ValidPolicyNumber));

        await fixture.HandleAsync(LugandaClaim);

        var turn = Assert.Single(fixture.Conversations.Saved);
        Assert.Equal(CustomerAssistantOutcome.AwaitingClaimConfirmation, turn.Outcome); // still understood
        Assert.Equal(TranslationStatus.FellBackToEnglish, turn.OutboundTranslationStatus);
        Assert.Null(turn.OutboundLocalisedText);
        var sent = Assert.Single(fixture.Outbound.SentRequests);
        Assert.Equal("en", sent.Language);
        Assert.Contains("Is that right?", sent.CustomerSafeBody);
    }

    [Fact]
    public async Task ALanguageThatIsNotEnabled_IsTreatedAsEnglishAndNeverTranslated()
    {
        var translator = LugandaClaimTranslator();
        var fixture = Fixture.Create(translator: translator, enabledLanguages: [SupportedLanguageCode.Sw]);
        fixture.SeedPolicyholder("Eugene Katusiime", "+256772118001", ("Motor", ValidPolicyNumber));

        await fixture.HandleAsync(LugandaClaim);

        Assert.Empty(translator.Calls);
        Assert.Null(Assert.Single(fixture.Conversations.Saved).CustomerLanguage);
    }

    [Fact]
    public async Task AShortMessageInAnotherLanguage_IsAskedWhichLanguageToUse()
    {
        var fixture = Fixture.Create(translator: new ScriptedTranslator());

        await fixture.HandleAsync("Habari");

        var turn = Assert.Single(fixture.Conversations.Saved);
        Assert.Equal(CustomerAssistantOutcome.AwaitingLanguageChoice, turn.Outcome);
        var sent = Assert.Single(fixture.Outbound.SentRequests);
        Assert.Contains("Reply 1 for English, 2 for Luganda, 3 for Kiswahili", sent.CustomerSafeBody);
    }

    [Fact]
    public async Task AShortEnglishGreeting_IsNotAnsweredWithALanguageMenu()
    {
        var fixture = Fixture.Create(translator: new ScriptedTranslator());

        await fixture.HandleAsync("hi");

        var sent = Assert.Single(fixture.Outbound.SentRequests);
        Assert.DoesNotContain("Reply 1 for English", sent.CustomerSafeBody);
        Assert.Contains("I'm Kato", sent.CustomerSafeBody);
    }

    [Fact]
    public async Task AnsweringTheLanguageMenu_SwitchesTheConversationAndRemembersTheChoice()
    {
        var fixture = Fixture.Create(translator: new ScriptedTranslator());
        await fixture.HandleAsync("Habari");

        await fixture.HandleAsync("3");

        var choice = fixture.Conversations.Saved[1];
        Assert.Equal("sw", choice.CustomerLanguage);
        Assert.Equal(CustomerAssistantOutcome.AwaitingMoreInformation, choice.Outcome);
        Assert.StartsWith("[sw]", fixture.Outbound.SentRequests[1].CustomerSafeBody);

        // A photo has no words to detect a language from, so it is answered in the chosen one.
        await fixture.HandleMediaAsync();
        Assert.StartsWith("[sw]", fixture.Outbound.SentRequests[2].CustomerSafeBody);
    }

    [Fact]
    public async Task APolicyNumberOnItsOwn_IsNotSentToTheEngine()
    {
        var translator = new ScriptedTranslator();
        var fixture = Fixture.Create(translator: translator);
        fixture.SeedPolicyholder("Eugene Katusiime", "+256772118001", ("Motor", ValidPolicyNumber));
        await fixture.HandleAsync(LugandaClaim); // makes Luganda this customer's language
        translator.Calls.Clear();

        await fixture.HandleAsync("NSA/MO/2026/00009");

        Assert.DoesNotContain(translator.Calls, call => call.To == SupportedLanguageCode.En);
    }

    [Fact]
    public async Task TheAuditTrailRecordsTheLanguageAndTheStatusButNeverTheWords()
    {
        var fixture = Fixture.Create(translator: LugandaClaimTranslator());
        fixture.SeedPolicyholder("Eugene Katusiime", "+256772118001", ("Motor", ValidPolicyNumber));

        await fixture.HandleAsync(LugandaClaim);

        var audit = Assert.Single(fixture.Audit.Written, e => e.ActionName.StartsWith("customer_assistant.", StringComparison.Ordinal));
        Assert.Contains("language lg", audit.AuditReason);
        Assert.Contains("inbound Translated", audit.AuditReason);
        Assert.Contains("outbound Translated", audit.AuditReason);
        Assert.DoesNotContain("akabenje", audit.AuditReason!, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("accident", audit.AuditReason!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task WithNoTranslationServiceAtAll_ALugandaMessageIsHandledAsItAlwaysWas()
    {
        var fixture = Fixture.Create();

        await fixture.HandleAsync(LugandaClaim);

        var turn = Assert.Single(fixture.Conversations.Saved);
        Assert.Null(turn.CustomerLanguage);
        Assert.Null(turn.OriginalInboundText);
    }

    /// <summary>Runs only when IIP_AFRISLM_TEST_ENDPOINT points at a real model server. Drives the
    /// whole assistant with the real model and prints what the customer would receive, so the
    /// output can be read by a person. Otherwise it does nothing, and is not evidence of anything.</summary>
    [Theory]
    [Trait("Category", "Live")]
    [InlineData("Nimepata ajali na gari langu leo asubuhi kwenye barabara ya Jinja, sera yangu ni NSA/MO/2026/00009")]
    [InlineData("Nfunye akabenje ku motoka yange leero ku makya, policy yange ye NSA/MO/2026/00009")]
    public async Task LiveEngine_AnswersAClaimMessageInTheCustomersOwnLanguage(string message)
    {
        var endpoint = Environment.GetEnvironmentVariable("IIP_AFRISLM_TEST_ENDPOINT");
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return;
        }

        var provider = new InsuranceIntelligence.Infrastructure.Translation.AfriSlmTranslationProvider(
            new InsuranceIntelligence.Infrastructure.Configuration.TranslationOptions { Endpoint = endpoint, RequestTimeoutSeconds = 120 },
            new HttpClient { Timeout = TimeSpan.FromSeconds(120) });
        var fixture = Fixture.Create(translator: provider);
        fixture.SeedPolicyholder("Eugene Katusiime", "+256772118001", ("Motor", ValidPolicyNumber));

        await fixture.HandleAsync(message);

        var turn = Assert.Single(fixture.Conversations.Saved);
        var sent = Assert.Single(fixture.Outbound.SentRequests);
        Console.WriteLine("=================================================");
        Console.WriteLine($"CUSTOMER WROTE : {message}");
        Console.WriteLine($"UNDERSTOOD AS  : {turn.InboundMessageText}   [{turn.InboundTranslationStatus}]");
        Console.WriteLine($"OUTCOME        : {turn.Outcome}");
        Console.WriteLine($"CUSTOMER GOT   : ({sent.Language}) {sent.CustomerSafeBody}");
        Console.WriteLine($"OUTBOUND STATUS: {turn.OutboundTranslationStatus}");
        Console.WriteLine("=================================================");
        Assert.Equal(CustomerAssistantOutcome.CaseOpened, turn.Outcome);
    }

    [Fact]
    public async Task ForLuganda_InsuranceWordsAreKeptInEnglish_ButForKiswahiliTheyAreTranslated()
    {
        var luganda = new ScriptedTranslator { Script = (text, from, to) => to == SupportedLanguageCode.En && text.Contains("akabenje") ? EnglishOfLugandaClaim : null };
        var lugandaFixture = Fixture.Create(translator: luganda);
        lugandaFixture.SeedPolicy(ValidPolicyNumber);
        await lugandaFixture.HandleAsync(LugandaClaim);

        var swahili = new ScriptedTranslator { Script = (text, from, to) => to == SupportedLanguageCode.En && text.Contains("ajali") ? EnglishOfLugandaClaim : null };
        var swahiliFixture = Fixture.Create(translator: swahili);
        swahiliFixture.SeedPolicy(ValidPolicyNumber);
        await swahiliFixture.HandleAsync("Nimepata ajali leo asubuhi na gari langu, sera yangu ni NSA/MO/2026/00009");

        // What the engine was asked to translate on the way out.
        var toLuganda = luganda.Calls.Where(c => c.To == SupportedLanguageCode.Lg).Select(c => c.Text).ToArray();
        var toSwahili = swahili.Calls.Where(c => c.To == SupportedLanguageCode.Sw).Select(c => c.Text).ToArray();
        Assert.NotEmpty(toLuganda);
        Assert.NotEmpty(toSwahili);
        Assert.All(toLuganda, text => Assert.DoesNotContain("claim", text, StringComparison.OrdinalIgnoreCase));
        Assert.Contains(toSwahili, text => text.Contains("claim", StringComparison.OrdinalIgnoreCase));

        // ...and the customer still reads the words, restored.
        Assert.Contains("claim", Assert.Single(lugandaFixture.Outbound.SentRequests).CustomerSafeBody, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class ScriptedTranslator : ITranslationProvider
    {
        public Task<bool> IsConfiguredAsync(InsuranceIntelligence.Domain.Common.TenantScope scope, CancellationToken cancellationToken = default) => Task.FromResult(true);

        public Task<string> GetModelTagAsync(InsuranceIntelligence.Domain.Common.TenantScope scope, CancellationToken cancellationToken = default) => Task.FromResult("scripted");

        public bool Throw { get; init; }

        public Func<string, SupportedLanguageCode, SupportedLanguageCode, string?>? Script { get; init; }

        public List<(string Text, SupportedLanguageCode From, SupportedLanguageCode To)> Calls { get; } = [];

        public Task<string> TranslateAsync(string text, SupportedLanguageCode from, SupportedLanguageCode to, InsuranceIntelligence.Domain.Common.TenantScope scope, CancellationToken cancellationToken = default)
        {
            Calls.Add((text, from, to));
            if (Throw)
            {
                throw new HttpRequestException("engine down");
            }

            var scripted = Script?.Invoke(text, from, to);
            if (scripted is not null)
            {
                return Task.FromResult(scripted);
            }

            // Out of English: a tagged copy that keeps every protected value, like a well-behaved
            // engine. Into English: an unchanged copy, which the service treats as nothing to do.
            return Task.FromResult(to == SupportedLanguageCode.En ? text : $"[{SupportedLanguage.For(to).Iso}] {text}");
        }
    }
}
