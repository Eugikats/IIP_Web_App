using InsuranceIntelligence.Domain.Acquisition;
using Microsoft.EntityFrameworkCore;
using InsuranceIntelligence.Application.Ai;
using InsuranceIntelligence.Application.CustomerAssistant;
using InsuranceIntelligence.Application.Notifications;
using InsuranceIntelligence.Application.Translation;
using InsuranceIntelligence.Domain.Translation;
using InsuranceIntelligence.Infrastructure.Translation;
using InsuranceIntelligence.Application.SalesLeads;
using InsuranceIntelligence.Domain.Agents;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Compliance;
using InsuranceIntelligence.Domain.Customers;
using InsuranceIntelligence.Domain.CustomerAssistant;
using InsuranceIntelligence.Domain.Integrations;
using InsuranceIntelligence.Domain.Knowledge;
using InsuranceIntelligence.Domain.Notifications;
using InsuranceIntelligence.Domain.Policies;
using InsuranceIntelligence.Domain.Products;
using InsuranceIntelligence.LocalTestDoubles;

namespace InsuranceIntelligence.Api.Tests;

/// <summary>
/// Covers the orchestrator itself, KeywordCustomerIntentClassifierTests and
/// CustomerAutoReplyGateTests already cover its two pure building blocks in isolation. Uses the
/// real KeywordCustomerIntentClassifier (not a stub) so these tests exercise the same
/// text-in/outcome-out path a real WhatsApp message takes.
/// </summary>
public sealed partial class CustomerAssistantServiceTests
{
    private const string ValidPolicyNumber = "NSA/MO/2026/00009";

    [Fact]
    public async Task NewClaim_FromThePolicyholder_IsReadBackAndOpensOnlyAfterYes()
    {
        var fixture = Fixture.Create();
        fixture.SeedPolicyholder("Eugene Katusiime", "+256772118001", ("Motor", ValidPolicyNumber));

        await fixture.HandleAsync("A taxi hit my car at the Kira roundabout this morning and broke the lights");

        var readBack = Assert.Single(fixture.Conversations.Saved);
        Assert.Equal(CustomerAssistantOutcome.AwaitingClaimConfirmation, readBack.Outcome);
        Assert.Contains("Is that right?", readBack.OutboundReplyText);
        Assert.Contains("Kira roundabout", readBack.OutboundReplyText);
        Assert.Empty(await fixture.Cases.ListAsync(fixture.Scope)); // nothing binding on the first telling

        var opened = await fixture.HandleAsync("Yes");

        var turn = fixture.Conversations.Saved[1];
        Assert.Equal(CustomerAssistantOutcome.CaseOpened, turn.Outcome);
        Assert.Equal(turn.CaseId!.Value, Assert.Single(opened));
        var created = await fixture.Cases.GetAsync(turn.CaseId!.Value);
        Assert.True(created!.RequiresConfirmation);
        Assert.Equal(ValidPolicyNumber, created.PolicyNumber);
        Assert.Equal("WhatsApp", created.Channel);
        Assert.Contains("Kira roundabout", created.IncidentDescription);
    }

    [Fact]
    public async Task NewClaim_ReadBackCorrection_IsAddedAndReadBackAgain_NoIsStartedAgain()
    {
        var fixture = Fixture.Create();
        fixture.SeedPolicyholder("Eugene Katusiime", "+256772118001", ("Motor", ValidPolicyNumber));

        await fixture.HandleAsync("A taxi hit my car at the Kira roundabout this morning and broke the lights");
        await fixture.HandleAsync("It was yesterday evening, not this morning");
        var corrected = fixture.Conversations.Saved[1];
        Assert.Equal(CustomerAssistantOutcome.AwaitingClaimConfirmation, corrected.Outcome);
        Assert.Contains("yesterday evening", corrected.OutboundReplyText);

        await fixture.HandleAsync("No");
        Assert.Equal(CustomerAssistantOutcome.AwaitingIncidentDetails, fixture.Conversations.Saved[2].Outcome);
        Assert.Empty(await fixture.Cases.ListAsync(fixture.Scope));
    }

    [Fact]
    public async Task NewClaim_ToldAgainAfterItWasOpened_SurfacesTheExistingClaim_AndNeverOpensASecond()
    {
        var fixture = Fixture.Create();
        fixture.SeedPolicyholder("Eugene Katusiime", "+256772118001", ("Motor", ValidPolicyNumber));
        await fixture.HandleAsync("A taxi hit my car at the Kira roundabout this morning and broke the lights");
        await fixture.HandleAsync("yes");
        var first = Assert.Single(await fixture.Cases.ListAsync(fixture.Scope));

        await fixture.HandleAsync("I had an accident at the Kira roundabout this morning, a taxi hit my car");

        var turn = fixture.Conversations.Saved[^1];
        Assert.Equal(first.CaseId, turn.CaseId);
        Assert.Contains(ClaimReference.For(first.CaseId), turn.OutboundReplyText);
        Assert.Contains("reply NEW", turn.OutboundReplyText);
        Assert.Single(await fixture.Cases.ListAsync(fixture.Scope));
        Assert.Contains("More from the customer", (await fixture.Cases.GetAsync(first.CaseId))!.IncidentDescription);

        // Answering anything but NEW keeps the one claim.
        await fixture.HandleAsync("ok thanks");
        Assert.Single(await fixture.Cases.ListAsync(fixture.Scope));

        // Saying it is a different accident reads a new one back, still nothing opened until yes.
        await fixture.HandleAsync("I had an accident at the Kira roundabout this morning, a taxi hit my car");
        await fixture.HandleAsync("NEW");
        Assert.Equal(CustomerAssistantOutcome.AwaitingClaimConfirmation, fixture.Conversations.Saved[^1].Outcome);
        Assert.Null(fixture.Conversations.Saved[^1].OfferedNewIncidentForCaseId);
        Assert.Single(await fixture.Cases.ListAsync(fixture.Scope));
    }

