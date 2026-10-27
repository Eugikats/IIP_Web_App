using InsuranceIntelligence.Application.BankEvents;
using InsuranceIntelligence.Application.CustomerAssistant;
using InsuranceIntelligence.Application.Rules;
using InsuranceIntelligence.Application.SalesLeads;
using InsuranceIntelligence.Application.Templates;
using InsuranceIntelligence.Application.Workspaces;
using InsuranceIntelligence.Domain.Acquisition;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Integrations;
using InsuranceIntelligence.Domain.Policies;
using InsuranceIntelligence.Infrastructure.Persistence;
using InsuranceIntelligence.LocalTestDoubles;
using Microsoft.EntityFrameworkCore;

namespace InsuranceIntelligence.Api.Tests;

/// <summary>The rules: approved once, run by themselves inside their limits, and every decision logged.</summary>
public sealed class RuleEngineTests
{
    private static readonly TenantScope Bank = new("org-bank", "main");
    private static readonly TenantScope Other = new("org-other", "main");
    private static readonly DateTimeOffset NoonEat = new(2026, 10, 27, 9, 0, 0, TimeSpan.Zero);
    private const string Phone = "256790000001";

    private sealed class StubChecklist : InsuranceIntelligence.Domain.Documents.IDocumentChecklistProvider
    {
        public Task<InsuranceIntelligence.Domain.Documents.DocumentCompletenessChecklist> BuildChecklistAsync(
            InsuranceIntelligence.Domain.Cases.ClaimCase claimCase, IReadOnlyCollection<InsuranceIntelligence.Domain.Documents.CaseDocument> documents, CancellationToken cancellationToken = default) =>
            Task.FromResult(new InsuranceIntelligence.Domain.Documents.DocumentCompletenessChecklist(
                claimCase.CaseId, claimCase.ProductLine, [], [], [],
                [new InsuranceIntelligence.Domain.Documents.MissingDocumentRequirement("PoliceAbstract", "the police report", true), new InsuranceIntelligence.Domain.Documents.MissingDocumentRequirement("MedicalReport", "the medical form", true)],
                new InsuranceIntelligence.Domain.Documents.CompletenessScore(0m, 0, 2)));
    }

    private sealed class MutableClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class Provider : IWhatsAppOutboundProvider
    {
        public bool IsConfigured => true;
        public List<WhatsAppProviderSendRequest> Sent { get; } = [];
        public Task<WhatsAppProviderSendResult> SendAsync(WhatsAppProviderSendRequest request, CancellationToken cancellationToken = default)
        {
            Sent.Add(request);
            return Task.FromResult(new WhatsAppProviderSendResult(true, $"wamid.{Sent.Count}", null, null));
        }

        public Task<bool> ShowTypingAsync(string businessPhoneNumberId, string inboundProviderMessageId, CancellationToken cancellationToken = default) => Task.FromResult(true);

        public Task<WhatsAppProviderSendResult> SendVoiceNoteAsync(string businessPhoneNumberId, string recipientWaId, byte[] oggOpusAudio, string? replyToProviderMessageId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new WhatsAppProviderSendResult(true, "v", null, null));
    }

    private sealed class Messages : IWhatsAppMessageRepository
    {
        public List<WhatsAppMessage> Saved { get; } = [];
        public Task SaveAsync(WhatsAppMessage message, CancellationToken cancellationToken = default) { Saved.RemoveAll(item => item.MessageId == message.MessageId); Saved.Add(message); return Task.CompletedTask; }
        public Task<WhatsAppMessage?> GetAsync(TenantScope scope, Guid messageId, CancellationToken cancellationToken = default) => Task.FromResult(Saved.SingleOrDefault(item => item.MessageId == messageId));
        public Task<WhatsAppMessage?> FindByProviderMessageIdAsync(TenantScope scope, string providerMessageId, CancellationToken cancellationToken = default) => Task.FromResult(Saved.SingleOrDefault(item => item.ProviderMessageId == providerMessageId));
        public Task<WhatsAppMessage?> FindByIdempotencyKeyAsync(TenantScope scope, string idempotencyKey, CancellationToken cancellationToken = default) => Task.FromResult(Saved.SingleOrDefault(item => item.IdempotencyKey == idempotencyKey && item.Scope.Matches(scope)));
        public Task<IReadOnlyList<WhatsAppMessage>> ListThreadAsync(TenantScope scope, string threadId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<WhatsAppMessage>>(Saved.Where(item => item.ThreadId == threadId).ToArray());
    }

    private sealed class Rig
    {
        public required MutableClock Clock { get; init; }
        public required InsuranceIntelligenceDbContext Db { get; init; }
        public required RuleService Rules { get; init; }
        public required RuleEngine Engine { get; init; }
        public required DecisionLog Log { get; init; }
        public required EventService Events { get; init; }
        public required TemplateCatalogService Templates { get; init; }
        public required WorkspaceBrandService Brands { get; init; }
        public required EfSalesLeadRepository Leads { get; init; }
        public required InMemoryCustomerRepository Customers { get; init; }
        public required InMemoryPolicyRepository Policies { get; init; }
        public required Provider Provider { get; init; }
        public required Messages Messages { get; init; }
        public required OutcomeService Outcomes { get; init; }
        public required InMemoryCaseRepository Cases { get; init; }
        public required PolicySweepService Sweep { get; init; }
        public required InMemoryConsentRepository Consents { get; init; }
    }

