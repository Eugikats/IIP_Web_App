using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using InsuranceIntelligence.Api.Agents;
using InsuranceIntelligence.Api.Jobs;
using InsuranceIntelligence.Application.CustomerAssistant;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Integrations;
using InsuranceIntelligence.Domain.Jobs;

namespace InsuranceIntelligence.Api.Integrations;

public sealed record WhatsAppAssistantRequest(
    TenantScope Scope,
    IReadOnlyList<WhatsAppInboundMessage> Messages,
    string CorrelationId,
    bool AutoReplyEnabled);

/// <summary>
/// Hands inbound WhatsApp messages to the customer assistant without making Meta wait for it, as a durable job
/// (build-loop item 27; it was an in-process queue until then).
///
/// The webhook used to await the assistant inside the request. That was fine while the assistant took a second
/// or two, but translation and the model calls behind it take longer, and Meta only waits a short time for a 200
/// before it retries. Worse, the request's cancellation token is cancelled when the caller disconnects, so a slow
/// assistant could be cancelled halfway through after the inbound message was already saved. The retry then sees a
/// message it has already stored, treats it as a redelivery, and the customer is never answered.
///
/// The message is saved before the job is made, and only messages seen for the first time get one, so a
/// redelivery is neither answered twice nor lost. The job lives in the database in the workspace's own scope, so a
/// message queued in the seconds before an API restart is still answered when the API is back, and a batch that
/// keeps failing is dead-lettered where an administrator can see it (Admin, Background work). The job carries the
/// identifiers of the stored messages and nothing a customer wrote; the words are read back from the message store
/// when the job runs.
/// </summary>
public static class WhatsAppAssistantJobs
{
    /// <summary>Makes the job for a batch of messages that were just stored. Returns false when the job could not
    /// be saved, in which case the caller must handle the batch itself rather than drop a customer's message.</summary>
    public static async Task<bool> TryEnqueueAsync(
        TenantScope scope,
        IReadOnlyList<WhatsAppInboundMessage> newlyStored,
        string correlationId,
        bool autoReplyEnabled,
        IWhatsAppMessageRepository messages,
        IBackgroundJobStore jobs,
        BackgroundPipelineOptions pipeline,
        CancellationToken cancellationToken)
    {
        try
        {
            var ids = new List<Guid>();
            foreach (var inbound in newlyStored)
            {
                var stored = await messages.FindByProviderMessageIdAsync(scope, inbound.ProviderMessageId, cancellationToken);
                if (stored is not null)
                {
                    ids.Add(stored.MessageId);
                }
            }

            if (ids.Count == 0)
            {
                return false;
            }

            var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join(',', ids.Order())))).ToLowerInvariant()[..32];
            var now = DateTimeOffset.UtcNow;
            await jobs.EnqueueAsync(
                new BackgroundJob
                {
                    JobId = Guid.NewGuid(),
                    Scope = scope,
                    Queue = BackgroundQueues.CustomerAssistant,
                    Type = BackgroundJobTypes.CustomerAssistantRespond,
                    Key = key,
                    PayloadJson = JsonSerializer.Serialize(new { messageIds = ids, autoReply = autoReplyEnabled }),
                    Status = BackgroundJobStatus.Pending,
                    // At most three tries: a retry after a reply was already sent could say it twice, and a failure the
                    // assistant could not handle has already been handed to a person by the assistant itself.
                    MaxAttempts = Math.Min(3, pipeline.MaxAttempts),
                    NotBefore = now,
                    CreatedAt = now,
                    UpdatedAt = now,
                    CorrelationId = correlationId,
                },
                coalesceWithRunning: true,
                cancellationToken);
            return true;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return false;
        }
    }
}

public sealed class WhatsAppAssistantJobHandler(ILogger<WhatsAppAssistantJobHandler> logger) : IBackgroundJobHandler
{
    public string Queue => BackgroundQueues.CustomerAssistant;

    public string Type => BackgroundJobTypes.CustomerAssistantRespond;

