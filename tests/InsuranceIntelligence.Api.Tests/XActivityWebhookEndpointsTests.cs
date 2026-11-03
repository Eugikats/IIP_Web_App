using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using InsuranceIntelligence.Api.Integrations;
using InsuranceIntelligence.Application.Channels;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Channels;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Compliance;
using InsuranceIntelligence.Domain.Integrations;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace InsuranceIntelligence.Api.Tests;

public sealed class XActivityWebhookEndpointsTests
{
    private const string AccountId = "1111111111111111111";
    private const string AppSecret = "x-client-secret-test";
    private const string InboundText = "I need help with my claim";

    [Fact]
    public async Task Challenge_ReturnsProviderCrcResponse()
    {
        var fixture = new Fixture();
        var context = CreateContext();
        context.Request.QueryString = new QueryString("?crc_token=challenge-token");

        var result = await XActivityWebhookEndpoints.Challenge(AccountId, context.Request, fixture.Connections,
            fixture.Protector, CancellationToken.None);

        await result.ExecuteAsync(context);
        context.Response.Body.Position = 0;
        using var json = await JsonDocument.ParseAsync(context.Response.Body);
        var actual = json.RootElement.GetProperty("response_token").GetString();
        var expected = "sha256=" + Convert.ToBase64String(HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(AppSecret), Encoding.UTF8.GetBytes("challenge-token")));
        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task Receive_ValidSignedDm_IsIngestedAndGrantsChannelConsent()
    {
        var fixture = new Fixture();
        var body = Payload(AccountId);
        var context = Request(body, Signature(body));

        var result = await XActivityWebhookEndpoints.Receive(AccountId, context.Request, fixture.Connections,
            fixture.Protector, fixture.Ingestion, fixture.Consents, fixture.Audit, CancellationToken.None);

        await result.ExecuteAsync(context);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        var message = Assert.Single(fixture.Ingestion.Messages);
        Assert.Equal(ChannelValues.X, message.Channel);
        Assert.Equal("2222222222222222222", message.ExternalRecipientId);
        Assert.Equal("2080765901528535096", message.ProviderMessageId);
        Assert.Equal(InboundText, message.CustomerSafeBody);
        Assert.Equal(ConsentPurposes.X, Assert.Single(fixture.Consents.Records).Purpose);
        Assert.Single(fixture.Audit.Events);
    }

    [Fact]
    public async Task Receive_InvalidSignature_IsRejectedBeforeIngestion()
    {
        var fixture = new Fixture();
        var body = Payload(AccountId);
        var context = Request(body, "sha256=not-a-valid-signature");

        var result = await XActivityWebhookEndpoints.Receive(AccountId, context.Request, fixture.Connections,
            fixture.Protector, fixture.Ingestion, fixture.Consents, fixture.Audit, CancellationToken.None);

        await result.ExecuteAsync(context);
        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
        Assert.Empty(fixture.Ingestion.Messages);
    }

    [Fact]
    public async Task Receive_AccountMismatch_IsIgnored()
    {
        var fixture = new Fixture();
        var body = Payload("999999999999999999");
        var context = Request(body, Signature(body));

        var result = await XActivityWebhookEndpoints.Receive(AccountId, context.Request, fixture.Connections,
            fixture.Protector, fixture.Ingestion, fixture.Consents, fixture.Audit, CancellationToken.None);

        await result.ExecuteAsync(context);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.Empty(fixture.Ingestion.Messages);
        Assert.Empty(fixture.Consents.Records);
    }

    [Fact]
    public async Task Receive_OptOutRecordsWithdrawal()
    {
        var fixture = new Fixture();
        var body = Payload(AccountId, "STOP");
        var context = Request(body, Signature(body));

        var result = await XActivityWebhookEndpoints.Receive(AccountId, context.Request, fixture.Connections, fixture.Protector,
            fixture.Ingestion, fixture.Consents, fixture.Audit, CancellationToken.None);
        await result.ExecuteAsync(context);

        var consent = Assert.Single(fixture.Consents.Records);
        Assert.Equal(ConsentBasis.Withdrawn, consent.Basis);
        Assert.NotNull(consent.WithdrawnAt);
    }

    [Fact]
    public async Task Receive_RetriedProviderEvent_DoesNotDuplicateMessageOrConsent()
    {
        var fixture = new Fixture();
        var body = Payload(AccountId);

        var firstContext = Request(body, Signature(body));
        var first = await XActivityWebhookEndpoints.Receive(AccountId, firstContext.Request,
            fixture.Connections, fixture.Protector, fixture.Ingestion, fixture.Consents, fixture.Audit, CancellationToken.None);
        var retryContext = Request(body, Signature(body));
        var retry = await XActivityWebhookEndpoints.Receive(AccountId, retryContext.Request,
            fixture.Connections, fixture.Protector, fixture.Ingestion, fixture.Consents, fixture.Audit, CancellationToken.None);

        await first.ExecuteAsync(firstContext);
        await retry.ExecuteAsync(retryContext);
        Assert.Equal(StatusCodes.Status200OK, firstContext.Response.StatusCode);
        Assert.Equal(StatusCodes.Status200OK, retryContext.Response.StatusCode);
        Assert.Single(fixture.Ingestion.Messages);
        Assert.Single(fixture.Consents.Records);
    }