    private static async Task<Rig> RigAsync(bool approveTemplate = true, bool demonstration = false, bool connected = true)
    {
        var db = new InsuranceIntelligenceDbContext(new DbContextOptionsBuilder<InsuranceIntelligenceDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var clock = new MutableClock(NoonEat);
        var store = new EfWorkspaceRecordStore(db);
        var brands = new WorkspaceBrandService(store);
        await brands.SaveAsync(Bank, new WorkspaceBrandSettings("Mavuno Bank", "Mavuno Bank", [MicroinsuranceCatalog.Mpingo, MicroinsuranceCatalog.Kifaru], LineFocus.Both, "mavuno-bank", demonstration));
        var templates = new TemplateCatalogService(store);
        if (approveTemplate)
        {
            await templates.SubmitAsync(Bank, "bank_cover_intro", "en", "admin");
            await templates.RecordDecisionAsync(Bank, "bank_cover_intro", "en", TemplateStatus.Approved, null, "admin");
        }

        var channels = new InMemoryWhatsAppChannelConfigurationRepository();
        if (connected)
        {
            await channels.SaveAsync(new WhatsAppChannelConfiguration
            {
                Scope = Bank, PhoneNumberId = "1099", EncryptedAccessToken = "x", EncryptedAppSecret = "x", EncryptedVerifyToken = "x",
                ConfiguredByUserId = "admin", ConfiguredAt = DateTimeOffset.UtcNow,
            });
        }

        var provider = new Provider();
        var messages = new Messages();
        var sender = new TemplateMessageService(templates, channels, messages, provider, clock: clock);
        var rules = new RuleService(store, brands, templates, clock);
        var log = new DecisionLog(store);
        var events = new EventService(store, clock);
        var leads = new EfSalesLeadRepository(db);
        var customers = new InMemoryCustomerRepository();
        var policies = new InMemoryPolicyRepository();
        var outcomes = new OutcomeService(store, log, leads);
        var cases = new InMemoryCaseRepository();
        var sweep = new PolicySweepService(policies, customers, events, clock);
        var consentStore = new InMemoryConsentRepository();
        var caseSweep = new CaseSweepService(cases, new InMemoryCaseDocumentRepository(), new StubChecklist(), events, clock);
        return new Rig
        {
            Clock = clock, Db = db, Rules = rules, Log = log, Events = events, Templates = templates, Brands = brands, Leads = leads, Customers = customers, Policies = policies,
            Provider = provider, Messages = messages, Outcomes = outcomes, Cases = cases, Sweep = sweep, Consents = consentStore,
            Engine = new RuleEngine(rules, log, events, brands, templates, sender, leads, customers, policies, messages, clock, outcomes, cases, sweep, caseSweep, consentStore),
        };
    }

    private static OutreachRule LoanRule(string name = "Loan Protector after a salary loan", int wait = 2, RuleLimits? limits = null, RuleConditions? conditions = null, string[]? lines = null) => new(
        "", name, 0, "loan_disbursed", conditions ?? new RuleConditions(2_000_000m, 12), wait,
        lines ?? [MicroinsuranceCatalog.LoanProtectorLine, MicroinsuranceCatalog.FuneralLine, MicroinsuranceCatalog.SchoolFeesLine],
        "bank_cover_intro", "en", new RuleHours(9, 18), limits ?? new RuleLimits(), null, null, RuleStatus.Draft, "writer", DateTimeOffset.UnixEpoch);

    private static async Task<OutreachRule> LiveRuleAsync(Rig rig, OutreachRule? rule = null)
    {
        var saved = await rig.Rules.SaveAsync(Bank, rule ?? LoanRule(), "writer");
        return await rig.Rules.ApproveAsync(Bank, saved.RuleId, "approver", ["Admin"]);
    }

    private static async Task AddEugeneAsync(Rig rig, ContactConsentStatus consent = ContactConsentStatus.Agreed, string phone = Phone, string name = "Eugene Katusiime")
    {
        var lead = new SalesLeadDetailReadModel(
            $"LEAD-{phone}", "Bank customer list", "Customer of the bank", "Demonstration", "To be assessed", "New", [], [], null, null,
            name, $"+{phone}", NoonEat.AddDays(-30), "test")
        {
            Phone = phone,
            BankCustomer = true,
            Facts = ProspectFacts.Merge([], new[]
            {
                new ProspectFact("bank_products", "Salary account, savings account, salary loan", ProspectFactSource.ImportedList, NoonEat, null, "test"),
                new ProspectFact("dependants", "2 children", ProspectFactSource.ImportedList, NoonEat, null, "test"),
                new ProspectFact("income_band", "About UGX 2.4m a month", ProspectFactSource.ImportedList, NoonEat, null, "test"),
            }),
            Consent = consent == ContactConsentStatus.NotRecorded ? null : new ContactConsent(consent, NoonEat.AddDays(-20), "Agreed to bank insurance marketing", "Bank list"),
        };
        await rig.Leads.CreateAsync(Bank.TenantId, Bank.WorkspaceId, lead);
    }

    private static Task<EventRecordResult> LoanEventAsync(Rig rig, int daysAgo = 2, decimal amount = 8_000_000m, int term = 36, string phone = Phone, string? external = null) =>
        rig.Events.RecordAsync(Bank, new EventInput("loan_disbursed", phone, NoonEat.AddDays(-daysAgo), external, amount, term, null, "Ntinda"), EventSource.Recorded, null, "staff");

    // ---- The Eugene case

    [Fact]
    public async Task ALoanDisbursedTwoDaysAgo_OffersTheBestFitCover_ByTheApprovedTemplate_AndLogsEveryGate()
    {
        var rig = await RigAsync();
        await LiveRuleAsync(rig);
        await AddEugeneAsync(rig);
        var made = await LoanEventAsync(rig);

        var run = await rig.Engine.RunAsync(Bank, "system");

        Assert.Equal(1, run.Sent);
        var decision = Assert.Single(await rig.Log.ListAsync(Bank));
        Assert.Equal(DecisionOutcome.Sent, decision.Outcome);
        Assert.Equal("Mpingo Loan Protector", decision.Cover);
        Assert.Equal("Mpingo Life Assurance", decision.Insurer);
        Assert.All(decision.Gates, gate => Assert.True(gate.Passed, $"{gate.Name}: {gate.Detail}"));
        Assert.Contains(decision.Gates, gate => gate.Name == "Agreed to be contacted");
        Assert.Contains(decision.Gates, gate => gate.Name == "Best-fit cover");
        var request = Assert.Single(rig.Provider.Sent);
        Assert.Equal("bank_cover_intro", request.TemplateName);
        Assert.Equal(["Eugene", "Mavuno Bank", "a salary account, a savings account and a salary loan", "Mpingo Loan Protector"], request.TemplateParameters!.OrderBy(pair => pair.Key).Select(pair => pair.Value).ToArray());
        Assert.Equal(EventProcessingState.Processed, (await rig.Events.GetAsync(Bank, made.Event.EventId))!.State);
        var reason = await rig.Log.GetReasonAsync(Bank, Phone);
        Assert.Equal("Loan disbursed", reason!.EventLabel);
        Assert.Contains("Mpingo Loan Protector", reason.OpeningText);
    }

    [Fact]
    public async Task RunningAgainSendsNothingMore()
    {
        var rig = await RigAsync();
        await LiveRuleAsync(rig);
        await AddEugeneAsync(rig);
        await LoanEventAsync(rig);

        await rig.Engine.RunAsync(Bank, "system");
        var second = await rig.Engine.RunAsync(Bank, "system");

        Assert.Equal(0, second.Considered);
        Assert.Single(rig.Provider.Sent);
    }

    // ---- Gates

    [Theory]
    [InlineData(ContactConsentStatus.NotRecorded, "no recorded agreement")]
    [InlineData(ContactConsentStatus.Declined, "asked not to be contacted")]
    [InlineData(ContactConsentStatus.Withdrawn, "asked not to be contacted")]
    public async Task WithoutAgreementNothingIsSent_AndTheLogSaysWhy(ContactConsentStatus consent, string expected)
    {
        var rig = await RigAsync();
        await LiveRuleAsync(rig);
        await AddEugeneAsync(rig, consent);
        await LoanEventAsync(rig);

        await rig.Engine.RunAsync(Bank, "system");

        var decision = Assert.Single(await rig.Log.ListAsync(Bank));
        Assert.Equal(DecisionOutcome.Skipped, decision.Outcome);
        Assert.Contains(expected, decision.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(rig.Provider.Sent);
    }

    [Fact]
    public async Task SomeoneWhoIsNotACustomerOfThisWorkspaceIsSkipped()
    {
        var rig = await RigAsync();
        await LiveRuleAsync(rig);
        await LoanEventAsync(rig, phone: "256790000999");

        await rig.Engine.RunAsync(Bank, "system");

        Assert.Equal("Known customer", Assert.Single(await rig.Log.ListAsync(Bank)).Gates.Last().Name);
        Assert.Empty(rig.Provider.Sent);
    }

    [Fact]
    public async Task AnEventBelowTheRulesConditionsIsSkipped()
    {
        var rig = await RigAsync();
        await LiveRuleAsync(rig);
        await AddEugeneAsync(rig);
        await LoanEventAsync(rig, amount: 500_000m, external: "a");
        await LoanEventAsync(rig, term: 6, external: "b");

        var run = await rig.Engine.RunAsync(Bank, "system");

        Assert.Equal(2, run.Skipped);
        Assert.Empty(rig.Provider.Sent);
    }

    [Fact]
    public async Task SomeoneWhoAlreadyHoldsTheOnlyCoverOffered_IsNotOffered_ButOtherCoversStillCanBe()
    {
        var rig = await RigAsync();
        await AddEugeneAsync(rig);
        await rig.Customers.UpsertAsync(new InsuranceIntelligence.Domain.Customers.Customer("c1", Bank.TenantId, Bank.WorkspaceId, "Eugene", null, $"+{Phone}"));
        await rig.Policies.UpsertAsync(new Policy("p1", Bank.TenantId, Bank.WorkspaceId, "MLA/LP/2026/1", MicroinsuranceCatalog.LoanProtectorLine, null, "c1", null,
            DateOnly.FromDateTime(NoonEat.UtcDateTime).AddDays(-30), DateOnly.FromDateTime(NoonEat.UtcDateTime).AddDays(300)));
        await LiveRuleAsync(rig, LoanRule(lines: [MicroinsuranceCatalog.LoanProtectorLine]));
        await LoanEventAsync(rig);

        var skipped = await rig.Engine.RunAsync(Bank, "system");

        Assert.Equal(1, skipped.Skipped);
        Assert.Empty(rig.Provider.Sent);

        // A second rule that may also offer funeral cover still finds something to offer.
        await LiveRuleAsync(rig, LoanRule("Also funeral", lines: [MicroinsuranceCatalog.LoanProtectorLine, MicroinsuranceCatalog.FuneralLine]));
        await LoanEventAsync(rig, external: "again");
        await rig.Engine.RunAsync(Bank, "system");
        Assert.Contains(rig.Provider.Sent, request => request.TemplateParameters!["4"].Contains("Funeral"));
    }

    [Fact]
    public async Task OnlyOneOfferPerPersonInTheWindow_EvenFromTwoEvents()
    {
        var rig = await RigAsync();
        await LiveRuleAsync(rig);
        await AddEugeneAsync(rig);
        await LoanEventAsync(rig, external: "one");
        await rig.Engine.RunAsync(Bank, "system");

        await LoanEventAsync(rig, daysAgo: 2, external: "two");
        var second = await rig.Engine.RunAsync(Bank, "system");

        Assert.Equal(1, second.Skipped);
        Assert.Single(rig.Provider.Sent);
        Assert.Contains("recently", (await rig.Log.ListAsync(Bank, outcome: DecisionOutcome.Skipped)).Single().Reason);
    }

    [Fact]
    public async Task AMessageThatIsNotYetDueWaits_AndGoesWhenItIs()
    {
        var rig = await RigAsync();
        await LiveRuleAsync(rig, LoanRule(wait: 3));
        await AddEugeneAsync(rig);
        var made = await LoanEventAsync(rig, daysAgo: 1);

        var first = await rig.Engine.RunAsync(Bank, "system");
        Assert.Equal(1, first.Scheduled);
        Assert.Empty(rig.Provider.Sent);
        Assert.Equal(EventProcessingState.Pending, (await rig.Events.GetAsync(Bank, made.Event.EventId))!.State);

        rig.Clock.Now = NoonEat.AddDays(2);
        var later = await rig.Engine.RunAsync(Bank, "system");

        Assert.Equal(1, later.Sent);
        Assert.Single(rig.Provider.Sent);
        Assert.Equal(DecisionOutcome.Sent, Assert.Single(await rig.Log.ListAsync(Bank)).Outcome);
    }

    [Fact]
    public async Task OutsideSendingHoursItWaitsForTheNextWindow()
    {
        var rig = await RigAsync();
        await LiveRuleAsync(rig);
        await AddEugeneAsync(rig);
        await LoanEventAsync(rig);
        rig.Clock.Now = new DateTimeOffset(2026, 10, 27, 20, 0, 0, TimeSpan.Zero); // 23:00 East Africa time

        var run = await rig.Engine.RunAsync(Bank, "system");

        Assert.Equal(1, run.Scheduled);
        var decision = Assert.Single(await rig.Log.ListAsync(Bank));
        Assert.Equal(new DateTimeOffset(2026, 10, 28, 6, 0, 0, TimeSpan.Zero), decision.NotBefore); // 09:00 East Africa time next morning
        Assert.Empty(rig.Provider.Sent);
    }

    [Fact]
    public async Task TheDailyLimitHoldsBackTheRestUntilTomorrow()
    {
        var rig = await RigAsync();
        await LiveRuleAsync(rig, LoanRule(limits: new RuleLimits(30, 90, 1)));
        await AddEugeneAsync(rig);
        await AddEugeneAsync(rig, phone: "256790000002", name: "Second Person");
        await LoanEventAsync(rig);
        await LoanEventAsync(rig, phone: "256790000002");

        var run = await rig.Engine.RunAsync(Bank, "system");

        Assert.Equal((1, 1), (run.Sent, run.Scheduled));
        Assert.Single(rig.Provider.Sent);
    }

    // ---- Approval

    [Fact]
    public async Task ADraftOrPausedOrChangedRuleDoesNothing()
    {
        var rig = await RigAsync();
        await AddEugeneAsync(rig);
        await LoanEventAsync(rig);
        var draft = await rig.Rules.SaveAsync(Bank, LoanRule(), "writer");
        Assert.Equal(0, (await rig.Engine.RunAsync(Bank, "system")).Considered);

        var live = await rig.Rules.ApproveAsync(Bank, draft.RuleId, "approver", ["Admin"]);
        await rig.Rules.PauseAsync(Bank, live.RuleId);
        Assert.Equal(0, (await rig.Engine.RunAsync(Bank, "system")).Considered);

        var resumed = await rig.Rules.ResumeAsync(Bank, live.RuleId);
        Assert.True(resumed.IsLive);
        var edited = await rig.Rules.SaveAsync(Bank, resumed with { WaitDays = 5 }, "writer");
        Assert.Equal(RuleStatus.Draft, edited.Status);
        Assert.Null(edited.ApprovedBy);
        Assert.False(edited.IsLive);
        Assert.Equal(0, (await rig.Engine.RunAsync(Bank, "system")).Considered);
    }

    [Fact]
    public async Task ARuleChangedWhilePausedCannotBeResumedWithoutApproval()
    {
        var rig = await RigAsync();
        var live = await LiveRuleAsync(rig);
        await rig.Rules.PauseAsync(Bank, live.RuleId);
        var tampered = (await rig.Rules.GetAsync(Bank, live.RuleId))! with { WaitDays = 9 };
        await new EfWorkspaceRecordStore(rig.Db).SaveAsync(Bank, RuleService.Kind, live.RuleId,
            System.Text.Json.JsonSerializer.Serialize(tampered, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web) { Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } }));

        var error = await Assert.ThrowsAsync<RuleException>(() => rig.Rules.ResumeAsync(Bank, live.RuleId));

        Assert.Equal("conflict", error.Code);
    }

