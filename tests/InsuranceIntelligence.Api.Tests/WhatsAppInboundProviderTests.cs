using System.Net;
using System.Security.Cryptography;
using System.Text;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Documents;
using InsuranceIntelligence.Domain.Integrations;
using InsuranceIntelligence.Infrastructure.Configuration;
using InsuranceIntelligence.Infrastructure.WhatsApp;
using Microsoft.Extensions.Logging.Abstractions;

namespace InsuranceIntelligence.Api.Tests;

public sealed class WhatsAppInboundProviderTests
{
    private const string SampleWebhook = """
        {
          "object": "whatsapp_business_account",
          "entry": [{
            "id": "WABA",
            "changes": [{
              "value": {
                "messaging_product": "whatsapp",
                "metadata": { "display_phone_number": "256700000000", "phone_number_id": "phone-1" },
                "messages": [{
                  "from": "256772118001",
                  "id": "wamid.ABC",
                  "timestamp": "1700000000",
                  "type": "image",
                  "image": {
                    "id": "media-42",
                    "mime_type": "image/jpeg",
                    "caption": "Police report for case 11111111-1111-1111-1111-111111111111"
                  }
                }]
              },
              "field": "messages"
            }]
          }]
        }
        """;

    [Fact]
    public void DisabledProvider_IsNotConfigured_AndRejectsVerification()
    {
        var provider = new DisabledWhatsAppInboundProvider();
        Assert.False(provider.IsConfigured);
        Assert.False(provider.TryVerifySubscription("subscribe", "token", "challenge", out _));
        Assert.Empty(provider.ParseInboundMediaMessages(SampleWebhook));
    }

    [Fact]
    public void Parser_ExtractsImageMediaWithCaption()
    {
        var messages = WhatsAppWebhookParser.ParseMediaMessages(SampleWebhook);
        Assert.Single(messages);
        Assert.Equal("media-42", messages[0].MediaId);
        Assert.Equal("image", messages[0].MediaType);
        Assert.Equal("phone-1", messages[0].BusinessPhoneNumberId);
        Assert.Contains("11111111-1111-1111-1111-111111111111", messages[0].Caption);
    }

    [Fact]
    public void CloudProvider_VerifySubscription_RequiresMatchingToken()
    {
        var provider = new WhatsAppCloudApiInboundProvider(new StaticWhatsAppRuntimeSettingsProvider(ConfiguredSettings()), NullLogger<WhatsAppCloudApiInboundProvider>.Instance);
        Assert.True(provider.IsConfigured);
        Assert.True(provider.TryVerifySubscription("subscribe", "verify-me", "12345", out var challenge));
        Assert.Equal("12345", challenge);
        Assert.False(provider.TryVerifySubscription("subscribe", "wrong", "12345", out _));
    }

    [Fact]
    public void CloudProvider_Signature_AcceptsValidHmac()
    {
        var provider = new WhatsAppCloudApiInboundProvider(new StaticWhatsAppRuntimeSettingsProvider(ConfiguredSettings()), NullLogger<WhatsAppCloudApiInboundProvider>.Instance);
        var body = Encoding.UTF8.GetBytes(SampleWebhook);
        var signature = Sign("app-secret", body);
        Assert.True(provider.IsValidSignature(body, signature));
        Assert.False(provider.IsValidSignature(body, "sha256=deadbeef"));
        Assert.False(provider.IsValidSignature(body, null));
    }

