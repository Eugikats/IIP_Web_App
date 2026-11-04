using InsuranceIntelligence.Application.Integrations;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace InsuranceIntelligence.Functions;

/// <summary>
/// Collects inbound mail on a schedule, because nothing else ever did.
///
/// Microsoft Graph change notifications would be the push-based alternative, but this codebase has
/// no working subscription registration or renewal, and IMAP has no push at all. A timer needs no
/// public endpoint, no subscription lifecycle and no three-day renewal, so it is the smaller and
/// more reliable way to make a connected mailbox actually deliver.
///
/// Every workspace that has a mailbox switched on is visited (build-loop item 28), each in its own
/// scope, Microsoft 365 and IMAP alike. A workspace that fails is skipped and counted so one
/// insurer's broken mailbox never stops another's mail.
/// </summary>
public class MailboxIngestionTimerFunction(
    IMailboxIngestionRunner runner,
    ILogger<MailboxIngestionTimerFunction> logger)
{
    private const string SystemActorId = "system:mailbox-ingestion";

    // Every five minutes. Email is not a real-time channel and a customer emailing a claim does not
    // expect an instant answer, but a window measured in hours would make the inbox look broken.
    [Function("MailboxIngestion")]
    public async Task Run([TimerTrigger("0 */5 * * * *")] TimerInfo timer, CancellationToken cancellationToken)
    {
        var correlationId = $"mailbox-ingestion-{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}";
        var result = await runner.RunAllAsync(correlationId, SystemActorId, cancellationToken);
        if (result.MessagesIngested > 0 || result.WorkspacesFailed > 0)
        {
            logger.LogInformation(
                "Mailbox ingestion: {Ingested} new message(s) across {Visited} workspace(s); {Failed} workspace(s) failed. CorrelationId={CorrelationId}",
                result.MessagesIngested, result.WorkspacesVisited, result.WorkspacesFailed, correlationId);
        }
    }
}