    [Fact]
    public async Task OnlyTheRolesTheAdministratorChoseMayApprove_AndTheApproverIsNamed()
    {
        var rig = await RigAsync();
        var saved = await rig.Rules.SaveAsync(Bank, LoanRule(), "writer");

        await Assert.ThrowsAsync<RuleException>(() => rig.Rules.ApproveAsync(Bank, saved.RuleId, "marketing-1", ["Marketing"]));
        await rig.Rules.SaveSettingsAsync(Bank, new RuleApprovalSettings(["Compliance", "Admin"]));
        var approved = await rig.Rules.ApproveAsync(Bank, saved.RuleId, "compliance-1", ["Compliance"]);

        Assert.Equal("compliance-1", approved.ApprovedBy);
        Assert.Equal("Compliance", approved.ApprovedByRole);
        Assert.NotNull(approved.ApprovedAt);
        Assert.True(approved.IsLive);
    }

    [Fact]
    public async Task WhenFourEyesIsOnTheWriterCannotApproveTheirOwnRule()
    {
        var rig = await RigAsync();
        await rig.Rules.SaveSettingsAsync(Bank, new RuleApprovalSettings(["Admin"], RequireDifferentApprover: true));
        var saved = await rig.Rules.SaveAsync(Bank, LoanRule(), "writer");

        await Assert.ThrowsAsync<RuleException>(() => rig.Rules.ApproveAsync(Bank, saved.RuleId, "writer", ["Admin"]));
        Assert.True((await rig.Rules.ApproveAsync(Bank, saved.RuleId, "colleague", ["Admin"])).IsLive);
    }

    [Fact]
    public async Task ARuleCannotOfferACoverTheOrganisationDoesNotSell_OrUseATemplateItCannotFill()
    {
        var rig = await RigAsync();

        await Assert.ThrowsAsync<RuleException>(() => rig.Rules.SaveAsync(Bank, LoanRule(lines: [MicroinsuranceCatalog.BodaAccidentLine]), "w"));
        await Assert.ThrowsAsync<RuleException>(() => rig.Rules.SaveAsync(Bank, LoanRule() with { TemplateName = "verify_code" }, "w"));
        await Assert.ThrowsAsync<RuleException>(() => rig.Rules.SaveAsync(Bank, LoanRule() with { TemplateName = "does_not_exist" }, "w"));
        await Assert.ThrowsAsync<RuleException>(() => rig.Rules.SaveAsync(Bank, LoanRule() with { Hours = new RuleHours(18, 9) }, "w"));
        await Assert.ThrowsAsync<RuleException>(() => rig.Rules.SaveAsync(Bank, LoanRule() with { EventType = "nonsense" }, "w"));
    }

    // ---- Delivery

    [Fact]
    public async Task WithoutAnApprovedTemplate_ARealWorkspaceBlocksAndSaysWhy()
    {
        var rig = await RigAsync(approveTemplate: false);
        await LiveRuleAsync(rig);
        await AddEugeneAsync(rig);
        await LoanEventAsync(rig);

        var run = await rig.Engine.RunAsync(Bank, "system");

        Assert.Equal(1, run.Blocked);
        Assert.Contains("not been approved", (await rig.Log.ListAsync(Bank)).Single().Reason);
        Assert.Empty(rig.Provider.Sent);
        Assert.Empty(rig.Messages.Saved);
    }

