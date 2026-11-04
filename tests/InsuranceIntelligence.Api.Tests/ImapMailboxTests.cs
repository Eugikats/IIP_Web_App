using System.Net;
using InsuranceIntelligence.Application.Integrations;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Integrations;
using InsuranceIntelligence.Infrastructure.Configuration;
using InsuranceIntelligence.Infrastructure.Email;
using InsuranceIntelligence.LocalTestDoubles;

namespace InsuranceIntelligence.Api.Tests;

/// <summary>
/// Mail collection for every workspace, and IMAP for insurers without Microsoft 365 (build-loop item 28): which hosts may be
/// reached, how an IMAP mailbox becomes inbound messages, how a scheduled pass visits each workspace on its own, and that one
/// workspace's mail and password never reach another.
/// </summary>
public sealed class ImapMailboxTests
{
    private static readonly TenantScope ScopeA = TenantScope.Create("tenant-a", "workspace-a");
    private static readonly TenantScope ScopeB = TenantScope.Create("tenant-b", "workspace-b");
    private static readonly TenantScope ScopeC = TenantScope.Create("tenant-c", "workspace-c");

    private static MailboxConnection Imap(TenantScope scope, string address = "claims@insurer.test", bool enabled = true, string? folder = null) =>
        new()
        {
            MailboxConnectionId = Guid.NewGuid(),
            Scope = scope,
            DisplayName = "Claims",
            MailboxAddress = address,
            IngestionEnabled = enabled,
            DefaultChannel = "Email",
            CreatedAt = DateTimeOffset.UtcNow,
            Source = MailboxSource.Imap,
            ImapHost = "imap.insurer.test",
            ImapPort = 993,
            ImapUsername = address,
            EncryptedImapPassword = "protected:s3cret-" + scope.TenantId,
            ProcessedFolderName = folder,
        };

    // ---- which hosts a workspace may point the platform at ----

    [Theory]
    [InlineData("imap.example.com", true)]
    [InlineData("mail-1.example.co.ug", true)]
    [InlineData("203.0.113.9", true)]
    [InlineData("", false)]
    [InlineData("imap.example.com:993", false)]
    [InlineData("https://imap.example.com", false)]
    [InlineData("imap.example.com/inbox", false)]
    [InlineData("imap example.com", false)]
    [InlineData("imap..example.com", false)]
    public void AHostMustBeAPlainHostName(string host, bool allowed)
    {
        Assert.Equal(allowed, ImapEndpointPolicy.ValidateHostSyntax(host) is null);
    }

    [Theory]
    [InlineData("8.8.8.8", true)]
    [InlineData("203.0.113.9", true)]
    [InlineData("127.0.0.1", false)]
    [InlineData("10.0.0.5", false)]
    [InlineData("172.16.4.4", false)]
    [InlineData("172.32.4.4", true)]
    [InlineData("192.168.1.10", false)]
    [InlineData("169.254.169.254", false)]
    [InlineData("100.64.0.1", false)]
    [InlineData("0.0.0.0", false)]
    [InlineData("224.0.0.1", false)]
    [InlineData("::1", false)]
    [InlineData("fe80::1", false)]
    [InlineData("fd00::1", false)]
    [InlineData("::ffff:10.0.0.5", false)]
    [InlineData("2606:4700:4700::1111", true)]
    public void OnlyPublicInternetAddressesAreReachable(string address, bool isPublic)
    {
        Assert.Equal(isPublic, ImapEndpointPolicy.IsPublic(IPAddress.Parse(address)));
    }

    [Fact]
    public async Task AProtectedRuntimeRefusesAHostThatIsNotOnThePublicInternet()
    {
        var policy = new ImapEndpointPolicy(allowPrivateNetworks: false);

        await Assert.ThrowsAsync<ImapEndpointRefusedException>(() => policy.ResolveAsync("127.0.0.1", CancellationToken.None));
        await Assert.ThrowsAsync<ImapEndpointRefusedException>(() => policy.ResolveAsync("169.254.169.254", CancellationToken.None));
        await Assert.ThrowsAsync<ImapEndpointRefusedException>(() => policy.ResolveAsync("localhost", CancellationToken.None));
        Assert.Equal([IPAddress.Parse("8.8.8.8")], await policy.ResolveAsync("8.8.8.8", CancellationToken.None));
    }

    [Fact]
    public async Task ADeveloperRuntimeMayReachALocalTestMailServer()
    {
        var policy = new ImapEndpointPolicy(allowPrivateNetworks: true);

        Assert.Equal([IPAddress.Parse("127.0.0.1")], await policy.ResolveAsync("127.0.0.1", CancellationToken.None));
    }

    // ---- an IMAP mailbox becomes inbound messages ----

