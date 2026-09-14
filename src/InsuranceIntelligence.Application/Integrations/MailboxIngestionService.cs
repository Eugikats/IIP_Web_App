using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Integrations;

namespace InsuranceIntelligence.Application.Integrations;

/// <summary>What one ingestion pass actually did. Honest about the difference between "nothing was
/// waiting" and "nothing could be collected" — a mailbox the provider refused is reported, not
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
/// Microsoft's subscription-validation handshake — it never pulls the messages a change
/// notification refers to — and the one real pull path (`TestIngestion`) is mapped only when
/// development diagnostics are enabled, so it does not exist in Staging or Production at all.
/// The practical effect was that an insurer could connect a mailbox in a deployed environment and
/// no email would ever appear, with no error to explain why.
///
/// Deliberately stops at ingestion. Turning an email into a claim stays where it already is — a
/// permissioned human action with a recorded reason (`ProcessMessage`) — because opening claims
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
    IAuditRepository auditRepository) : IMailboxIngestionService
{
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
                pulled = await graphMailProvider.PullMessagesAsync(mailbox, cancellationToken);
            }
            catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException)
            {
                // One misconfigured or unreachable mailbox must not stop the others from being
                // collected, and must not look like an empty inbox either.
                unavailable.Add(mailbox.MailboxAddress);
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
}
