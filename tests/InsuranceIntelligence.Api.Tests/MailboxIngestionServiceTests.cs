using InsuranceIntelligence.Application.Integrations;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Integrations;

namespace InsuranceIntelligence.Api.Tests;

/// <summary>
/// Covers the path that previously did not exist at all: mail arriving in the inbound store without
/// a human pressing anything. Before this service, a deployed environment had no working inbound
/// email route — the Graph webhook only answered the subscription handshake, and the one real pull
/// endpoint was mapped only when development diagnostics were on.
/// </summary>
public sealed class MailboxIngestionServiceTests
{
    private static readonly TenantScope Scope = TenantScope.Create("tenant-mail", "workspace-claims");

    [Fact]
    public async Task IngestAsync_CollectsFromEveryEnabledMailboxAndLandsMessagesInTheInboundStore()
    {
        var enabled = Mailbox("claims@insurer.test", ingestionEnabled: true);
        var alsoEnabled = Mailbox("motor@insurer.test", ingestionEnabled: true);
        var emails = new FakeInboundEmailRepository();
        var provider = new FakeGraphMailProvider
        {
            ByMailbox =
            {
                ["claims@insurer.test"] = [Message(enabled, "ext-1", "Accident yesterday")],
                ["motor@insurer.test"] = [Message(alsoEnabled, "ext-2", "Windscreen damage")],
            }
        };
        var service = new MailboxIngestionService(
            new FakeMailboxRepository(enabled, alsoEnabled), emails, provider, new FakeAuditRepository());

        var result = await service.IngestAsync(Scope, "corr-1", "system:test");

        Assert.Equal(2, result.MailboxesPolled);
        Assert.Equal(2, result.MessagesIngested);
        Assert.Equal(2, (await emails.ListAsync(Scope)).Count);
    }

    [Fact]
    public async Task IngestAsync_SkipsMailboxesWithIngestionTurnedOff()
    {
        var off = Mailbox("archive@insurer.test", ingestionEnabled: false);
        var provider = new FakeGraphMailProvider
        {
            ByMailbox = { ["archive@insurer.test"] = [Message(off, "ext-9", "Should never be collected")] }
        };
        var emails = new FakeInboundEmailRepository();
        var service = new MailboxIngestionService(
            new FakeMailboxRepository(off), emails, provider, new FakeAuditRepository());

        var result = await service.IngestAsync(Scope, "corr-2", "system:test");

        Assert.Equal(0, result.MailboxesPolled);
        Assert.Empty(await emails.ListAsync(Scope));
        Assert.Empty(provider.Polled);
    }

    [Fact]
    public async Task IngestAsync_RunningTwiceDoesNotDuplicateTheSameMessage()
    {
        // The timer re-polls every few minutes and Graph keeps returning unread mail until it is
        // moved, so a second pass over the same message must be a no-op, not a second inbox row.
        var mailbox = Mailbox("claims@insurer.test", ingestionEnabled: true);
        var emails = new FakeInboundEmailRepository();
        var provider = new FakeGraphMailProvider
        {
            ByMailbox = { ["claims@insurer.test"] = [Message(mailbox, "ext-1", "Accident yesterday")] }
        };
        var service = new MailboxIngestionService(
            new FakeMailboxRepository(mailbox), emails, provider, new FakeAuditRepository());

        await service.IngestAsync(Scope, "corr-1", "system:test");
        var second = await service.IngestAsync(Scope, "corr-2", "system:test");

        Assert.Equal(0, second.MessagesIngested);
        Assert.Equal(1, second.AlreadyPresent);
        Assert.Single(await emails.ListAsync(Scope));
    }

    [Fact]
    public async Task IngestAsync_OneUnreachableMailboxDoesNotStopTheOthersAndIsReportedNotSilentlyZero()
    {
        var broken = Mailbox("broken@insurer.test", ingestionEnabled: true);
        var working = Mailbox("claims@insurer.test", ingestionEnabled: true);
        var emails = new FakeInboundEmailRepository();
        var provider = new FakeGraphMailProvider
        {
            Unreachable = { "broken@insurer.test" },
            ByMailbox = { ["claims@insurer.test"] = [Message(working, "ext-1", "Accident yesterday")] }
        };
        var audit = new FakeAuditRepository();
        var service = new MailboxIngestionService(new FakeMailboxRepository(broken, working), emails, provider, audit);

        var result = await service.IngestAsync(Scope, "corr-3", "system:test");

        Assert.Equal(1, result.MessagesIngested);
        Assert.Equal("broken@insurer.test", Assert.Single(result.MailboxesUnavailable));
        // A partial run must be distinguishable from a clean one in the audit trail, or an operator
        // reading "collected 1 message" has no way to know a mailbox was silently skipped.
        Assert.Equal("partial", Assert.Single(audit.Events).ValidationResult);
    }

    [Fact]
    public async Task IngestAsync_NoMailboxesConfigured_IsAQuietNoOpWithNoAuditNoise()
    {
        var audit = new FakeAuditRepository();
        var service = new MailboxIngestionService(
            new FakeMailboxRepository(), new FakeInboundEmailRepository(), new FakeGraphMailProvider(), audit);

        var result = await service.IngestAsync(Scope, "corr-4", "system:test");

        Assert.Equal(0, result.MailboxesPolled);
        // The timer runs every five minutes forever; an insurer who has connected no mailbox must
        // not accumulate an audit event per tick saying nothing happened.
        Assert.Empty(audit.Events);
    }