    [Fact]
    public async Task InADemonstrationWorkspaceTheSameSituationIsShownAsSent_AndSaidNotDelivered()
    {
        var rig = await RigAsync(approveTemplate: false, demonstration: true, connected: false);
        await LiveRuleAsync(rig);
        await AddEugeneAsync(rig);
        await LoanEventAsync(rig);

        var run = await rig.Engine.RunAsync(Bank, "system");

        Assert.Equal(1, run.DemoSent);
        var decision = (await rig.Log.ListAsync(Bank)).Single();
        Assert.Equal(DecisionOutcome.DemoSent, decision.Outcome);
        Assert.Contains("Not delivered to a phone", decision.Reason);
        Assert.Empty(rig.Provider.Sent);
        Assert.Contains("Mpingo Loan Protector", Assert.Single(rig.Messages.Saved).CustomerSafeBody);
        Assert.True((await rig.Log.GetReasonAsync(Bank, Phone))!.Demonstration);
    }

    [Fact]
    public async Task ARuleInOneWorkspaceNeverActsInAnother()
    {
        var rig = await RigAsync();
        await LiveRuleAsync(rig);
        await rig.Events.RecordAsync(Other, new EventInput("loan_disbursed", Phone, NoonEat.AddDays(-2), null, 8_000_000m, 36), EventSource.Recorded, null, "staff");

        var run = await rig.Engine.RunAsync(Other, "system");

        Assert.Equal(0, run.Considered);
        Assert.Empty(rig.Provider.Sent);
        Assert.Empty(await rig.Rules.ListAsync(Other));
    }

    // ---- Replies, hand-offs, STOP and follow-ups

    [Theory]
    [InlineData("Yes, show me", OutcomeKind.Interested)]
    [InlineData("yes", OutcomeKind.Interested)]
    [InlineData("Not now", OutcomeKind.NotNow)]
    [InlineData("Maybe later!", OutcomeKind.NotNow)]
    [InlineData("No thanks", OutcomeKind.Declined)]
    [InlineData("STOP", OutcomeKind.Stopped)]
    [InlineData("Stop messages", OutcomeKind.Stopped)]
    [InlineData("  stop. ", OutcomeKind.Stopped)]
    [InlineData("Unsubscribe", OutcomeKind.Stopped)]
    public void ShortRepliesAreReadTheWayTheButtonsAndObviousWordsMean(string text, OutcomeKind expected) =>
        Assert.Equal(expected, OutcomeService.Classify(text, renewalContext: false));

    [Theory]
    [InlineData("I will not stop paying my loan")]
    [InlineData("Please stop by my shop tomorrow")]
    [InlineData("How much does it cost?")]
    public void AnythingElseIsNotMistakenForAnAnswerOrAStop(string text) =>
        Assert.Null(OutcomeService.Classify(text, renewalContext: false));

    [Fact]
    public void OnARenewalOfferAYesMeansRenew()
    {
        Assert.Equal(OutcomeKind.RenewalRequested, OutcomeService.Classify("Yes", renewalContext: true));
        Assert.Equal(OutcomeKind.RenewalRequested, OutcomeService.Classify("Renew", renewalContext: false));
    }

    private static async Task<Rig> OfferedAsync()
    {
        var rig = await RigAsync();
        await LiveRuleAsync(rig);
        await AddEugeneAsync(rig);
        await LoanEventAsync(rig);
        await rig.Engine.RunAsync(Bank, "system");
        return rig;
    }

    [Fact]
    public async Task YesShowMe_IsRecordedAgainstTheOffer_AndListedForAPersonWithItsContext()
    {
        var rig = await OfferedAsync();

        var handled = await rig.Outcomes.RecordReplyAsync(Bank, Phone, "Yes, show me", NoonEat.AddMinutes(5));

        Assert.False(handled.Stop);
        var outcome = Assert.Single(await rig.Outcomes.ListAsync(Bank, needsPerson: true));
        Assert.Equal(OutcomeKind.Interested, outcome.Kind);
        Assert.Equal("Mpingo Loan Protector", outcome.Cover);
        Assert.Equal("Loan Protector after a salary loan", outcome.RuleName);
        Assert.Contains("Hello Eugene", outcome.OpeningText);
        Assert.Equal("Eugene Katusiime", outcome.CustomerName);
        var lead = await rig.Leads.FindByPhoneAsync(Bank.TenantId, Bank.WorkspaceId, Phone);
        Assert.Contains(lead!.Activity, activity => activity.Text.Contains("Asked for details of Mpingo Loan Protector"));

        await rig.Outcomes.MarkDoneAsync(Bank, outcome.OutcomeId, "officer-1", NoonEat.AddHours(1));
        Assert.Empty(await rig.Outcomes.ListAsync(Bank, needsPerson: true));
        Assert.Equal("officer-1", (await rig.Outcomes.GetAsync(Bank, outcome.OutcomeId))!.DoneBy);
    }

    [Fact]
    public async Task NotNowAndNoThanksAreRecorded_ButDoNotNeedAPerson()
    {
        var rig = await OfferedAsync();

        await rig.Outcomes.RecordReplyAsync(Bank, Phone, "Not now", NoonEat.AddMinutes(5));

        var outcome = Assert.Single(await rig.Outcomes.ListAsync(Bank));
        Assert.Equal(OutcomeKind.NotNow, outcome.Kind);
        Assert.False(outcome.NeedsPerson);
    }

    [Fact]
    public async Task StopWithdrawsConsent_NothingMoreGoesOut_AndTheDecisionLogSaysWhy()
    {
        var rig = await OfferedAsync();
        var stop = await rig.Outcomes.RecordReplyAsync(Bank, Phone, "STOP", NoonEat.AddMinutes(5));
        Assert.True(stop.Stop);

        await rig.Outcomes.WithdrawAsync(Bank, Phone, NoonEat.AddMinutes(5));

        var lead = await rig.Leads.FindByPhoneAsync(Bank.TenantId, Bank.WorkspaceId, Phone);
        Assert.Equal(ContactConsentStatus.Withdrawn, lead!.Consent!.Status);
        Assert.Equal(OutcomeKind.Stopped, Assert.Single(await rig.Outcomes.ListAsync(Bank)).Kind);

        rig.Clock.Now = NoonEat.AddDays(40);
        await LoanEventAsync(rig, daysAgo: 0, external: "later");
        await rig.Engine.RunAsync(Bank, "system");
        var latest = (await rig.Log.ListAsync(Bank)).First();
        Assert.Equal(DecisionOutcome.Skipped, latest.Outcome);
        Assert.Contains("asked not to be contacted", latest.Reason);
        Assert.Single(rig.Provider.Sent);
    }

    [Fact]
    public async Task AStopFromSomeoneNoRuleWroteTo_IsStillHonoured()
    {
        var rig = await RigAsync();
        await AddEugeneAsync(rig);

        var handled = await rig.Outcomes.RecordReplyAsync(Bank, Phone, "stop", NoonEat);
        await rig.Outcomes.WithdrawAsync(Bank, Phone, NoonEat);

        Assert.True(handled.Stop);
        Assert.Equal(ContactConsentStatus.Withdrawn, (await rig.Leads.FindByPhoneAsync(Bank.TenantId, Bank.WorkspaceId, Phone))!.Consent!.Status);
    }

    [Fact]
    public async Task AReplyLongAfterTheOfferIsNotAttachedToIt()
    {
        var rig = await OfferedAsync();

        var handled = await rig.Outcomes.RecordReplyAsync(Bank, Phone, "Yes, show me", NoonEat.AddDays(30));

        Assert.Null(handled.Kind);
        Assert.Empty(await rig.Outcomes.ListAsync(Bank));
    }

