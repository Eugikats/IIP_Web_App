using InsuranceIntelligence.Application.Templates;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Compliance;
using InsuranceIntelligence.Domain.Integrations;
using InsuranceIntelligence.Infrastructure.Persistence;
using InsuranceIntelligence.LocalTestDoubles;
using Microsoft.EntityFrameworkCore;

namespace InsuranceIntelligence.Api.Tests;

/// <summary>The template catalogue, and sending an approved template to someone who has not written in.</summary>
public sealed class TemplateCatalogTests
{
    private static readonly TenantScope Ours = new("org-a", "main");
    private static readonly TenantScope Theirs = new("org-b", "main");

    private readonly InsuranceIntelligenceDbContext db = new(
        new DbContextOptionsBuilder<InsuranceIntelligenceDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private TemplateCatalogService Catalogue() => new(new EfWorkspaceRecordStore(db));

    private static MessageTemplate Greeting(string name = "hello_world", string body = "Hello {{1}}, welcome to {{2}}.") =>
        new(name, "en", TemplateCategory.Utility, TemplateStatus.Draft, "Test", body, ["customer_name", "organisation"], ["Yes"], DateTimeOffset.UtcNow);

    [Fact]
    public async Task ANewWorkspaceStartsWithTheStandardDrafts_NoneApproved()
    {
        var all = await Catalogue().ListAsync(Ours);

        Assert.Equal(DefaultTemplates.All.Count, all.Count);
        Assert.Contains(all, template => template.Name == "bank_cover_intro" && template.Category == TemplateCategory.Marketing);
        Assert.All(all, template => Assert.Equal(TemplateStatus.Draft, template.Status));
        Assert.DoesNotContain(all, template => template.IsSendable);
    }

    [Fact]
    public void EveryStandardTemplateIsFitToSubmit()
    {
        foreach (var template in DefaultTemplates.All)
        {
            Assert.True(template.Problems().Count == 0, $"{template.Name}: {string.Join(" ", template.Problems())}");
            Assert.True(template.Body.Length <= 330, $"{template.Name} is longer than the plan's limit");
        }
    }

    [Fact]
    public async Task ATemplateGoesFromDraftToSubmittedToApproved_AndOnlyThenCanBeSent()
    {
        var catalogue = Catalogue();
        await catalogue.SaveDraftAsync(Ours, Greeting(), "admin-1");
        Assert.Null(await catalogue.FindSendableAsync(Ours, "hello_world", "en"));

        var submitted = await catalogue.SubmitAsync(Ours, "hello_world", "en", "admin-1");
        Assert.Equal(TemplateStatus.AwaitingApproval, submitted.Status);
        Assert.Null(await catalogue.FindSendableAsync(Ours, "hello_world", "en"));

        var approved = await catalogue.RecordDecisionAsync(Ours, "hello_world", "en", TemplateStatus.Approved, null, "admin-1");
        Assert.True(approved.IsSendable);
        Assert.NotNull(approved.DecidedAt);
        Assert.NotNull(await catalogue.FindSendableAsync(Ours, "hello_world", "en"));
    }

    [Fact]
    public async Task AnApprovedTemplateCannotBeEditedInPlace_AndASubmittedOneIsLocked()
    {
        var catalogue = Catalogue();
        await catalogue.SaveDraftAsync(Ours, Greeting(), "admin-1");
        await catalogue.SubmitAsync(Ours, "hello_world", "en", "admin-1");

        await Assert.ThrowsAsync<TemplateException>(() => catalogue.SaveDraftAsync(Ours, Greeting(body: "Hi {{1}}, welcome to {{2}}."), "admin-1"));

        await catalogue.RecordDecisionAsync(Ours, "hello_world", "en", TemplateStatus.Approved, null, "admin-1");
        var error = await Assert.ThrowsAsync<TemplateException>(() => catalogue.SaveDraftAsync(Ours, Greeting(body: "Hi {{1}}, welcome to {{2}}."), "admin-1"));
        Assert.Equal("conflict", error.Code);
    }

    [Fact]
    public async Task ARejectedTemplateNeedsAReason_AndCanBeEditedAndSubmittedAgain()
    {
        var catalogue = Catalogue();
        await catalogue.SaveDraftAsync(Ours, Greeting(), "admin-1");
        await catalogue.SubmitAsync(Ours, "hello_world", "en", "admin-1");

        await Assert.ThrowsAsync<TemplateException>(() => catalogue.RecordDecisionAsync(Ours, "hello_world", "en", TemplateStatus.Rejected, " ", "admin-1"));
        var rejected = await catalogue.RecordDecisionAsync(Ours, "hello_world", "en", TemplateStatus.Rejected, "Looks promotional", "admin-1");
        Assert.Equal("Looks promotional", rejected.DecisionNote);

        await catalogue.SaveDraftAsync(Ours, Greeting(body: "Hello {{1}}, thank you for banking with {{2}}."), "admin-1");
        Assert.Equal(TemplateStatus.AwaitingApproval, (await catalogue.SubmitAsync(Ours, "hello_world", "en", "admin-1")).Status);
    }

    [Fact]
    public async Task ADecisionCannotBeRecordedForADraftThatWasNeverSubmitted()
    {
        await Assert.ThrowsAsync<TemplateException>(() =>
            Catalogue().RecordDecisionAsync(Ours, "renewal_30_days", "en", TemplateStatus.Approved, null, "admin-1"));
    }

    [Fact]
    public async Task OneWorkspacesTemplatesAreInvisibleToAnother()
    {
        var catalogue = Catalogue();
        await catalogue.SaveDraftAsync(Ours, Greeting(), "admin-1");
        await catalogue.SubmitAsync(Ours, "hello_world", "en", "admin-1");
        await catalogue.RecordDecisionAsync(Ours, "hello_world", "en", TemplateStatus.Approved, null, "admin-1");

        Assert.Null(await catalogue.FindSendableAsync(Theirs, "hello_world", "en"));
        Assert.DoesNotContain(await catalogue.ListAsync(Theirs), template => template.Name == "hello_world");
    }

    [Fact]
    public async Task ASendableTemplateInTheCustomersLanguageIsPreferred_ElseEnglish()
    {
        var catalogue = Catalogue();
        await catalogue.ImportAsync(
            Ours,
            [
                Greeting() with { Status = TemplateStatus.Approved },
                Greeting() with { Language = "sw", Body = "Habari {{1}}, karibu {{2}}.", Status = TemplateStatus.Approved },
            ],
            "admin-1");

        Assert.Equal("sw", (await catalogue.FindSendableAsync(Ours, "hello_world", "sw"))!.Language);
        Assert.Equal("en", (await catalogue.FindSendableAsync(Ours, "hello_world", "lg"))!.Language);
    }

    [Theory]
    [InlineData("Hello {{2}}, welcome to {{3}}.")]
    [InlineData("{{1}} welcome to {{2}}")]
    [InlineData("Welcome to {{1}} and {{2}}.{{3}}")]
    public void BodiesThatWhatsAppWouldRefuseAreCaughtBeforeSubmitting(string body)
    {
        var parameters = new[] { "a", "b", "c" }.Take(body.Count(character => character == '{') / 2).ToArray();
        Assert.NotEmpty((Greeting(body: body) with { Parameters = parameters }).Problems());
    }

    [Fact]
    public void RenderingFillsEveryPlaceholderAndRefusesAGap()
    {
        var template = Greeting();

        Assert.Equal("Hello Eugene, welcome to Mavuno Bank.", template.Render(["Eugene", "Mavuno Bank"]));
        Assert.Equal("Hello Eugene, welcome to Mavuno Bank.", template.Render(["Eugene", "Mavuno\nBank"]));
        Assert.Throws<TemplateRenderException>(() => template.Render(["Eugene"]));
        Assert.Throws<TemplateRenderException>(() => template.Render(["Eugene", " "]));
    }

    [Fact]
    public async Task ImportKeepsTheStatusInTheFile_AndRefusesTemplatesThatAreNotFit()
    {
        var result = await Catalogue().ImportAsync(
            Ours,
            [Greeting() with { Status = TemplateStatus.Approved }, Greeting("bad name!"), Greeting("no_params") with { Parameters = [] }],
            "admin-1");

        Assert.Equal(1, result.Stored);
        Assert.Equal(2, result.Refused.Count);
        Assert.True((await Catalogue().FindSendableAsync(Ours, "hello_world", "en"))!.IsSendable);
    }

    // ---- Sending

    private sealed class RecordingProvider(bool configured = true, bool accepted = true) : IWhatsAppOutboundProvider
    {
        public bool IsConfigured => configured;
        public List<WhatsAppProviderSendRequest> Sent { get; } = [];

        public Task<WhatsAppProviderSendResult> SendAsync(WhatsAppProviderSendRequest request, CancellationToken cancellationToken = default)
        {
            Sent.Add(request);
            return Task.FromResult(new WhatsAppProviderSendResult(accepted, accepted ? "wamid.test" : null, accepted ? null : "131047", accepted ? null : "Not delivered"));
        }

        public Task<bool> ShowTypingAsync(string businessPhoneNumberId, string inboundProviderMessageId, CancellationToken cancellationToken = default) => Task.FromResult(true);

        public Task<WhatsAppProviderSendResult> SendVoiceNoteAsync(
            string businessPhoneNumberId, string recipientWaId, byte[] oggOpusAudio, string? replyToProviderMessageId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new WhatsAppProviderSendResult(true, "wamid.voice", null, null));
    }

    private sealed class MessageStore : IWhatsAppMessageRepository
    {
        public List<WhatsAppMessage> Saved { get; } = [];

        public Task SaveAsync(WhatsAppMessage message, CancellationToken cancellationToken = default)
        {
            Saved.RemoveAll(item => item.MessageId == message.MessageId);
            Saved.Add(message);
            return Task.CompletedTask;
        }

        public Task<WhatsAppMessage?> GetAsync(TenantScope scope, Guid messageId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Saved.SingleOrDefault(item => item.MessageId == messageId && item.Scope.Matches(scope)));

        public Task<WhatsAppMessage?> FindByProviderMessageIdAsync(TenantScope scope, string providerMessageId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Saved.SingleOrDefault(item => item.ProviderMessageId == providerMessageId && item.Scope.Matches(scope)));

        public Task<WhatsAppMessage?> FindByIdempotencyKeyAsync(TenantScope scope, string idempotencyKey, CancellationToken cancellationToken = default) =>
            Task.FromResult(Saved.SingleOrDefault(item => item.IdempotencyKey == idempotencyKey && item.Scope.Matches(scope)));

        public Task<IReadOnlyList<WhatsAppMessage>> ListThreadAsync(TenantScope scope, string threadId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<WhatsAppMessage>>(Saved.Where(item => item.ThreadId == threadId && item.Scope.Matches(scope)).ToArray());
    }

    private async Task<(TemplateMessageService Service, RecordingProvider Provider, MessageStore Messages)> SenderAsync(
        bool connected = true, bool configured = true, bool approve = true, string template = "welcome_and_consent")
    {
        var catalogue = Catalogue();
        if (approve && await catalogue.FindSendableAsync(Ours, template, "en") is null)
        {
            await catalogue.SubmitAsync(Ours, template, "en", "admin-1");
            await catalogue.RecordDecisionAsync(Ours, template, "en", TemplateStatus.Approved, null, "admin-1");
        }

        var channels = new InMemoryWhatsAppChannelConfigurationRepository();
        if (connected)
        {
            await channels.SaveAsync(new WhatsAppChannelConfiguration
            {
                Scope = Ours,
                PhoneNumberId = "1099",
                EncryptedAccessToken = "x",
                EncryptedAppSecret = "x",
                EncryptedVerifyToken = "x",
                ConfiguredByUserId = "admin-1",
                ConfiguredAt = DateTimeOffset.UtcNow,
            });
        }

        var provider = new RecordingProvider(configured);
        var messages = new MessageStore();
        return (new TemplateMessageService(catalogue, channels, messages, provider), provider, messages);
    }

    private static TemplateSendRequest Request(string key = "k1", string name = "welcome_and_consent", string[]? values = null) =>
        new(name, "en", "+256 790 000 001", values ?? ["Eugene", "Mavuno Bank"], "bank_first_touch", "Agreed to bank insurance marketing on 2026-10-01", key, "rule-1");

    [Fact]
    public async Task AnApprovedTemplateGoesToSomeoneWhoHasNotWrittenIn_WithItsValuesInOrder()
    {
        var (service, provider, messages) = await SenderAsync();

        var result = await service.SendAsync(Ours, Request());

        Assert.True(result.Sent, result.Reason);
        Assert.Equal("Hello Eugene, this is Kato from Mavuno Bank. May we message you here about your insurance?", result.Text);
        var sent = Assert.Single(provider.Sent);
        Assert.Equal("welcome_and_consent", sent.TemplateName);
        Assert.Equal("1099", sent.BusinessPhoneNumberId);
        Assert.Equal("256790000001", sent.RecipientWaId);
        Assert.Equal(["1", "2"], sent.TemplateParameters!.Keys.ToArray());
        Assert.Equal("Eugene", sent.TemplateParameters["1"]);
        var saved = Assert.Single(messages.Saved);
        Assert.Equal(WhatsAppContentType.Template, saved.ContentType);
        Assert.Equal(WhatsAppDeliveryState.ProviderAccepted, saved.DeliveryState);
    }

    [Fact]
    public async Task ADraftTemplateIsNeverSent_AndTheReasonSaysWhy()
    {
        var (service, provider, _) = await SenderAsync(approve: false);

        var result = await service.SendAsync(Ours, Request());

        Assert.False(result.Sent);
        Assert.Contains("not been approved", result.Reason);
        Assert.Contains("still a draft", result.Reason);
        Assert.Empty(provider.Sent);
    }

    [Fact]
    public async Task ARetryWithTheSameKeyNeverSendsTwice()
    {
        var (service, provider, messages) = await SenderAsync();

        var first = await service.SendAsync(Ours, Request("same"));
        var second = await service.SendAsync(Ours, Request("same"));

        Assert.True(first.Sent);
        Assert.True(second.Sent);
        Assert.Equal(first.MessageId, second.MessageId);
        Assert.Single(provider.Sent);
        Assert.Single(messages.Saved);
    }

    [Fact]
    public async Task NothingIsSentWithoutAConnectedNumber_OrAConfiguredProvider()
    {
        var (notConnected, provider1, _) = await SenderAsync(connected: false);
        var (notConfigured, provider2, _) = await SenderAsync(configured: false);

        Assert.Equal("WhatsApp is not connected for this workspace.", (await notConnected.SendAsync(Ours, Request())).Reason);
        Assert.Equal("WhatsApp outbound is not configured.", (await notConfigured.SendAsync(Ours, Request())).Reason);
        Assert.Empty(provider1.Sent);
        Assert.Empty(provider2.Sent);
    }

    [Fact]
    public async Task AValueThatIsMissingBlocksTheSendInsteadOfLeavingAHole()
    {
        var (service, provider, _) = await SenderAsync();

        var result = await service.SendAsync(Ours, Request(values: ["Eugene"]));

        Assert.False(result.Sent);
        Assert.Contains("needs 2 value", result.Reason);
        Assert.Empty(provider.Sent);
    }

    [Fact]
    public async Task ARecordedWithdrawalOfConsentStopsTheSend()
    {
        var catalogue = Catalogue();
        await catalogue.SubmitAsync(Ours, "welcome_and_consent", "en", "admin-1");
        await catalogue.RecordDecisionAsync(Ours, "welcome_and_consent", "en", TemplateStatus.Approved, null, "admin-1");
        var channels = new InMemoryWhatsAppChannelConfigurationRepository();
        await channels.SaveAsync(new WhatsAppChannelConfiguration
        {
            Scope = Ours, PhoneNumberId = "1099", EncryptedAccessToken = "x", EncryptedAppSecret = "x", EncryptedVerifyToken = "x",
            ConfiguredByUserId = "admin-1", ConfiguredAt = DateTimeOffset.UtcNow,
        });
        var provider = new RecordingProvider();
        var service = new TemplateMessageService(catalogue, channels, new MessageStore(), provider, new WithdrawnConsent());

        var result = await service.SendAsync(Ours, Request());

        Assert.False(result.Sent);
        Assert.Contains("withdrew consent", result.Reason);
        Assert.Empty(provider.Sent);
    }

    [Fact]
    public async Task AFailureFromWhatsAppIsReportedAsBlocked_NotAsSent()
    {
        var catalogue = Catalogue();
        await catalogue.SubmitAsync(Ours, "welcome_and_consent", "en", "admin-1");
        await catalogue.RecordDecisionAsync(Ours, "welcome_and_consent", "en", TemplateStatus.Approved, null, "admin-1");
        var channels = new InMemoryWhatsAppChannelConfigurationRepository();
        await channels.SaveAsync(new WhatsAppChannelConfiguration
        {
            Scope = Ours, PhoneNumberId = "1099", EncryptedAccessToken = "x", EncryptedAppSecret = "x", EncryptedVerifyToken = "x",
            ConfiguredByUserId = "admin-1", ConfiguredAt = DateTimeOffset.UtcNow,
        });
        var messages = new MessageStore();
        var service = new TemplateMessageService(catalogue, channels, messages, new RecordingProvider(accepted: false));

        var result = await service.SendAsync(Ours, Request());

        Assert.False(result.Sent);
        Assert.Equal("Not delivered", result.Reason);
        Assert.Equal(WhatsAppDeliveryState.Failed, Assert.Single(messages.Saved).DeliveryState);
    }

    private sealed class WithdrawnConsent : IConsentRepository
    {
        public Task SaveAsync(ConsentRecord record, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<ConsentRecord?> GetCurrentAsync(TenantScope scope, string customerIdentifier, string purpose, CancellationToken cancellationToken = default) =>
            Task.FromResult<ConsentRecord?>(new ConsentRecord
            {
                RecordId = Guid.NewGuid(), Scope = scope, CustomerIdentifier = customerIdentifier, Purpose = purpose, Basis = ConsentBasis.Withdrawn,
                Channel = "whatsapp", RecordedAt = DateTimeOffset.UtcNow, WithdrawnAt = DateTimeOffset.UtcNow, RecordedByUserId = "customer", CorrelationId = "c",
            });

        public Task<IReadOnlyCollection<ConsentRecord>> ListForCustomerAsync(TenantScope scope, string customerIdentifier, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<ConsentRecord>>([]);
    }
}
