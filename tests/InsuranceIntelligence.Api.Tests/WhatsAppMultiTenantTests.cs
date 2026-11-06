using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using InsuranceIntelligence.Api.Integrations;
using InsuranceIntelligence.Api.Workflow;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Customers;
using InsuranceIntelligence.Domain.Documents;
using InsuranceIntelligence.Domain.Integrations;
using InsuranceIntelligence.Domain.Policies;
using InsuranceIntelligence.Infrastructure.Configuration;
using InsuranceIntelligence.Infrastructure.WhatsApp;
using InsuranceIntelligence.LocalTestDoubles;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace InsuranceIntelligence.Api.Tests;

/// <summary>
/// Several workspaces on one deployment each connect their own WhatsApp number (build-loop item 27). A webhook is routed
/// by the business phone number it names and proved by that workspace's own app secret; credentials, media, replies and the
/// sender are each the workspace's own and never another's.
/// </summary>
public sealed class WhatsAppMultiTenantTests
{
    private static readonly TenantScope ScopeA = TenantScope.Create("tenant-a", "workspace-a");
    private static readonly TenantScope ScopeB = TenantScope.Create("tenant-b", "workspace-b");
    private const string PhoneA = "phone-a";
    private const string PhoneB = "phone-b";
    private const string SecretA = "app-secret-a";
    private const string SecretB = "app-secret-b";
    private const string CustomerPhone = "256772118001";

    private static WhatsAppChannelConfiguration Config(TenantScope scope, string phone, string token, string secret, string verify, bool active = true) =>
        new()
        {
            Scope = scope,
            PhoneNumberId = phone,
            EncryptedAccessToken = "protected:" + token,
            EncryptedAppSecret = "protected:" + secret,
            EncryptedVerifyToken = "protected:" + verify,
            IsActive = active,
            ConfiguredByUserId = "admin",
            ConfiguredAt = DateTimeOffset.UtcNow,
        };

    private static async Task<(WhatsAppRuntimeSettingsProvider Provider, CountingRepository Repository)> TwoWorkspacesAsync(TimeSpan? missInterval = null)
    {
        var repository = new CountingRepository();
        await repository.SaveAsync(Config(ScopeA, PhoneA, "token-a", SecretA, "verify-a"));
        await repository.SaveAsync(Config(ScopeB, PhoneB, "token-b", SecretB, "verify-b"));
        var services = new ServiceCollection()
            .AddSingleton<IWhatsAppChannelConfigurationRepository>(repository)
            .AddSingleton<IWhatsAppCredentialProtector, FakeWhatsAppCredentialProtector>()
            .BuildServiceProvider();
        var provider = new WhatsAppRuntimeSettingsProvider(
            services.GetRequiredService<IServiceScopeFactory>(),
            new InfrastructureOptions { ChannelProvider = "WhatsAppCloudApi" },
            missInterval);
        return (provider, repository);
    }

    private static string Webhook(string phone, string messageId = "wamid.1", string type = "text", string? caption = null) =>
        WebhookOf([(phone, messageId, type, caption)]);

    private static string WebhookOf(params (string Phone, string MessageId, string Type, string? Caption)[] messages) =>
        JsonSerializer.Serialize(new
        {
            @object = "whatsapp_business_account",
            entry = messages.Select(item => new
            {
                id = "WABA",
                changes = new[]
                {
                    new
                    {
                        field = "messages",
                        value = new
                        {
                            messaging_product = "whatsapp",
                            metadata = new { display_phone_number = "256700000000", phone_number_id = item.Phone },
                            messages = new object[]
                            {
                                item.Type == "image"
                                    ? new { from = CustomerPhone, id = item.MessageId, timestamp = "1700000000", type = "image", image = (object?)new { id = "media-1", mime_type = "image/jpeg", caption = item.Caption }, text = (object?)null }
                                    : new { from = CustomerPhone, id = item.MessageId, timestamp = "1700000000", type = "text", image = (object?)null, text = (object?)new { body = "hello" } },
                            },
                        },
                    },
                },
            }).ToArray(),
        });