    [Fact]
    public async Task OneGentleFollowUpGoesAfterSilence_AndNeverAfterAnAnswer()
    {
        var rig = await RigAsync();
        await rig.Templates.SubmitAsync(Bank, "quote_follow_up", "en", "admin");
        await rig.Templates.RecordDecisionAsync(Bank, "quote_follow_up", "en", TemplateStatus.Approved, null, "admin");
        await LiveRuleAsync(rig, LoanRule() with { FollowUpAfterDays = 3, FollowUpTemplate = "quote_follow_up" });
        await AddEugeneAsync(rig);
        await AddEugeneAsync(rig, phone: "256790000002", name: "Second Person");
        await LoanEventAsync(rig);
        await LoanEventAsync(rig, phone: "256790000002");
        await rig.Engine.RunAsync(Bank, "system");
        Assert.Equal(2, rig.Provider.Sent.Count);
        await rig.Outcomes.RecordReplyAsync(Bank, "256790000002", "Not now", NoonEat.AddHours(2));

        rig.Clock.Now = NoonEat.AddDays(2);
        await rig.Engine.RunAsync(Bank, "system");
        Assert.Equal(2, rig.Provider.Sent.Count);

        rig.Clock.Now = NoonEat.AddDays(4);
        var run = await rig.Engine.RunAsync(Bank, "system");
        var again = await rig.Engine.RunAsync(Bank, "system");

        Assert.Equal(1, run.Sent);
        Assert.Equal(0, again.Considered);
        Assert.Equal(3, rig.Provider.Sent.Count);
        var nudge = Assert.Single(rig.Provider.Sent, request => request.TemplateName == "quote_follow_up");
        Assert.Equal("Eugene", nudge.TemplateParameters!["1"]);
        var followUps = await rig.Log.ListAsync(Bank);
        Assert.Contains(followUps, decision => decision.Kind == "follow_up" && decision.Outcome == DecisionOutcome.Sent);
        Assert.Contains(followUps, decision => decision.Kind == "follow_up" && decision.Outcome == DecisionOutcome.Skipped && decision.Reason == "They already answered.");
    }

    [Fact]
    public async Task SomeoneWithAClaimInProgressIsNotOfferedAnything_AndIsOnceTheClaimIsSettled()
    {
        var rig = await RigAsync();
        await LiveRuleAsync(rig);
        await AddEugeneAsync(rig);
        var claim = new InsuranceIntelligence.Domain.Cases.ClaimCase
        {
            CaseId = Guid.NewGuid(), Scope = Bank, CustomerName = "Eugene", CustomerPhone = $"+{Phone}", PolicyNumber = "P1", ProductLine = "Loan Protector",
            ClaimType = "Test", Channel = "WhatsApp", IncidentDate = DateOnly.FromDateTime(NoonEat.UtcDateTime), IncidentDescription = "Test",
            Status = InsuranceIntelligence.Domain.Cases.CaseStatus.InReview, CreatedByUserId = "t", CreatedAt = NoonEat, UpdatedAt = NoonEat, CorrelationId = "c",
            Priority = InsuranceIntelligence.Domain.Workflow.CasePriority.Normal, SlaDueAt = NoonEat.AddDays(5), RiskLevel = "Low", CompletenessScore = 0m,
        };
        await rig.Cases.SaveAsync(claim);
        await LoanEventAsync(rig, external: "during-claim");

        var during = await rig.Engine.RunAsync(Bank, "system");

        Assert.Equal(1, during.Skipped);
        Assert.Contains("claim in progress", (await rig.Log.ListAsync(Bank)).Single().Reason);
        Assert.Empty(rig.Provider.Sent);

        await rig.Cases.SaveAsync(claim with { Status = InsuranceIntelligence.Domain.Cases.CaseStatus.Settled });
        await LoanEventAsync(rig, external: "after-claim");
        var after = await rig.Engine.RunAsync(Bank, "system");
        Assert.Equal(1, after.Sent);
    }

    [Fact]
    public async Task TheDemonstrationRulesForTheBankAreValid_AndStartAsDraftsNobodyHasApproved()
    {
        var rig = await RigAsync();
        var scenario = InsuranceIntelligence.Application.DemoScenarios.DemoScenarioCatalog.Find(InsuranceIntelligence.Application.DemoScenarios.DemoScenarioCatalog.MavunoBank)!;

        foreach (var rule in InsuranceIntelligence.Application.DemoScenarios.DemoScenarioRules.For(scenario))
        {
            await rig.Rules.SaveAsync(Bank, rule, "demo-setup");
        }

        var saved = await rig.Rules.ListAsync(Bank);
        Assert.Equal(3, saved.Count);
        Assert.All(saved, rule => Assert.Equal(RuleStatus.Draft, rule.Status));
        foreach (var id in new[] { InsuranceIntelligence.Application.DemoScenarios.DemoScenarioCatalog.NileLife, InsuranceIntelligence.Application.DemoScenarios.DemoScenarioCatalog.NileGeneral })
        {
            var insurer = InsuranceIntelligence.Application.DemoScenarios.DemoScenarioRules.For(InsuranceIntelligence.Application.DemoScenarios.DemoScenarioCatalog.Find(id)!);
            Assert.Equal(id == InsuranceIntelligence.Application.DemoScenarios.DemoScenarioCatalog.NileLife ? 3 : 6, insurer.Count);
            Assert.All(insurer, rule => Assert.True(BankEventCatalog.Find(rule.EventType)!.IsTimeTrigger));
        }
    }

    // ---- Renewals and premiums, from dates already in the policy records

    private static async Task AddFuneralPolicyAsync(Rig rig, int daysUntilEnd, string policyId = "p-funeral", string customerId = "c1", string phone = Phone)
    {
        await rig.Customers.UpsertAsync(new InsuranceIntelligence.Domain.Customers.Customer(customerId, Bank.TenantId, Bank.WorkspaceId, "Eugene Katusiime", null, $"+{phone}"));
        var today = DateOnly.FromDateTime(NoonEat.UtcDateTime);
        await rig.Policies.UpsertAsync(new Policy(policyId, Bank.TenantId, Bank.WorkspaceId, "MLA/FF/2026/00001", MicroinsuranceCatalog.FuneralLine, null, customerId, null,
            today.AddDays(daysUntilEnd - 364), today.AddDays(daysUntilEnd), 96_000m, "UGX", 3_000_000m));
    }

    [Fact]
    public async Task TheSweepNoticesRenewalsLapsesAndPremiumsFromPolicyDates_OnceEach()
    {
        var rig = await RigAsync();
        await AddFuneralPolicyAsync(rig, 20, "p-30", "c30", "256790000030");
        await AddFuneralPolicyAsync(rig, 5, "p-7", "c7", "256790000007");
        await AddFuneralPolicyAsync(rig, -4, "p-lapsed", "cl", "256790000004");
        await AddFuneralPolicyAsync(rig, 200, "p-far", "cf", "256790000200");

        var first = await rig.Sweep.SweepAsync(Bank);
        var second = await rig.Sweep.SweepAsync(Bank);

        var types = (await rig.Events.ListAsync(Bank, take: 100)).Select(item => item.EventType).Order().ToArray();
        Assert.Contains("policy_ending_30", types);
        Assert.Contains("policy_ending_7", types);
        Assert.Contains("policy_lapsed", types);
        Assert.Equal(first.New, (await rig.Events.ListAsync(Bank, take: 100)).Count);
        Assert.Equal(0, second.New);
        var ending = (await rig.Events.ListAsync(Bank, eventType: "policy_ending_30")).Single();
        Assert.Equal("MLA/FF/2026/00001", ending.Details["policy_number"]);
        Assert.Equal("Mpingo Family Funeral Cover", ending.Details["product"]);
        Assert.Equal(EventSource.PolicyDates, ending.Source);
    }

