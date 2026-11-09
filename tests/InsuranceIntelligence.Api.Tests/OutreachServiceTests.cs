using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Application.CustomerAssistant;
using InsuranceIntelligence.Application.SalesLeads;
using InsuranceIntelligence.Application.WhatsApp;
using InsuranceIntelligence.Domain.Acquisition;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Integrations;

namespace InsuranceIntelligence.Api.Tests;

public sealed class OutreachServiceTests
{
    private static SalesLeadDetailReadModel Lead(ContactConsentStatus consent, string phone = "256790000002") =>
        new("L1", "Bank customer list", "need", "summary", "To be assessed", "New", [], [], null, null, CustomerName: "Brian Okello", ContactInfo: "+" + phone, CreatedAt: DateTimeOffset.UtcNow)
        {
            Phone = phone,
            Consent = new ContactConsent(consent, DateTimeOffset.UtcNow, "wording", "Bank list"),
            Triage = new ProspectTriage(
                DateTimeOffset.UtcNow, 80, ReadinessBand.HighPriority, "High",
                [new ProductFit(MicroinsuranceCatalog.BodaAccident.Name, "Boda Rider Accident", "Nile Shield Assurance", 90, ["Works as boda boda rider, which this cover is for (boda)"])],
                [], []),
        };

    private static (OutreachService Service, Store Store, Messages Messages) Build(SalesLeadDetailReadModel lead, bool allowDemo = true)
    {
        var brand = new KatoBrand("Mavuno Bank", "Mavuno", [MicroinsuranceCatalog.NileShield, MicroinsuranceCatalog.Mpingo]);
        var store = new Store(lead);
        var messages = new Messages();
        var service = new OutreachService(new Ctx(), new Allow(), store, store, new NullAudit(), new NoSend(), messages, new OutreachOptions(allowDemo), brand: brand);
        return (service, store, messages);
    }

    private static OutreachDraft Draft() =>
        new(Guid.NewGuid(), OutreachKind.Introduction, "Hello Brian, original text.", MicroinsuranceCatalog.BodaAccident.Name, "Nile Shield Assurance", OutreachStatus.Draft, DateTimeOffset.UtcNow, "Outreach drafting agent");

    [Fact]
    public async Task Drafting_writes_one_introduction_for_a_person_who_has_not_declined_and_not_twice()
    {
        var (service, store, _) = Build(Lead(ContactConsentStatus.Agreed));

        var first = await service.DraftAsync(null);
        var second = await service.DraftAsync(null);

        Assert.Equal(1, first.Drafted);
        Assert.Equal(0, second.Drafted);
        var draft = Assert.Single(store.Lead.Outreach);
        Assert.Equal(OutreachStatus.Draft, draft.Status);
        Assert.Contains("Hello Brian", draft.Text);
        Assert.Contains("Nile Shield Assurance", draft.Text);
    }

    [Theory]
    [InlineData(ContactConsentStatus.Declined)]
    [InlineData(ContactConsentStatus.Withdrawn)]
    public async Task Nobody_who_asked_not_to_be_contacted_gets_a_draft(ContactConsentStatus consent)
    {
        var (service, store, _) = Build(Lead(consent));

        Assert.Equal(0, (await service.DraftAsync(null)).Drafted);
        Assert.Empty(store.Lead.Outreach);
    }

