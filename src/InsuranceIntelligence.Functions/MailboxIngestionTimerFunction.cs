using InsuranceIntelligence.Application.Integrations;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Infrastructure.Configuration;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace InsuranceIntelligence.Functions;

/// <summary>
/// Collects inbound mail on a schedule, because nothing else ever did.
///
/// Microsoft Graph change notifications would be the push-based alternative, but this codebase has
/// no working subscription registration or renewal, the only IWebhookSubscriptionProvider
/// registered anywhere is the disabled one, and GraphWebhook answers the validation handshake
/// without ever pulling the messages a notification points at. A timer needs no public endpoint,
/// no subscription lifecycle and no three-day renewal, so it is the smaller and more reliable of
/// the two ways to make a connected mailbox actually deliver.
///
/// Scope comes from the deployment's own TenantId/WorkspaceId, the same single-tenant-per-
/// deployment pattern RetentionEnforcementTimerFunction already uses, there is no cross-tenant
/// enumeration capability in this edition's repositories to iterate instead.
/// </summary>
public class MailboxIngestionTimerFunction(
    IMailboxIngestionService ingestionService,
    InfrastructureOptions options,
    ILogger<MailboxIngestionTimerFunction> logger)
{
    private const string SystemActorId = "system:mailbox-ingestion";

    // Every five minutes. Email is not a real-time channel and a customer emailing a claim does not
    // expect an instant answer, but a window measured in hours would make the inbox look broken.
    [Function("MailboxIngestion")]
    public async Task Run([TimerTrigger("0 */5 * * * *")] TimerInfo timer, CancellationToken cancellationToken)
    {
        if (options.MultiTenant)
        {
            // Shared mailbox settings belong to one deployment, not to any one insurer. Until each
            // insurer connects its own mailbox, SaaS mode collects no email rather than guess whose it is.
            return;
        }

        if (string.IsNullOrWhiteSpace(options.TenantId) || string.IsNullOrWhiteSpace(options.WorkspaceId))
        {
            logger.LogWarning("Mailbox ingestion run skipped: no deployment-level TenantId/WorkspaceId is configured.");
            return;
        }

        if (!options.EmailProvider.Equals("Graph", StringComparison.OrdinalIgnoreCase))
        {
            // Not an error: an insurer who has not connected a mailbox has nothing to collect.
            return;
        }

        var scope = TenantScope.Create(options.TenantId, options.WorkspaceId);
        var correlationId = $"mailbox-ingestion-{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}";

        var result = await ingestionService.IngestAsync(scope, correlationId, SystemActorId, cancellationToken);

        if (result.MessagesIngested > 0 || result.MailboxesUnavailable.Count > 0)
        {
            logger.LogInformation(
                "Mailbox ingestion: {Ingested} new, {AlreadyPresent} already present, from {Polled} mailbox(es). Unavailable: {Unavailable}. CorrelationId={CorrelationId}",
                result.MessagesIngested,
                result.AlreadyPresent,
                result.MailboxesPolled,
                result.MailboxesUnavailable.Count == 0 ? "none" : string.Join(", ", result.MailboxesUnavailable),
                correlationId);
        }
    }
}