    [Fact]
    public async Task CloudProvider_DownloadMedia_UsesMetadataThenBinary()
    {
        var handler = new SequencingHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"url":"https://cdn.example/media","mime_type":"image/jpeg","id":"media-42"}""", Encoding.UTF8, "application/json"),
            },
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent([1, 2, 3, 4])
                {
                    Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg") },
                },
            });
        var provider = new WhatsAppCloudApiInboundProvider(
            new StaticWhatsAppRuntimeSettingsProvider(ConfiguredSettings()),
            NullLogger<WhatsAppCloudApiInboundProvider>.Instance,
            new HttpClient(handler));

        var content = await provider.DownloadMediaAsync("media-42");

        Assert.Equal("image/jpeg", content.ContentType);
        Assert.Equal(4, content.ContentBytes.Length);
        Assert.Equal(2, handler.RequestCount);
        Assert.Contains("Authorization", handler.LastHeaderKeys);
    }

    [Fact]
    public async Task Ingestion_WhenNotConfigured_ReturnsConfigurationRequired()
    {
        var service = new WhatsAppInboundDocumentIngestionService(
            new InfrastructureOptions(),
            new StaticWhatsAppRuntimeSettingsProvider(null),
            new DisabledWhatsAppInboundProvider(),
            new StubMatcher(null),
            new DisabledWhatsAppAttachmentProcessor(),
            new StubCaseRepository(),
            new StubDocumentRepository(),
            new StubAuditRepository(),
            NullLogger<WhatsAppInboundDocumentIngestionService>.Instance);

        var result = await service.IngestWebhookPayloadAsync(Encoding.UTF8.GetBytes(SampleWebhook), "sha256=x");
        Assert.Equal("ConfigurationRequired", result.Status);
    }

    [Fact]
    public async Task Ingestion_WhenSignatureInvalid_Rejects()
    {
        var options = new InfrastructureOptions { ChannelProvider = "WhatsAppCloudApi" };
        var settingsProvider = new StaticWhatsAppRuntimeSettingsProvider(ConfiguredSettings());
        var provider = new WhatsAppCloudApiInboundProvider(settingsProvider, NullLogger<WhatsAppCloudApiInboundProvider>.Instance);
        var service = new WhatsAppInboundDocumentIngestionService(
            options,
            settingsProvider,
            provider,
            new StubMatcher(null),
            new DisabledWhatsAppAttachmentProcessor(),
            new StubCaseRepository(),
            new StubDocumentRepository(),
            new StubAuditRepository(),
            NullLogger<WhatsAppInboundDocumentIngestionService>.Instance);

        var result = await service.IngestWebhookPayloadAsync(Encoding.UTF8.GetBytes(SampleWebhook), "sha256=bad");
        Assert.Equal("InvalidSignature", result.Status);
    }

    [Fact]
    public async Task Ingestion_WhenMatched_StoresDocument()
    {
        var caseId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var options = new InfrastructureOptions { ChannelProvider = "WhatsAppCloudApi" };
        var settingsProvider = new StaticWhatsAppRuntimeSettingsProvider(ConfiguredSettings());
        var handler = new SequencingHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"url":"https://cdn.example/media","mime_type":"image/jpeg","id":"media-42"}""", Encoding.UTF8, "application/json"),
            },
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent([9, 9, 9])
                {
                    Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg") },
                },
            });
        var provider = new WhatsAppCloudApiInboundProvider(
            settingsProvider,
            NullLogger<WhatsAppCloudApiInboundProvider>.Instance,
            new HttpClient(handler));
        var storage = new CapturingStorageProvider();
        var processor = new WhatsAppAttachmentProcessor(
            options,
            provider,
            storage,
            new StubProcessingProvider());
        var documents = new StubDocumentRepository();
        var body = Encoding.UTF8.GetBytes(SampleWebhook);
        var service = new WhatsAppInboundDocumentIngestionService(
            options,
            settingsProvider,
            provider,
            new StubMatcher(caseId),
            processor,
            new StubCaseRepository(CreateCase(caseId)),
            documents,
            new StubAuditRepository(),
            NullLogger<WhatsAppInboundDocumentIngestionService>.Instance);

        var result = await service.IngestWebhookPayloadAsync(body, Sign("app-secret", body));

        Assert.Equal("Accepted", result.Status);
        Assert.Equal(1, result.DocumentsStored);
        Assert.Equal(0, result.ManualReviewRequired);
        Assert.Single(documents.Saved);
        Assert.NotNull(storage.LastRequest);

        var replay = await service.IngestWebhookPayloadAsync(body, Sign("app-secret", body));
        Assert.Equal("Accepted", replay.Status);
        Assert.Equal(0, replay.DocumentsStored);
        Assert.Single(documents.Saved);
    }

    [Fact]
    public async Task Matcher_UsesCaptionGuid()
    {
        var caseId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var matcher = new RuleBasedWhatsAppToCaseMatcher(new StubCaseRepository(CreateCase(caseId)));
        var message = new WhatsAppInboundMediaMessage(
            "wamid.1",
            "phone-1",
            "256772118001",
            "media-1",
            "image",
            "image/jpeg",
            null,
            $"Claim {caseId}",
            DateTimeOffset.UtcNow);

        var match = await matcher.MatchAsync(message, TenantScope.Create("tenant-a", "workspace-a"));
        Assert.Equal(caseId, match.CaseId);
        Assert.False(match.RequiresManualReview);
    }

    private static WhatsAppEffectiveSettings ConfiguredSettings() => new(
        PhoneNumberId: "phone-1",
        AccessToken: "token",
        AppSecret: "app-secret",
        VerifyToken: "verify-me",
        GraphBaseUrl: "https://graph.facebook.com/v21.0",
        RequestTimeoutSeconds: 20,
        TenantId: "tenant-a",
        WorkspaceId: "workspace-a",
        IntegrationUserId: "whatsapp-integration",
        Source: "EnvironmentConfigured");

    private static string Sign(string secret, byte[] body)
    {
        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), body);
        return "sha256=" + Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static ClaimCase CreateCase(Guid caseId) =>
        new()
        {
            CaseId = caseId,
            Scope = TenantScope.Create("tenant-a", "workspace-a"),
            CustomerName = "Test Customer",
            PolicyNumber = "POL-1",
            ProductLine = "Motor",
            ClaimType = "Accident",
            Channel = "WhatsApp",
            IncidentDate = DateOnly.FromDateTime(DateTime.UtcNow),
            IncidentDescription = "Test incident",
            Status = CaseStatus.New,
            CompletenessScore = 0,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            CreatedByUserId = "officer",
            CorrelationId = "corr",
        };

    private sealed class SequencingHandler(params HttpResponseMessage[] responses) : HttpMessageHandler
    {
        private int index;
        public int RequestCount { get; private set; }
        public string LastHeaderKeys { get; private set; } = string.Empty;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            LastHeaderKeys = string.Join(",", request.Headers.Select(h => h.Key));
            var response = responses[Math.Min(index, responses.Length - 1)];
            index++;
            return Task.FromResult(response);
        }
    }

    private sealed class StubMatcher(Guid? caseId) : IWhatsAppToCaseMatcher
    {
        public Task<WhatsAppToCaseMatchResult> MatchAsync(
            WhatsAppInboundMediaMessage message,
            TenantScope scope,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new WhatsAppToCaseMatchResult(caseId, caseId is null ? "no_match" : "case_id", caseId is null));
    }

    private sealed class StubCaseRepository(ClaimCase? claimCase = null) : ICaseRepository
    {
        public Task SaveAsync(ClaimCase entity, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<ClaimCase?> GetAsync(Guid caseId, CancellationToken cancellationToken = default) =>
            Task.FromResult(claimCase is not null && claimCase.CaseId == caseId ? claimCase : null);
        public Task<IReadOnlyCollection<ClaimCase>> ListAsync(TenantScope scope, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<ClaimCase>>(claimCase is null ? [] : [claimCase]);
    }

    private sealed class StubDocumentRepository : ICaseDocumentRepository
    {
        public List<CaseDocument> Saved { get; } = [];
        public Task SaveAsync(CaseDocument document, CancellationToken cancellationToken = default)
        {
            Saved.Add(document);
            return Task.CompletedTask;
        }

        public Task<CaseDocument?> GetAsync(Guid caseId, Guid documentId, TenantScope scope, CancellationToken cancellationToken = default) =>
            Task.FromResult<CaseDocument?>(null);

        public Task<IReadOnlyCollection<CaseDocument>> ListForCaseAsync(Guid caseId, TenantScope scope, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<CaseDocument>>(Saved);
    }

    private sealed class StubAuditRepository : IAuditRepository
    {
        public Task WriteAsync(InsuranceIntelligence.Domain.Audit.AuditEvent auditEvent, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
        public Task<IReadOnlyCollection<InsuranceIntelligence.Domain.Audit.AuditEvent>> ListForCaseAsync(Guid caseId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<InsuranceIntelligence.Domain.Audit.AuditEvent>>([]);
        public Task<IReadOnlyCollection<InsuranceIntelligence.Domain.Audit.AuditEvent>> ListAsync(TenantScope scope, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<InsuranceIntelligence.Domain.Audit.AuditEvent>>([]);
    }

    private sealed class CapturingStorageProvider : IDocumentStorageProvider
    {
        public DocumentStorageRequest? LastRequest { get; private set; }
        public Task<DocumentStorageResult> StoreAsync(DocumentStorageRequest request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return Task.FromResult(new DocumentStorageResult($"blob/{request.DocumentId:N}", request.ContentType, request.FileSizeBytes));
        }
    }

    private sealed class StubProcessingProvider : IDocumentProcessingProvider
    {
        public Task<DocumentProcessingResult> ProcessAsync(DocumentProcessingRequest request, CancellationToken cancellationToken = default)
        {
            var now = DateTimeOffset.UtcNow;
            return Task.FromResult(new DocumentProcessingResult(
                new DocumentClassificationResult(DocumentType.PhotoEvidence, 0.9m, "Stub", now),
                new DocumentExtractionResult(
                    new Dictionary<string, string>(),
                    new Dictionary<string, decimal>(),
                    "Stub",
                    now,
                    Approved: false,
                    ApprovedByUserId: null,
                    ApprovedAt: null),
                Array.Empty<DocumentValidationWarning>()));
        }
    }
}