    [Fact]
    public async Task AnImapMailboxIsReadWithItsOwnDecryptedLogin_AndTheMessageGetsAStableIdOwnedByTheWorkspace()
    {
        var client = new FakeImapClient();
        client.Messages.Add(new ImapMailboxMessage("77:5", "<id@x>", "customer@example.test", "Accident", "A taxi hit me\r\nnear Kira", DateTimeOffset.UtcNow,
            [new ImapMailboxAttachment("2", "police.pdf", "application/pdf", 1234)]));
        var provider = new ImapMailProvider(new InfrastructureOptions(), client, new FakeConnectionProtector());
        var mailbox = Imap(ScopeA);

        var first = Assert.Single(await provider.PullMessagesAsync(mailbox));
        var again = Assert.Single(await provider.PullMessagesAsync(mailbox));

        Assert.Equal(("imap.insurer.test", 993, "claims@insurer.test", "s3cret-tenant-a"), client.LastSettings is { } s ? (s.Host, s.Port, s.Username, s.Password) : default);
        Assert.Equal(first.MessageId, again.MessageId);
        Assert.Equal(ScopeA, first.Scope);
        Assert.Equal("A taxi hit me near Kira", first.BodyPreview);
        Assert.Equal("77:5", first.ExternalMessageId);
        var attachment = Assert.Single(first.Attachments);
        Assert.Equal("2", attachment.ExternalAttachmentId);
        Assert.Equal(first.MessageId, attachment.MessageId);

        // The same mailbox address and message in another workspace is a different message.
        var other = Assert.Single(await provider.PullMessagesAsync(Imap(ScopeB)));
        Assert.NotEqual(first.MessageId, other.MessageId);
        Assert.Equal(ScopeB, other.Scope);
    }

    [Fact]
    public async Task AMailboxThatIsNotImapOrIsSwitchedOffIsNotReadByTheImapProvider()
    {
        var client = new FakeImapClient();
        var provider = new ImapMailProvider(new InfrastructureOptions(), client, new FakeConnectionProtector());

        Assert.Empty(await provider.PullMessagesAsync(Imap(ScopeA, enabled: false)));
        Assert.Empty(await provider.PullMessagesAsync(Imap(ScopeA) with { Source = MailboxSource.Graph }));
        Assert.Equal(0, client.ReadCalls);
    }

    [Fact]
    public async Task AMailboxMissingItsLoginCannotBeRead()
    {
        var provider = new ImapMailProvider(new InfrastructureOptions(), new FakeImapClient(), new FakeConnectionProtector());

        await Assert.ThrowsAsync<InvalidOperationException>(() => provider.PullMessagesAsync(Imap(ScopeA) with { EncryptedImapPassword = null }));
    }

    [Fact]
    public async Task ProcessingAMessageMarksItReadAndMovesItToTheMailboxsFolder()
    {
        var client = new FakeImapClient();
        var provider = new ImapMailProvider(new InfrastructureOptions(), client, new FakeConnectionProtector());
        var mailbox = Imap(ScopeA, folder: "Processed by IIP");
        var message = Assert.Single(await new ImapMailProvider(new InfrastructureOptions(), new FakeImapClient { Messages = { new ImapMailboxMessage("1:9", null, "a@b.test", "s", "b", DateTimeOffset.UtcNow, []) } }, new FakeConnectionProtector()).PullMessagesAsync(mailbox));

        await provider.CompleteMessageAsync(mailbox, message);

        Assert.Equal(("1:9", "Processed by IIP"), client.Marked.Single());
    }

    // ---- a scheduled pass visits every workspace ----

    [Fact]
    public async Task TheScheduledPassVisitsEveryWorkspaceWithAMailboxSwitchedOn_AndEachWorkspacesMailStaysInItsOwn()
    {
        var mailboxes = new InMemoryMailboxConnectionRepository();
        var emails = new InMemoryInboundEmailRepository();
        await mailboxes.SaveAsync(Imap(ScopeA));
        await mailboxes.SaveAsync(Imap(ScopeB, "claims@other.test"));
        await mailboxes.SaveAsync(Imap(ScopeC, "off@third.test", enabled: false));
        var client = new FakeImapClient();
        client.Messages.Add(new ImapMailboxMessage("1:1", null, "customer@example.test", "Hello", "Body", DateTimeOffset.UtcNow, []));
        var ingestion = new MailboxIngestionService(
            mailboxes, emails, new FakeGraphProvider(), new InMemoryAuditRepository(),
            new ImapMailProvider(new InfrastructureOptions(), client, new FakeConnectionProtector()));
        var runner = new MailboxIngestionRunner(mailboxes, ingestion);

        var result = await runner.RunAllAsync("corr", "system:test");

        Assert.Equal(new MailboxRunResult(2, 2, 0), result);
        Assert.Single(await emails.ListAsync(ScopeA));
        Assert.Single(await emails.ListAsync(ScopeB));
        Assert.Empty(await emails.ListAsync(ScopeC));
        Assert.NotEqual((await emails.ListAsync(ScopeA)).Single().MessageId, (await emails.ListAsync(ScopeB)).Single().MessageId);
    }