    public async Task HandleAsync(BackgroundJob job, IServiceProvider services, CancellationToken cancellationToken)
    {
        using var payload = JsonDocument.Parse(job.PayloadJson);
        if (!payload.RootElement.TryGetProperty("messageIds", out var idsElement) || idsElement.ValueKind != JsonValueKind.Array)
        {
            throw new JobFailure("The job does not say which messages to answer.", retry: false);
        }

        var autoReply = payload.RootElement.TryGetProperty("autoReply", out var autoReplyElement) && autoReplyElement.ValueKind == JsonValueKind.True;
        var repository = services.GetRequiredService<IWhatsAppMessageRepository>();
        var messages = new List<WhatsAppInboundMessage>();
        foreach (var idElement in idsElement.EnumerateArray())
        {
            if (!idElement.TryGetGuid(out var id))
            {
                continue;
            }

            // Read back in the job's own workspace, so an identifier from another workspace finds nothing.
            var stored = await repository.GetAsync(job.Scope, id, cancellationToken);
            if (stored is null || stored.Direction != WhatsAppMessageDirection.Inbound || stored.ProviderMessageId is null)
            {
                continue;
            }

            messages.Add(new WhatsAppInboundMessage(
                stored.ProviderMessageId, stored.BusinessPhoneNumberId, stored.ContactWaId, stored.ContentType,
                stored.CustomerSafeBody, stored.ReplyToProviderMessageId, stored.CreatedAt)
            {
                ProfileName = stored.ProfileName,
                MediaId = stored.MediaId,
            });
        }

        if (messages.Count == 0)
        {
            throw new JobFailure("None of the messages this job was made for could be found.", retry: false);
        }

        await WhatsAppAssistantRunner.RunAsync(
            new WhatsAppAssistantRequest(job.Scope, messages, job.CorrelationId, autoReply), services, logger, cancellationToken);
    }
}

/// <summary>Runs the assistant over one batch with a time limit and a way out for the customer when it runs over.
/// Used by the background job, and by the webhook itself in the rare case the job could not be saved, so the two
/// paths cannot drift apart.</summary>
public static class WhatsAppAssistantRunner
{
    /// <summary>Long enough for a slow model and a translation each way, short enough that a hung
    /// dependency cannot hold every later customer behind it for long.</summary>
    public static readonly TimeSpan RunTimeout = TimeSpan.FromMinutes(3);

    public static async Task RunAsync(
        WhatsAppAssistantRequest request,
        IServiceScopeFactory scopeFactory,
        ILogger logger,
        CancellationToken stoppingToken,
        TimeSpan? timeLimit = null)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        await RunAsync(request, scope.ServiceProvider, logger, stoppingToken, timeLimit);
    }

    public static async Task RunAsync(
        WhatsAppAssistantRequest request,
        IServiceProvider services,
        ILogger logger,
        CancellationToken stoppingToken,
        TimeSpan? timeLimit = null)
    {
        var assistant = services.GetRequiredService<ICustomerAssistantService>();

        var budget = timeLimit ?? RunTimeout;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        timeout.CancelAfter(budget);

        try
        {
            var openedCaseIds = await assistant.HandleInboundMessagesAsync(
                request.Scope, request.Messages, request.CorrelationId, request.AutoReplyEnabled, timeout.Token);

            // A claim reported on WhatsApp gets the same immediate agent review as one entered by
            // staff or filed on the claimant portal.
            var triageQueue = services.GetService<IClaimTriageQueue>();
            foreach (var caseId in openedCaseIds)
            {
                if (triageQueue is not null)
                {
                    await triageQueue.EnqueueAsync(request.Scope, caseId, request.CorrelationId, "whatsapp_assistant", stoppingToken);
                }
            }
        }
        catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
        {
            // Ran out of time. The customer must not be left in silence: hand them to a person,
            // with a fresh token because the one that ran out cannot be used to say so.
            logger.LogWarning(
                "Customer assistant timed out after {Timeout} on a WhatsApp batch. TenantId={TenantId} CorrelationId={CorrelationId}",
                budget, request.Scope.TenantId, request.CorrelationId);
            using var escalation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await assistant.EscalateUnhandledAsync(
                request.Scope, request.Messages, request.CorrelationId,
                new TimeoutException("The assistant did not finish in time."), escalation.Token);
        }
    }
}