    private static string Sign(string secret, byte[] body) =>
        "sha256=" + Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), body)).ToLowerInvariant();

    private static WhatsAppInboundDocumentIngestionService Ingestion(
        WhatsAppRuntimeSettingsProvider settings,
        IWhatsAppInboundProvider? inbound = null,
        IWhatsAppToCaseMatcher? matcher = null,
        IWhatsAppAttachmentProcessor? processor = null,
        ICaseRepository? cases = null,
        ICaseDocumentRepository? documents = null) =>
        new(
            new InfrastructureOptions { ChannelProvider = "WhatsAppCloudApi" },
            settings,
            inbound ?? new WhatsAppCloudApiInboundProvider(settings, NullLogger<WhatsAppCloudApiInboundProvider>.Instance),
            matcher ?? new NoMatch(),
            processor ?? new DisabledWhatsAppAttachmentProcessor(),
            cases ?? new InMemoryCaseRepository(),
            documents ?? new InMemoryCaseDocumentRepository(),
            new InMemoryAuditRepository(),
            NullLogger<WhatsAppInboundDocumentIngestionService>.Instance);

    // ---- who owns a number ----

    [Fact]
    public async Task EachNumberResolvesToItsOwnWorkspaceAndNoOther()
    {
        var (settings, _) = await TwoWorkspacesAsync();

        var a = settings.ForPhoneNumber(PhoneA)!;
        var b = settings.ForPhoneNumber(PhoneB)!;

        Assert.Equal(("tenant-a", "workspace-a", "token-a", SecretA), (a.TenantId, a.WorkspaceId, a.AccessToken, a.AppSecret));
        Assert.Equal(("tenant-b", "workspace-b", "token-b", SecretB), (b.TenantId, b.WorkspaceId, b.AccessToken, b.AppSecret));
        Assert.Equal(PhoneA, settings.ForScope(ScopeA)!.PhoneNumberId);
        Assert.Equal(PhoneB, settings.ForScope(ScopeB)!.PhoneNumberId);
        Assert.Null(settings.ForScope(TenantScope.Create("tenant-c", "workspace-c")));
        Assert.Null(settings.ForPhoneNumber("someone-elses-number"));
        Assert.True(settings.HasAny);
    }

    [Fact]
    public async Task WithSeveralConnectionsThereIsNoSingleCurrentOne()
    {
        var (settings, _) = await TwoWorkspacesAsync();

        Assert.Null(settings.Current);
    }

    [Fact]
    public async Task AVerifyTokenSaysWhichWorkspaceIsAsking()
    {
        var (settings, _) = await TwoWorkspacesAsync();

        Assert.Equal("tenant-a", settings.ForVerifyToken("verify-a")!.TenantId);
        Assert.Equal("tenant-b", settings.ForVerifyToken("verify-b")!.TenantId);
        Assert.Null(settings.ForVerifyToken("verify-c"));
        Assert.Null(settings.ForVerifyToken(string.Empty));
        var provider = new WhatsAppCloudApiInboundProvider(settings, NullLogger<WhatsAppCloudApiInboundProvider>.Instance);
        Assert.True(provider.TryVerifySubscription("subscribe", "verify-b", "12345", out var challenge));
        Assert.Equal("12345", challenge);
        Assert.False(provider.TryVerifySubscription("subscribe", "verify-c", "12345", out _));
    }

    [Fact]
    public async Task ANumberTwoWorkspacesHold_BelongsToNobody()
    {
        var (settings, repository) = await TwoWorkspacesAsync();
        await repository.SaveAsync(Config(ScopeB, PhoneA, "token-b", SecretB, "verify-b"));
        await settings.RefreshAsync();

        Assert.Null(settings.ForPhoneNumber(PhoneA));
    }

    [Fact]
    public async Task ADisconnectedWorkspaceDropsOut_AfterTheRefreshASaveOrDeleteCauses()
    {
        var (settings, repository) = await TwoWorkspacesAsync();
        await repository.SaveAsync(Config(ScopeA, PhoneA, "token-a", SecretA, "verify-a", active: false));
        await repository.DeleteAsync(ScopeB);
        await settings.RefreshAsync();

        Assert.Null(settings.ForPhoneNumber(PhoneA));
        Assert.Null(settings.ForPhoneNumber(PhoneB));
        Assert.False(settings.HasAny);
    }

    [Fact]
    public async Task ANumberSavedElsewhereIsFoundOnAMiss_ButAStrangerCannotMakeTheStoreBeReadOverAndOver()
    {
        var (settings, repository) = await TwoWorkspacesAsync(missInterval: TimeSpan.Zero);
        await repository.SaveAsync(Config(TenantScope.Create("tenant-c", "workspace-c"), "phone-c", "token-c", "secret-c", "verify-c"));

        var found = await settings.FindByPhoneNumberAsync("phone-c");

        Assert.Equal("tenant-c", found!.TenantId);

        var (throttled, counting) = await TwoWorkspacesAsync(missInterval: TimeSpan.FromHours(1));
        _ = throttled.Current;
        var readsAfterFirstLoad = counting.ListCalls;
        for (var i = 0; i < 20; i++)
        {
            Assert.Null(await throttled.FindByPhoneNumberAsync($"random-{i}"));
        }

        Assert.Equal(readsAfterFirstLoad, counting.ListCalls);
    }

    // ---- routing a webhook ----

    [Fact]
    public async Task AWebhookSignedWithTheOwnersSecretIsRoutedToTheOwner()
    {
        var (settings, _) = await TwoWorkspacesAsync();
        var body = Encoding.UTF8.GetBytes(Webhook(PhoneB));

        var routing = await Ingestion(settings).RouteAsync(body, Sign(SecretB, body));

        Assert.True(routing.IsRouted);
        var batch = Assert.Single(routing.Batches);
        Assert.Equal(ScopeB, batch.Scope);
    }

    [Fact]
    public async Task AWebhookForOneNumberSignedWithAnotherWorkspacesSecretIsRefused()
    {
        var (settings, _) = await TwoWorkspacesAsync();
        var body = Encoding.UTF8.GetBytes(Webhook(PhoneA));

        var routing = await Ingestion(settings).RouteAsync(body, Sign(SecretB, body));

        Assert.Equal("InvalidSignature", routing.Status);
        Assert.Empty(routing.Batches);
    }

    [Fact]
    public async Task AWebhookForANumberNobodyHasConnected_IsAnsweredLikeABadSignature()
    {
        var (settings, _) = await TwoWorkspacesAsync();
        var body = Encoding.UTF8.GetBytes(Webhook("not-connected"));

        var routing = await Ingestion(settings).RouteAsync(body, Sign(SecretA, body));

        Assert.Equal("InvalidSignature", routing.Status);
    }

    [Fact]
    public async Task AWebhookNamingTwoWorkspacesNumbers_NeedsBothSecretsAndOneSignatureCannotHaveThat()
    {
        var (settings, _) = await TwoWorkspacesAsync();
        var json = WebhookOf((PhoneA, "wamid.a", "text", null), (PhoneB, "wamid.b", "text", null));
        var body = Encoding.UTF8.GetBytes(json);

        var routing = await Ingestion(settings).RouteAsync(body, Sign(SecretA, body));

        Assert.Equal("InvalidSignature", routing.Status);
    }

    [Fact]
    public async Task AWebhookNamingNoNumber_CannotBeChecked_WhenSeveralWorkspacesShareTheDeployment()
    {
        var (settings, _) = await TwoWorkspacesAsync();
        var body = Encoding.UTF8.GetBytes("""{"object":"whatsapp_business_account","entry":[]}""");

        var routing = await Ingestion(settings).RouteAsync(body, Sign(SecretA, body));

        Assert.Equal("InvalidSignature", routing.Status);
    }

    [Fact]
    public async Task WithNothingConnected_TheChannelSaysItIsNotConfigured()
    {
        var services = new ServiceCollection()
            .AddSingleton<IWhatsAppChannelConfigurationRepository>(new InMemoryWhatsAppChannelConfigurationRepository())
            .AddSingleton<IWhatsAppCredentialProtector, FakeWhatsAppCredentialProtector>()
            .BuildServiceProvider();
        var settings = new WhatsAppRuntimeSettingsProvider(services.GetRequiredService<IServiceScopeFactory>(), new InfrastructureOptions());
        var body = Encoding.UTF8.GetBytes(Webhook(PhoneA));

        var routing = await Ingestion(settings).RouteAsync(body, Sign(SecretA, body));

        Assert.Equal("ConfigurationRequired", routing.Status);
    }

    // ---- media lands on the right workspace's claim ----

    [Fact]
    public async Task APhotoSentToOneWorkspacesNumberLandsOnThatWorkspacesClaimOnly_EvenWhenTheSamePhoneIsACustomerOfBoth()
    {
        var (settings, _) = await TwoWorkspacesAsync();
        var cases = new InMemoryCaseRepository();
        var customers = new InMemoryCustomerRepository();
        var policies = new InMemoryPolicyRepository();
        var claimA = Claim(ScopeA, "POL-A");
        var claimB = Claim(ScopeB, "POL-B");
        await cases.SaveAsync(claimA);
        await cases.SaveAsync(claimB);
        foreach (var (scope, policyNumber, customerId) in new[] { (ScopeA, "POL-A", "cust-a"), (ScopeB, "POL-B", "cust-b") })
        {
            await customers.UpsertAsync(new InsuranceIntelligence.Domain.Customers.Customer(customerId, scope.TenantId, scope.WorkspaceId, "Amina Okello", null, "+" + CustomerPhone), CancellationToken.None);
            await policies.UpsertAsync(new Policy("pol-" + customerId, scope.TenantId, scope.WorkspaceId, policyNumber, "Motor", null, customerId, null, null, null), CancellationToken.None);
        }

        var processor = new RecordingProcessor();
        var documents = new InMemoryCaseDocumentRepository();
        var service = Ingestion(settings, matcher: new RuleBasedWhatsAppToCaseMatcher(cases, customers, policies), processor: processor, cases: cases, documents: documents);
        var body = Encoding.UTF8.GetBytes(Webhook(PhoneA, "wamid.photo", "image"));

        var result = await service.IngestWebhookPayloadAsync(body, Sign(SecretA, body));

        Assert.Equal("Accepted", result.Status);
        Assert.Equal(1, result.DocumentsStored);
        Assert.Equal(claimA.CaseId, Assert.Single(processor.Claims).CaseId);
        Assert.Single(await documents.ListForCaseAsync(claimA.CaseId, ScopeA));
        Assert.Empty(await documents.ListForCaseAsync(claimB.CaseId, ScopeB));
    }

    // ---- sending and downloading use the owner's credentials ----

    [Fact]
    public async Task EachWorkspaceSendsWithItsOwnToken_AndANumberNobodyOwnsIsRefused()
    {
        var (settings, _) = await TwoWorkspacesAsync();
        var handler = new RecordingHandler();
        var outbound = new WhatsAppCloudApiOutboundProvider(settings, new HttpClient(handler));

        var fromA = await outbound.SendAsync(Request(PhoneA));
        var fromB = await outbound.SendAsync(Request(PhoneB));
        var stranger = await outbound.SendAsync(Request("not-connected"));

        Assert.True(fromA.Accepted);
        Assert.True(fromB.Accepted);
        Assert.False(stranger.Accepted);
        Assert.Equal(2, handler.Calls.Count);
        Assert.Equal(("Bearer token-a", true), (handler.Calls[0].Authorization, handler.Calls[0].Url.Contains("/phone-a/messages")));
        Assert.Equal(("Bearer token-b", true), (handler.Calls[1].Authorization, handler.Calls[1].Url.Contains("/phone-b/messages")));
    }

    [Fact]
    public async Task MediaAndVoiceNotesAreDownloadedWithTheOwnersToken()
    {
        var (settings, _) = await TwoWorkspacesAsync();
        var handler = new RecordingHandler(mediaMetadata: true);
        var inbound = new WhatsAppCloudApiInboundProvider(settings, NullLogger<WhatsAppCloudApiInboundProvider>.Instance, new HttpClient(handler));

        await inbound.DownloadMediaAsync("media-1", PhoneB);

        Assert.All(handler.Calls, call => Assert.Equal("Bearer token-b", call.Authorization));
        await Assert.ThrowsAsync<InvalidOperationException>(() => inbound.DownloadMediaAsync("media-1", "not-connected"));
    }

    [Fact]
    public async Task EachWorkspaceGetsItsOwnSender_AndASenderWillNotSendFromAnotherWorkspacesNumber()
    {
        var (settings, _) = await TwoWorkspacesAsync();
        var handler = new RecordingHandler();
        var resolver = new ConfiguredWhatsAppSenderResolver(settings, new WhatsAppCloudApiOutboundProvider(settings, new HttpClient(handler)));

        var senderA = await resolver.ResolveAsync(ScopeA);
        var senderB = await resolver.ResolveAsync(ScopeB);
        var nobody = await resolver.ResolveAsync(TenantScope.Create("tenant-c", "workspace-c"));

        Assert.Equal(PhoneA, senderA!.BusinessPhoneNumberId);
        Assert.Equal(PhoneB, senderB!.BusinessPhoneNumberId);
        Assert.Null(nobody);
        var crossed = await senderA.Provider.SendAsync(Request(PhoneB));
        Assert.False(crossed.Accepted);
        Assert.Empty(handler.Calls);
        Assert.True((await senderA.Provider.SendAsync(Request(PhoneA))).Accepted);
    }

    // ---- one number, one workspace ----

    [Fact]
    public async Task ANumberAlreadyConnectedToAnotherWorkspaceCannotBeSavedAgain()
    {
        var repository = new InMemoryWhatsAppChannelConfigurationRepository();
        var protector = new FakeWhatsAppCredentialProtector();
        var validation = new FakeWhatsAppGraphValidationClient(new WhatsAppGraphValidationResult(true, "+256700000000", "Acme", null));
        var provider = new RecordingSettings();
        var first = await SaveAsync(repository, protector, validation, provider, "tenant-one", "phone-shared");
        var second = await SaveAsync(repository, protector, validation, provider, "tenant-two", "phone-shared");
        var secondOwnNumber = await SaveAsync(repository, protector, validation, provider, "tenant-two", "phone-two");
        var firstAgain = await SaveAsync(repository, protector, validation, provider, "tenant-one", "phone-shared");

        Assert.Equal(StatusCodes.Status200OK, first);
        Assert.Equal(StatusCodes.Status409Conflict, second);
        Assert.Equal(StatusCodes.Status200OK, secondOwnNumber);
        Assert.Equal(StatusCodes.Status200OK, firstAgain);
        Assert.Equal("phone-shared", (await repository.GetAsync(TenantScope.Create("tenant-one", "workspace-claims")))!.PhoneNumberId);
        Assert.Equal("phone-two", (await repository.GetAsync(TenantScope.Create("tenant-two", "workspace-claims")))!.PhoneNumberId);
    }

    [Fact]
    public async Task TheHealthOfTheChannelIsTheCallersOwn()
    {
        var (settings, _) = await TwoWorkspacesAsync();
        var inbound = new WhatsAppCloudApiInboundProvider(settings, NullLogger<WhatsAppCloudApiInboundProvider>.Instance);
        var outbound = new WhatsAppCloudApiOutboundProvider(settings, new HttpClient(new RecordingHandler()));
        var options = new InfrastructureOptions { AuthMode = "Development", ChannelProvider = "WhatsAppCloudApi" };

        var a = await HealthAsync(inbound, outbound, settings, options, "tenant-a", "workspace-a");
        var unconnected = await HealthAsync(inbound, outbound, settings, options, "tenant-c", "workspace-c");

        Assert.True(a.GetProperty("configured").GetBoolean());
        Assert.False(unconnected.GetProperty("configured").GetBoolean());
        Assert.Equal("Not configured", unconnected.GetProperty("status").GetString());
    }

    private static async Task<JsonElement> HealthAsync(
        IWhatsAppInboundProvider inbound, IWhatsAppOutboundProvider outbound, IWhatsAppRuntimeSettingsProvider settings, InfrastructureOptions options, string tenant, string workspace)
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Tenant-Id"] = tenant;
        context.Request.Headers["X-Workspace-Id"] = workspace;
        context.Request.Headers["x-demo-role"] = "ITAdmin";
        context.Request.Headers["x-demo-user-id"] = "admin-1";
        context.RequestServices = new ServiceCollection().AddLogging().BuildServiceProvider();
        var result = await WhatsAppIntegrationEndpoints.GetHealth(inbound, outbound, settings, options, context, CancellationToken.None);
        var response = new DefaultHttpContext { RequestServices = context.RequestServices };
        response.Response.Body = new MemoryStream();
        await result.ExecuteAsync(response);
        response.Response.Body.Seek(0, SeekOrigin.Begin);
        return (await JsonDocument.ParseAsync(response.Response.Body)).RootElement.Clone();
    }

    private static async Task<int?> SaveAsync(
        InMemoryWhatsAppChannelConfigurationRepository repository, FakeWhatsAppCredentialProtector protector, FakeWhatsAppGraphValidationClient validation,
        RecordingSettings settings, string tenant, string phone)
    {
        var context = new DefaultHttpContext { TraceIdentifier = "trace" };
        context.Request.Headers["X-Tenant-Id"] = tenant;
        context.Request.Headers["X-Workspace-Id"] = "workspace-claims";
        context.Request.Headers["x-demo-role"] = "ITAdmin";
        context.Request.Headers["x-demo-user-id"] = "admin-1";
        context.Request.Headers["X-Audit-Reason"] = "Connecting our WhatsApp Business number.";
        context.RequestServices = new ServiceCollection().AddLogging().AddProblemDetails().BuildServiceProvider();
        var result = await WhatsAppConfigurationEndpoints.SaveConfiguration(
            new WhatsAppSaveConfigurationRequest(phone, "token", "secret", "waba", null),
            repository, protector, validation, settings, new InMemoryAuditRepository(),
            new InfrastructureOptions { AuthMode = "Development", ChannelProvider = "WhatsAppCloudApi" }, context, CancellationToken.None);
        var response = new DefaultHttpContext { RequestServices = context.RequestServices };
        response.Response.Body = new MemoryStream();
        await result.ExecuteAsync(response);
        return response.Response.StatusCode;
    }

    private static WhatsAppProviderSendRequest Request(string phone) =>
        new(phone, CustomerPhone, "Hello", null, "en", null, Guid.NewGuid().ToString("N"));

    private static ClaimCase Claim(TenantScope scope, string policyNumber) =>
        new()
        {
            CaseId = Guid.NewGuid(),
            Scope = scope,
            CustomerName = "Amina Okello",
            CustomerPhone = "+" + CustomerPhone,
            PolicyNumber = policyNumber,
            ProductLine = "Motor",
            ClaimType = "Accident",
            Channel = "WhatsApp",
            IncidentDate = DateOnly.FromDateTime(DateTime.UtcNow),
            IncidentDescription = "Accident",
            Status = CaseStatus.New,
            CompletenessScore = 0,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            CreatedByUserId = "officer",
            CorrelationId = "corr",
        };

    private sealed class CountingRepository : IWhatsAppChannelConfigurationRepository
    {
        private readonly InMemoryWhatsAppChannelConfigurationRepository inner = new();
        public int ListCalls { get; private set; }

        public Task<WhatsAppChannelConfiguration?> GetAsync(TenantScope scope, CancellationToken cancellationToken = default) => inner.GetAsync(scope, cancellationToken);
        public Task SaveAsync(WhatsAppChannelConfiguration configuration, CancellationToken cancellationToken = default) => inner.SaveAsync(configuration, cancellationToken);
        public Task DeleteAsync(TenantScope scope, CancellationToken cancellationToken = default) => inner.DeleteAsync(scope, cancellationToken);

        public Task<IReadOnlyList<WhatsAppChannelConfiguration>> ListActiveAsync(CancellationToken cancellationToken = default)
        {
            ListCalls++;
            return inner.ListActiveAsync(cancellationToken);
        }
    }

    private sealed class NoMatch : IWhatsAppToCaseMatcher
    {
        public Task<WhatsAppToCaseMatchResult> MatchAsync(WhatsAppInboundMediaMessage message, TenantScope scope, CancellationToken cancellationToken = default) =>
            Task.FromResult(new WhatsAppToCaseMatchResult(null, "no_match", true));
    }

    private sealed class RecordingProcessor : IWhatsAppAttachmentProcessor
    {
        public List<ClaimCase> Claims { get; } = [];

        public Task<IReadOnlyCollection<CaseDocument>> ProcessAsync(ClaimCase claimCase, WhatsAppInboundMediaMessage message, string actorUserId, CancellationToken cancellationToken = default)
        {
            Claims.Add(claimCase);
            var now = DateTimeOffset.UtcNow;
            IReadOnlyCollection<CaseDocument> documents =
            [
                new CaseDocument
                {
                    DocumentId = Guid.NewGuid(),
                    CaseId = claimCase.CaseId,
                    Scope = claimCase.Scope,
                    OriginalFileName = "photo.jpg",
                    ContentType = "image/jpeg",
                    FileSizeBytes = 3,
                    ObjectKey = "key",
                    DocumentType = DocumentType.Unknown,
                    ProcessingStatus = DocumentProcessingStatus.Uploaded,
                    Classification = new DocumentClassificationResult(DocumentType.Unknown, 0m, "test", now),
                    Extraction = new DocumentExtractionResult(new Dictionary<string, string>(), new Dictionary<string, decimal>(), "test", now, false, null, null),
                    ValidationWarnings = [],
                    UploadedByUserId = actorUserId,
                    UploadedAt = now,
                    CorrelationId = $"whatsapp-{message.ProviderMessageId}",
                },
            ];
            return Task.FromResult(documents);
        }
    }

    private sealed class RecordingSettings : IWhatsAppRuntimeSettingsProvider
    {
        public WhatsAppEffectiveSettings? Current => null;
        public bool HasAny => false;
        public WhatsAppEffectiveSettings? ForScope(TenantScope scope) => null;
        public WhatsAppEffectiveSettings? ForPhoneNumber(string phoneNumberId) => null;
        public WhatsAppEffectiveSettings? ForVerifyToken(string verifyToken) => null;
        public Task<WhatsAppEffectiveSettings?> FindByPhoneNumberAsync(string phoneNumberId, CancellationToken cancellationToken = default) => Task.FromResult<WhatsAppEffectiveSettings?>(null);
        public Task RefreshIfStaleAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RefreshAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class RecordingHandler(bool mediaMetadata = false) : HttpMessageHandler
    {
        public List<(string Url, string? Authorization)> Calls { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls.Add((request.RequestUri!.ToString(), request.Headers.Authorization?.ToString()));
            if (mediaMetadata)
            {
                var url = request.RequestUri.ToString();
                return Task.FromResult(url.Contains("cdn.example", StringComparison.Ordinal)
                    ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([1, 2, 3]) { Headers = { ContentType = new("image/jpeg") } } }
                    : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("""{"url":"https://cdn.example/m","mime_type":"image/jpeg","id":"media-1"}""", Encoding.UTF8, "application/json") });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"messages":[{"id":"wamid.out"}]}""", Encoding.UTF8, "application/json"),
            });
        }
    }
}