    [Fact]
    public async Task NewClaim_FromANumberWithNoCover_OpensNothingAndOffersAPerson()
    {
        var fixture = Fixture.Create();

        await fixture.HandleAsync("I had an accident and need to make a claim");

        var turn = Assert.Single(fixture.Conversations.Saved);
        Assert.Equal(CustomerIntent.NewClaim, turn.ClassifiedIntent);
        Assert.Equal(CustomerAssistantOutcome.Declined, turn.Outcome);
        Assert.Null(turn.CaseId);
        Assert.Empty(await fixture.Cases.ListAsync(fixture.Scope));
        Assert.DoesNotContain("PERSON", turn.OutboundReplyText);
        Assert.DoesNotContain("policy number", turn.OutboundReplyText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExistingClaimStatus_FromAnUnrecognisedNumber_NeverDisclosesAnything()
    {
        var fixture = Fixture.Create();
        var existingCaseId = fixture.SeedCase(ValidPolicyNumber);

        await fixture.HandleAsync("What is the status of my claim?");

        var turn = Assert.Single(fixture.Conversations.Saved);
        Assert.Equal(CustomerIntent.ExistingClaimStatus, turn.ClassifiedIntent);
        Assert.Equal(CustomerAssistantOutcome.Declined, turn.Outcome);
        Assert.Null(turn.CaseId);
        Assert.DoesNotContain(existingCaseId.ToString()[..8], turn.OutboundReplyText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ATypedPolicyNumber_FromAnUnrecognisedNumber_DisclosesNoClaimStatus()
    {
        var fixture = Fixture.Create();
        fixture.SeedPolicy(ValidPolicyNumber);
        var existingCaseId = fixture.SeedCase(ValidPolicyNumber, CaseStatus.InReview);

        await fixture.HandleAsync($"What is the status of my claim on {ValidPolicyNumber}?");

        var turn = Assert.Single(fixture.Conversations.Saved);
        Assert.Equal(CustomerAssistantOutcome.Declined, turn.Outcome);
        Assert.Null(turn.CaseId);
        Assert.DoesNotContain(ClaimReference.For(existingCaseId), turn.OutboundReplyText);
        Assert.DoesNotContain("review", turn.OutboundReplyText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ATypedPolicyNumber_FromAnUnrecognisedNumber_NeverOpensAClaimOrIssuesALink()
    {
        var fixture = Fixture.Create(withAccessLinks: true);
        fixture.SeedPolicy(ValidPolicyNumber);

        await fixture.HandleAsync("I had an accident on Jinja Road this morning");
        await fixture.HandleAsync(ValidPolicyNumber);
        await fixture.HandleAsync($"I was in an accident on Jinja Road, my policy is {ValidPolicyNumber}");
        await fixture.HandleAsync("yes");
        await fixture.HandleAsync("Please send me the link to upload my documents");

        Assert.Empty(await fixture.Cases.ListAsync(fixture.Scope));
        Assert.Empty(fixture.LinkIssuer.Issued);
        Assert.DoesNotContain(fixture.Outbound.SentRequests, r => r.CustomerSafeBody.Contains("access/verify", StringComparison.Ordinal));
    }

    [Fact]
    public async Task APolicyholderTypingSomeoneElsesPolicyNumber_IsDeclined()
    {
        var fixture = Fixture.Create();
        fixture.SeedPolicyholder("Eugene Katusiime", "+256772118001", ("Motor", ValidPolicyNumber));
        fixture.SeedPolicy("NSA/MO/2026/00077");

        await fixture.HandleAsync("I was in an accident on Jinja Road this morning, my policy is NSA/MO/2026/00077");

        Assert.Equal(CustomerAssistantOutcome.Declined, Assert.Single(fixture.Conversations.Saved).Outcome);
        Assert.Empty(await fixture.Cases.ListAsync(fixture.Scope));
    }



    private const string DemoPhone = "256772118001";

    [Fact]
    public async Task Reset_FromTheDemoPhone_ClearsTheNumberAndTheNextMessageStartsFresh()
    {
        var fixture = Fixture.Create(demoPhone: DemoPhone);
        fixture.SeedPolicyholder("Aisha Nakato", "+256772118001", ("Motor", ValidPolicyNumber));
        var openClaim = fixture.SeedCase(ValidPolicyNumber, CaseStatus.MissingDocuments);
        await fixture.LanguagePreferences.SaveAsync(new CustomerLanguagePreference
        {
            PreferenceId = Guid.NewGuid(), Scope = fixture.Scope, ContactWaId = DemoPhone,
            Language = SupportedLanguageCode.Lg, SetAt = DateTimeOffset.UtcNow,
        });
        await fixture.HandleAsync("I had an accident"); // leaves the thread waiting for incident details

        await fixture.HandleAsync("#reset");

        var customer = Assert.Single(fixture.Customers.Customers);
        Assert.Equal("Erased customer", customer.FullName);
        Assert.Null(customer.Phone);
        Assert.Equal(CaseStatus.Closed, (await fixture.Cases.GetAsync(openClaim))!.Status);
        Assert.Null(await fixture.LanguagePreferences.GetCurrentAsync(fixture.Scope, DemoPhone));
        Assert.Contains(fixture.Audit.Written, e => e.ActionName == "demo.session_reset");
        Assert.Contains(fixture.Audit.Written, e => e.ActionName == "case.closed" && e.CaseId == openClaim);
        Assert.StartsWith("Reset done", fixture.Outbound.SentRequests[^1].CustomerSafeBody);
        Assert.Equal(CustomerAssistantOutcome.SessionReset, fixture.Conversations.Saved[^1].Outcome);

        // The earlier "waiting for incident details" belongs to the previous participant.
        await fixture.HandleAsync("Hi");
        var turn = fixture.Conversations.Saved[^1];
        Assert.NotEqual(CustomerIntent.NewClaim, turn.ClassifiedIntent);
        Assert.DoesNotContain("Aisha", turn.OutboundReplyText);
    }

    [Fact]
    public async Task Reset_FromAnyOtherNumber_IsAnOrdinaryMessageAndChangesNothing()
    {
        var fixture = Fixture.Create(demoPhone: DemoPhone);
        fixture.SeedPolicyholder("Brian Okello", "+256700000001", ("Motor", ValidPolicyNumber));
        var openClaim = fixture.SeedCase(ValidPolicyNumber, CaseStatus.MissingDocuments);

        await fixture.HandleFromAsync("#reset", "256700000001");

        Assert.Equal("Brian Okello", Assert.Single(fixture.Customers.Customers).FullName);
        Assert.Equal(CaseStatus.MissingDocuments, (await fixture.Cases.GetAsync(openClaim))!.Status);
        Assert.DoesNotContain(fixture.Audit.Written, e => e.ActionName == "demo.session_reset");
        Assert.DoesNotContain(fixture.Outbound.SentRequests, r => r.CustomerSafeBody.StartsWith("Reset done", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("#Reset")]
    [InlineData("reset")]
    [InlineData("please #reset")]
    [InlineData("#reset now")]
    public async Task Reset_OnlyTheExactCommandTriggersIt(string text)
    {
        var fixture = Fixture.Create(demoPhone: DemoPhone);
        fixture.SeedPolicyholder("Aisha Nakato", "+256772118001", ("Motor", ValidPolicyNumber));

        await fixture.HandleAsync(text);

        Assert.Equal("Aisha Nakato", Assert.Single(fixture.Customers.Customers).FullName);
        Assert.DoesNotContain(fixture.Audit.Written, e => e.ActionName == "demo.session_reset");
    }

    [Fact]
    public async Task Reset_WorksEvenWhenTheAssistantIsSwitchedOff()
    {
        var fixture = Fixture.Create(agentEnabled: false, demoPhone: DemoPhone);
        fixture.SeedPolicyholder("Aisha Nakato", "+256772118001", ("Motor", ValidPolicyNumber));

        await fixture.HandleAsync("  #reset  ");

        Assert.Equal("Erased customer", Assert.Single(fixture.Customers.Customers).FullName);
        Assert.Contains(fixture.Audit.Written, e => e.ActionName == "demo.session_reset");
    }

    [Fact]
    public void Reset_IsOffOutsideTheDemoProfileAndForUnconfiguredNumbers()
    {
        Assert.False(DemoSessionSettings.Disabled.Allows(DemoPhone));
        Assert.False(new DemoSessionSettings(true, null).Allows(DemoPhone));
        Assert.False(new DemoSessionSettings(false, DemoPhone).Allows(DemoPhone));
        Assert.True(new DemoSessionSettings(true, "+256772118001").Allows(DemoPhone));
        Assert.True(new DemoSessionSettings(true, "0772118001").Allows(DemoPhone));
    }

    [Fact]
    public async Task TheCustomerSeesTypingOnTheirOwnMessageBeforeTheReplyArrives()
    {
        var fixture = Fixture.Create();

        await fixture.HandleAsync("Hi");

        var events = fixture.Outbound.Events;
        Assert.StartsWith("typing:wamid.", events[0]);
        Assert.Equal("send", events[^1]);
        Assert.Single(events, e => e.StartsWith("typing:", StringComparison.Ordinal)); // a fast reply needs no refresh
    }

    [Fact]
    public async Task NoTypingIsShownWhenTheAssistantIsSwitchedOff()
    {
        // With the kill switch on, the assistant is not the one replying, so it must not look as if it is.
        var fixture = Fixture.Create(agentEnabled: false);

        await fixture.HandleAsync("Hi");

        Assert.DoesNotContain(fixture.Outbound.Events, e => e.StartsWith("typing:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Greeting_GetsTheWelcomeMenuNotAnEscalation()
    {
        var fixture = Fixture.Create();

        await fixture.HandleNamedAsync("Hi", "~Sarah N.");

        var turn = Assert.Single(fixture.Conversations.Saved);
        Assert.Equal(CustomerAssistantOutcome.AwaitingMoreInformation, turn.Outcome);
        Assert.StartsWith("Hello, Sarah. I'm Kato, Nile Shield's helper", turn.OutboundReplyText);
        Assert.Contains("get your money", turn.OutboundReplyText);
        Assert.DoesNotContain("PERSON", turn.OutboundReplyText);
        Assert.DoesNotContain("voice note", turn.OutboundReplyText); // voice is not offered until it works
        AssertOnlyAFirstContactRecord(fixture);
    }

    [Fact]
    public async Task ExistingClaimStatus_WithUnrecognizedPolicyNumber_IsDeclined()
    {
        var fixture = Fixture.Create();
        // Note: no SeedPolicy call, the number is well-formed but unknown to the repository.

        await fixture.HandleAsync($"What is the status of my claim on {ValidPolicyNumber}?");

        var turn = Assert.Single(fixture.Conversations.Saved);
        Assert.Equal(CustomerAssistantOutcome.Declined, turn.Outcome);
        Assert.Null(turn.CaseId);
    }

    [Fact]
    public async Task GeneralQuestion_WithNoRetrievalChunks_NeverAutoSends()
    {
        var fixture = Fixture.Create(autoReplyEnabled: true);
        fixture.Retrieval.ChunksToReturn = [];

        await fixture.HandleAsync("What does my policy cover?");

        var turn = Assert.Single(fixture.Conversations.Saved);
        Assert.Equal(CustomerIntent.GeneralQuestion, turn.ClassifiedIntent);
        Assert.Equal(CustomerAssistantOutcome.QueuedForHumanApproval, turn.Outcome);
        var sent = Assert.Single(fixture.Outbound.SentRequests);
        Assert.Contains("get back to you shortly", sent.CustomerSafeBody);
    }

    [Fact]
    public async Task GeneralQuestion_CitedAndWithinLimitAndTenantOptedIn_AutoRepliesAndSends()
    {
        var fixture = Fixture.Create(autoReplyEnabled: true);
        fixture.Retrieval.ChunksToReturn = [fixture.MakeChunk()];
        fixture.Assistant.AnswerToReturn = new AssistantAnswer(
            "Your motor policy covers own damage and third-party liability.",
            AiConfidenceScore.Create(0.9m),
            EscalationRequired: false,
            Sources: [new SourceCitation(Guid.NewGuid(), "chunk-1", "Motor Policy Wording", "Motor", "v1", "excerpt", "Motor Policy Wording")],
            GuardrailMessage: "",
            RiskFlag: "None");

        await fixture.HandleAsync("What does my policy cover?");

        var turn = Assert.Single(fixture.Conversations.Saved);
        Assert.Equal(CustomerAssistantOutcome.AutoReplied, turn.Outcome);
        var sent = Assert.Single(fixture.Outbound.SentRequests);
        Assert.EndsWith(fixture.Assistant.AnswerToReturn.AnswerText, sent.CustomerSafeBody);
        Assert.StartsWith(KatoReplies.FirstContactPrefix(null), sent.CustomerSafeBody); // an unknown number meets Kato first
        Assert.Single(fixture.WhatsAppMessages.Saved);
    }

    [Fact]
    public async Task GeneralQuestion_TenantNotOptedIn_QueuesForHumanApprovalEvenWithCitation()
    {
        var fixture = Fixture.Create(autoReplyEnabled: false);
        fixture.Retrieval.ChunksToReturn = [fixture.MakeChunk()];
        fixture.Assistant.AnswerToReturn = new AssistantAnswer(
            "Your motor policy covers own damage and third-party liability.",
            AiConfidenceScore.Create(0.9m),
            EscalationRequired: false,
            Sources: [new SourceCitation(Guid.NewGuid(), "chunk-1", "Motor Policy Wording", "Motor", "v1", "excerpt", "Motor Policy Wording")],
            GuardrailMessage: "",
            RiskFlag: "None");

        await fixture.HandleAsync("What does my policy cover?");

        var turn = Assert.Single(fixture.Conversations.Saved);
        Assert.Equal(CustomerAssistantOutcome.QueuedForHumanApproval, turn.Outcome);
        // The held AI answer never reaches the customer; only the fixed holding message does.
        var sent = Assert.Single(fixture.Outbound.SentRequests);
        Assert.DoesNotContain("own damage", sent.CustomerSafeBody);
        Assert.Contains("get back to you shortly", sent.CustomerSafeBody);
    }

    /// <summary>Writing in puts someone in acquisition at once, as a new contact: no interest, no hand-over, only the fact that they wrote.</summary>
    private static SalesLeadDetailReadModel AssertOnlyAFirstContactRecord(Fixture fixture)
    {
        var lead = Assert.Single(fixture.SalesLeads.Created);
        Assert.Equal("WhatsApp", lead.LeadSource);
        Assert.Null(lead.HandedToPersonAt);
        Assert.Empty(lead.Interests);
        Assert.Equal("Wrote in on WhatsApp", lead.Activity[0].Text);
        return lead;
    }

    [Fact]
    public async Task ProductInterest_WithNoCover_AsksWhatWorkTheyDo_ThenExplainsFittingCoverAndCreatesALead()
    {
        var fixture = Fixture.Create();

        await fixture.HandleAsync("I'm interested in getting a quote for insurance");
        Assert.Equal(CustomerAssistantOutcome.AwaitingOccupation, Assert.Single(fixture.Conversations.Saved).Outcome);
        var firstContact = AssertOnlyAFirstContactRecord(fixture);

        await fixture.HandleAsync("I sell tomatoes in Nakasero market");

        var turn = fixture.Conversations.Saved[1];
        Assert.Equal(CustomerIntent.ProductInterest, turn.ClassifiedIntent);
        Assert.Equal(CustomerAssistantOutcome.LeadCreated, turn.Outcome);
        Assert.Equal("I sell tomatoes in Nakasero market", turn.Occupation);
        Assert.Contains("Market Stall Stock", turn.OutboundReplyText);
        Assert.Contains("Hospital Cash", turn.OutboundReplyText);
        Assert.Contains("nothing is sold in this chat", turn.OutboundReplyText);
        var lead = Assert.Single(fixture.SalesLeads.Created);
        Assert.Equal(firstContact.LeadId, lead.LeadId);
        Assert.Contains("Market Stall Stock", lead.ProductFit);

        // Outcome P: "route it to a licensed person." A lead has no assigned officer at
        // creation, so routing is the same team-broadcast ApprovalRequired already uses.
        var notification = Assert.Single(fixture.TeamsQueue.Enqueued);
        Assert.Equal(TeamsNotificationType.LeadCreated, notification.NotificationType);
    }


    [Fact]
    public async Task ANewContact_IsInAcquisitionFromTheFirstMessage_UnderTheNameOnTheirWhatsAppProfile()
    {
        var fixture = Fixture.Create();

        await fixture.HandleNamedAsync("Hello", "Sarah Namukasa");

        var lead = AssertOnlyAFirstContactRecord(fixture);
        Assert.Equal("Sarah Namukasa", lead.CustomerName);
        Assert.Equal("Sarah Namukasa", Assert.Single(fixture.Customers.Customers).FullName);
        Assert.NotNull(lead.ThreadId);
        Assert.Equal("WhatsApp", lead.PrimaryChannel);
        Assert.True(InsuranceIntelligence.Domain.Customers.PhoneNumbers.SameNumber("256772118001", lead.Phone));
    }

    [Fact]
    public async Task WritingAgain_DoesNotMakeASecondRecord_AndKeepsTheProfileName()
    {
        var fixture = Fixture.Create();

        await fixture.HandleNamedAsync("Hello", "Sarah Namukasa");
        await fixture.HandleNamedAsync("What covers do you have?", "Sarah Namukasa");

        Assert.Equal("Sarah Namukasa", Assert.Single(fixture.SalesLeads.Created).CustomerName);
    }

    [Fact]
    public async Task WithNoUsableProfileName_TheRecordUsesAPlaceholder_UntilThePersonSaysWhoTheyAre()
    {
        var fixture = Fixture.Create();

        await fixture.HandleNamedAsync("Hello", "\U0001F600");
        Assert.Equal("WhatsApp customer", Assert.Single(fixture.SalesLeads.Created).CustomerName);

        await fixture.HandleNamedAsync("My name is Moses Kato", "\U0001F600");

        var lead = Assert.Single(fixture.SalesLeads.Created);
        Assert.Equal("Moses Kato", lead.CustomerName);
        Assert.Contains(lead.Activity, entry => entry.Text == "Told Kato their name");
        Assert.Equal("Moses Kato", Assert.Single(fixture.Customers.Customers).FullName);
    }

    [Fact]
    public async Task ANameThePersonStates_IsBelievedOverTheirProfileName_OnBothRecords()
    {
        var fixture = Fixture.Create();

        await fixture.HandleNamedAsync("Hello", "Sam K");
        await fixture.HandleNamedAsync("call me Samuel Okello", "Sam K");

        Assert.Equal("Samuel Okello", Assert.Single(fixture.SalesLeads.Created).CustomerName);
        Assert.Equal("Samuel Okello", Assert.Single(fixture.Customers.Customers).FullName);
    }

    [Fact]
    public async Task ANameThePersonStates_NeverReplacesANameSomeoneOnTheTeamSet()
    {
        var fixture = Fixture.Create();
        await fixture.HandleNamedAsync("Hello", "Sam K");
        var lead = Assert.Single(fixture.SalesLeads.Created);
        await fixture.SalesLeads.CreateAsync("tenant-ca", "workspace-ca", lead with { CustomerName = "Samuel Okello (Equator staff list)" });

        await fixture.HandleNamedAsync("my name is Someone Else", "Sam K");

        Assert.Equal("Samuel Okello (Equator staff list)", Assert.Single(fixture.SalesLeads.Created).CustomerName);
    }

    [Theory]
    [InlineData("I am hungry")]
    [InlineData("I'm interested in cover")]
    [InlineData("This is urgent")]
    [InlineData("my name is the other one")]
    public void ASentenceThatIsNotAnIntroduction_IsNotTakenAsAName(string message) =>
        Assert.Null(CustomerAssistantService.IntroducedName(message));

    [Theory]
    [InlineData("My name is Grace Nakato", "Grace Nakato")]
    [InlineData("call me sam", "Sam")]
    [InlineData("Naitwa Juma", "Juma")]
    public void AnUnambiguousIntroduction_GivesTheName(string message, string expected) =>
        Assert.Equal(expected, CustomerAssistantService.IntroducedName(message));

    [Fact]
    public async Task ACustomerWithCover_IsNotMadeAProspectByWritingIn()
    {
        var fixture = Fixture.Create();
        fixture.SeedPolicyholder("Brian Okello", "+256772118001", ("Motor", ValidPolicyNumber));

        await fixture.HandleNamedAsync("Hello", "Bee");

        Assert.Empty(fixture.SalesLeads.Created);
    }

    [Fact]
    public async Task TheAgent_SavesANameAsSoonAsItHearsOne_WithoutRegisteringThemForCover()
    {
        var model = new ScriptedAgentModel()
            .Calls("save_name", """{"full_name":"Aisha Namutebi"}""")
            .Says("Nice to meet you, Aisha. What do you do for work?");
        var fixture = Fixture.Create(agent: model);

        await fixture.HandleNamedAsync("Hi, I'm Aisha Namutebi", "~");

        var lead = Assert.Single(fixture.SalesLeads.Created);
        Assert.Equal("Aisha Namutebi", lead.CustomerName);
        Assert.Equal("Aisha Namutebi", Assert.Single(fixture.Customers.Customers).FullName);
        Assert.Empty(await fixture.Policies.ListByCustomerAsync(fixture.Customers.Customers[0].CustomerId, fixture.Scope, default));
        Assert.Contains("saved", model.ToolResults[0]);
    }

    private static InsuranceIntelligence.Application.Workspaces.IWorkspaceRecordStore OutcomeStore() =>
        new InsuranceIntelligence.Infrastructure.Persistence.EfWorkspaceRecordStore(new InsuranceIntelligence.Infrastructure.Persistence.InsuranceIntelligenceDbContext(
            new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<InsuranceIntelligence.Infrastructure.Persistence.InsuranceIntelligenceDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options));

    private static async Task<(InsuranceIntelligence.Application.Rules.DecisionLog Log, TenantScope Scope)> LogWithReasonAsync(string phone, DateTimeOffset sentAt)
    {
        var db = new InsuranceIntelligence.Infrastructure.Persistence.InsuranceIntelligenceDbContext(
            new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<InsuranceIntelligence.Infrastructure.Persistence.InsuranceIntelligenceDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var log = new InsuranceIntelligence.Application.Rules.DecisionLog(new InsuranceIntelligence.Infrastructure.Persistence.EfWorkspaceRecordStore(db));
        var scope = TenantScope.Create("tenant-ca", "workspace-ca");
        await log.SaveReasonAsync(scope, new InsuranceIntelligence.Application.Rules.ConversationReason(
            phone, "rule-1", "Loan Protector", "loan_disbursed", "Loan disbursed", "Mpingo Loan Protector", "Mpingo Life Assurance", "bank_cover_intro",
            "Hello Eugene, this is Kato from Mavuno Bank.", sentAt, Demonstration: false));
        return (log, scope);
    }

    [Fact]
    public async Task TheAgent_KnowsWhyTheConversationBegan_WhenARuleWroteFirst()
    {
        var (log, _) = await LogWithReasonAsync("256790000001", DateTimeOffset.UtcNow.AddHours(-3));
        var model = new ScriptedAgentModel().Says("Mpingo Loan Protector pays off your loan.");
        var fixture = Fixture.Create(agent: model, decisionLog: log);

        await fixture.HandleFromAsync("Yes, show me", "256790000001");

        var system = model.Conversations[0][0].Content!;
        Assert.Contains("Why this conversation exists", system);
        Assert.Contains("loan disbursed", system);
        Assert.Contains("Mpingo Loan Protector", system);
        Assert.Contains("Mpingo Life Assurance provides and pays", system);
        Assert.DoesNotContain("8,000,000", system);
    }

    [Fact]
    public async Task ABanksAgent_IsToldToAnswerHonestlyThatTheBankEarnsACommission()
    {
        using var bank = KatoBrand.Use(new KatoBrand("Mavuno Bank", "Mavuno Bank", [MicroinsuranceCatalog.Mpingo, MicroinsuranceCatalog.Kifaru]));
        var model = new ScriptedAgentModel().Says("Hello.");

        await Fixture.Create(agent: model).HandleFromAsync("Hi", "256790000555");

        Assert.Contains("earns a commission from the insurer", model.Conversations[0][0].Content!);
        Assert.Contains("never changes which cover you recommend", model.Conversations[0][0].Content!);
    }

    [Fact]
    public async Task Stop_IsAnsweredOnceAndPlainly_WithdrawsConsent_AndTheModelIsNeverAsked()
    {
        var (log, _) = await LogWithReasonAsync("256790000001", DateTimeOffset.UtcNow.AddHours(-2));
        var model = new ScriptedAgentModel().Says("This must not be said.");
        var fixture = Fixture.Create(agent: model, decisionLog: log, stopHandling: true);

        await fixture.HandleFromAsync("STOP", "256790000001");

        Assert.Empty(model.Conversations);
        var sent = Assert.Single(fixture.Outbound.SentRequests);
        Assert.Contains("You will not get any more messages", sent.CustomerSafeBody);
        var lead = Assert.Single(fixture.SalesLeads.Created, item => item.Consent?.Status == ContactConsentStatus.Withdrawn);
        Assert.Contains(lead.Activity, activity => activity.Text.Contains("STOP"));
    }

    [Fact]
    public async Task AnyoneWhoSaysStop_IsHonoured_EvenWhenNoRuleWroteToThem()
    {
        var model = new ScriptedAgentModel().Says("This must not be said.");
        var fixture = Fixture.Create(agent: model, stopHandling: true);

        await fixture.HandleFromAsync("stop", "256790000222");

        Assert.Empty(model.Conversations);
        Assert.Contains("You will not get any more messages", Assert.Single(fixture.Outbound.SentRequests).CustomerSafeBody);
    }

    [Fact]
    public async Task OnARenewalReminderTheAgentIsToldAPersonCompletesTheRenewal()
    {
        var db = new InsuranceIntelligence.Infrastructure.Persistence.InsuranceIntelligenceDbContext(
            new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<InsuranceIntelligence.Infrastructure.Persistence.InsuranceIntelligenceDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var log = new InsuranceIntelligence.Application.Rules.DecisionLog(new InsuranceIntelligence.Infrastructure.Persistence.EfWorkspaceRecordStore(db));
        await log.SaveReasonAsync(TenantScope.Create("tenant-ca", "workspace-ca"), new InsuranceIntelligence.Application.Rules.ConversationReason(
            "256790000001", "rule-1", "Renewal, 30 days", "policy_ending_30", "Policy ends within 30 days", "Nile Shield Life Family Funeral Cover", "Nile Shield Life Assurance",
            "renewal_30_days", "Hello Sarah, your cover ends on 30 November.", DateTimeOffset.UtcNow.AddHours(-1), Demonstration: false));
        var model = new ScriptedAgentModel().Says("A colleague will help.");

        await Fixture.Create(agent: model, decisionLog: log).HandleFromAsync("Yes, renew", "256790000001");

        var system = model.Conversations[0][0].Content!;
        Assert.Contains("reminder about a policy they already hold", system);
        Assert.Contains("a colleague will complete it with them", system);
        Assert.DoesNotContain("explain Nile Shield Life Family Funeral Cover from the approved wording", system);
    }

    [Fact]
    public async Task TheAgent_IgnoresAReasonThatIsTooOld_OrBelongsToAnotherNumber()
    {
        var (oldLog, _) = await LogWithReasonAsync("256790000001", DateTimeOffset.UtcNow.AddDays(-30));
        var model = new ScriptedAgentModel().Says("Hello.");
        await Fixture.Create(agent: model, decisionLog: oldLog).HandleFromAsync("Hi", "256790000001");
        Assert.DoesNotContain("Why this conversation exists", model.Conversations[0][0].Content!);

        var (log, _) = await LogWithReasonAsync("256790000001", DateTimeOffset.UtcNow.AddHours(-1));
        var other = new ScriptedAgentModel().Says("Hello.");
        await Fixture.Create(agent: other, decisionLog: log).HandleFromAsync("Hi", "256790000777");
        Assert.DoesNotContain("Why this conversation exists", other.Conversations[0][0].Content!);
    }

    [Fact]
    public async Task TheAgent_IsToldWhenWhatItWasGivenIsNotAName()
    {
        var model = new ScriptedAgentModel()
            .Calls("save_name", """{"full_name":"!!"}""")
            .Says("What would you like me to call you?");
        var fixture = Fixture.Create(agent: model);

        await fixture.HandleNamedAsync("hello", "~");

        Assert.Contains("not_a_name", model.ToolResults[0]);
        Assert.Equal("WhatsApp customer", Assert.Single(fixture.SalesLeads.Created).CustomerName);
    }

    [Theory]
    [InlineData("I sell tomatoes in Nakasero", "Market Stall Stock")]
    [InlineData("I ride a boda boda in Kawempe", "Boda Rider Accident")]
    [InlineData("I work as a tailor", "Hospital Cash")]
    public async Task AFirstMessageDescribingWork_FromSomeoneWithNoCover_GetsFittingCoverExplained(string message, string expectedProduct)
    {
        var fixture = Fixture.Create();

        await fixture.HandleAsync(message);

        var turn = Assert.Single(fixture.Conversations.Saved);
        Assert.Equal(CustomerAssistantOutcome.LeadCreated, turn.Outcome);
        Assert.Contains(expectedProduct, turn.OutboundReplyText);
        Assert.Contains("UGX", turn.OutboundReplyText);
        Assert.DoesNotContain("IRA/", turn.OutboundReplyText);
    }

    [Fact]
    public async Task HumanRequested_EscalatesAndCreatesLead()
    {
        var fixture = Fixture.Create();

        await fixture.HandleAsync("I want to speak to a human please");

        var turn = Assert.Single(fixture.Conversations.Saved);
        Assert.Equal(CustomerIntent.HumanRequested, turn.ClassifiedIntent);
        Assert.Equal(CustomerAssistantOutcome.Escalated, turn.Outcome);
        Assert.Single(fixture.SalesLeads.Created);
        Assert.Single(fixture.TeamsQueue.Enqueued);
    }

    [Fact]
    public async Task EveryOutcome_WritesAnAuditEvent()
    {
        var fixture = Fixture.Create();

        await fixture.HandleAsync("I'm interested in getting a quote");

        var auditEvent = Assert.Single(fixture.Audit.Written);
        Assert.Equal("system:customer-assistant", auditEvent.UserId);
        Assert.Equal(ActorType.Integration, auditEvent.ActorType);
        Assert.StartsWith("customer_assistant.", auditEvent.ActionName);
    }

    [Fact]
    public async Task AgentDisabled_EscalatesEveryMessageWithoutClassifyingOrActing()
    {
        // The kill switch (Admin > Agents) must actually stop the agent, not just record a
        // database row nothing reads, no case, no lead, no classification, every message
        // escalated to a human instead.
        var fixture = Fixture.Create(agentEnabled: false);
        fixture.SeedPolicy(ValidPolicyNumber);

        await fixture.HandleAsync($"I was in an accident, my policy is {ValidPolicyNumber}");

        var turn = Assert.Single(fixture.Conversations.Saved);
        Assert.Equal(CustomerIntent.Unknown, turn.ClassifiedIntent); // never classified, the gate short-circuits before that
        Assert.Equal(CustomerAssistantOutcome.Escalated, turn.Outcome);
        Assert.Null(turn.CaseId);
        Assert.Empty(await fixture.Cases.ListAsync(fixture.Scope));
        var auditEvent = Assert.Single(fixture.Audit.Written);
        Assert.Equal("customer_assistant.agent_disabled", auditEvent.ActionName);
        Assert.Contains("disabled", auditEvent.AuditReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Media_FromUnknownSender_IsAcknowledgedAndAsksWhichClaim()
    {
        var fixture = Fixture.Create();

        await fixture.HandleMediaAsync();

        var turn = Assert.Single(fixture.Conversations.Saved);
        Assert.Equal(CustomerAssistantOutcome.DocumentReceived, turn.Outcome);
        Assert.Null(turn.CaseId);
        Assert.Contains("Someone from our team will look at it", Assert.Single(fixture.Outbound.SentRequests).CustomerSafeBody);
    }

    [Fact]
    public async Task Media_FromRecognisedSender_IsAcknowledgedAgainstTheirOpenClaim_OncePerBurst()
    {
        var fixture = Fixture.Create();
        fixture.SeedPolicyholder("Eugene Katusiime", "+256772118001", ("Motor", ValidPolicyNumber));
        var caseId = fixture.SeedCase(ValidPolicyNumber, CaseStatus.MissingDocuments);

        await fixture.HandleMediaAsync();
        await fixture.HandleMediaAsync();

        Assert.Equal(2, fixture.Conversations.Saved.Count);
        Assert.All(fixture.Conversations.Saved, turn => Assert.Equal(caseId, turn.CaseId));
        var sent = Assert.Single(fixture.Outbound.SentRequests); // a burst of photos gets one reply
        Assert.Contains(ClaimReference.For(caseId), sent.CustomerSafeBody);
        Assert.Contains("Eugene", sent.CustomerSafeBody);
    }

    [Fact]
    public async Task RecognisedSender_IsGreetedByName()
    {
        var fixture = Fixture.Create();
        fixture.SeedPolicyholder("Eugene Katusiime", "+256772118001", ("Motor", ValidPolicyNumber));

        await fixture.HandleAsync("Hello");

        Assert.Contains("Hello Eugene, it's Kato from Nile Shield", Assert.Single(fixture.Conversations.Saved).OutboundReplyText);
    }

    [Fact]
    public async Task RecognisedSender_WithOnePolicy_IsAskedWhatHappened_ThenTheClaimOpensWithoutAPolicyNumber()
    {
        var fixture = Fixture.Create(withAccessLinks: true);
        fixture.SeedPolicyholder("Eugene Katusiime", "+256772118001", ("Motor", ValidPolicyNumber));

        await fixture.HandleAsync("I had an accident");
        var first = Assert.Single(fixture.Conversations.Saved);
        Assert.Equal(CustomerAssistantOutcome.AwaitingIncidentDetails, first.Outcome);
        Assert.Contains("what happened", first.OutboundReplyText);
        Assert.DoesNotContain("reply with your policy number", first.OutboundReplyText);

        await fixture.HandleAsync("A taxi hit the back of my car at the Kira roundabout this morning");
        Assert.Equal(CustomerAssistantOutcome.AwaitingClaimConfirmation, fixture.Conversations.Saved[1].Outcome);
        var opened = await fixture.HandleAsync("yes");

        var second = fixture.Conversations.Saved[2];
        Assert.Equal(CustomerAssistantOutcome.CaseOpened, second.Outcome);
        Assert.Equal(second.CaseId!.Value, Assert.Single(opened)); // returned so the webhook can start the claim review
        var created = await fixture.Cases.GetAsync(second.CaseId!.Value);
        Assert.Equal(ValidPolicyNumber, created!.PolicyNumber);
        Assert.Equal("Eugene Katusiime", created.CustomerName);
        Assert.Contains("I had an accident", created.IncidentDescription);
        Assert.Contains("Kira roundabout", created.IncidentDescription);
        Assert.True(created.RequiresConfirmation); // a claims officer still confirms every WhatsApp claim
        Assert.Contains("https://portal.example/access/verify?token=", second.OutboundReplyText);
        Assert.Contains("police abstract", second.OutboundReplyText);
        Assert.Equal(created.CaseId, Assert.Single(fixture.LinkIssuer.Issued).CaseId);
    }

    [Fact]
    public async Task RecognisedSender_WithTwoPolicies_IsAskedWhichCover_AndTheChoiceCarriesThrough()
    {
        var fixture = Fixture.Create();
        fixture.SeedPolicyholder("Eugene Katusiime", "+256772118001", ("Motor", ValidPolicyNumber), ("Medical", "NSA/ME/2026/00010"));

        await fixture.HandleAsync("I had an accident");
        Assert.Equal(CustomerAssistantOutcome.AwaitingPolicyChoice, fixture.Conversations.Saved[0].Outcome);

        // Listed alphabetically: 1. Medical, 2. Motor.
        await fixture.HandleAsync("2");
        Assert.Equal(CustomerAssistantOutcome.AwaitingIncidentDetails, fixture.Conversations.Saved[1].Outcome);
        Assert.Equal(ValidPolicyNumber, fixture.Conversations.Saved[1].PolicyNumber);

        await fixture.HandleAsync("Someone reversed into my parked car outside the office yesterday afternoon");
        await fixture.HandleAsync("yes");

        var created = await fixture.Cases.GetAsync(fixture.Conversations.Saved[3].CaseId!.Value);
        Assert.Equal(ValidPolicyNumber, created!.PolicyNumber);
        Assert.DoesNotContain("\n2", created.IncidentDescription); // the "2" answered a question, it is not part of the story
        Assert.Equal(DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1), created.IncidentDate);
    }

    [Fact]
    public async Task RecognisedSender_WhoNamesTheCover_IsNotAskedWhichOne()
    {
        var fixture = Fixture.Create();
        fixture.SeedPolicyholder("Eugene Katusiime", "+256772118001", ("Motor", ValidPolicyNumber), ("Medical", "NSA/ME/2026/00010"));

        await fixture.HandleAsync("My car was hit by a boda boda at the Jinja Road roundabout this morning");

        var turn = Assert.Single(fixture.Conversations.Saved);
        Assert.Equal(CustomerAssistantOutcome.AwaitingClaimConfirmation, turn.Outcome);
        Assert.Equal(ValidPolicyNumber, turn.PolicyNumber);
    }

    [Fact]
    public async Task RecognisedSender_ClaimStatus_ListsOnlyTheirOwnOpenClaims_WithoutAskingForAPolicyNumber()
    {
        var fixture = Fixture.Create();
        fixture.SeedPolicyholder("Eugene Katusiime", "+256772118001", ("Motor", ValidPolicyNumber));
        var theirs = fixture.SeedCase(ValidPolicyNumber, CaseStatus.InReview);
        fixture.SeedPolicy("NSA/MO/2026/00077");
        var someoneElses = fixture.SeedCase("NSA/MO/2026/00077", CaseStatus.PendingApproval);

        await fixture.HandleAsync("What is the status of my claim?");

        var reply = Assert.Single(fixture.Conversations.Saved).OutboundReplyText!;
        Assert.Contains(ClaimReference.For(theirs), reply);
        Assert.Contains("under review", reply, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(ClaimReference.For(someoneElses), reply);
    }

    [Fact]
    public async Task SharedNumber_OnFileForTwoDifferentPeople_RecognisesNobody()
    {
        var fixture = Fixture.Create();
        fixture.SeedPolicyholder("Eugene Katusiime", "+256772118001", ("Motor", ValidPolicyNumber));
        fixture.SeedPolicyholder("Grace Nakato", "0772118001", ("Medical", "NSA/ME/2026/00010"));

        await fixture.HandleAsync("What is the status of my claim?");

        var turn = Assert.Single(fixture.Conversations.Saved);
        Assert.Equal(CustomerAssistantOutcome.Declined, turn.Outcome);
        Assert.Contains("phone number on the policy", turn.OutboundReplyText);
    }

    [Fact]
    public async Task UnclearMessage_IsClarifiedOnce_ThenHandedToAPerson()
    {
        var fixture = Fixture.Create();

        await fixture.HandleAsync("the thing from last week");
        await fixture.HandleAsync("the other one");
        await fixture.HandleAsync("that one");

        // A first message nobody can read still meets Kato and hears what he can do, and "the
        // other one" is not mistaken for the name he asked for.
        Assert.Contains("I'm Kato", fixture.Conversations.Saved[0].OutboundReplyText);
        Assert.Empty(fixture.Customers.Customers);
        Assert.Equal(CustomerAssistantOutcome.Clarifying, fixture.Conversations.Saved[1].Outcome);
        Assert.Equal(CustomerAssistantOutcome.Escalated, fixture.Conversations.Saved[2].Outcome);
    }

    [Fact]
    public async Task FirstContact_WithAProfileName_GreetsByIt_AndKeepsAContactThatUnlocksNothing()
    {
        var fixture = Fixture.Create();

        await fixture.HandleNamedAsync("Hello", "Grace Nakato");
        await fixture.HandleNamedAsync("I had an accident on my boda", "Grace Nakato");

        Assert.StartsWith("Hello, Grace.", fixture.Conversations.Saved[0].OutboundReplyText);
        var contact = Assert.Single(fixture.Customers.Customers);
        Assert.Equal("Grace Nakato", contact.FullName);
        Assert.True(PhoneNumbers.SameNumber("256772118001", contact.Phone));
        // A contact record holds no cover: no claim, and no claim confirmation.
        Assert.Empty(await fixture.Cases.ListAsync(fixture.Scope));
        Assert.NotEqual(CustomerAssistantOutcome.AwaitingClaimConfirmation, fixture.Conversations.Saved[1].Outcome);
    }

    [Fact]
    public async Task FirstContact_WithNoProfileName_AsksForOne_AndSavesTheAnswer()
    {
        var fixture = Fixture.Create();

        await fixture.HandleAsync("Hi");
        await fixture.HandleAsync("My name is Grace");

        Assert.Equal(CustomerAssistantOutcome.AwaitingName, fixture.Conversations.Saved[0].Outcome);
        Assert.EndsWith(KatoReplies.AskName, fixture.Conversations.Saved[0].OutboundReplyText);
        Assert.StartsWith("Nice to meet you, Grace.", fixture.Conversations.Saved[1].OutboundReplyText);
        Assert.Equal("Grace", Assert.Single(fixture.Customers.Customers).FullName);
    }

    [Fact]
    public async Task FirstContact_NeverAddsASecondNameToANumberAlreadyOnFile()
    {
        var fixture = Fixture.Create();
        fixture.SeedPolicyholder("Brian Okello", "+256772118001", ("Motor", ValidPolicyNumber));

        await fixture.HandleNamedAsync("Hello", "Bee");

        Assert.Equal("Brian Okello", Assert.Single(fixture.Customers.Customers).FullName);
        Assert.StartsWith("Hello Brian", fixture.Conversations.Saved[0].OutboundReplyText);
    }

    [Theory]
    [InlineData("~Sarah N. *", "Sarah")]
    [InlineData("  okello  ", "Okello")]
    [InlineData("*** ", null)]
    [InlineData(null, null)]
    public void ProfileName_GivesAUsableFirstName(string? profile, string? expected) =>
        Assert.Equal(expected, CustomerAssistantService.FirstNameOf(profile));

    [Fact]
    public async Task VoiceNote_IsHeard_AndHandledLikeTheWordsWereTyped()
    {
        var voice = new FakeVoiceNoteTranscriber("I had an accident on my boda and hurt my leg");
        var fixture = Fixture.Create(voice: voice);
        fixture.SeedPolicyholder("Brian Okello", "+256772118001", ("Boda Rider Accident", ValidPolicyNumber));

        await fixture.HandleVoiceAsync();

        Assert.Equal("media-voice-1", voice.LastMediaId);
        var turn = Assert.Single(fixture.Conversations.Saved);
        Assert.Equal(CustomerAssistantOutcome.AwaitingClaimConfirmation, turn.Outcome);
        Assert.Contains("hurt my leg", turn.OutboundReplyText);
        Assert.Empty(await fixture.Cases.ListAsync(fixture.Scope)); // read back first, never opened on a voice note alone
    }

    [Fact]
    public async Task VoiceNote_ThatCannotBeHeard_GetsAWrittenReply_NeverSilence()
    {
        var voice = new FakeVoiceNoteTranscriber(null);
        var fixture = Fixture.Create(voice: voice);

        await fixture.HandleVoiceAsync();

        var sent = Assert.Single(fixture.Outbound.SentRequests);
        // Voice notes work here, so the customer is told this one could not be made out, never that Kato cannot listen.
        Assert.DoesNotContain("can't listen", sent.CustomerSafeBody);
        Assert.Contains("could not make out that voice note", sent.CustomerSafeBody);
        Assert.Equal(2, voice.Attempts); // tried again once before giving up
    }

    [Fact]
    public async Task VoiceNote_WithNoSpeechEngine_GetsAWrittenReply_AndKatoDoesNotOfferVoice()
    {
        var fixture = Fixture.Create();

        await fixture.HandleVoiceAsync();
        await fixture.HandleNamedAsync("Hello", "Grace");

        Assert.Contains("can't listen to voice notes yet", fixture.Outbound.SentRequests[0].CustomerSafeBody);
        Assert.DoesNotContain("voice note", fixture.Conversations.Saved[^1].OutboundReplyText);
    }

    [Fact]
    public async Task WithASpeechEngine_KatoInvitesVoiceNotes()
    {
        var fixture = Fixture.Create(voice: new FakeVoiceNoteTranscriber("hello"));

        await fixture.HandleNamedAsync("Hello", "Grace");

        Assert.Contains("voice note", Assert.Single(fixture.Conversations.Saved).OutboundReplyText);
    }

    private sealed class FakeVoiceNoteTranscriber(string? transcript) : IVoiceNoteTranscriber
    {
        public string? LastMediaId { get; private set; }

        public int Attempts { get; private set; }

        public Task<bool> IsAvailableAsync(TenantScope scope, CancellationToken cancellationToken = default) => Task.FromResult(true);

        public Task<string?> TranscribeAsync(TenantScope scope, string mediaId, SupportedLanguageCode? preferredLanguage, string? businessPhoneNumberId = null, CancellationToken cancellationToken = default)
        {
            LastMediaId = mediaId;
            Attempts++;
            return Task.FromResult(transcript);
        }
    }

    [Fact]
    public async Task VoiceNote_IsAnsweredWithAVoiceNote_NotText()
    {
        var speaker = new FakeVoiceNoteSpeaker();
        var fixture = Fixture.Create(voice: new FakeVoiceNoteTranscriber("hello"), speaker: speaker);

        await fixture.HandleVoiceAsync();

        Assert.Single(fixture.Outbound.VoiceNotes);
        Assert.Empty(fixture.Outbound.SentRequests);
        Assert.Contains("Kato", speaker.Spoken.Single());
    }

    [Fact]
    public void VoiceReply_NeverReadsOutALinkOrTheTranslationNote() =>
        Assert.Equal("Here is your link. It works for 24 hours.",
            CustomerAssistantService.SpokenText("Here is your link. It works for 24 hours: https://portal.example/access?token=abc\n\n(This message was translated by a computer.)"));

    [Fact]
    public async Task VoiceReply_WhenSpeechFails_FallsBackToText()
    {
        var fixture = Fixture.Create(voice: new FakeVoiceNoteTranscriber("hello"), speaker: new FakeVoiceNoteSpeaker(fail: true));

        await fixture.HandleVoiceAsync();

        Assert.Empty(fixture.Outbound.VoiceNotes);
        Assert.Single(fixture.Outbound.SentRequests);
    }

    [Fact]
    public async Task TypedMessage_IsAnsweredInText_EvenWithASpeechEngine()
    {
        var fixture = Fixture.Create(voice: new FakeVoiceNoteTranscriber("hello"), speaker: new FakeVoiceNoteSpeaker());

        await fixture.HandleNamedAsync("Hello", "Grace");

        Assert.Empty(fixture.Outbound.VoiceNotes);
        Assert.Single(fixture.Outbound.SentRequests);
    }

    private sealed class FakeVoiceNoteSpeaker(bool fail = false) : IVoiceNoteSpeaker
    {
        public List<string> Spoken { get; } = [];

        public Task<byte[]?> SpeakAsync(TenantScope scope, string text, SupportedLanguageCode language, CancellationToken cancellationToken = default)
        {
            Spoken.Add(text);
            return Task.FromResult<byte[]?>(fail ? null : [1, 2, 3]);
        }
    }

    // ---- Kato as an agent: the rules hold in the tools, whatever the model says ----

    private const string AgentDemoPhone = "+256772118001"; // the fixture default sender

    [Fact]
    public async Task Agent_OnTheDemoPhone_RegistersANewPerson_AndIssuesTheCoverTheyChoose()
    {
        var model = new ScriptedAgentModel()
            .Calls("register_customer", """{"full_name":"Grace Nakato","occupation":"boutique owner","area":"Jinja road"}""")
            .Calls("apply_for_cover", """{"product":"Small Business Shop"}""")
            .Says("Done, Grace. Your Small Business Shop Cover is active.");
        var fixture = Fixture.Create(agent: model, demoPhone: AgentDemoPhone);

        await fixture.HandleAsync("I'm Grace Nakato, I own a boutique on Jinja road. Register me for shop cover");

        var customer = Assert.Single(fixture.Customers.Customers);
        Assert.Equal("Grace Nakato", customer.FullName);
        var policy = Assert.Single(fixture.Policies.Policies.Values);
        Assert.Equal(MicroinsuranceCatalog.ShopLine, policy.ProductLine);
        Assert.Equal(customer.CustomerId, policy.CustomerId);
        Assert.Contains("demo policy", policy.CoverType);
        Assert.Contains("active_demo_policy", model.ToolResults[1]);
        Assert.Equal("Done, Grace. Your Small Business Shop Cover is active.", Assert.Single(fixture.Outbound.SentRequests).CustomerSafeBody);
    }

    [Fact]
    public async Task Agent_ChoosesTheShopCoverLevel_ThatMatchesTheStockValue()
    {
        var model = new ScriptedAgentModel()
            .Calls("register_customer", """{"full_name":"Grace Nakato"}""")
            .Calls("apply_for_cover", """{"product":"Small Business Shop","cover_amount_ugx":3000000}""")
            .Says("Done.");
        var fixture = Fixture.Create(agent: model, demoPhone: AgentDemoPhone);

        await fixture.HandleAsync("Go ahead, about 3 million in stock");

        var policy = Assert.Single(fixture.Policies.Policies.Values);
        Assert.Equal(4_000_000m, policy.SumInsured);
        Assert.Equal(9_000m * 12, policy.AnnualPremium);
    }

    [Fact]
    public async Task Agent_OnAnyOtherPhone_AnApplicationWaitsForAPerson_AndNoPolicyIsIssued()
    {
        var model = new ScriptedAgentModel()
            .Calls("register_customer", """{"full_name":"Grace Nakato"}""")
            .Calls("apply_for_cover", """{"product":"Small Business Shop"}""")
            .Says("Thanks Grace, your application is in.");
        var fixture = Fixture.Create(agent: model);

        await fixture.HandleAsync("Register me for shop cover, I'm Grace Nakato");

        Assert.Empty(fixture.Policies.Policies);
        Assert.Contains("application_received", model.ToolResults[1]);
        // One person, one prospect record: registering created it and the application updated it.
        var lead = Assert.Single(fixture.SalesLeads.Created);
        Assert.Equal("Grace Nakato", lead.CustomerName);
        Assert.Equal(MicroinsuranceCatalog.FindByLineOrName("Small Business Shop")!.Name, lead.AppliedFor);
        Assert.Contains(lead.Activity, entry => entry.Text.StartsWith("Applied for", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Agent_OnePersonIsOneProspect_HowEverManyTimesTheyWrite()
    {
        var model = new ScriptedAgentModel()
            .Calls("register_customer", """{"full_name":"Grace Nakato","occupation":"I sell shoes","area":"Kalerwe"}""")
            .Says("You are registered, Grace.")
            .Calls("hand_to_person", """{"reason":"Wants to talk about Motor cover"}""")
            .Says("Someone from the team will reply here soon.");
        var fixture = Fixture.Create(agent: model);

        await fixture.HandleAsync("I am Grace Nakato, I sell shoes in Kalerwe");
        await fixture.HandleAsync("Can I talk to someone about motor insurance?");

        var lead = Assert.Single(fixture.SalesLeads.Created);
        Assert.Equal("Grace Nakato", lead.CustomerName);
        Assert.NotNull(lead.HandedToPersonAt);
        Assert.Equal("I sell shoes", lead.Facts.Single(fact => fact.Key == "occupation").Value);
        Assert.Equal("Kalerwe", lead.Facts.Single(fact => fact.Key == "area").Value);
        Assert.All(lead.Facts, fact => Assert.Equal(ProspectFactSource.Customer, fact.Source));
        Assert.Equal(["Wrote in on WhatsApp", "Registered on WhatsApp", "Asked to speak to a person"], lead.Activity.Select(entry => entry.Text));
        Assert.NotNull(lead.ThreadId);
    }

    [Fact]
    public async Task Agent_AskingForAPersonAlone_MarksTheFirstContactRecordHandedOver_WithoutAnyInterest()
    {
        var model = new ScriptedAgentModel()
            .Calls("hand_to_person", """{"reason":"Unhappy about how long a claim is taking"}""")
            .Says("Someone from the team will reply here soon.");
        var fixture = Fixture.Create(agent: model);

        await fixture.HandleAsync("I want to talk to a manager about my claim");

        // Asking for a person is not interest in a cover: the first-contact record is marked handed over, and shows no interest.
        var lead = Assert.Single(fixture.SalesLeads.Created);
        Assert.NotNull(lead.HandedToPersonAt);
        Assert.Empty(lead.Interests);
    }

    [Fact]
    public async Task Agent_RecordsContactConsent_WithTheExactQuestionAsked()
    {
        const string question = "Would you like Nile Shield to send you a WhatsApp message later about shop cover?";
        var model = new ScriptedAgentModel()
            .Says(question)
            .Calls("record_contact_consent", """{"agreed":true}""")
            .Says("Thank you, we will.");
        var fixture = Fixture.Create(agent: model);

        await fixture.HandleAsync("I am thinking about shop cover");
        await fixture.HandleAsync("Yes please");

        var lead = Assert.Single(fixture.SalesLeads.Created);
        Assert.NotNull(lead.Consent);
        Assert.Equal(ContactConsentStatus.Agreed, lead.Consent!.Status);
        Assert.Equal(question, lead.Consent.Wording);
        Assert.Equal("WhatsApp", lead.Consent.Channel);
        Assert.Contains(fixture.Audit.Written, entry => entry.ActionName == "sales_lead.contact_consent_recorded");
    }

    [Fact]
    public async Task Agent_DoesNotRecordConsent_WhenTheCustomerDidNotAnswerTheQuestion()
    {
        var model = new ScriptedAgentModel()
            .Says("Would you like Nile Shield to message you later about cover?")
            .Calls("record_contact_consent", """{"agreed":true}""")
            .Says("Shop cover is UGX 15,000 a month.");
        var fixture = Fixture.Create(agent: model);

        await fixture.HandleAsync("I am thinking about shop cover");
        await fixture.HandleAsync("How much does it cost?");

        Assert.Contains("not_their_answer", model.ToolResults[0]);
        AssertOnlyAFirstContactRecord(fixture);
    }

    [Fact]
    public async Task WhileAPersonHasTheConversation_KatoStaysSilent_ButRecordsAndReadsTheMessage()
    {
        var model = new ScriptedAgentModel().Says("This must never be sent.");
        var fixture = Fixture.Create(agent: model);
        var threadId = InsuranceIntelligence.Application.WhatsApp.WhatsAppMessagingService.BuildThreadId("phone-1", "256772118001");
        await fixture.Conversations.SaveAsync(new CustomerConversationTurn
        {
            TurnId = Guid.NewGuid(),
            Scope = fixture.Scope,
            ThreadId = threadId,
            ContactWaId = "256772118001",
            InboundMessageText = string.Empty,
            ClassifiedIntent = CustomerIntent.Unknown,
            Outcome = CustomerAssistantOutcome.HumanTookOver,
            OccurredAt = DateTimeOffset.UtcNow.AddMinutes(-2),
            CorrelationId = "takeover",
        });

        await fixture.HandleAsync("Is the shop cover still UGX 15,000?");

        Assert.Empty(fixture.Outbound.SentRequests);
        Assert.Empty(model.Conversations);
        var turns = await fixture.Conversations.ListForThreadAsync(fixture.Scope, threadId);
        Assert.Contains(turns, turn => turn.Outcome == CustomerAssistantOutcome.HeldForPerson && turn.InboundMessageText.Contains("shop cover"));
        Assert.Single(fixture.InsightsQueue.Requests);
    }

    [Fact]
    public async Task Agent_AsksForTheConversationToBeReadAgain_AfterEveryReply()
    {
        var model = new ScriptedAgentModel().Says("Hello, I am Kato from Nile Shield.");
        var fixture = Fixture.Create(agent: model);

        await fixture.HandleAsync("Hello");

        var request = Assert.Single(fixture.InsightsQueue.Requests);
        Assert.Equal("256772118001", request.WaId);
    }

    [Fact]
    public async Task Agent_ClaimIsNotFiled_UntilTheCustomersOwnReplySaysYes()
    {
        var model = new ScriptedAgentModel()
            .Calls("file_claim", """{"what_happened":"Thieves broke into the shop and took stock"}""")
            .Says("Let me read that back.")
            .Calls("file_claim", """{"what_happened":"Thieves broke into the shop and took stock","when_it_happened":"last night"}""")
            .Says("Your claim is in.");
        var fixture = Fixture.Create(agent: model, demoPhone: AgentDemoPhone);
        fixture.SeedPolicyholder("Grace Nakato", AgentDemoPhone, (MicroinsuranceCatalog.ShopLine, "NSA/SB/2026/12345"));

        await fixture.HandleAsync("I lost my shop, thieves broke in last night");
        Assert.Contains("not_confirmed", model.ToolResults[0]);
        Assert.Empty(await fixture.Cases.ListAsync(fixture.Scope));

        await fixture.HandleAsync("Yes that's right");
        var claim = Assert.Single(await fixture.Cases.ListAsync(fixture.Scope));
        Assert.Equal("NSA/SB/2026/12345", claim.PolicyNumber);
        Assert.Contains("claim_filed", model.ToolResults[1]);
        Assert.Equal(CustomerAssistantOutcome.CaseOpened, fixture.Conversations.Saved[^1].Outcome);
    }

    [Fact]
    public async Task Agent_AStranger_CannotFileAClaimOrGetALink_ForSomeoneElsesPolicy()
    {
        var model = new ScriptedAgentModel()
            .Calls("file_claim", $$"""{"product_or_policy_number":"{{ValidPolicyNumber}}","what_happened":"accident"}""")
            .Calls("send_claim_link", "{}")
            .Says("I can't find cover on this number.");
        var fixture = Fixture.Create(agent: model);
        fixture.SeedPolicyholder("Brian Okello", "+256700000001", ("Motor", ValidPolicyNumber));

        await fixture.HandleAsync("yes");

        Assert.Contains("no_cover", model.ToolResults[0]);
        Assert.Contains("not_registered", model.ToolResults[1]);
        Assert.Empty(await fixture.Cases.ListAsync(fixture.Scope));
    }

    [Fact]
    public async Task Agent_WhenTheModelFails_TheRuleBasedRepliesAnswerInstead()
    {
        var fixture = Fixture.Create(agent: new ScriptedAgentModel().Throws());

        await fixture.HandleNamedAsync("Hello", "Grace");

        Assert.Contains("I'm Kato", Assert.Single(fixture.Outbound.SentRequests).CustomerSafeBody);
    }

    [Fact]
    public async Task Agent_SeesTheConversationSoFar()
    {
        var model = new ScriptedAgentModel().Says("Hello Grace, how can I help?").Says("For a business, the Small Business Shop Cover fits.");
        var fixture = Fixture.Create(agent: model);

        await fixture.HandleNamedAsync("Hello", "Grace");
        await fixture.HandleNamedAsync("Then for businesses?", "Grace");

        var second = model.Conversations[1];
        Assert.Contains(second, m => m.Role == "user" && m.Content == "Hello");
        Assert.Contains(second, m => m.Role == "assistant" && m.Content == "Hello Grace, how can I help?");
        Assert.Equal("Then for businesses?", second[^1].Content);
    }

    [Theory]
    [InlineData("Customer", null)]
    [InlineData("Mr Okello", "Okello")]
    public void ProfileName_ThatIsNotAName_IsNotUsed(string profile, string? expected) =>
        Assert.Equal(expected, CustomerAssistantService.FirstNameOf(profile));

    // Run by hand against the real model (needs az login with access to the dev Azure OpenAI):
    //   KATO_LIVE_TRANSCRIPT=path/to/transcript.txt dotnet test --filter Agent_LiveInterviewScripts
    [Fact]
    public async Task Agent_LiveInterviewScripts_WhenRequested()
    {
        var path = Environment.GetEnvironmentVariable("KATO_LIVE_TRANSCRIPT");
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        var model = new InsuranceIntelligence.Api.Azure.AzureOpenAiToolCallingChatProvider(
            new InsuranceIntelligence.Infrastructure.Configuration.InfrastructureOptions
            {
                AzureOpenAI = new InsuranceIntelligence.Infrastructure.Configuration.AzureOpenAiOptions
                {
                    Endpoint = "https://oai-iip-dev-h5girbnzrayqe.openai.azure.com/",
                    ChatDeployment = "iip-chat",
                },
            },
            Microsoft.Extensions.Logging.Abstractions.NullLogger<InsuranceIntelligence.Api.Azure.AzureOpenAiToolCallingChatProvider>.Instance,
            tokenCredential: new global::Azure.Identity.AzureCliCredential());

        string[][] scripts =
        [
            ["Hello?", "I own a shop in Kalerwe, how can you help me?", "I sell shoes and bags", "It's a lockable shop. Stock is around 5 million. I buy from Kikuubo and bring it on a boda", "Okay let's do it. I'm Grace Nakato", "Thieves broke into my shop last night and took shoes", "Yes", "How will I get my money"],
            ["Hi", "I got into an accident", "I ride a boda", "Register me, I'm Musa Kato from Kawempe", "The boda one", "I got into an accident this morning at Wandegeya, a taxi hit me and I hurt my leg", "yes correct", "Help me with a link"],
            ["Hello, I sell tomatoes in Nakasero market", "Yes, and I'm newly married, we are planning our first baby", "What would that cost?", "Ok set it up, I'm Sarah Namubiru"],
            ["Hi, a friend told me about you", "I run a salon in Ntinda", "Hmm, but insurance is expensive and they never pay", "I have never had a fire in 6 years", "Okay, how much for my equipment, it's about 3 million", "Let me think about it"],
        ];

        var transcript = new System.Text.StringBuilder();
        for (var i = 0; i < scripts.Length; i++)
        {
            var fixture = Fixture.Create(agent: model, demoPhone: AgentDemoPhone);
            transcript.AppendLine($"===== Conversation {i + 1} =====");
            foreach (var line in scripts[i])
            {
                var before = fixture.Outbound.SentRequests.Count;
                var started = DateTimeOffset.UtcNow;
                await fixture.HandleNamedAsync(line, "Customer");
                transcript.AppendLine($"THEM: {line}");
                foreach (var sent in fixture.Outbound.SentRequests.Skip(before))
                {
                    transcript.AppendLine($"KATO ({(DateTimeOffset.UtcNow - started).TotalSeconds:0}s): {sent.CustomerSafeBody}");
                }

                transcript.AppendLine($"   [outcome {fixture.Conversations.Saved[^1].Outcome}]");
            }

            transcript.AppendLine($"   [customers: {string.Join(", ", fixture.Customers.Customers.Select(c => c.FullName))}; policies: {string.Join(", ", fixture.Policies.Policies.Values.Select(p => p.ProductLine))}; claims: {(await fixture.Cases.ListAsync(fixture.Scope)).Count}]");
            transcript.AppendLine();
        }

        File.WriteAllText(path, transcript.ToString());
    }

    // Run by hand, as above, with real Sunbird translation both ways:
    //   KATO_LIVE_TRANSCRIPT=... KATO_SUNBIRD_KEY_FILE=path/to/key dotnet test --filter Agent_LiveLugandaInterview
    [Fact]
    public async Task Agent_LiveLugandaInterview_WhenRequested()
    {
        var path = Environment.GetEnvironmentVariable("KATO_LIVE_TRANSCRIPT");
        var keyFile = Environment.GetEnvironmentVariable("KATO_SUNBIRD_KEY_FILE");
        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(keyFile) || !File.Exists(keyFile))
        {
            return;
        }

        var model = new InsuranceIntelligence.Api.Azure.AzureOpenAiToolCallingChatProvider(
            new InsuranceIntelligence.Infrastructure.Configuration.InfrastructureOptions
            {
                AzureOpenAI = new InsuranceIntelligence.Infrastructure.Configuration.AzureOpenAiOptions
                {
                    Endpoint = "https://oai-iip-dev-h5girbnzrayqe.openai.azure.com/",
                    ChatDeployment = "iip-chat",
                },
            },
            Microsoft.Extensions.Logging.Abstractions.NullLogger<InsuranceIntelligence.Api.Azure.AzureOpenAiToolCallingChatProvider>.Instance,
            tokenCredential: new global::Azure.Identity.AzureCliCredential());
        var sunbird = new SunbirdTranslationProvider(
            new SunbirdSettings(File.ReadAllText(keyFile).Trim(), SunbirdSettings.DefaultEndpoint, SunbirdSettings.DefaultModel, 60),
            new HttpClient());

        string[] script =
        [
            "Gyebale ko",
            "Nnina edduuka e Kalerwe, onnyamba otya?",
            "Ntunda engatto n'ensawo",
            "Edduuka lirina oluggi olusibibwa. Ebintu biri nga obukadde butaano. Ebyamaguzi mbiggya Kikuubo ku boda",
            "Kale kitandikibwe. Nze Grace Nakato",
            "Ababbi bamenye edduuka lyange ekiro ne batwala engatto",
            "Yee",
        ];

        var fixture = Fixture.Create(agent: model, demoPhone: AgentDemoPhone, translator: sunbird);
        var transcript = new System.Text.StringBuilder();
        foreach (var line in script)
        {
            var before = fixture.Outbound.SentRequests.Count;
            var started = DateTimeOffset.UtcNow;
            await fixture.HandleNamedAsync(line, "Customer");
            var turn = fixture.Conversations.Saved[^1];
            transcript.AppendLine($"THEM: {line}");
            transcript.AppendLine($"   [Kato understood: {turn.InboundMessageText}]");
            transcript.AppendLine($"   [Kato wrote in English: {turn.OutboundReplyText}]");
            foreach (var sent in fixture.Outbound.SentRequests.Skip(before))
            {
                transcript.AppendLine($"KATO ({(DateTimeOffset.UtcNow - started).TotalSeconds:0}s): {sent.CustomerSafeBody}");
            }

            transcript.AppendLine($"   [outcome {turn.Outcome}]");
        }

        transcript.AppendLine($"   [customers: {string.Join(", ", fixture.Customers.Customers.Select(c => c.FullName))}; policies: {string.Join(", ", fixture.Policies.Policies.Values.Select(p => p.ProductLine))}; claims: {(await fixture.Cases.ListAsync(fixture.Scope)).Count}]");
        File.WriteAllText(path, transcript.ToString());
    }

    private sealed class ScriptedAgentModel : IToolCallingChatProvider
    {
        private readonly Queue<AgentChatStep> steps = new();
        private bool fail;

        public List<string> ToolResults { get; } = [];
        public List<IReadOnlyList<AgentChatMessage>> Conversations { get; } = [];

        public ScriptedAgentModel Calls(string tool, string argumentsJson)
        {
            steps.Enqueue(new AgentChatStep(null, [new AgentToolCall($"call-{steps.Count}", tool, argumentsJson)]));
            return this;
        }

        public ScriptedAgentModel Says(string text)
        {
            steps.Enqueue(new AgentChatStep(text, []));
            return this;
        }

        public ScriptedAgentModel Throws()
        {
            fail = true;
            return this;
        }

        public Task<AgentChatStep> NextAsync(IReadOnlyList<AgentChatMessage> messages, IReadOnlyList<AgentToolSpec> tools, string? correlationId, CancellationToken cancellationToken = default)
        {
            if (fail)
            {
                throw new InvalidOperationException("model down");
            }

            if (messages[^1].Role == "tool")
            {
                ToolResults.Add(messages[^1].Content!);
            }
            else
            {
                Conversations.Add(messages.ToArray());
            }

            return Task.FromResult(steps.Count > 0 ? steps.Dequeue() : new AgentChatStep("OK.", []));
        }
    }

    [Fact]
    public async Task AskingIfItIsABot_IsAnsweredHonestly()
    {
        var fixture = Fixture.Create();

        await fixture.HandleAsync("Am I talking to a bot?");

        var reply = Assert.Single(fixture.Conversations.Saved).OutboundReplyText!;
        Assert.Contains("I'm not a person myself", reply);
        Assert.DoesNotContain("PERSON", reply);
    }

    [Fact]
    public async Task Thanks_IsAnsweredWarmly_NotEscalated()
    {
        var fixture = Fixture.Create();

        await fixture.HandleAsync("Ok thank you so much");

        var turn = Assert.Single(fixture.Conversations.Saved);
        Assert.Equal(CustomerAssistantOutcome.AutoReplied, turn.Outcome);
        Assert.Contains("You're welcome", turn.OutboundReplyText);
        AssertOnlyAFirstContactRecord(fixture);
    }

    [Fact]
    public async Task MessageNoKeywordMatches_IsUnderstoodByTheModel_AndStartsAClaim()
    {
        var fixture = Fixture.Create(modelAnswer: "NEW_CLAIM");
        fixture.SeedPolicyholder("Eugene Katusiime", "+256772118001", ("Motor", ValidPolicyNumber));

        await fixture.HandleAsync("a matatu rammed into me at the roundabout");

        var turn = Assert.Single(fixture.Conversations.Saved);
        Assert.Equal(CustomerIntent.NewClaim, turn.ClassifiedIntent);
    }

    [Fact]
    public async Task ModelFailure_FallsBackToAskingTheCustomer()
    {
        var fixture = Fixture.Create(modelThrows: true);
        await fixture.HandleAsync("Hi");

        await fixture.HandleAsync("the paperwork thing from before");

        Assert.Equal(CustomerAssistantOutcome.Clarifying, fixture.Conversations.Saved[1].Outcome);
    }

    [Fact]
    public async Task LinkRequest_FromRecognisedSender_GetsASecureLinkForTheirOpenClaim()
    {
        var fixture = Fixture.Create(withAccessLinks: true);
        fixture.SeedPolicyholder("Eugene Katusiime", "+256772118001", ("Motor", ValidPolicyNumber));
        var caseId = fixture.SeedCase(ValidPolicyNumber, CaseStatus.MissingDocuments);

        await fixture.HandleAsync("Please send me the link to upload my documents");

        var turn = Assert.Single(fixture.Conversations.Saved);
        Assert.Equal(caseId, turn.CaseId);
        Assert.Contains("access/verify?token=", turn.OutboundReplyText);
    }

    [Fact]
    public async Task AMessageThatFails_IsEscalatedToAPersonInsteadOfLost()
    {
        var fixture = Fixture.Create(casesThrow: true);
        fixture.SeedPolicyholder("Eugene Katusiime", "+256772118001", ("Motor", ValidPolicyNumber));

        var opened = await fixture.HandleAsync("What is the status of my claim NSA/MO/2026/00009?");

        Assert.Empty(opened);
        var turn = Assert.Single(fixture.Conversations.Saved);
        Assert.Equal(CustomerAssistantOutcome.Escalated, turn.Outcome);
        var sent = Assert.Single(fixture.Outbound.SentRequests);
        Assert.Contains("get back to you shortly", sent.CustomerSafeBody);
        var audit = Assert.Single(fixture.Audit.Written);
        Assert.Equal("customer_assistant.processing_failed", audit.ActionName);
    }

    [Fact]
    public async Task AFailedMessage_NeverPutsTheCustomersWordsInTheAuditTrail()
    {
        var fixture = Fixture.Create(casesThrow: true);
        fixture.SeedPolicyholder("Eugene Katusiime", "+256772118001", ("Motor", ValidPolicyNumber));

        await fixture.HandleAsync("What is the status of my claim NSA/MO/2026/00009? This is my private note.");

        var audit = Assert.Single(fixture.Audit.Written);
        Assert.DoesNotContain("private note", audit.AuditReason!, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("case store down", audit.AuditReason!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("InvalidOperationException", audit.AuditReason);
    }

    [Fact]
    public async Task EscalateUnhandled_EscalatesEachTextMessageAndSkipsPhotos()
    {
        var fixture = Fixture.Create();
        var messages = new[]
        {
            new WhatsAppInboundMessage("wamid.1", "phone-1", "256772118001", WhatsAppContentType.Text, "first", null, DateTimeOffset.UtcNow),
            new WhatsAppInboundMessage("wamid.2", "phone-1", "256772118001", WhatsAppContentType.Image, "[Image received]", null, DateTimeOffset.UtcNow),
            new WhatsAppInboundMessage("wamid.3", "phone-1", "256772118001", WhatsAppContentType.Text, "second", null, DateTimeOffset.UtcNow),
        };

        await fixture.Service.EscalateUnhandledAsync(fixture.Scope, messages, "corr-1", new TimeoutException("slow"));

        Assert.Equal(2, fixture.Conversations.Saved.Count);
        Assert.All(fixture.Conversations.Saved, turn => Assert.Equal(CustomerAssistantOutcome.Escalated, turn.Outcome));
        Assert.Equal(2, fixture.Outbound.SentRequests.Count);
    }

    private sealed class Fixture
    {
        private Fixture(
            TenantScope scope,
            bool autoReplyEnabled,
            InMemoryCaseRepository cases,
            FakePolicyRepository policies,
            FakeCustomerRepository customers,
            FakeSalesLeadWriteRepository salesLeads,
            FakeWhatsAppOutboundProvider outbound,
            FakeWhatsAppMessageRepository whatsAppMessages,
            FakeConversationRepository conversations,
            FakeAgentBehaviourProfileRepository behaviourProfiles,
            FakeAuditRepository audit,
            FakeRetrievalProvider retrieval,
            FakeAiAssistantProvider assistant,
            FakeTeamsNotificationQueue teamsQueue,
            CustomerAssistantService service)
        {
            Scope = scope;
            AutoReplyEnabled = autoReplyEnabled;
            Cases = cases;
            Policies = policies;
            Customers = customers;
            SalesLeads = salesLeads;
            Outbound = outbound;
            WhatsAppMessages = whatsAppMessages;
            Conversations = conversations;
            BehaviourProfiles = behaviourProfiles;
            Audit = audit;
            Retrieval = retrieval;
            Assistant = assistant;
            TeamsQueue = teamsQueue;
            Service = service;
        }

        public TenantScope Scope { get; }
        public bool AutoReplyEnabled { get; }
        public InMemoryCaseRepository Cases { get; }
        public FakePolicyRepository Policies { get; }
        public FakeCustomerRepository Customers { get; }
        public FakeSalesLeadWriteRepository SalesLeads { get; }
        public FakeWhatsAppOutboundProvider Outbound { get; }
        public FakeWhatsAppMessageRepository WhatsAppMessages { get; }
        public FakeConversationRepository Conversations { get; }
        public FakeAgentBehaviourProfileRepository BehaviourProfiles { get; }
        public FakeAuditRepository Audit { get; }
        public FakeRetrievalProvider Retrieval { get; }
        public FakeAiAssistantProvider Assistant { get; }
        public FakeTeamsNotificationQueue TeamsQueue { get; }
        public CustomerAssistantService Service { get; }
        public FakeAccessLinkIssuer LinkIssuer { get; private init; } = new();

        public static Fixture Create(
            bool autoReplyEnabled = false,
            bool agentEnabled = true,
            bool withAccessLinks = false,
            string? modelAnswer = null,
            bool modelThrows = false,
            bool casesThrow = false,
            ITranslationProvider? translator = null,
            IReadOnlyCollection<SupportedLanguageCode>? enabledLanguages = null,
            bool allowUnreviewedTranslations = true,
            string? demoPhone = null,
            IVoiceNoteTranscriber? voice = null,
            IVoiceNoteSpeaker? speaker = null,
            IToolCallingChatProvider? agent = null,
            InsuranceIntelligence.Application.Rules.DecisionLog? decisionLog = null,
            bool stopHandling = false)
        {
            var scope = TenantScope.Create("tenant-ca", "workspace-ca");
            var cases = new InMemoryCaseRepository();
            var policies = new FakePolicyRepository();
            var customers = new FakeCustomerRepository();
            var salesLeads = new FakeSalesLeadWriteRepository();
            var outbound = new FakeWhatsAppOutboundProvider();
            var whatsAppMessages = new FakeWhatsAppMessageRepository();
            var conversations = new FakeConversationRepository();
            var behaviourProfiles = new FakeAgentBehaviourProfileRepository();
            var consent = new FakeConsentRepository();
            var audit = new FakeAuditRepository();
            var retrieval = new FakeRetrievalProvider();
            var assistant = new FakeAiAssistantProvider();
            var teamsQueue = new FakeTeamsNotificationQueue();
            var agentGate = agentEnabled ? new LocalTestAgentGate() : new LocalTestAgentGate("customer-assistant");
            var linkIssuer = new FakeAccessLinkIssuer();
            var languagePreferences = new InMemoryCustomerLanguagePreferenceRepository();
            var insightsQueue = new RecordingInsightsQueue();
            IChatCompletionProvider? model = modelThrows
                ? new FakeChatCompletionProvider(null)
                : modelAnswer is null ? null : new FakeChatCompletionProvider(modelAnswer);

            var service = new CustomerAssistantService(
                new KeywordCustomerIntentClassifier(),
                retrieval,
                assistant,
                casesThrow ? new ThrowingCaseRepository() : cases,
                policies,
                customers,
                salesLeads,
                outbound,
                whatsAppMessages,
                conversations,
                behaviourProfiles,
                consent,
                agentGate,
                audit,
                new FakeProductRepository(),
                new NoOpNotificationEmitter(),
                teamsQueue,
                model,
                withAccessLinks ? linkIssuer : null,
                translator is null ? null : new TranslationService(translator, new InMemoryTranslationCache()),
                translator is null ? null : new CustomerLanguageResolutionService(languagePreferences, new WordListLanguageDetector()),
                translator is null ? null : new WordListLanguageDetector(),
                translator is null ? null : new TestLanguageSettings(enabledLanguages ?? [SupportedLanguageCode.Lg, SupportedLanguageCode.Sw], allowUnreviewedTranslations),
                demoPhone is null ? null : new DemoSessionCommands(
                    new DemoSessionSettings(true, demoPhone), customers, policies, cases, languagePreferences, audit),
                voice,
                speaker,
                agent,
                insightsQueue,
                decisionLog: decisionLog,
                outcomes: stopHandling ? new InsuranceIntelligence.Application.Rules.OutcomeService(OutcomeStore(), decisionLog ?? new InsuranceIntelligence.Application.Rules.DecisionLog(OutcomeStore()), salesLeads, consent) : null);

            return new Fixture(scope, autoReplyEnabled, cases, policies, customers, salesLeads, outbound, whatsAppMessages, conversations, behaviourProfiles, audit, retrieval, assistant, teamsQueue, service)
            {
                LinkIssuer = linkIssuer,
                LanguagePreferences = languagePreferences,
                InsightsQueue = insightsQueue,
            };
        }

        public InMemoryCustomerLanguagePreferenceRepository LanguagePreferences { get; private init; } = new();

        public RecordingInsightsQueue InsightsQueue { get; private init; } = new();

        public Task<IReadOnlyList<Guid>> HandleFromAsync(string text, string waId) =>
            Service.HandleInboundMessagesAsync(
                Scope,
                [new WhatsAppInboundMessage($"wamid.{Guid.NewGuid():N}", "phone-1", waId, WhatsAppContentType.Text, text, null, DateTimeOffset.UtcNow)],
                $"correlation-{Guid.NewGuid():N}",
                AutoReplyEnabled);

        public Task<IReadOnlyList<Guid>> HandleAsync(string text) =>
            Service.HandleInboundMessagesAsync(
                Scope,
                [new WhatsAppInboundMessage($"wamid.{Guid.NewGuid():N}", "phone-1", "256772118001", WhatsAppContentType.Text, text, null, DateTimeOffset.UtcNow)],
                $"correlation-{Guid.NewGuid():N}",
                AutoReplyEnabled);

        public Task<IReadOnlyList<Guid>> HandleNamedAsync(string text, string? profileName) =>
            Service.HandleInboundMessagesAsync(
                Scope,
                [new WhatsAppInboundMessage($"wamid.{Guid.NewGuid():N}", "phone-1", "256772118001", WhatsAppContentType.Text, text, null, DateTimeOffset.UtcNow) { ProfileName = profileName }],
                $"correlation-{Guid.NewGuid():N}",
                AutoReplyEnabled);

        public Task<IReadOnlyList<Guid>> HandleVoiceAsync(string mediaId = "media-voice-1") =>
            Service.HandleInboundMessagesAsync(
                Scope,
                [new WhatsAppInboundMessage($"wamid.{Guid.NewGuid():N}", "phone-1", "256772118001", WhatsAppContentType.Audio, "[Voice note received]", null, DateTimeOffset.UtcNow) { MediaId = mediaId }],
                $"correlation-{Guid.NewGuid():N}",
                AutoReplyEnabled);

        public Task<IReadOnlyList<Guid>> HandleMediaAsync() =>
            Service.HandleInboundMessagesAsync(
                Scope,
                [new WhatsAppInboundMessage($"wamid.{Guid.NewGuid():N}", "phone-1", "256772118001", WhatsAppContentType.Image, "[Image received]", null, DateTimeOffset.UtcNow)],
                $"correlation-{Guid.NewGuid():N}",
                AutoReplyEnabled);

        /// <summary>A customer on file with the given phone number and one policy per (line, number).</summary>
        public void SeedPolicyholder(string fullName, string phone, params (string ProductLine, string PolicyNumber)[] policies)
        {
            var customerId = Guid.NewGuid().ToString();
            Customers.Customers.Add(new InsuranceIntelligence.Domain.Customers.Customer(
                customerId, Scope.TenantId, Scope.WorkspaceId, fullName, null, phone));
            foreach (var (productLine, policyNumber) in policies)
            {
                Policies.Policies[policyNumber] = new Policy(
                    PolicyId: Guid.NewGuid().ToString(), TenantId: Scope.TenantId, WorkspaceId: Scope.WorkspaceId,
                    PolicyNumber: policyNumber, ProductLine: productLine, CoverType: "Comprehensive", CustomerId: customerId,
                    BranchId: null, EffectiveFrom: null, EffectiveTo: null);
            }
        }

        public void SeedPolicy(string policyNumber) =>
            Policies.Policies[policyNumber] = new Policy(
                PolicyId: Guid.NewGuid().ToString(), TenantId: Scope.TenantId, WorkspaceId: Scope.WorkspaceId,
                PolicyNumber: policyNumber, ProductLine: "Motor", CoverType: "Comprehensive", CustomerId: null,
                BranchId: null, EffectiveFrom: null, EffectiveTo: null);

        public Guid SeedCase(string policyNumber, CaseStatus status = CaseStatus.New)
        {
            var caseId = Guid.NewGuid();
            Cases.SaveAsync(new ClaimCase
            {
                CaseId = caseId,
                Scope = Scope,
                CustomerName = "WhatsApp Customer",
                PolicyNumber = policyNumber,
                ProductLine = "Motor",
                ClaimType = "Own damage",
                Channel = "WhatsApp",
                IncidentDate = new DateOnly(2026, 8, 1),
                IncidentDescription = "Seeded for test",
                Status = status,
                CompletenessScore = 40,
                CreatedByUserId = "system:customer-assistant",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
                CorrelationId = "seed-correlation",
            }).GetAwaiter().GetResult();
            return caseId;
        }

        public KnowledgeChunk MakeChunk() => new()
        {
            ChunkId = "chunk-1",
            KnowledgeDocumentId = Guid.NewGuid(),
            Scope = Scope,
            DocumentName = "Motor Policy Wording",
            ProductLine = "Motor",
            Version = "v1",
            ChunkIndex = 0,
            Content = "Own damage and third-party liability are covered.",
            SourceReference = new KnowledgeSourceReference(Guid.NewGuid(), "chunk-1", "Motor Policy Wording", "Motor", "v1", "object-key"),
        };
    }

    private sealed class FakePolicyRepository : IPolicyRepository
    {
        public Dictionary<string, Policy> Policies { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Task<Policy?> GetAsync(string policyId, TenantScope scope, CancellationToken cancellationToken) =>
            Task.FromResult(Policies.Values.SingleOrDefault(p => p.PolicyId == policyId));

        public Task<Policy?> GetByPolicyNumberAsync(string policyNumber, TenantScope scope, CancellationToken cancellationToken) =>
            Task.FromResult(Policies.GetValueOrDefault(policyNumber));

        public Task<IReadOnlyCollection<Policy>> ListAsync(TenantScope scope, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyCollection<Policy>>(Policies.Values.ToArray());

        public Task<IReadOnlyCollection<Policy>> ListByCustomerAsync(string customerId, TenantScope scope, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyCollection<Policy>>(Policies.Values.Where(p => p.CustomerId == customerId).ToArray());

        public Task UpsertAsync(Policy policy, CancellationToken cancellationToken)
        {
            Policies[policy.PolicyNumber] = policy;
            return Task.CompletedTask;
        }
    }

    // Qualified explicitly: bare "Customer" resolves to the sibling InsuranceIntelligence.Api.Customer
    // namespace (an enclosing-namespace match beats the using-imported Domain.Customers.Customer type)
    // since this file's own namespace, InsuranceIntelligence.Api.Tests, nests under InsuranceIntelligence.Api.
    private sealed class FakeCustomerRepository : ICustomerRepository
    {
        public List<InsuranceIntelligence.Domain.Customers.Customer> Customers { get; } = [];
        public Task<InsuranceIntelligence.Domain.Customers.Customer?> GetAsync(string customerId, TenantScope scope, CancellationToken cancellationToken) =>
            Task.FromResult(Customers.FirstOrDefault(c => c.CustomerId == customerId));
        public Task<IReadOnlyCollection<InsuranceIntelligence.Domain.Customers.Customer>> ListAsync(TenantScope scope, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyCollection<InsuranceIntelligence.Domain.Customers.Customer>>(Customers.ToArray());
        public Task UpsertAsync(InsuranceIntelligence.Domain.Customers.Customer customer, CancellationToken cancellationToken)
        {
            Customers.RemoveAll(existing => existing.CustomerId == customer.CustomerId);
            Customers.Add(customer);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeAccessLinkIssuer : IClaimantAccessLinkIssuer
    {
        public List<(Guid CaseId, string ContactPhone)> Issued { get; } = [];
        public Task<string?> IssueForClaimAsync(TenantScope scope, Guid caseId, string policyNumber, string contactPhone, string correlationId, CancellationToken cancellationToken = default)
        {
            Issued.Add((caseId, contactPhone));
            return Task.FromResult<string?>($"https://portal.example/access/verify?token=test-{Issued.Count}");
        }
    }

    /// <summary>A model that answers with a fixed label, or throws when given no answer, the
    /// way a timed-out or unconfigured provider does.</summary>
    private sealed class FakeChatCompletionProvider(string? answer) : IChatCompletionProvider
    {
        public Task<ChatCompletionResult> CompleteAsync(ChatCompletionRequest request, CancellationToken cancellationToken = default) =>
            answer is null
                ? throw new InvalidOperationException("Model unavailable.")
                : Task.FromResult(new ChatCompletionResult(answer, "Fake", null, DateTimeOffset.UtcNow));
    }

    /// <summary>Behaves like the real store: a save replaces the lead with the same id. Created is the
    /// latest version of each lead, in the order the leads were first saved; Saves is every write.</summary>
    private sealed class FakeSalesLeadWriteRepository : ISalesLeadWriteRepository
    {
        public List<SalesLeadDetailReadModel> Created { get; } = [];
        public List<SalesLeadDetailReadModel> Saves { get; } = [];

        public Task CreateAsync(string tenantId, string workspaceId, SalesLeadDetailReadModel lead, CancellationToken cancellationToken = default)
        {
            Saves.Add(lead);
            var index = Created.FindIndex(existing => existing.LeadId == lead.LeadId);
            if (index >= 0)
            {
                Created[index] = lead;
            }
            else
            {
                Created.Add(lead);
            }

            return Task.CompletedTask;
        }

        public Task DeleteAsync(string tenantId, string workspaceId, string leadId, CancellationToken cancellationToken = default)
        {
            Created.RemoveAll(lead => lead.LeadId == leadId);
            return Task.CompletedTask;
        }

        public Task<SalesLeadDetailReadModel?> FindByPhoneAsync(string tenantId, string workspaceId, string phone, CancellationToken cancellationToken = default) =>
            Task.FromResult(Created.LastOrDefault(lead => InsuranceIntelligence.Domain.Customers.PhoneNumbers.SameNumber(lead.Phone ?? lead.ContactInfo, phone)));
    }

    private sealed class RecordingInsightsQueue : IConversationInsightsQueue
    {
        public List<(string WaId, string ThreadId)> Requests { get; } = [];

        public void Enqueue(TenantScope scope, string waId, string threadId, string correlationId) => Requests.Add((waId, threadId));
    }

    private sealed class FakeWhatsAppOutboundProvider : IWhatsAppOutboundProvider
    {
        public bool IsConfigured => true;
        public List<WhatsAppProviderSendRequest> SentRequests { get; } = [];

        /// <summary>Typing indicators and sends in the order they happened, e.g. "typing:wamid.x", "send".</summary>
        public List<string> Events { get; } = [];

        public Task<WhatsAppProviderSendResult> SendAsync(WhatsAppProviderSendRequest request, CancellationToken cancellationToken = default)
        {
            SentRequests.Add(request);
            Events.Add("send");
            return Task.FromResult(new WhatsAppProviderSendResult(true, $"wamid.out.{Guid.NewGuid():N}", null, null));
        }

        public List<byte[]> VoiceNotes { get; } = [];

        public Task<WhatsAppProviderSendResult> SendVoiceNoteAsync(
            string businessPhoneNumberId, string recipientWaId, byte[] oggOpusAudio, string? replyToProviderMessageId, CancellationToken cancellationToken = default)
        {
            VoiceNotes.Add(oggOpusAudio);
            Events.Add("voice");
            return Task.FromResult(new WhatsAppProviderSendResult(true, $"wamid.voice.{Guid.NewGuid():N}", null, null));
        }

        public Task<bool> ShowTypingAsync(string businessPhoneNumberId, string inboundProviderMessageId, CancellationToken cancellationToken = default)
        {
            Events.Add($"typing:{inboundProviderMessageId}");
            return Task.FromResult(true);
        }
    }

    private sealed class FakeWhatsAppMessageRepository : IWhatsAppMessageRepository
    {
        public List<WhatsAppMessage> Saved { get; } = [];
        public Task SaveAsync(WhatsAppMessage message, CancellationToken cancellationToken = default)
        {
            Saved.Add(message);
            return Task.CompletedTask;
        }
        public Task<WhatsAppMessage?> GetAsync(TenantScope scope, Guid messageId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Saved.SingleOrDefault(m => m.MessageId == messageId));
        public Task<WhatsAppMessage?> FindByProviderMessageIdAsync(TenantScope scope, string providerMessageId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Saved.SingleOrDefault(m => m.ProviderMessageId == providerMessageId));
        public Task<WhatsAppMessage?> FindByIdempotencyKeyAsync(TenantScope scope, string idempotencyKey, CancellationToken cancellationToken = default) =>
            Task.FromResult(Saved.SingleOrDefault(m => m.IdempotencyKey == idempotencyKey));
        public Task<IReadOnlyList<WhatsAppMessage>> ListThreadAsync(TenantScope scope, string threadId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<WhatsAppMessage>>(Saved.Where(m => m.ThreadId == threadId).ToArray());
    }

    private sealed class FakeConversationRepository : ICustomerConversationRepository
    {
        public List<CustomerConversationTurn> Saved { get; } = [];
        public Task SaveAsync(CustomerConversationTurn turn, CancellationToken cancellationToken = default)
        {
            Saved.Add(turn);
            return Task.CompletedTask;
        }
        public Task<IReadOnlyCollection<CustomerConversationTurn>> ListForThreadAsync(TenantScope scope, string threadId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<CustomerConversationTurn>>(Saved.Where(t => t.ThreadId == threadId).ToArray());
        public Task<RetentionDeletionSummary> DeleteOlderThanAsync(TenantScope scope, DateTimeOffset cutoff, CancellationToken cancellationToken = default)
        {
            var expired = Saved.Where(t => t.OccurredAt < cutoff).ToArray();
            foreach (var turn in expired) Saved.Remove(turn);
            return Task.FromResult(expired.Length == 0
                ? RetentionDeletionSummary.None
                : new RetentionDeletionSummary(expired.Length, expired.Min(t => t.OccurredAt), expired.Max(t => t.OccurredAt)));
        }
    }

    private sealed class FakeAgentBehaviourProfileRepository : IAgentBehaviourProfileRepository
    {
        public Task<AgentBehaviourProfile?> GetCurrentAsync(TenantScope scope, string agentKey, CancellationToken cancellationToken = default) =>
            Task.FromResult<AgentBehaviourProfile?>(null);
        public Task<IReadOnlyCollection<AgentBehaviourProfile>> ListVersionsAsync(TenantScope scope, string agentKey, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<AgentBehaviourProfile>>([]);
        public Task<IReadOnlyCollection<AgentBehaviourProfile>> ListCurrentForAllAgentsAsync(TenantScope scope, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<AgentBehaviourProfile>>([]);
        public Task SaveAsync(AgentBehaviourProfile version, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    // Real in-memory behaviour (not an always-permissive stub): CustomerAssistantService captures
    // a ConsentRecord on every inbound message before it ever checks one, so a real store/retrieve
    // fake proves the capture-then-check flow actually works end to end, the same way a live
    // EfConsentRepository would for a single-message conversation.
    private sealed class FakeConsentRepository : IConsentRepository
    {
        private readonly List<ConsentRecord> records = [];
        public Task SaveAsync(ConsentRecord record, CancellationToken cancellationToken = default)
        {
            records.Add(record);
            return Task.CompletedTask;
        }
        public Task<ConsentRecord?> GetCurrentAsync(TenantScope scope, string customerIdentifier, string purpose, CancellationToken cancellationToken = default) =>
            Task.FromResult(records
                .Where(r => r.Scope.Matches(scope) && r.CustomerIdentifier == customerIdentifier && r.Purpose == purpose)
                .OrderByDescending(r => r.RecordedAt)
                .FirstOrDefault());
        public Task<IReadOnlyCollection<ConsentRecord>> ListForCustomerAsync(TenantScope scope, string customerIdentifier, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<ConsentRecord>>(records.Where(r => r.Scope.Matches(scope) && r.CustomerIdentifier == customerIdentifier).ToArray());
    }

    private sealed class NoOpNotificationEmitter : INotificationEmitter
    {
        public Task NotifyAsync(TenantScope scope, string recipientUserId, NotificationType type, string message, Guid caseId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task NotifyOnceAsync(TenantScope scope, string recipientUserId, NotificationType type, string message, Guid caseId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class FakeProductRepository : IProductRepository
    {
        public Task<Product?> GetCurrentAsync(TenantScope scope, Guid productId, CancellationToken cancellationToken = default) =>
            Task.FromResult<Product?>(null);
        public Task<Product?> GetVersionAsync(TenantScope scope, Guid productId, int version, CancellationToken cancellationToken = default) =>
            Task.FromResult<Product?>(null);
        public Task<IReadOnlyCollection<Product>> ListVersionsAsync(TenantScope scope, Guid productId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<Product>>([]);
        public Task<IReadOnlyCollection<Product>> ListCurrentForAllProductsAsync(TenantScope scope, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<Product>>([]);
        public Task SaveAsync(Product version, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeTeamsNotificationQueue : ITeamsNotificationQueue
    {
        public List<TeamsNotification> Enqueued { get; } = [];

        public Task EnqueueAsync(TeamsNotification notification, CancellationToken cancellationToken = default)
        {
            Enqueued.Add(notification);
            return Task.CompletedTask;
        }
    }

    private sealed class TestLanguageSettings(IReadOnlyCollection<SupportedLanguageCode> enabled, bool allowUnreviewed) : IAssistantLanguageSettings
    {
        public IReadOnlyCollection<SupportedLanguageCode> EnabledLanguages => enabled;

        public bool AllowUnreviewedTranslations => allowUnreviewed;
    }

    /// <summary>A case store that is down, to prove a failing message is handed to a person.</summary>
    private sealed class ThrowingCaseRepository : InsuranceIntelligence.Domain.Cases.ICaseRepository
    {
        public Task SaveAsync(ClaimCase claimCase, CancellationToken cancellationToken = default) => throw new InvalidOperationException("case store down");
        public Task<ClaimCase?> GetAsync(Guid caseId, CancellationToken cancellationToken = default) => throw new InvalidOperationException("case store down");
        public Task<IReadOnlyCollection<ClaimCase>> ListAsync(TenantScope scope, CancellationToken cancellationToken = default) => throw new InvalidOperationException("case store down");
    }

    private sealed class FakeAuditRepository : IAuditRepository
    {
        public List<AuditEvent> Written { get; } = [];
        public Task WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default)
        {
            Written.Add(auditEvent);
            return Task.CompletedTask;
        }
        public Task<IReadOnlyCollection<AuditEvent>> ListForCaseAsync(Guid caseId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<AuditEvent>>(Written.Where(e => e.CaseId == caseId).ToArray());
        public Task<IReadOnlyCollection<AuditEvent>> ListAsync(TenantScope scope, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<AuditEvent>>(Written.Where(e => e.Scope.Matches(scope)).ToArray());
    }

    private sealed class FakeRetrievalProvider : IRetrievalProvider
    {
        public IReadOnlyCollection<KnowledgeChunk> ChunksToReturn { get; set; } = [];
        public Task<RetrievalResult> RetrieveAsync(AssistantQuestion question, CancellationToken cancellationToken = default) =>
            Task.FromResult(new RetrievalResult(question.Question, question.ProductLine, ChunksToReturn, []));
    }

    private sealed class FakeAiAssistantProvider : IAiAssistantProvider
    {
        public AssistantAnswer AnswerToReturn { get; set; } = new(
            "Default test answer.", AiConfidenceScore.Create(0.5m), EscalationRequired: false, Sources: [], GuardrailMessage: "", RiskFlag: "None");
        public Task<AssistantAnswer> AnswerPolicyQuestionAsync(AssistantQuestion question, RetrievalResult retrievalResult, CancellationToken cancellationToken = default) =>
            Task.FromResult(AnswerToReturn);
    }
}
