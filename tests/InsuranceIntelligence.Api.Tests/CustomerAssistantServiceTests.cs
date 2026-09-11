using InsuranceIntelligence.Application.CustomerAssistant;
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
using InsuranceIntelligence.Domain.Policies;
using InsuranceIntelligence.LocalTestDoubles;

namespace InsuranceIntelligence.Api.Tests;

/// <summary>
/// Covers the orchestrator itself — KeywordCustomerIntentClassifierTests and
/// CustomerAutoReplyGateTests already cover its two pure building blocks in isolation. Uses the
/// real KeywordCustomerIntentClassifier (not a stub) so these tests exercise the same
/// text-in/outcome-out path a real WhatsApp message takes.
/// </summary>
public sealed class CustomerAssistantServiceTests
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
        Assert.Contains("InReview", turn.OutboundReplyText);
    }

    [Fact]
    public async Task ExistingClaimStatus_WithUnrecognizedPolicyNumber_IsDeclined()
    {
        var fixture = Fixture.Create();
        // Note: no SeedPolicy call — the number is well-formed but unknown to the repository.

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
        Assert.Empty(fixture.Outbound.SentRequests);
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
        Assert.Empty(fixture.Outbound.SentRequests);
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
    public async Task MediaOnlyMessage_IsSkippedEntirely()
    {
        var fixture = Fixture.Create();

        await fixture.Service.HandleInboundMessagesAsync(
            fixture.Scope,
            [new WhatsAppInboundMessage("wamid.1", "phone-1", "256772118001", WhatsAppContentType.Image, string.Empty, null, DateTimeOffset.UtcNow)],
            "correlation-1",
            autoReplyEnabled: false);

        Assert.Empty(fixture.Conversations.Saved);
        Assert.Empty(fixture.Audit.Written);
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
        public CustomerAssistantService Service { get; }

        public static Fixture Create(bool autoReplyEnabled = false)
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

            var service = new CustomerAssistantService(
                new KeywordCustomerIntentClassifier(),
                retrieval,
                assistant,
                cases,
                policies,
                customers,
                salesLeads,
                outbound,
                whatsAppMessages,
                conversations,
                behaviourProfiles,
                consent,
                audit);

            return new Fixture(scope, autoReplyEnabled, cases, policies, customers, salesLeads, outbound, whatsAppMessages, conversations, behaviourProfiles, audit, retrieval, assistant, service);
        }

        public Task HandleAsync(string text) =>
            Service.HandleInboundMessagesAsync(
                Scope,
                [new WhatsAppInboundMessage($"wamid.{Guid.NewGuid():N}", "phone-1", "256772118001", WhatsAppContentType.Text, text, null, DateTimeOffset.UtcNow)],
                $"correlation-{Guid.NewGuid():N}",
                AutoReplyEnabled);

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
        public Task<InsuranceIntelligence.Domain.Customers.Customer?> GetAsync(string customerId, TenantScope scope, CancellationToken cancellationToken) =>
            Task.FromResult<InsuranceIntelligence.Domain.Customers.Customer?>(null);
        public Task<IReadOnlyCollection<InsuranceIntelligence.Domain.Customers.Customer>> ListAsync(TenantScope scope, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyCollection<InsuranceIntelligence.Domain.Customers.Customer>>([]);
        public Task UpsertAsync(InsuranceIntelligence.Domain.Customers.Customer customer, CancellationToken cancellationToken) => Task.CompletedTask;
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
