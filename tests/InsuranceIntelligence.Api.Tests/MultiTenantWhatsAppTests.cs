using System.Net;
using System.Security.Cryptography;
using System.Text;
using InsuranceIntelligence.Api.Integrations;
using InsuranceIntelligence.Application.WhatsApp;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Documents;
using InsuranceIntelligence.Domain.Integrations;
using InsuranceIntelligence.Infrastructure.Configuration;
using InsuranceIntelligence.Infrastructure.WhatsApp;
using InsuranceIntelligence.LocalTestDoubles;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace InsuranceIntelligence.Api.Tests;

/// <summary>SaaS mode: several insurers share one webhook, each with its own WhatsApp number,
/// credentials and app secret. Nothing may cross between them.</summary>
public sealed class MultiTenantWhatsAppTests
{
    private static readonly TenantScope Acme = TenantScope.Create("tenant-acme", "workspace-main");
    private static readonly TenantScope Baraka = TenantScope.Create("tenant-baraka", "workspace-main");

    private const string WebhookTemplate = """
        {"object":"whatsapp_business_account","entry":[{"id":"WABA","changes":[{"value":{
          "messaging_product":"whatsapp",
          "metadata":{"display_phone_number":"256700000000","phone_number_id":"__PHONE__"},
          "messages":[{"from":"__FROM__","id":"__ID__","timestamp":"1700000000","type":"text","text":{"body":"Hello"}}]
        },"field":"messages"}]}]}
        """;

    private static string Webhook(string phoneNumberId, string from = "256772118001") =>
        WebhookTemplate.Replace("__PHONE__", phoneNumberId).Replace("__FROM__", from).Replace("__ID__", $"wamid.{Guid.NewGuid():N}");

    private static async Task<(WhatsAppRuntimeSettingsProvider Provider, InMemoryWhatsAppChannelConfigurationRepository Repository)> TwoInsurersAsync()
    {
        var repository = new InMemoryWhatsAppChannelConfigurationRepository();
        var protector = new FakeWhatsAppCredentialProtector();
        await repository.SaveAsync(Connection(Acme, "phone-acme", "token-acme", "secret-acme", "verify-acme", DateTimeOffset.UtcNow.AddDays(-2), protector), CancellationToken.None);
        await repository.SaveAsync(Connection(Baraka, "phone-baraka", "token-baraka", "secret-baraka", "verify-baraka", DateTimeOffset.UtcNow.AddDays(-1), protector), CancellationToken.None);

        var services = new ServiceCollection()
            .AddSingleton<IWhatsAppChannelConfigurationRepository>(repository)
            .AddSingleton<IWhatsAppCredentialProtector>(protector)
            .BuildServiceProvider();
        var provider = new WhatsAppRuntimeSettingsProvider(
            services.GetRequiredService<IServiceScopeFactory>(),
            new InfrastructureOptions { MultiTenant = true, ChannelProvider = "Disabled" });
        return (provider, repository);
    }

    private static WhatsAppChannelConfiguration Connection(
        TenantScope scope, string phone, string token, string secret, string verify, DateTimeOffset at, IWhatsAppCredentialProtector protector) => new()
    {
        Scope = scope,
        PhoneNumberId = phone,
        EncryptedAccessToken = protector.Protect(token),
        EncryptedAppSecret = protector.Protect(secret),
        EncryptedVerifyToken = protector.Protect(verify),
        ConfiguredByUserId = "admin",
        ConfiguredAt = at,
    };