    [Fact]
    public async Task ARenewalReminderGoesThirtyDaysBefore_AndTheSevenDayOneStillGoesLater()
    {
        var rig = await RigAsync();
        foreach (var name in new[] { "renewal_30_days", "renewal_7_days" })
        {
            await rig.Templates.SubmitAsync(Bank, name, "en", "admin");
            await rig.Templates.RecordDecisionAsync(Bank, name, "en", TemplateStatus.Approved, null, "admin");
        }

        string[] lines = [MicroinsuranceCatalog.FuneralLine, MicroinsuranceCatalog.LoanProtectorLine, MicroinsuranceCatalog.SchoolFeesLine];
        await LiveRuleAsync(rig, new OutreachRule("", "Renewal, 30 days", 0, "policy_ending_30", new RuleConditions(), 0, lines, "renewal_30_days", "en", new RuleHours(9, 18), new RuleLimits(), null, null, RuleStatus.Draft, "w", DateTimeOffset.UnixEpoch));
        await LiveRuleAsync(rig, new OutreachRule("", "Renewal, 7 days", 0, "policy_ending_7", new RuleConditions(), 0, lines, "renewal_7_days", "en", new RuleHours(9, 18), new RuleLimits(), null, null, RuleStatus.Draft, "w", DateTimeOffset.UnixEpoch));
        await AddEugeneAsync(rig);
        await AddFuneralPolicyAsync(rig, 20);

        var first = await rig.Engine.RunAsync(Bank, "system");
        Assert.Equal(1, first.Sent);
        var request = Assert.Single(rig.Provider.Sent);
        Assert.Equal("renewal_30_days", request.TemplateName);
        var endDate = DateOnly.FromDateTime(NoonEat.UtcDateTime).AddDays(20).ToString("d MMMM yyyy", System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(["Eugene", "Mpingo Family Funeral Cover", "MLA/FF/2026/00001", endDate], request.TemplateParameters!.OrderBy(pair => pair.Key).Select(pair => pair.Value).ToArray());

        Assert.Equal(0, (await rig.Engine.RunAsync(Bank, "system")).Sent);

        rig.Clock.Now = NoonEat.AddDays(15);
        var later = await rig.Engine.RunAsync(Bank, "system");

        Assert.Equal(1, later.Sent);
        Assert.Equal(["renewal_30_days", "renewal_7_days"], rig.Provider.Sent.Select(sent => sent.TemplateName).ToArray());
    }

    [Fact]
    public async Task APremiumReminderGivesTheAmountAndTheDueDate()
    {
        var rig = await RigAsync();
        await rig.Templates.SubmitAsync(Bank, "premium_due", "en", "admin");
        await rig.Templates.RecordDecisionAsync(Bank, "premium_due", "en", TemplateStatus.Approved, null, "admin");
        await LiveRuleAsync(rig, new OutreachRule("", "Premium due", 0, "premium_due", new RuleConditions(), 0, [MicroinsuranceCatalog.FuneralLine], "premium_due", "en", new RuleHours(9, 18), new RuleLimits(), null, null, RuleStatus.Draft, "w", DateTimeOffset.UnixEpoch));
        await AddEugeneAsync(rig);
        var today = DateOnly.FromDateTime(NoonEat.UtcDateTime);
        await rig.Customers.UpsertAsync(new InsuranceIntelligence.Domain.Customers.Customer("c1", Bank.TenantId, Bank.WorkspaceId, "Eugene Katusiime", null, $"+{Phone}"));
        // Began on a date whose day of the month falls two days from now.
        var started = today.AddDays(2).AddMonths(-5);
        await rig.Policies.UpsertAsync(new Policy("p1", Bank.TenantId, Bank.WorkspaceId, "MLA/FF/2026/00001", MicroinsuranceCatalog.FuneralLine, null, "c1", null, started, started.AddYears(1), 96_000m, "UGX", 3_000_000m));

        var run = await rig.Engine.RunAsync(Bank, "system");

        Assert.Equal(1, run.Sent);
        var request = Assert.Single(rig.Provider.Sent);
        Assert.Equal("UGX 8,000", request.TemplateParameters!["3"]);
        Assert.Equal(today.AddDays(2).ToString("d MMMM yyyy", System.Globalization.CultureInfo.InvariantCulture), request.TemplateParameters["4"]);
    }

    [Fact]
    public async Task ARenewalRuleDoesNotApplyToAProductItDoesNotList()
    {
        var rig = await RigAsync();
        await rig.Templates.SubmitAsync(Bank, "renewal_30_days", "en", "admin");
        await rig.Templates.RecordDecisionAsync(Bank, "renewal_30_days", "en", TemplateStatus.Approved, null, "admin");
        await LiveRuleAsync(rig, new OutreachRule("", "Renewal", 0, "policy_ending_30", new RuleConditions(), 0, [MicroinsuranceCatalog.HomeContentsLine], "renewal_30_days", "en", new RuleHours(9, 18), new RuleLimits(), null, null, RuleStatus.Draft, "w", DateTimeOffset.UnixEpoch));
        await AddEugeneAsync(rig);
        await AddFuneralPolicyAsync(rig, 20);

        var run = await rig.Engine.RunAsync(Bank, "system");

        Assert.Equal(1, run.Skipped);
        Assert.Empty(rig.Provider.Sent);
    }

    // ---- Claims

    private static InsuranceIntelligence.Domain.Cases.ClaimCase Claim(InsuranceIntelligence.Domain.Cases.CaseStatus status, int createdDaysAgo = 1) => new()
    {
        CaseId = Guid.Parse("11111111-2222-3333-4444-555555555555"), Scope = Bank, CustomerName = "Eugene Katusiime", CustomerPhone = $"+{Phone}", PolicyNumber = "P1",
        ProductLine = "Hospital Cash", ClaimType = "Hospital Cash", Channel = "WhatsApp", IncidentDate = DateOnly.FromDateTime(NoonEat.UtcDateTime), IncidentDescription = "Test",
        Status = status, CreatedByUserId = "t", CreatedAt = NoonEat.AddDays(-createdDaysAgo), UpdatedAt = NoonEat, CorrelationId = "c",
        Priority = InsuranceIntelligence.Domain.Workflow.CasePriority.Normal, SlaDueAt = NoonEat.AddDays(5), RiskLevel = "Low", CompletenessScore = 0m,
    };

    private static async Task ApproveTemplateAsync(Rig rig, string name)
    {
        await rig.Templates.SubmitAsync(Bank, name, "en", "admin");
        await rig.Templates.RecordDecisionAsync(Bank, name, "en", TemplateStatus.Approved, null, "admin");
    }

    private static OutreachRule ClaimRule(string type, string template) =>
        new("", type, 0, type, new RuleConditions(), 0, [], template, "en", new RuleHours(0, 24), new RuleLimits(), null, null, RuleStatus.Draft, "w", DateTimeOffset.UnixEpoch);

    [Fact]
    public async Task AClaimWaitingForPapersIsChased_NamingThePapersFromTheChecklist_AndAgainEveryThreeDays()
    {
        var rig = await RigAsync();
        await ApproveTemplateAsync(rig, "claim_documents_needed");
        await LiveRuleAsync(rig, ClaimRule("claim_documents_needed", "claim_documents_needed"));
        await rig.Consents.SaveAsync(new InsuranceIntelligence.Domain.Compliance.ConsentRecord
        {
            RecordId = Guid.NewGuid(), Scope = Bank, CustomerIdentifier = Phone, Basis = InsuranceIntelligence.Domain.Compliance.ConsentBasis.CustomerInitiatedConversation,
            Purpose = InsuranceIntelligence.Domain.Compliance.ConsentPurposes.WhatsApp, Channel = "whatsapp", RecordedByUserId = "customer", RecordedAt = NoonEat.AddDays(-1), CorrelationId = "c",
        });
        await rig.Cases.SaveAsync(Claim(InsuranceIntelligence.Domain.Cases.CaseStatus.MissingDocuments));

        var first = await rig.Engine.RunAsync(Bank, "system");

        Assert.Equal(1, first.Sent);
        var request = Assert.Single(rig.Provider.Sent);
        Assert.Equal("claim_documents_needed", request.TemplateName);
        Assert.Equal("Eugene", request.TemplateParameters!["1"]);
        Assert.Equal("the police report, the medical form", request.TemplateParameters["3"]);
        Assert.Equal(0, (await rig.Engine.RunAsync(Bank, "system")).Sent);

        rig.Clock.Now = NoonEat.AddDays(3);
        Assert.Equal(1, (await rig.Engine.RunAsync(Bank, "system")).Sent);
        Assert.Equal(2, rig.Provider.Sent.Count);
        Assert.All(await rig.Log.ListAsync(Bank), decision => Assert.Equal("notice", decision.Kind));
    }

    [Fact]
    public async Task AClaimantWhoNeverAgreedAndNeverWroteIsNotMessaged()
    {
        var rig = await RigAsync();
        await ApproveTemplateAsync(rig, "claim_documents_needed");
        await LiveRuleAsync(rig, ClaimRule("claim_documents_needed", "claim_documents_needed"));
        await rig.Cases.SaveAsync(Claim(InsuranceIntelligence.Domain.Cases.CaseStatus.MissingDocuments));

        var run = await rig.Engine.RunAsync(Bank, "system");

        Assert.Equal(1, run.Skipped);
        Assert.Empty(rig.Provider.Sent);
        Assert.Contains("no agreement", (await rig.Log.ListAsync(Bank)).Single().Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AStatusUpdateSaysWhereTheClaimIsAndNeverAnOutcome()
    {
        var rig = await RigAsync();
        await ApproveTemplateAsync(rig, "claim_status_update");
        await LiveRuleAsync(rig, ClaimRule("claim_status_update", "claim_status_update"));
        await AddEugeneAsync(rig);
        await rig.Cases.SaveAsync(Claim(InsuranceIntelligence.Domain.Cases.CaseStatus.InReview, createdDaysAgo: 10));

        await rig.Engine.RunAsync(Bank, "system");

        var request = Assert.Single(rig.Provider.Sent);
        Assert.Equal("a claims officer is reviewing it", request.TemplateParameters!["3"]);
        Assert.Contains("CLM-", request.TemplateParameters["2"]);
        foreach (var status in Enum.GetValues<InsuranceIntelligence.Domain.Cases.CaseStatus>())
        {
            var words = CaseSweepService.StatusWords(status);
            Assert.True(words is null || !new[] { "approved", "rejected", "denied", "will be paid", "paid" }.Any(word => words.Contains(word, StringComparison.OrdinalIgnoreCase)), $"{status}: {words}");
        }
    }

    [Fact]
    public async Task ANewClaimIsAcknowledged_AndAClosedOneOnlyGetsItsStatus()
    {
        var rig = await RigAsync();
        await ApproveTemplateAsync(rig, "claim_received");
        await LiveRuleAsync(rig, ClaimRule("claim_received", "claim_received"));
        await AddEugeneAsync(rig);
        await rig.Cases.SaveAsync(Claim(InsuranceIntelligence.Domain.Cases.CaseStatus.New, createdDaysAgo: 0));

        var run = await rig.Engine.RunAsync(Bank, "system");

        Assert.Equal(1, run.Sent);
        Assert.Contains("we received your claim", (await rig.Log.GetReasonAsync(Bank, Phone))!.OpeningText);
    }

    // ---- The rehearsal: a demonstration customer and the story of why they were messaged

    private static InsuranceIntelligence.Application.DemoScenarios.DemoCustomerService Customers(Rig rig) => new(rig.Leads, rig.Events, rig.Brands, rig.Clock);

    private static InsuranceIntelligence.Application.DemoScenarios.DemoCustomerRequest EugeneRequest(
        InsuranceIntelligence.Application.DemoScenarios.DemoConsent consent = InsuranceIntelligence.Application.DemoScenarios.DemoConsent.Agreed, string phone = "+256 790 000 001") =>
        new("Eugene", phone, "Ntinda branch", ["Salary account", "Savings account", "Salary loan"], "About UGX 2.4m a month", "2 children", 8_000_000m, 36, 2, consent);

    [Fact]
    public async Task ADemonstrationCustomerCanOnlyBeAddedToADemonstrationWorkspace()
    {
        var real = await RigAsync(demonstration: false);

        var error = await Assert.ThrowsAsync<InsuranceIntelligence.Application.DemoScenarios.DemoCustomerException>(() => Customers(real).AddAsync(Bank, EugeneRequest(), "staff"));

        Assert.Contains("demonstration workspace", error.Message);
        Assert.Null(await real.Leads.FindByPhoneAsync(Bank.TenantId, Bank.WorkspaceId, Phone));
    }

    [Fact]
    public async Task EugeneIsAddedWithHisFactsHisConsentAndHisLoan_AndTheRuleOffersHimTheLoanProtector()
    {
        var rig = await RigAsync(approveTemplate: false, demonstration: true, connected: false);
        await LiveRuleAsync(rig);

        var added = await Customers(rig).AddAsync(Bank, EugeneRequest(), "staff");
        await Customers(rig).AddAsync(Bank, EugeneRequest(), "staff");
        var run = await rig.Engine.RunAsync(Bank, "system");

        Assert.True(added.EventRecorded);
        Assert.Equal(1, run.DemoSent);
        var lead = (await rig.Leads.FindByPhoneAsync(Bank.TenantId, Bank.WorkspaceId, Phone))!;
        Assert.Equal(ContactConsentStatus.Agreed, lead.Consent!.Status);
        Assert.Contains(lead.Facts, fact => fact.Key == "bank_products" && fact.Value.Contains("Salary loan"));
        var decision = Assert.Single(await rig.Log.ListAsync(Bank));
        Assert.Equal("Mpingo Loan Protector", decision.Cover);
        Assert.Single(await rig.Events.ListAsync(Bank));
    }

    [Theory]
    [InlineData(InsuranceIntelligence.Application.DemoScenarios.DemoConsent.NotAsked, "no recorded agreement")]
    [InlineData(InsuranceIntelligence.Application.DemoScenarios.DemoConsent.Declined, "asked not to be contacted")]
    public async Task AnEugeneWhoNeverAgreedIsSkipped_WhichIsWhatTheDemonstrationOfConsentShows(InsuranceIntelligence.Application.DemoScenarios.DemoConsent consent, string expected)
    {
        var rig = await RigAsync(approveTemplate: false, demonstration: true, connected: false);
        await LiveRuleAsync(rig);
        await Customers(rig).AddAsync(Bank, EugeneRequest(consent), "staff");

        await rig.Engine.RunAsync(Bank, "system");

        Assert.Contains(expected, (await rig.Log.ListAsync(Bank)).Single().Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(rig.Messages.Saved);
    }

    [Fact]
    public async Task ABadPhoneOrNameIsRefused()
    {
        var rig = await RigAsync(demonstration: true);

        await Assert.ThrowsAsync<InsuranceIntelligence.Application.DemoScenarios.DemoCustomerException>(() => Customers(rig).AddAsync(Bank, EugeneRequest(phone: "123"), "staff"));
        await Assert.ThrowsAsync<InsuranceIntelligence.Application.DemoScenarios.DemoCustomerException>(() => Customers(rig).AddAsync(Bank, EugeneRequest() with { Name = " " }, "staff"));
    }

    [Fact]
    public async Task TheTimelineTellsTheWholeStoryInOrder_FromTheAgreementToTheirAnswer()
    {
        var rig = await RigAsync(approveTemplate: false, demonstration: true, connected: false);
        await LiveRuleAsync(rig);
        await Customers(rig).AddAsync(Bank, EugeneRequest(), "staff");
        await rig.Engine.RunAsync(Bank, "system");
        await rig.Outcomes.RecordReplyAsync(Bank, Phone, "Yes, show me", NoonEat.AddMinutes(10));
        var timeline = new PersonTimelineService(rig.Events, rig.Log, rig.Outcomes, rig.Leads);

        var story = await timeline.GetAsync(Bank, Phone);

        Assert.Equal("Eugene", story.CustomerName);
        Assert.Contains("Agreed", story.ConsentSummary);
        Assert.Equal(["consent", "event", "decision", "message", "reply"], story.Entries.Select(entry => entry.Kind).ToArray());
        var decision = story.Entries.Single(entry => entry.Kind == "decision");
        Assert.Contains("shown as sent", decision.Title);
        Assert.Contains(decision.Gates!, gate => gate.Name == "Agreed to be contacted" && gate.Passed);
        Assert.Contains("UGX 8,000,000", story.Entries.Single(entry => entry.Kind == "event").Detail);
        Assert.Contains("Hello Eugene", story.Entries.Single(entry => entry.Kind == "message").Detail);
        Assert.Contains("Yes, show me", story.Entries.Single(entry => entry.Kind == "reply").Detail);
    }

    [Fact]
    public async Task OnePersonsTimelineNeverIncludesAnotherPersonOrAnotherWorkspace()
    {
        var rig = await RigAsync(approveTemplate: false, demonstration: true, connected: false);
        await LiveRuleAsync(rig);
        await Customers(rig).AddAsync(Bank, EugeneRequest(), "staff");
        await Customers(rig).AddAsync(Bank, EugeneRequest(phone: "+256 790 000 002") with { Name = "Someone Else" }, "staff");
        await rig.Engine.RunAsync(Bank, "system");
        var timeline = new PersonTimelineService(rig.Events, rig.Log, rig.Outcomes, rig.Leads);

        var mine = await timeline.GetAsync(Bank, Phone);
        var elsewhere = await timeline.GetAsync(Other, Phone);

        Assert.All(mine.Entries.Where(entry => entry.Kind == "decision"), entry => Assert.DoesNotContain("Someone Else", entry.Detail));
        Assert.Equal(1, mine.Entries.Count(entry => entry.Kind == "event"));
        Assert.Empty(elsewhere.Entries);
    }

    // ---- Conversions for the bank's staff

    private static ConversionReportService Report(Rig rig) => new(rig.Log, rig.Outcomes, rig.Leads, rig.Customers, rig.Policies, rig.Brands);

    [Fact]
    public async Task AnApplicationForTheOfferedInsurersCoverCountsAsAConversion_ByInsurer()
    {
        var rig = await OfferedAsync();
        await rig.Outcomes.RecordReplyAsync(Bank, Phone, "Yes, show me", NoonEat.AddMinutes(5));
        var lead = (await rig.Leads.FindByPhoneAsync(Bank.TenantId, Bank.WorkspaceId, Phone))!;
        await rig.Leads.CreateAsync(Bank.TenantId, Bank.WorkspaceId, lead with { AppliedFor = "Mpingo Loan Protector" });

        var report = await Report(rig).BuildAsync(Bank);

        var row = Assert.Single(report.Insurers);
        Assert.Equal("Mpingo Life Assurance", row.Insurer);
        Assert.Equal((1, 1, 1, 1), (row.Offers, row.Replied, row.AskedForDetails, row.ConvertedAfterOffer));
        Assert.Equal(1, report.Total.ConvertedAfterOffer);
        var person = Assert.Single(report.Converted);
        Assert.Equal("Eugene Katusiime", person.CustomerName);
        Assert.Equal("Applied for the cover", person.How);
        Assert.Equal(1, report.Applications);
    }

    [Fact]
    public async Task AnApplicationForADifferentInsurersCoverIsNotCountedAgainstTheOffer()
    {
        var rig = await OfferedAsync();
        var lead = (await rig.Leads.FindByPhoneAsync(Bank.TenantId, Bank.WorkspaceId, Phone))!;
        await rig.Leads.CreateAsync(Bank.TenantId, Bank.WorkspaceId, lead with { AppliedFor = "Kifaru Home Contents" });

        var report = await Report(rig).BuildAsync(Bank);

        Assert.Equal(0, report.Total.ConvertedAfterOffer);
        Assert.Empty(report.Converted);
        Assert.Equal(1, report.Applications);
    }

    [Fact]
    public async Task APolicyThatBeganAfterTheOfferCounts_AndOneThatBeganBeforeDoesNot()
    {
        var rig = await OfferedAsync();
        await rig.Customers.UpsertAsync(new InsuranceIntelligence.Domain.Customers.Customer("c1", Bank.TenantId, Bank.WorkspaceId, "Eugene Katusiime", null, $"+{Phone}"));
        var today = DateOnly.FromDateTime(NoonEat.UtcDateTime);
        await rig.Policies.UpsertAsync(new Policy("old", Bank.TenantId, Bank.WorkspaceId, "MLA/LP/2025/1", MicroinsuranceCatalog.LoanProtectorLine, null, "c1", null, today.AddDays(-100), today.AddDays(265)));

        Assert.Equal(0, (await Report(rig).BuildAsync(Bank)).Total.ConvertedAfterOffer);

        await rig.Policies.UpsertAsync(new Policy("new", Bank.TenantId, Bank.WorkspaceId, "MLA/LP/2026/2", MicroinsuranceCatalog.LoanProtectorLine, null, "c1", null, today, today.AddDays(364)));
        var report = await Report(rig).BuildAsync(Bank);

        Assert.Equal(1, report.Total.ConvertedAfterOffer);
        Assert.Equal("Holds a policy that began after the offer", Assert.Single(report.Converted).How);
    }

    [Fact]
    public async Task TheReportSaysPlainlyThatItHoldsNoCommissionRates_AndAnEmptyWorkspaceHasNothingToCount()
    {
        var rig = await RigAsync();

        var report = await Report(rig).BuildAsync(Bank);

        Assert.Empty(report.Insurers);
        Assert.Equal(0, report.Total.Offers);
        Assert.Contains("does not record rates", report.Note);
        Assert.DoesNotContain(typeof(InsurerConversion).GetProperties(), property => property.Name.Contains("Commission", StringComparison.OrdinalIgnoreCase));
    }

    // ---- Results by rule

    private static CampaignReportService Campaigns(Rig rig) => new(rig.Rules, rig.Log, rig.Outcomes, Report(rig), rig.Messages);

    [Fact]
    public async Task DeliveredAndReadComeFromWhatsAppsOwnReports_ForRealSendsOnly()
    {
        var rig = await OfferedAsync();
        var sent = Assert.Single(rig.Messages.Saved);

        var before = Assert.Single(await Campaigns(rig).BuildAsync(Bank));
        Assert.Equal((1, 1, 0, 0), (before.Considered, before.Sent, before.Delivered, before.Read));

        await rig.Messages.SaveAsync(sent with { DeliveryState = WhatsAppDeliveryState.Delivered });
        var delivered = Assert.Single(await Campaigns(rig).BuildAsync(Bank));
        Assert.Equal((1, 0), (delivered.Delivered, delivered.Read));

        await rig.Messages.SaveAsync(sent with { DeliveryState = WhatsAppDeliveryState.Read });
        var read = Assert.Single(await Campaigns(rig).BuildAsync(Bank));
        Assert.Equal((1, 1), (read.Delivered, read.Read));
    }

    [Fact]
    public async Task AMessageShownAsSentInADemonstrationIsNeverCountedAsDelivered()
    {
        var rig = await RigAsync(approveTemplate: false, demonstration: true, connected: false);
        await LiveRuleAsync(rig);
        await AddEugeneAsync(rig);
        await LoanEventAsync(rig);
        await rig.Engine.RunAsync(Bank, "system");

        var row = Assert.Single(await Campaigns(rig).BuildAsync(Bank));

        Assert.Equal((0, 1, 0, 0), (row.Sent, row.ShownAsSent, row.Delivered, row.Read));
    }

    [Fact]
    public async Task EveryoneNotMessagedIsAccountedFor_WithTheReasonCounted_AndAnswersAndConversionsLineUp()
    {
        var rig = await RigAsync();
        await LiveRuleAsync(rig);
        await AddEugeneAsync(rig);
        await AddEugeneAsync(rig, ContactConsentStatus.NotRecorded, "256790000002", "No Consent One");
        await AddEugeneAsync(rig, ContactConsentStatus.NotRecorded, "256790000003", "No Consent Two");
        await AddEugeneAsync(rig, ContactConsentStatus.Declined, "256790000004", "Said No");
        foreach (var phone in new[] { Phone, "256790000002", "256790000003", "256790000004" })
        {
            await LoanEventAsync(rig, phone: phone);
        }

        await rig.Engine.RunAsync(Bank, "system");
        await rig.Outcomes.RecordReplyAsync(Bank, Phone, "Yes, show me", NoonEat.AddMinutes(5));
        var lead = (await rig.Leads.FindByPhoneAsync(Bank.TenantId, Bank.WorkspaceId, Phone))!;
        await rig.Leads.CreateAsync(Bank.TenantId, Bank.WorkspaceId, lead with { AppliedFor = "Mpingo Loan Protector" });

        var row = Assert.Single(await Campaigns(rig).BuildAsync(Bank));

        Assert.Equal((4, 1, 3), (row.Considered, row.Sent, row.NotSent));
        Assert.Equal((1, 1, 1), (row.Answered, row.AskedForDetails, row.ConvertedAfterOffer));
        Assert.Equal(row.Considered, row.Sent + row.ShownAsSent + row.NotSent + row.Waiting + row.CouldNotSend);
        Assert.Contains(row.WhyNotSent, item => item.People == 2 && item.Reason.Contains("no recorded agreement", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(row.WhyNotSent, item => item.People == 1 && item.Reason.Contains("asked not to be contacted", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ARuleThatHasLookedAtNobodyIsNotListed()
    {
        var rig = await RigAsync();
        await LiveRuleAsync(rig);

        Assert.Empty(await Campaigns(rig).BuildAsync(Bank));
    }

    [Fact]
    public void TheBankRelationshipNamesKindsOfProductOnly()
    {
        var lead = new SalesLeadDetailReadModel("L", "s", "n", "c", "p", "q", [], [], null, null, "N", "+1", NoonEat, "t")
        {
            Facts = ProspectFacts.Merge([], new[] { new ProspectFact("bank_products", "Overdraft, savings account", ProspectFactSource.ImportedList, NoonEat, null, "t") }),
        };

        Assert.Equal("an overdraft and a savings account", RuleEngine.BankRelationship(lead));
        Assert.Equal("an account with us", RuleEngine.BankRelationship(lead with { Facts = [] }));
    }
}
