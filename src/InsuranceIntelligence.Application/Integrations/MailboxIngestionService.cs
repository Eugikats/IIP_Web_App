using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Integrations;

namespace InsuranceIntelligence.Application.Integrations;

/// <summary>What one ingestion pass actually did. Honest about the difference between "nothing was
/// waiting" and "nothing could be collected", a mailbox the provider refused is reported, not
/// folded into a zero.</summary>
public sealed record MailboxIngestionResult(
    int MailboxesPolled,
    int MessagesIngested,
    int AlreadyPresent,
    IReadOnlyList<string> MailboxesUnavailable)
{
    public static MailboxIngestionResult Nothing { get; } = new(0, 0, 0, []);
}

public interface IMailboxIngestionService
{
    Task<MailboxIngestionResult> IngestAsync(
        TenantScope scope,
        string correlationId,
        string actorUserId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Collects mail from every ingestion-enabled mailbox in a workspace and lands it in the inbound
/// store, which is what the Channels inbox reads.
///
/// This exists because nothing else did it. `EmailIntegrationEndpoints.GraphWebhook` only answers
/// Microsoft's subscription-validation handshake, it never pulls the messages a change
/// notification refers to, and the one real pull path (`TestIngestion`) is mapped only when
/// development diagnostics are enabled, so it does not exist in Staging or Production at all.
/// The practical effect was that an insurer could connect a mailbox in a deployed environment and
/// no email would ever appear, with no error to explain why.
///
/// Deliberately stops at ingestion. Turning an email into a claim stays where it already is, a
/// permissioned human action with a recorded reason (`ProcessMessage`), because opening claims
/// automatically from unattended mail is a decision about human authority, not a gap in plumbing.
/// An ingested-but-unprocessed email shows in the inbox with "Process into a claim" against it,
/// which is the honest state: it arrived, and a person still owes it something.
///
/// Idempotent by construction: GraphMailProvider derives each message's id as a stable hash of
/// tenant + mailbox + provider message id, so re-polling the same message yields the same id and
/// is skipped rather than duplicated.
/// </summary>
public sealed class MailboxIngestionService(
    IMailboxConnectionRepository mailboxRepository,
    IInboundEmailRepository emailRepository,
    IGraphMailProvider graphMailProvider,
    IAuditRepository auditRepository,
    // Optional: without it an IMAP mailbox is reported as unreachable instead of being read (build-loop item 28).
    IImapMailProvider? imapMailProvider = null) : IMailboxIngestionService
{
    private static readonly TimeSpan StatusRefreshAfter = TimeSpan.FromMinutes(30);

    public async Task<MailboxIngestionResult> IngestAsync(
        TenantScope scope,
        string correlationId,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        var mailboxes = (await mailboxRepository.ListAsync(scope, cancellationToken))
            .Where(mailbox => mailbox.IngestionEnabled)
            .ToArray();
        if (mailboxes.Length == 0)
        {
            return MailboxIngestionResult.Nothing;
        }

        var known = (await emailRepository.ListAsync(scope, cancellationToken))
            .Select(message => message.MessageId)
            .ToHashSet();

        var ingested = 0;
        var alreadyPresent = 0;
        var unavailable = new List<string>();

        foreach (var mailbox in mailboxes)
        {
            IReadOnlyCollection<InboundEmailMessage> pulled;
            try
            {
                pulled = mailbox.Source == MailboxSource.Imap
                    ? imapMailProvider is null
                        ? throw new InvalidOperationException("IMAP mailboxes are not available in this deployment.")
                        : await imapMailProvider.PullMessagesAsync(mailbox, cancellationToken)
                    : await graphMailProvider.PullMessagesAsync(mailbox, cancellationToken);
            }
            catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException or IOException or System.Security.Authentication.AuthenticationException or TimeoutException)
            {
                // One misconfigured or unreachable mailbox must not stop the others from being
                // collected, and must not look like an empty inbox either.
                unavailable.Add(mailbox.MailboxAddress);
                await RecordStatusAsync(mailbox, "CouldNotConnect", cancellationToken);
                continue;
            }

            foreach (var message in pulled)
            {
                if (!known.Add(message.MessageId))
                {
                    alreadyPresent++;
                    continue;
                }

                await emailRepository.SaveAsync(message, cancellationToken);
                ingested++;
            }

            await RecordStatusAsync(mailbox, "Collected", cancellationToken);
        }

        if (ingested > 0 || unavailable.Count > 0)
        {
            await auditRepository.WriteAsync(
                new AuditEvent(
                    Guid.NewGuid(), scope, null, null, actorUserId, ActorType.Integration,
                    "email.ingestion_run", DateTimeOffset.UtcNow,
                    null, null, [], "GraphMail", null,
                    unavailable.Count == 0 ? "accepted" : "partial", null,
                    $"Collected {ingested} new message(s) from {mailboxes.Length} mailbox(es); {alreadyPresent} already present."
                    + (unavailable.Count == 0
                        ? string.Empty
                        : $" Could not reach: {string.Join(", ", unavailable)}.")
                    + " Message bodies omitted from evidence.",
                    correlationId),
                cancellationToken);
        }

        return new MailboxIngestionResult(mailboxes.Length, ingested, alreadyPresent, unavailable);
    }

    /// <summary>Remembers how the last pass went, so an administrator can see a mailbox that has stopped working. Written only when
    /// the answer changes or the note is old, so a healthy mailbox does not cost a write every few minutes.</summary>
    private async Task RecordStatusAsync(MailboxConnection mailbox, string status, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        if (mailbox.LastCheckStatus == status && mailbox.LastCheckedAt is { } last && now - last < StatusRefreshAfter)
        {
            return;
        }

        try
        {
            await mailboxRepository.SaveAsync(mailbox with { LastCheckedAt = now, LastCheckStatus = status }, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // The note is a convenience; failing to write it must not undo a collection that worked.
        }
    }
}

public sealed record MailboxRunResult(int WorkspacesVisited, int MessagesIngested, int WorkspacesFailed);

public interface IMailboxIngestionRunner
{
    /// <summary>Collects mail for every workspace that has a mailbox switched on.</summary>
    Task<MailboxRunResult> RunAllAsync(string correlationId, string actorUserId, CancellationToken cancellationToken = default);
}

/// <summary>
/// The scheduled collection (build-loop item 28). It used to read one workspace, the deployment's own. It now visits every
/// workspace with a mailbox switched on, one at a time, each in its own scope; a workspace that fails is counted and skipped so
/// one insurer's broken mailbox never stops another's mail.
/// </summary>
public sealed class MailboxIngestionRunner(
    IMailboxConnectionRepository mailboxRepository,
    IMailboxIngestionService ingestionService) : IMailboxIngestionRunner
{
    public async Task<MailboxRunResult> RunAllAsync(string correlationId, string actorUserId, CancellationToken cancellationToken = default)
    {
        var visited = 0;
        var ingested = 0;
        var failed = 0;
        foreach (var scope in await mailboxRepository.ListScopesWithIngestionAsync(cancellationToken))
        {
            visited++;
            try
            {
                var result = await ingestionService.IngestAsync(scope, correlationId, actorUserId, cancellationToken);
                ingested += result.MessagesIngested;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                failed++;
            }
        }

        return new MailboxRunResult(visited, ingested, failed);
    }
}
