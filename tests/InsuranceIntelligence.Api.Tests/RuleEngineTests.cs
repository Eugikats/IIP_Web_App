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
        var sender = new TemplateMessageService(templates, channels, messages, provider);
        var rules = new RuleService(store, brands, templates, clock);
        var log = new DecisionLog(store);
        var events = new EventService(store, clock);
        var leads = new EfSalesLeadRepository(db);
        var customers = new InMemoryCustomerRepository();
        var policies = new InMemoryPolicyRepository();
        var outcomes = new OutcomeService(store, log, leads);
        return new Rig
        {
            Clock = clock, Db = db, Rules = rules, Log = log, Events = events, Templates = templates, Brands = brands, Leads = leads, Customers = customers, Policies = policies,
            Provider = provider, Messages = messages, Outcomes = outcomes,
            Engine = new RuleEngine(rules, log, events, brands, templates, sender, leads, customers, policies, messages, clock, outcomes),
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
        await Assert.ThrowsAsync<RuleException>(() => rig.Rules.SaveAsync(Bank, LoanRule() with { TemplateName = "renewal_30_days" }, "w"));
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