    [Fact]
    public async Task OneWorkspacesBrokenMailboxDoesNotStopAnothersMail_AndTheBrokenOneIsMarked()
    {
        var mailboxes = new InMemoryMailboxConnectionRepository();
        var emails = new InMemoryInboundEmailRepository();
        var broken = Imap(ScopeA);
        var working = Imap(ScopeB, "claims@other.test");
        await mailboxes.SaveAsync(broken);
        await mailboxes.SaveAsync(working);
        var client = new FakeImapClient { FailFor = "tenant-a" };
        client.Messages.Add(new ImapMailboxMessage("1:1", null, "customer@example.test", "Hello", "Body", DateTimeOffset.UtcNow, []));
        var ingestion = new MailboxIngestionService(
            mailboxes, emails, new FakeGraphProvider(), new InMemoryAuditRepository(),
            new ImapMailProvider(new InfrastructureOptions(), client, new FakeConnectionProtector()));

        var result = await new MailboxIngestionRunner(mailboxes, ingestion).RunAllAsync("corr", "system:test");

        Assert.Equal(1, result.MessagesIngested);
        Assert.Empty(await emails.ListAsync(ScopeA));
        Assert.Single(await emails.ListAsync(ScopeB));
        Assert.Equal("CouldNotConnect", (await mailboxes.GetAsync(broken.MailboxConnectionId, ScopeA))!.LastCheckStatus);
        Assert.Equal("Collected", (await mailboxes.GetAsync(working.MailboxConnectionId, ScopeB))!.LastCheckStatus);
    }

    [Fact]
    public async Task AWorkspaceThatThrowsOutrightIsCountedAndSkipped()
    {
        var mailboxes = new InMemoryMailboxConnectionRepository();
        await mailboxes.SaveAsync(Imap(ScopeA));
        await mailboxes.SaveAsync(Imap(ScopeB, "claims@other.test"));
        var runner = new MailboxIngestionRunner(mailboxes, new ThrowingForScope(ScopeA));

        var result = await runner.RunAllAsync("corr", "system:test");

        Assert.Equal(new MailboxRunResult(2, 1, 1), result);
    }

    [Fact]
    public async Task AnImapMailboxIsUnavailableWhenTheDeploymentHasNoImapProvider_NotSilentlyEmpty()
    {
        var mailboxes = new InMemoryMailboxConnectionRepository();
        await mailboxes.SaveAsync(Imap(ScopeA));
        var service = new MailboxIngestionService(mailboxes, new InMemoryInboundEmailRepository(), new FakeGraphProvider(), new InMemoryAuditRepository());

        var result = await service.IngestAsync(ScopeA, "corr", "system:test");

        Assert.Equal(["claims@insurer.test"], result.MailboxesUnavailable);
    }

    private sealed class FakeImapClient : IImapMailClient
    {
        public List<ImapMailboxMessage> Messages { get; } = [];
        public List<(string Id, string? Folder)> Marked { get; } = [];
        public ImapSettings? LastSettings { get; private set; }
        public int ReadCalls { get; private set; }
        public string? FailFor { get; init; }

        public Task<IReadOnlyCollection<ImapMailboxMessage>> ReadMessagesAsync(ImapSettings settings, int max, CancellationToken cancellationToken = default)
        {
            ReadCalls++;
            LastSettings = settings;
            if (FailFor is not null && settings.Password.EndsWith(FailFor, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("The mail host could not be reached.");
            }

            return Task.FromResult<IReadOnlyCollection<ImapMailboxMessage>>(Messages.ToArray());
        }

        public Task<GraphAttachmentContent> DownloadAttachmentAsync(ImapSettings settings, string externalMessageId, string externalAttachmentId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task MarkProcessedAsync(ImapSettings settings, string externalMessageId, string? folderName, CancellationToken cancellationToken = default)
        {
            Marked.Add((externalMessageId, folderName));
            return Task.CompletedTask;
        }

        public Task<ImapCheckResult> CheckAsync(ImapSettings settings, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ImapCheckResult(true, null));
    }

    private sealed class FakeConnectionProtector : IConnectionSecretProtector
    {
        public string Protect(string plaintext) => "protected:" + plaintext;

        public string Unprotect(string ciphertext) => ciphertext["protected:".Length..];
    }

    private sealed class FakeGraphProvider : IGraphMailProvider
    {
        public Task<IReadOnlyCollection<InboundEmailMessage>> PullMessagesAsync(MailboxConnection mailbox, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<InboundEmailMessage>>([]);

        public Task CompleteMessageAsync(MailboxConnection mailbox, InboundEmailMessage message, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class ThrowingForScope(TenantScope bad) : IMailboxIngestionService
    {
        public Task<MailboxIngestionResult> IngestAsync(TenantScope scope, string correlationId, string actorUserId, CancellationToken cancellationToken = default) =>
            scope == bad ? throw new InvalidOperationException("boom") : Task.FromResult(new MailboxIngestionResult(1, 1, 0, []));
    }
}