    private static byte[] Payload(string recipient, string text = InboundText) => Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
    {
        data = new
        {
            event_type = "dm.received",
            filter = new { user_id = AccountId },
            payload = new
            {
                direct_message_events = new[]
                {
                    new
                    {
                        type = "message_create", id = "2080765901528535096", created_timestamp = "1784928224372",
                        message_create = new
                        {
                            target = new { recipient_id = recipient }, sender_id = "2222222222222222222",
                            message_data = new { text },
                        },
                    },
                },
            },
        },
    }));

    private static string Signature(byte[] body) => "sha256=" + Convert.ToBase64String(
        HMACSHA256.HashData(Encoding.UTF8.GetBytes(AppSecret), body));

    private static DefaultHttpContext Request(byte[] body, string signature)
    {
        var context = CreateContext();
        context.TraceIdentifier = "x-hook-test";
        context.Request.Body = new MemoryStream(body);
        context.Request.ContentLength = body.Length;
        context.Request.Headers["X-Twitter-Webhooks-Signature-OAuth2"] = signature;
        return context;
    }

    private static DefaultHttpContext CreateContext()
    {
        var context = new DefaultHttpContext();
        context.RequestServices = new ServiceCollection().AddLogging().AddRouting().BuildServiceProvider();
        context.Response.Body = new MemoryStream();
        return context;
    }

    private sealed class Fixture
    {
        public TenantScope Scope { get; } = TenantScope.Create("tenant-x", "workspace-x");
        public StubProtector Protector { get; } = new();
        public StubConnections Connections { get; }
        public StubIngestion Ingestion { get; } = new();
        public StubConsents Consents { get; } = new();
        public StubAudit Audit { get; } = new();

        public Fixture()
        {
            var settings = JsonSerializer.Serialize(new Dictionary<string, string> { ["clientSecret"] = AppSecret });
            Connections = new StubConnections(new IntegrationConnection
            {
                Scope = Scope, ConnectionType = ConnectionType.X, ProviderKey = "XApi",
                EncryptedSecretSettingsJson = settings, NonSecretSettingsJson = JsonSerializer.Serialize(new Dictionary<string, string> { ["userId"] = AccountId }),
                ConfiguredByUserId = "test-admin", ConfiguredAt = DateTimeOffset.UtcNow,
            });
        }
    }

    private sealed class StubProtector : IConnectionSecretProtector
    {
        public string Protect(string plaintext) => plaintext;
        public string Unprotect(string ciphertext) => ciphertext;
    }

    private sealed class StubConnections(IntegrationConnection connection) : IIntegrationConnectionRepository
    {
        public IntegrationConnection? Saved { get; private set; }
        public Task<IntegrationConnection?> GetAsync(TenantScope scope, ConnectionType connectionType, CancellationToken cancellationToken = default) => Task.FromResult<IntegrationConnection?>(connection);
        public Task<IntegrationConnection?> FindByExternalAccountAsync(ConnectionType connectionType, string accountId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IntegrationConnection?>(connection.ConnectionType == connectionType && accountId == AccountId ? Saved ?? connection : null);
        public Task SaveAsync(IntegrationConnection value, CancellationToken cancellationToken = default) { Saved = value; return Task.CompletedTask; }
        public Task DeleteAsync(TenantScope scope, ConnectionType connectionType, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class StubIngestion : IChannelMessageIngestionService
    {
        public List<ChannelMessage> Messages { get; } = [];
        public Task<bool> IngestInboundAsync(ChannelMessage message, CancellationToken cancellationToken = default)
        {
            if (Messages.Any(existing => existing.AccountId == message.AccountId && existing.ProviderMessageId == message.ProviderMessageId))
                return Task.FromResult(false);
            Messages.Add(message);
            return Task.FromResult(true);
        }
    }

    private sealed class StubConsents : IConsentRepository
    {
        public List<ConsentRecord> Records { get; } = [];
        public Task SaveAsync(ConsentRecord record, CancellationToken cancellationToken = default) { Records.Add(record); return Task.CompletedTask; }
        public Task<ConsentRecord?> GetCurrentAsync(TenantScope scope, string customerIdentifier, string purpose, CancellationToken cancellationToken = default) => Task.FromResult<ConsentRecord?>(Records.LastOrDefault(x => x.CustomerIdentifier == customerIdentifier && x.Purpose == purpose));
        public Task<IReadOnlyCollection<ConsentRecord>> ListForCustomerAsync(TenantScope scope, string customerIdentifier, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyCollection<ConsentRecord>>(Records.Where(x => x.CustomerIdentifier == customerIdentifier).ToArray());
    }

    private sealed class StubAudit : IAuditRepository
    {
        public List<AuditEvent> Events { get; } = [];
        public Task WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default) { Events.Add(auditEvent); return Task.CompletedTask; }
        public Task<IReadOnlyCollection<AuditEvent>> ListForCaseAsync(Guid caseId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyCollection<AuditEvent>>(Events);
        public Task<IReadOnlyCollection<AuditEvent>> ListAsync(TenantScope scope, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyCollection<AuditEvent>>(Events);
    }
}
