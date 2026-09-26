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
    public async Task NewClaim_WithValidPolicyNumber_CreatesUnconfirmedCase()
    {
        var fixture = Fixture.Create();
        fixture.SeedPolicy(ValidPolicyNumber);

        await fixture.HandleAsync($"I was in an accident, my policy is {ValidPolicyNumber}");

        var turn = Assert.Single(fixture.Conversations.Saved);
        Assert.Equal(CustomerIntent.NewClaim, turn.ClassifiedIntent);
        Assert.Equal(CustomerAssistantOutcome.CaseOpened, turn.Outcome);
        Assert.NotNull(turn.CaseId);

        var created = await fixture.Cases.GetAsync(turn.CaseId!.Value);
        Assert.NotNull(created);
        Assert.True(created!.RequiresConfirmation);
        Assert.Equal(ValidPolicyNumber, created.PolicyNumber);
        Assert.Equal("WhatsApp", created.Channel);
    }

    [Fact]
    public async Task NewClaim_WithoutPolicyNumber_AsksForItAndCreatesNoCase()
    {
        var fixture = Fixture.Create();

        await fixture.HandleAsync("I had an accident and need to make a claim");

        var turn = Assert.Single(fixture.Conversations.Saved);
        Assert.Equal(CustomerIntent.NewClaim, turn.ClassifiedIntent);
        Assert.Equal(CustomerAssistantOutcome.AwaitingMoreInformation, turn.Outcome);
        Assert.Null(turn.CaseId);
        Assert.Empty(await fixture.Cases.ListAsync(fixture.Scope));
    }

    [Fact]
    public async Task ExistingClaimStatus_WithoutPolicyNumber_NeverDisclosesAnything()
    {
        var fixture = Fixture.Create();
        var existingCaseId = fixture.SeedCase(ValidPolicyNumber);

        await fixture.HandleAsync("What is the status of my claim?");

        var turn = Assert.Single(fixture.Conversations.Saved);
        Assert.Equal(CustomerIntent.ExistingClaimStatus, turn.ClassifiedIntent);
        Assert.Equal(CustomerAssistantOutcome.AwaitingMoreInformation, turn.Outcome);
        Assert.Null(turn.CaseId);
        Assert.DoesNotContain(existingCaseId.ToString()[..8], turn.OutboundReplyText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExistingClaimStatus_WithMatchingPolicyNumber_DisclosesOnlyCoarseStatus()
    {
        var fixture = Fixture.Create();
        fixture.SeedPolicy(ValidPolicyNumber);
        var existingCaseId = fixture.SeedCase(ValidPolicyNumber, CaseStatus.InReview);

        await fixture.HandleAsync($"What is the status of my claim on {ValidPolicyNumber}?");

        var turn = Assert.Single(fixture.Conversations.Saved);
        Assert.Equal(CustomerAssistantOutcome.AutoReplied, turn.Outcome);
        Assert.Equal(existingCaseId, turn.CaseId);
        Assert.Contains("Under review", turn.OutboundReplyText);
    }

    [Fact]
    public async Task NewClaim_PolicyNumberSentAsAFollowUp_OpensTheClaimWithTheOriginalDescription()
    {
        var fixture = Fixture.Create();
        fixture.SeedPolicy(ValidPolicyNumber);

        await fixture.HandleAsync("I had an accident on Jinja Road this morning");
        await fixture.HandleAsync(ValidPolicyNumber);

        Assert.Equal(2, fixture.Conversations.Saved.Count);
        var followUp = fixture.Conversations.Saved[1];
        Assert.Equal(CustomerIntent.NewClaim, followUp.ClassifiedIntent);
        Assert.Equal(CustomerAssistantOutcome.CaseOpened, followUp.Outcome);
        var created = await fixture.Cases.GetAsync(followUp.CaseId!.Value);
        Assert.Contains("Jinja Road", created!.IncidentDescription);
        Assert.Contains(ClaimReference.For(created.CaseId), followUp.OutboundReplyText);
    }

    [Fact]
    public async Task NewClaim_RetypedPolicyNumberAfterNotFound_UsesTheNewNumber()
    {
        var fixture = Fixture.Create();
        fixture.SeedPolicy(ValidPolicyNumber);

        await fixture.HandleAsync("I was in an accident, my policy is NSA/MO/2026/99999");
        await fixture.HandleAsync($"Sorry, it is {ValidPolicyNumber}");

        var followUp = fixture.Conversations.Saved[1];
        Assert.Equal(CustomerAssistantOutcome.CaseOpened, followUp.Outcome);
        var created = await fixture.Cases.GetAsync(followUp.CaseId!.Value);
        Assert.Equal(ValidPolicyNumber, created!.PolicyNumber);
    }

    [Fact]
    public async Task ClaimStatus_PolicyNumberSentAsAFollowUp_DisclosesStatusOnlyAfterTheMatch()
    {
        var fixture = Fixture.Create();
        fixture.SeedPolicy(ValidPolicyNumber);
        var existingCaseId = fixture.SeedCase(ValidPolicyNumber, CaseStatus.MissingDocuments);

        await fixture.HandleAsync("What is the status of my claim?");
        await fixture.HandleAsync(ValidPolicyNumber);

        var followUp = fixture.Conversations.Saved[1];
        Assert.Equal(CustomerIntent.ExistingClaimStatus, followUp.ClassifiedIntent);
        Assert.Equal(existingCaseId, followUp.CaseId);
        Assert.Contains("Waiting on documents", followUp.OutboundReplyText);
    }

    [Fact]
    public async Task Greeting_GetsTheWelcomeMenuNotAnEscalation()
    {
        var fixture = Fixture.Create();

        await fixture.HandleAsync("Hi");

        var turn = Assert.Single(fixture.Conversations.Saved);
        Assert.Equal(CustomerAssistantOutcome.AwaitingMoreInformation, turn.Outcome);
        Assert.Contains("report a claim", turn.OutboundReplyText);
        Assert.Empty(fixture.SalesLeads.Created);
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
        Assert.Contains("passed it to our team", sent.CustomerSafeBody);
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
        Assert.Equal(fixture.Assistant.AnswerToReturn.AnswerText, sent.CustomerSafeBody);
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
        Assert.Contains("passed it to our team", sent.CustomerSafeBody);
    }

    [Fact]
    public async Task ProductInterest_CreatesLead()
    {
        var fixture = Fixture.Create();

        await fixture.HandleAsync("I'm interested in getting a quote for home insurance");

        var turn = Assert.Single(fixture.Conversations.Saved);
        Assert.Equal(CustomerIntent.ProductInterest, turn.ClassifiedIntent);
        Assert.Equal(CustomerAssistantOutcome.LeadCreated, turn.Outcome);
        Assert.NotNull(turn.LeadId);
        var lead = Assert.Single(fixture.SalesLeads.Created);
        Assert.Equal("WhatsApp", lead.LeadSource);

        // Outcome P: "route it to a licensed person." A lead has no assigned officer at
        // creation, so routing is the same team-broadcast ApprovalRequired already uses.
        var notification = Assert.Single(fixture.TeamsQueue.Enqueued);
        Assert.Equal(TeamsNotificationType.LeadCreated, notification.NotificationType);
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
        Assert.Contains("policy number or claim reference", Assert.Single(fixture.Outbound.SentRequests).CustomerSafeBody);
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

        Assert.Contains("Hi Eugene", Assert.Single(fixture.Conversations.Saved).OutboundReplyText);
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

        var opened = await fixture.HandleAsync("A taxi hit the back of my car at the Kira roundabout this morning");

        var second = fixture.Conversations.Saved[1];
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

        var created = await fixture.Cases.GetAsync(fixture.Conversations.Saved[2].CaseId!.Value);
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
        Assert.Equal(CustomerAssistantOutcome.CaseOpened, turn.Outcome);
        Assert.Equal(ValidPolicyNumber, (await fixture.Cases.GetAsync(turn.CaseId!.Value))!.PolicyNumber);
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
        Assert.Contains("Under review", reply);
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
        Assert.Equal(CustomerAssistantOutcome.AwaitingMoreInformation, turn.Outcome);
        Assert.Contains("policy number", turn.OutboundReplyText);
    }

    [Fact]
    public async Task UnclearMessage_IsClarifiedOnce_ThenHandedToAPerson()
    {
        var fixture = Fixture.Create();

        await fixture.HandleAsync("the thing from last week");
        await fixture.HandleAsync("the other one");

        Assert.Equal(CustomerAssistantOutcome.Clarifying, fixture.Conversations.Saved[0].Outcome);
        Assert.Equal(CustomerAssistantOutcome.Escalated, fixture.Conversations.Saved[1].Outcome);
    }

    [Fact]
    public async Task AskingIfItIsABot_IsAnsweredHonestly()
    {
        var fixture = Fixture.Create();

        await fixture.HandleAsync("Am I talking to a bot?");

        var reply = Assert.Single(fixture.Conversations.Saved).OutboundReplyText!;
        Assert.Contains("automated assistant", reply);
        Assert.Contains("person", reply);
    }

    [Fact]
    public async Task Thanks_IsAnsweredWarmly_NotEscalated()
    {
        var fixture = Fixture.Create();

        await fixture.HandleAsync("Ok thank you so much");

        var turn = Assert.Single(fixture.Conversations.Saved);
        Assert.Equal(CustomerAssistantOutcome.AutoReplied, turn.Outcome);
        Assert.Contains("You're welcome", turn.OutboundReplyText);
        Assert.Empty(fixture.SalesLeads.Created);
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

        await fixture.HandleAsync("the paperwork thing from before");

        Assert.Equal(CustomerAssistantOutcome.Clarifying, Assert.Single(fixture.Conversations.Saved).Outcome);
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

        var opened = await fixture.HandleAsync("What is the status of my claim NSA/MO/2026/00009?");

        Assert.Empty(opened);
        var turn = Assert.Single(fixture.Conversations.Saved);
        Assert.Equal(CustomerAssistantOutcome.Escalated, turn.Outcome);
        var sent = Assert.Single(fixture.Outbound.SentRequests);
        Assert.Contains("passed this to our team", sent.CustomerSafeBody);
        var audit = Assert.Single(fixture.Audit.Written);
        Assert.Equal("customer_assistant.processing_failed", audit.ActionName);
    }

    [Fact]
    public async Task AFailedMessage_NeverPutsTheCustomersWordsInTheAuditTrail()
    {
        var fixture = Fixture.Create(casesThrow: true);

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
            bool allowUnreviewedTranslations = true)
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
                translator is null ? null : new TestLanguageSettings(enabledLanguages ?? [SupportedLanguageCode.Lg, SupportedLanguageCode.Sw], allowUnreviewedTranslations));

            return new Fixture(scope, autoReplyEnabled, cases, policies, customers, salesLeads, outbound, whatsAppMessages, conversations, behaviourProfiles, audit, retrieval, assistant, teamsQueue, service)
            {
                LinkIssuer = linkIssuer,
            };
        }

        public Task<IReadOnlyList<Guid>> HandleAsync(string text) =>
            Service.HandleInboundMessagesAsync(
                Scope,
                [new WhatsAppInboundMessage($"wamid.{Guid.NewGuid():N}", "phone-1", "256772118001", WhatsAppContentType.Text, text, null, DateTimeOffset.UtcNow)],
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

    private sealed class FakeSalesLeadWriteRepository : ISalesLeadWriteRepository
    {
        public List<SalesLeadDetailReadModel> Created { get; } = [];
        public Task CreateAsync(string tenantId, string workspaceId, SalesLeadDetailReadModel lead, CancellationToken cancellationToken = default)
        {
            Created.Add(lead);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeWhatsAppOutboundProvider : IWhatsAppOutboundProvider
    {
        public bool IsConfigured => true;
        public List<WhatsAppProviderSendRequest> SentRequests { get; } = [];
        public Task<WhatsAppProviderSendResult> SendAsync(WhatsAppProviderSendRequest request, CancellationToken cancellationToken = default)
        {
            SentRequests.Add(request);
            return Task.FromResult(new WhatsAppProviderSendResult(true, $"wamid.out.{Guid.NewGuid():N}", null, null));
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