    [Fact]
    public async Task Sending_without_a_recorded_agreement_is_blocked_with_the_reason()
    {
        var (service, store, messages) = Build(Lead(ContactConsentStatus.NotRecorded));
        store.Lead = store.Lead with { Outreach = [Draft()] };

        var result = await service.SendAsync("L1", store.Lead.Outreach[0].DraftId);

        Assert.Equal("Blocked", result.Status);
        Assert.Contains("no recorded agreement", result.Reason!, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(messages.Saved);
    }

    [Fact]
    public async Task A_demo_contact_with_no_open_window_is_a_labelled_demo_send_never_a_real_one()
    {
        var (service, store, messages) = Build(Lead(ContactConsentStatus.Agreed));
        store.Lead = store.Lead with { Outreach = [Draft()] };

        var result = await service.SendAsync("L1", store.Lead.Outreach[0].DraftId);

        Assert.Equal("DemoSent", result.Status);
        Assert.Contains("not delivered to a phone", result.StatusLabel);
        Assert.Equal(OutreachService.DemoNumberId, Assert.Single(messages.Saved).BusinessPhoneNumberId);
    }

    [Fact]
    public async Task In_a_protected_runtime_the_same_send_is_blocked_not_faked()
    {
        var (service, store, messages) = Build(Lead(ContactConsentStatus.Agreed), allowDemo: false);
        store.Lead = store.Lead with { Outreach = [Draft()] };

        var result = await service.SendAsync("L1", store.Lead.Outreach[0].DraftId);

        Assert.Equal("Blocked", result.Status);
        Assert.Contains("template", result.Reason!, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(messages.Saved);
    }

    [Fact]
    public async Task Editing_changes_the_wording_and_the_edited_wording_is_what_is_sent()
    {
        var (service, store, messages) = Build(Lead(ContactConsentStatus.Agreed));
        store.Lead = store.Lead with { Outreach = [Draft()] };
        var id = store.Lead.Outreach[0].DraftId;

        await service.EditAsync("L1", id, "Hello Brian, a shorter note.");
        await service.SendAsync("L1", id);

        Assert.Equal("Hello Brian, a shorter note.", Assert.Single(messages.Saved).CustomerSafeBody);
    }

    [Fact]
    public async Task A_sent_message_cannot_be_edited_or_sent_again()
    {
        var (service, store, messages) = Build(Lead(ContactConsentStatus.Agreed));
        store.Lead = store.Lead with { Outreach = [Draft()] };
        var id = store.Lead.Outreach[0].DraftId;

        await service.SendAsync("L1", id);
        await service.SendAsync("L1", id);

        Assert.Null(await service.EditAsync("L1", id, "changed"));
        Assert.Single(messages.Saved);
    }

    [Fact]
    public async Task Unknown_provider_outcome_is_not_reported_as_sent_or_retried()
    {
        var lead = Lead(ContactConsentStatus.Agreed) with
        {
            ThreadId = "thread-1",
            Interests = [new ProspectInterest(MicroinsuranceCatalog.BodaAccident.Name, InterestStrength.Interested, "asked", DateTimeOffset.UtcNow.AddDays(-5))],
            LastInteractionAt = DateTimeOffset.UtcNow.AddDays(-5),
        };
        var store = new Store(lead with { Outreach = [Draft()] });
        var messages = new Messages();
        messages.ThreadMessages.Add(new WhatsAppMessage
        {
            MessageId = Guid.NewGuid(), Scope = new TenantScope("tenant-demo", "workspace-operations"),
            ThreadId = "thread-1", BusinessPhoneNumberId = "business-number", ContactWaId = lead.Phone!,
            Direction = WhatsAppMessageDirection.Inbound, ContentType = WhatsAppContentType.Text,
            CustomerSafeBody = "Hello", DeliveryState = WhatsAppDeliveryState.Received,
            CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow, CorrelationId = "test",
        });
        var sender = new RespondingSend(WhatsAppDeliveryState.Unknown);
        var service = new OutreachService(new Ctx(), new Allow(), store, store, new NullAudit(), sender, messages,
            new OutreachOptions(AllowDemoDelivery: false), brand: new KatoBrand("Mavuno Bank", "Mavuno", [MicroinsuranceCatalog.NileShield]));
        var draftId = store.Lead.Outreach[0].DraftId;

        var first = await service.SendAsync("L1", draftId);
        var retry = await service.SendAsync("L1", draftId);

        Assert.Equal("Unknown", first.Status);
        Assert.Contains("reconciliation", first.StatusLabel, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Unknown", retry.Status);
        Assert.Equal(1, sender.Calls);

        var outboundId = Guid.Parse(store.Lead.Outreach[0].SentMessageId!);
        messages.ThreadMessages.Add(new WhatsAppMessage
        {
            MessageId = outboundId, Scope = new TenantScope("tenant-demo", "workspace-operations"),
            ThreadId = "thread-1", BusinessPhoneNumberId = "business-number", ContactWaId = lead.Phone!,
            Direction = WhatsAppMessageDirection.Outbound, ContentType = WhatsAppContentType.Text,
            CustomerSafeBody = "Hello Brian", DeliveryState = WhatsAppDeliveryState.Delivered,
            CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow, CorrelationId = "test",
        });
        var reconciled = await service.SendAsync("L1", draftId);

        Assert.Equal("Delivered", reconciled.Status);
        Assert.Equal(1, sender.Calls);
        await service.DraftFollowUpsAsync();
        Assert.Single(store.Lead.Outreach);
    }

    [Fact]
    public async Task Bulk_sending_judges_each_message_on_its_own()
    {
        var (service, store, _) = Build(Lead(ContactConsentStatus.Agreed));
        store.Lead = store.Lead with { Outreach = [Draft()] };

        var results = await service.SendManyAsync([new OutreachRef("L1", store.Lead.Outreach[0].DraftId), new OutreachRef("missing", Guid.NewGuid())]);

        Assert.Equal(["DemoSent", "NotFound"], results.Select(result => result.Status));
    }

    private static SalesLeadDetailReadModel Interested(int daysQuiet, ContactConsentStatus consent = ContactConsentStatus.Agreed) =>
        Lead(consent) with
        {
            LastInteractionAt = DateTimeOffset.UtcNow.AddDays(-daysQuiet),
            Interests = [new ProspectInterest(MicroinsuranceCatalog.BodaAccident.Name, InterestStrength.Interested, "asked", DateTimeOffset.UtcNow.AddDays(-daysQuiet))],
        };

    [Fact]
    public async Task Someone_interested_who_has_gone_quiet_gets_a_follow_up_draft_naming_the_cover()
    {
        var (service, store, _) = Build(Interested(daysQuiet: 5));

        var result = await service.DraftFollowUpsAsync();

        Assert.Equal(1, result.Quiet);
        var draft = Assert.Single(store.Lead.Outreach);
        Assert.Equal(OutreachKind.QuietFollowUp, draft.Kind);
        Assert.Contains("Boda Rider Accident", draft.Text);
        Assert.EndsWith(OutreachDrafting.OptOut, draft.Text);
    }

    [Fact]
    public async Task Someone_who_wrote_recently_is_not_chased()
    {
        var (service, store, _) = Build(Interested(daysQuiet: 1));

        Assert.Equal(0, (await service.DraftFollowUpsAsync()).Quiet);
        Assert.Empty(store.Lead.Outreach);
    }

    [Fact]
    public async Task Nobody_without_a_recorded_agreement_gets_a_follow_up()
    {
        var (service, store, _) = Build(Interested(daysQuiet: 9, ContactConsentStatus.NotRecorded));

        Assert.Equal(0, (await service.DraftFollowUpsAsync()).Quiet);
        Assert.Empty(store.Lead.Outreach);
    }

    [Fact]
    public async Task Running_the_follow_up_agent_twice_does_not_stack_drafts()
    {
        var (service, store, _) = Build(Interested(daysQuiet: 5));

        await service.DraftFollowUpsAsync();
        await service.DraftFollowUpsAsync();

        Assert.Single(store.Lead.Outreach);
    }

    [Fact]
    public void Sending_hours_are_seven_to_twenty_in_east_africa()
    {
        Assert.True(OutreachService.WithinSendingHoursForTests(new DateTimeOffset(2026, 10, 7, 6, 0, 0, TimeSpan.Zero)));
        Assert.False(OutreachService.WithinSendingHoursForTests(new DateTimeOffset(2026, 10, 7, 18, 0, 0, TimeSpan.Zero)));
        Assert.False(OutreachService.WithinSendingHoursForTests(new DateTimeOffset(2026, 10, 7, 3, 0, 0, TimeSpan.Zero)));
    }

    private sealed class Ctx : ITenantContextAccessor
    {
        public TenantContext Current => new("tenant-demo", "workspace-operations", "user-1", ["Admin"]);
        public bool HasCurrent => true;
    }

    private sealed class Allow : IPermissionService
    {
        public Task RequirePermissionAsync(Permission permission, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<bool> HasPermissionAsync(Permission permission, CancellationToken cancellationToken = default) => Task.FromResult(true);
    }

    private sealed class NullAudit : IAuditWriter
    {
        public Task WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class NoSend : IWhatsAppMessagingService
    {
        public Task<IReadOnlyList<WhatsAppMessageDto>> GetThreadAsync(string threadId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<WhatsAppMessageDto> SendAsync(SendWhatsAppMessageCommand command, CancellationToken cancellationToken = default) => throw new NotSupportedException("No real send should be attempted.");
        public Task<IReadOnlyList<WhatsAppInboundMessage>> IngestInboundAsync(TenantScope scope, IReadOnlyList<WhatsAppInboundMessage> messages, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<int> ApplyDeliveryEventsAsync(TenantScope scope, IReadOnlyList<WhatsAppDeliveryEvent> events, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class RespondingSend(WhatsAppDeliveryState state) : IWhatsAppMessagingService
    {
        public int Calls { get; private set; }
        public Task<IReadOnlyList<WhatsAppMessageDto>> GetThreadAsync(string threadId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<WhatsAppMessageDto> SendAsync(SendWhatsAppMessageCommand command, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(new WhatsAppMessageDto(Guid.NewGuid(), null, command.ThreadId, "business-number", command.RecipientWaId,
                WhatsAppMessageDirection.Outbound, WhatsAppContentType.Text, command.CustomerSafeBody, null, null, null, state,
                DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, "Provider acknowledgement timed out; reconcile before retrying."));
        }
        public Task<IReadOnlyList<WhatsAppInboundMessage>> IngestInboundAsync(TenantScope scope, IReadOnlyList<WhatsAppInboundMessage> messages, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<int> ApplyDeliveryEventsAsync(TenantScope scope, IReadOnlyList<WhatsAppDeliveryEvent> events, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class Messages : IWhatsAppMessageRepository
    {
        public List<WhatsAppMessage> Saved { get; } = [];
        public List<WhatsAppMessage> ThreadMessages { get; } = [];

        public Task SaveAsync(WhatsAppMessage message, CancellationToken cancellationToken = default)
        {
            Saved.Add(message);
            return Task.CompletedTask;
        }

        public Task<WhatsAppMessage?> GetAsync(TenantScope scope, Guid messageId, CancellationToken cancellationToken = default) => Task.FromResult<WhatsAppMessage?>(Saved.Concat(ThreadMessages).FirstOrDefault(message => message.MessageId == messageId && message.Scope == scope));
        public Task<WhatsAppMessage?> FindByProviderMessageIdAsync(TenantScope scope, string providerMessageId, CancellationToken cancellationToken = default) => Task.FromResult<WhatsAppMessage?>(null);
        public Task<WhatsAppMessage?> FindByIdempotencyKeyAsync(TenantScope scope, string idempotencyKey, CancellationToken cancellationToken = default) => Task.FromResult<WhatsAppMessage?>(null);
        public Task<IReadOnlyList<WhatsAppMessage>> ListThreadAsync(TenantScope scope, string threadId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<WhatsAppMessage>>(ThreadMessages.Where(message => message.ThreadId == threadId).ToArray());
    }

    private sealed class Store(SalesLeadDetailReadModel lead) : ISalesLeadReadRepository, ISalesLeadWriteRepository
    {
        public SalesLeadDetailReadModel Lead { get; set; } = lead;

        public Task<SalesLeadDetailReadModel?> GetLeadDetailAsync(string tenantId, string workspaceId, string leadId, CancellationToken cancellationToken = default) =>
            Task.FromResult<SalesLeadDetailReadModel?>(Lead.LeadId == leadId ? Lead : null);

        public Task<IReadOnlyList<SalesLeadDetailReadModel>> ListLeadsAsync(string tenantId, string workspaceId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<SalesLeadDetailReadModel>>([Lead]);

        public Task CreateAsync(string tenantId, string workspaceId, SalesLeadDetailReadModel lead, CancellationToken cancellationToken = default)
        {
            Lead = lead;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(string tenantId, string workspaceId, string leadId, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<SalesLeadDetailReadModel?> FindByPhoneAsync(string tenantId, string workspaceId, string phone, CancellationToken cancellationToken = default) =>
            Task.FromResult<SalesLeadDetailReadModel?>(Lead);
    }
}