    private static string Sign(string secret, string body) =>
        "sha256=" + Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(body))).ToLowerInvariant();

    [Fact]
    public async Task EachNumberResolvesToItsOwnInsurer_AndThereIsNoDeploymentWideDefault()
    {
        var (provider, _) = await TwoInsurersAsync();

        Assert.Null(provider.Current); // SaaS mode: nobody gets "the" connection
        Assert.Equal(2, provider.All.Count);
        Assert.Equal(Acme.TenantId, provider.ForPhoneNumberId("phone-acme")!.TenantId);
        Assert.Equal(Baraka.TenantId, provider.ForPhoneNumberId("phone-baraka")!.TenantId);
        Assert.Null(provider.ForPhoneNumberId("phone-unknown"));
        Assert.Equal("phone-baraka", provider.ForTenant(Baraka)!.PhoneNumberId);
        Assert.Null(provider.ForTenant(TenantScope.Create("tenant-nobody", "workspace-main")));
    }

    [Fact]
    public async Task ANumberAlreadyHeldByAnInsurer_CannotBeTakenOverByALaterConnection()
    {
        var (provider, repository) = await TwoInsurersAsync();
        var protector = new FakeWhatsAppCredentialProtector();
        var thief = TenantScope.Create("tenant-thief", "workspace-main");
        await repository.SaveAsync(Connection(thief, "phone-acme", "stolen", "stolen-secret", "stolen-verify", DateTimeOffset.UtcNow, protector), CancellationToken.None);

        await provider.RefreshAsync();

        Assert.Equal(Acme.TenantId, provider.ForPhoneNumberId("phone-acme")!.TenantId);
        Assert.Equal("token-acme", provider.ForPhoneNumberId("phone-acme")!.AccessToken);
    }

    [Fact]
    public async Task ASignatureMadeWithAnotherInsurersSecret_IsRejectedForThisInsurersNumber()
    {
        var (provider, _) = await TwoInsurersAsync();
        var inbound = new WhatsAppCloudApiInboundProvider(provider, NullLogger<WhatsAppCloudApiInboundProvider>.Instance);
        var body = Webhook("phone-acme");
        var bytes = Encoding.UTF8.GetBytes(body);

        Assert.True(inbound.IsValidSignatureFor("phone-acme", bytes, Sign("secret-acme", body)));
        Assert.False(inbound.IsValidSignatureFor("phone-acme", bytes, Sign("secret-baraka", body)));
        Assert.False(inbound.IsValidSignatureFor("phone-unknown", bytes, Sign("secret-acme", body)));
        Assert.False(inbound.IsValidSignature(bytes, Sign("secret-acme", body))); // no default connection to fall back on
    }

    [Fact]
    public async Task TheOneWebhookConfirmsAnyInsurersVerifyToken_ButNoOthers()
    {
        var (provider, _) = await TwoInsurersAsync();
        var inbound = new WhatsAppCloudApiInboundProvider(provider, NullLogger<WhatsAppCloudApiInboundProvider>.Instance);

        Assert.True(inbound.TryVerifySubscription("subscribe", "verify-acme", "challenge-1", out var first));
        Assert.Equal("challenge-1", first);
        Assert.True(inbound.TryVerifySubscription("subscribe", "verify-baraka", "challenge-2", out _));
        Assert.False(inbound.TryVerifySubscription("subscribe", "guess", "challenge-3", out _));
    }

    [Fact]
    public async Task RepliesLeaveThroughTheNumberAndTokenOfTheInsurerThatOwnsThem()
    {
        var (provider, _) = await TwoInsurersAsync();
        var handler = new CapturingHandler();
        var outbound = new WhatsAppCloudApiOutboundProvider(provider, new HttpClient(handler));

        var fromBaraka = await outbound.SendAsync(Send("phone-baraka"));
        var fromUnknown = await outbound.SendAsync(Send("phone-unknown"));

        Assert.True(fromBaraka.Accepted);
        Assert.Equal("Bearer token-baraka", handler.Authorization.Single());
        Assert.Contains("/phone-baraka/messages", handler.Urls.Single());
        Assert.False(fromUnknown.Accepted); // never sent from a number nobody here owns
        Assert.Single(handler.Urls);
    }

    [Fact]
    public async Task APayloadSignedForOneInsurerButAboutAnothersNumber_IsRefused_AndOneForAnUnknownNumberIsIgnored()
    {
        var (provider, _) = await TwoInsurersAsync();
        var options = new InfrastructureOptions { MultiTenant = true, ChannelProvider = "Disabled" };
        var inbound = new WhatsAppCloudApiInboundProvider(provider, NullLogger<WhatsAppCloudApiInboundProvider>.Instance);
        var service = new WhatsAppInboundDocumentIngestionService(
            options, provider, inbound, new NeverMatches(), new NoAttachments(),
            new InMemoryCaseRepository(), new InMemoryCaseDocumentRepository(), new InMemoryAuditRepository(),
            NullLogger<WhatsAppInboundDocumentIngestionService>.Instance);

        var aboutAcme = Webhook("phone-acme");
        var forged = await service.IngestWebhookPayloadAsync(Encoding.UTF8.GetBytes(aboutAcme), Sign("secret-baraka", aboutAcme));
        var genuine = await service.IngestWebhookPayloadAsync(Encoding.UTF8.GetBytes(aboutAcme), Sign("secret-acme", aboutAcme));
        var aboutStranger = Webhook("phone-unknown");
        var unknown = await service.IngestWebhookPayloadAsync(Encoding.UTF8.GetBytes(aboutStranger), Sign("secret-acme", aboutStranger));
        var garbage = await service.IngestWebhookPayloadAsync(Encoding.UTF8.GetBytes("not json"), "sha256=00");

        Assert.Equal("InvalidSignature", forged.Status);
        Assert.NotEqual("InvalidSignature", genuine.Status);
        Assert.Equal("Ignored", unknown.Status);
        Assert.Equal("InvalidSignature", garbage.Status);
    }

    [Fact]
    public async Task ConnectingANumberAnotherInsurerAlreadyHolds_IsRefused()
    {
        var (provider, repository) = await TwoInsurersAsync();
        var validation = new FakeWhatsAppGraphValidationClient(new WhatsAppGraphValidationResult(true, "+256700000000", "Impostor", null));
        var context = new DefaultHttpContext { TraceIdentifier = "trace" };
        context.Request.Headers["X-Tenant-Id"] = "tenant-impostor";
        context.Request.Headers["X-Workspace-Id"] = "workspace-main";
        context.Request.Headers["x-demo-role"] = "ITAdmin";
        context.Request.Headers["x-demo-user-id"] = "impostor-admin";
        context.Request.Headers["X-Audit-Reason"] = "Connecting WhatsApp.";
        context.RequestServices = new ServiceCollection().AddLogging().AddProblemDetails().BuildServiceProvider();

        var result = await WhatsAppConfigurationEndpoints.SaveConfiguration(
            new WhatsAppSaveConfigurationRequest("phone-acme", "token", "secret", "waba", null),
            repository, new FakeWhatsAppCredentialProtector(), validation, provider, new InMemoryAuditRepository(),
            new InfrastructureOptions { AuthMode = "Development", MultiTenant = true }, context, CancellationToken.None);

        var response = new DefaultHttpContext();
        response.RequestServices = context.RequestServices;
        response.Response.Body = new MemoryStream();
        await result.ExecuteAsync(response);

        Assert.Equal(StatusCodes.Status409Conflict, response.Response.StatusCode);
        Assert.Empty(validation.Calls); // refused before Meta was even asked
        Assert.Equal(Acme.TenantId, provider.ForPhoneNumberId("phone-acme")!.TenantId);
    }

    private static WhatsAppProviderSendRequest Send(string fromPhone) => new(
        fromPhone, "256772118001", "Hello", TemplateName: null, Language: "en", ReplyToProviderMessageId: null, IdempotencyKey: Guid.NewGuid().ToString("N"));

    private sealed class CapturingHandler : HttpMessageHandler
    {
        public List<string> Authorization { get; } = [];
        public List<string> Urls { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Authorization.Add(request.Headers.Authorization?.ToString() ?? string.Empty);
            Urls.Add(request.RequestUri!.ToString());
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"messages":[{"id":"wamid.out"}]}""", Encoding.UTF8, "application/json"),
            });
        }
    }

    // Regression for the live outage that began when per-insurer routing shipped: a plain text message has
    // no attachment, so the document step reports "Ignored" for it, and the webhook returned on that. Meta got
    // a 200 and nothing was saved or answered, for every text.
    [Fact]
    public async Task ASignedTextMessageForAnInsurersNumber_IsSavedAndHandedToTheAssistant_NotDropped()
    {
        var (provider, _) = await TwoInsurersAsync();
        var (messaging, queue) = (new RecordingMessaging(), new RecordingQueue());

        var status = await ReceiveAsync(provider, Webhook("phone-baraka"), "secret-baraka", messaging, queue);

        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.Equal(Baraka, messaging.IngestedFor.Single());
        Assert.Equal(1, messaging.IngestedCount);
        var handed = Assert.Single(queue.Requests);
        Assert.Equal(Baraka, handed.Scope);
        Assert.Single(handed.Messages);
    }

    [Fact]
    public async Task ATextMessageForANumberNoInsurerHolds_IsAcceptedButNothingIsSavedOrAnswered()
    {
        var (provider, _) = await TwoInsurersAsync();
        var (messaging, queue) = (new RecordingMessaging(), new RecordingQueue());

        var status = await ReceiveAsync(provider, Webhook("phone-unknown"), "secret-baraka", messaging, queue);

        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.Empty(messaging.IngestedFor);
        Assert.Empty(queue.Requests);
    }

    [Fact]
    public async Task ATextMessageSignedWithTheWrongSecret_IsRefusedAndNothingIsSaved()
    {
        var (provider, _) = await TwoInsurersAsync();
        var (messaging, queue) = (new RecordingMessaging(), new RecordingQueue());

        var status = await ReceiveAsync(provider, Webhook("phone-baraka"), "secret-acme", messaging, queue);

        Assert.Equal(StatusCodes.Status401Unauthorized, status);
        Assert.Empty(messaging.IngestedFor);
        Assert.Empty(queue.Requests);
    }

    [Fact]
    public async Task Every_call_to_the_webhook_for_an_insurers_number_leaves_a_receipt_with_counts_and_no_message_text()
    {
        var (provider, _) = await TwoInsurersAsync();
        var (messaging, queue, receipts) = (new RecordingMessaging(), new RecordingQueue(), new RecordingReceipts());

        await ReceiveAsync(provider, Webhook("phone-baraka"), "secret-baraka", messaging, queue, receipts);
        await ReceiveAsync(provider, Webhook("phone-baraka"), "secret-acme", messaging, queue, receipts);
        await ReceiveAsync(provider, Webhook("phone-unknown"), "secret-baraka", messaging, queue, receipts);

        Assert.Equal(2, receipts.Items.Count); // the call about a number nobody here owns is not recorded
        var accepted = receipts.Items.Single(r => r.Outcome == "accepted");
        Assert.Equal(Baraka, accepted.Scope);
        Assert.Equal("phone-baraka", accepted.PhoneNumberId);
        Assert.Equal(1, accepted.MessagesInCall);
        Assert.Equal(1, accepted.MessagesNew);
        var refused = receipts.Items.Single(r => r.Outcome == "refused_signature");
        Assert.Equal(Baraka, refused.Scope);
        Assert.Equal(0, refused.MessagesNew);
    }

    [Fact]
    public async Task A_failure_to_record_a_receipt_never_fails_the_webhook()
    {
        var (provider, _) = await TwoInsurersAsync();
        var (messaging, queue) = (new RecordingMessaging(), new RecordingQueue());

        var status = await ReceiveAsync(provider, Webhook("phone-baraka"), "secret-baraka", messaging, queue, new RecordingReceipts { Throw = true });

        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.Single(queue.Requests); // the customer is still answered
    }

    private static async Task<int> ReceiveAsync(
        WhatsAppRuntimeSettingsProvider provider, string body, string signedWith, RecordingMessaging messaging, RecordingQueue queue, RecordingReceipts? receipts = null)
    {
        receipts ??= new RecordingReceipts();
        var options = new InfrastructureOptions { MultiTenant = true, ChannelProvider = "Disabled" };
        var inbound = new WhatsAppCloudApiInboundProvider(provider, NullLogger<WhatsAppCloudApiInboundProvider>.Instance);
        var ingestion = new WhatsAppInboundDocumentIngestionService(
            options, provider, inbound, new NeverMatches(), new NoAttachments(),
            new InMemoryCaseRepository(), new InMemoryCaseDocumentRepository(), new InMemoryAuditRepository(),
            NullLogger<WhatsAppInboundDocumentIngestionService>.Instance);

        var http = new DefaultHttpContext();
        http.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        http.Request.Headers["X-Hub-Signature-256"] = Sign(signedWith, body);
        http.RequestServices = new ServiceCollection().AddLogging().AddProblemDetails().BuildServiceProvider();
        http.Response.Body = new MemoryStream();

        var result = await WhatsAppIntegrationEndpoints.ReceiveWebhook(
            http.Request, ingestion, messaging, queue,
            http.RequestServices.GetRequiredService<IServiceScopeFactory>(), provider, options, receipts,
            NullLogger<WhatsAppCustomerAssistantLogMarker>.Instance, CancellationToken.None);

        await result.ExecuteAsync(http);
        return http.Response.StatusCode;
    }

    private sealed class RecordingMessaging : IWhatsAppMessagingService
    {
        public List<TenantScope> IngestedFor { get; } = [];
        public int IngestedCount { get; private set; }

        public Task<IReadOnlyList<WhatsAppMessageDto>> GetThreadAsync(string threadId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<WhatsAppMessageDto> SendAsync(SendWhatsAppMessageCommand command, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<WhatsAppInboundMessage>> IngestInboundAsync(
            TenantScope scope, IReadOnlyList<WhatsAppInboundMessage> messages, CancellationToken cancellationToken = default)
        {
            IngestedFor.Add(scope);
            IngestedCount += messages.Count;
            return Task.FromResult(messages);
        }

        public Task<int> ApplyDeliveryEventsAsync(TenantScope scope, IReadOnlyList<WhatsAppDeliveryEvent> events, CancellationToken cancellationToken = default) => Task.FromResult(0);
    }

    private sealed class RecordingReceipts : IWhatsAppWebhookReceiptRepository
    {
        public List<WhatsAppWebhookReceipt> Items { get; } = [];
        public bool Throw { get; set; }

        public Task AddAsync(WhatsAppWebhookReceipt receipt, CancellationToken cancellationToken = default)
        {
            if (Throw) throw new InvalidOperationException("storage is down");
            Items.Add(receipt);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<WhatsAppWebhookReceipt>> ListAsync(TenantScope scope, int limit, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<WhatsAppWebhookReceipt>>(Items.Where(item => item.Scope == scope).Take(limit).ToArray());
    }

    private sealed class RecordingQueue : IWhatsAppAssistantQueue
    {
        public List<WhatsAppAssistantRequest> Requests { get; } = [];

        public int Pending => 0;

        public bool TryEnqueue(WhatsAppAssistantRequest request)
        {
            Requests.Add(request);
            return true;
        }
    }

    private sealed class NeverMatches : IWhatsAppToCaseMatcher
    {
        public Task<WhatsAppToCaseMatchResult> MatchAsync(WhatsAppInboundMediaMessage message, TenantScope scope, CancellationToken cancellationToken = default) =>
            Task.FromResult(new WhatsAppToCaseMatchResult(null, "no_match", RequiresManualReview: true));
    }

    private sealed class NoAttachments : IWhatsAppAttachmentProcessor
    {
        public Task<IReadOnlyCollection<CaseDocument>> ProcessAsync(
            ClaimCase claimCase, WhatsAppInboundMediaMessage message, string actorUserId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<CaseDocument>>([]);
    }
}