    [Fact]
    public async Task IngestAsync_NothingWaiting_WritesNoAuditEventEither()
    {
        var mailbox = Mailbox("claims@insurer.test", ingestionEnabled: true);
        var audit = new FakeAuditRepository();
        var service = new MailboxIngestionService(
            new FakeMailboxRepository(mailbox), new FakeInboundEmailRepository(), new FakeGraphMailProvider(), audit);

        var result = await service.IngestAsync(Scope, "corr-5", "system:test");

        Assert.Equal(1, result.MailboxesPolled);
        Assert.Equal(0, result.MessagesIngested);
        Assert.Empty(audit.Events);
    }

    [Fact]
    public async Task IngestAsync_IngestedMessagesArriveUnprocessed_SoAPersonStillDecidesWhetherItIsAClaim()
    {
        // Ingestion deliberately stops short of opening claims: that remains a permissioned human
        // action. This asserts the boundary rather than assuming it.
        var mailbox = Mailbox("claims@insurer.test", ingestionEnabled: true);
        var emails = new FakeInboundEmailRepository();
        var service = new MailboxIngestionService(
            new FakeMailboxRepository(mailbox),
            emails,
            new FakeGraphMailProvider
            {
                ByMailbox = { ["claims@insurer.test"] = [Message(mailbox, "ext-1", "Accident yesterday")] }
            },
            new FakeAuditRepository());

        await service.IngestAsync(Scope, "corr-6", "system:test");

        var stored = Assert.Single(await emails.ListAsync(Scope));
        Assert.Equal(EmailProcessingStatus.Received, stored.ProcessingStatus);
        Assert.Null(stored.LinkedCaseId);
    }

    private static MailboxConnection Mailbox(string address, bool ingestionEnabled) => new()
    {
        MailboxConnectionId = Guid.NewGuid(),
        Scope = Scope,
        DisplayName = address,
        MailboxAddress = address,
        IngestionEnabled = ingestionEnabled,
        DefaultChannel = "email",
        CreatedAt = DateTimeOffset.UtcNow,
    };

    private static InboundEmailMessage Message(MailboxConnection mailbox, string externalId, string subject) => new()
    {
        // Mirrors GraphMailProvider's stable-id derivation, which is what makes re-polling idempotent.
        MessageId = StableId($"{mailbox.Scope.TenantId}:{mailbox.MailboxAddress}:{externalId}"),
        Scope = mailbox.Scope,
        MailboxConnectionId = mailbox.MailboxConnectionId,
        ExternalMessageId = externalId,
        Sender = "claimant@example.test",
        Subject = subject,
        BodyPreview = "Details of the loss.",
        ReceivedAt = DateTimeOffset.UtcNow,
        ProcessingStatus = EmailProcessingStatus.Received,
        Attachments = [],
    };

    private static Guid StableId(string value)
    {
        var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value));
        return new Guid(hash.AsSpan(0, 16));
    }

    private sealed class FakeMailboxRepository(params MailboxConnection[] mailboxes) : IMailboxConnectionRepository
    {
        public Task SaveAsync(MailboxConnection mailbox, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<IReadOnlyCollection<MailboxConnection>> ListAsync(TenantScope scope, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<MailboxConnection>>(mailboxes.Where(m => m.Scope.Matches(scope)).ToArray());

        public Task<MailboxConnection?> GetAsync(Guid id, TenantScope scope, CancellationToken cancellationToken = default) =>
            Task.FromResult(mailboxes.FirstOrDefault(m => m.MailboxConnectionId == id && m.Scope.Matches(scope)));
    }

    private sealed class FakeInboundEmailRepository : IInboundEmailRepository
    {
        private readonly List<InboundEmailMessage> stored = [];

        public Task SaveAsync(InboundEmailMessage message, CancellationToken cancellationToken = default)
        {
            stored.RemoveAll(m => m.MessageId == message.MessageId);
            stored.Add(message);
            return Task.CompletedTask;
        }

        public Task<InboundEmailMessage?> GetAsync(Guid messageId, TenantScope scope, CancellationToken cancellationToken = default) =>
            Task.FromResult(stored.FirstOrDefault(m => m.MessageId == messageId && m.Scope.Matches(scope)));

        public Task<IReadOnlyCollection<InboundEmailMessage>> ListAsync(TenantScope scope, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<InboundEmailMessage>>(stored.Where(m => m.Scope.Matches(scope)).ToArray());
    }

    private sealed class FakeGraphMailProvider : IGraphMailProvider
    {
        public Dictionary<string, InboundEmailMessage[]> ByMailbox { get; } = [];
        public HashSet<string> Unreachable { get; } = [];
        public List<string> Polled { get; } = [];

        public Task<IReadOnlyCollection<InboundEmailMessage>> PullMessagesAsync(MailboxConnection mailbox, CancellationToken cancellationToken = default)
        {
            Polled.Add(mailbox.MailboxAddress);
            if (Unreachable.Contains(mailbox.MailboxAddress))
            {
                throw new InvalidOperationException("Graph mail provider is not configured.");
            }

            return Task.FromResult<IReadOnlyCollection<InboundEmailMessage>>(
                ByMailbox.TryGetValue(mailbox.MailboxAddress, out var messages) ? messages : []);
        }

        public Task CompleteMessageAsync(MailboxConnection mailbox, InboundEmailMessage message, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class FakeAuditRepository : IAuditRepository
    {
        public List<AuditEvent> Events { get; } = [];

        public Task WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default)
        {
            Events.Add(auditEvent);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyCollection<AuditEvent>> ListForCaseAsync(Guid caseId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<AuditEvent>>(Events.Where(e => e.CaseId == caseId).ToArray());

        public Task<IReadOnlyCollection<AuditEvent>> ListAsync(TenantScope scope, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<AuditEvent>>(Events.Where(e => e.Scope.Matches(scope)).ToArray());
    }
}
