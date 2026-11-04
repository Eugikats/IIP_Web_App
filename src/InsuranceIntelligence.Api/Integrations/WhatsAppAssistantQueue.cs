using System.Threading.Channels;
using InsuranceIntelligence.Api.Agents;
using InsuranceIntelligence.Application.CustomerAssistant;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Integrations;

namespace InsuranceIntelligence.Api.Integrations;

public sealed record WhatsAppAssistantRequest(
    TenantScope Scope,
    IReadOnlyList<WhatsAppInboundMessage> Messages,
    string CorrelationId,
    bool AutoReplyEnabled);

/// <summary>
/// Hands inbound WhatsApp messages to the customer assistant without making Meta wait for it.
///
/// The webhook used to await the assistant inside the request. That was fine while the assistant
/// took a second or two, but translation and the model calls behind it take longer, and Meta only
/// waits a short time for a 200 before it retries. Worse, the request's cancellation token is
/// cancelled when the caller disconnects, so a slow assistant could be cancelled halfway through
/// after the inbound message was already saved. The retry then sees a message it has already
/// stored, treats it as a redelivery, and the customer is never answered.
///
/// The message is saved before it reaches this queue, and only messages seen for the first time are
/// enqueued, so a redelivery is neither answered twice nor lost. In-process rather than Service Bus
/// for the same reason as the claim review queue once was. The cost is the same: a message queued in
/// the seconds before an API restart is not answered, and stays visible in the Inbox for a person.
/// </summary>
public interface IWhatsAppAssistantQueue
{
    /// <summary>False when the queue is full. The caller must then handle the request itself
    /// rather than drop a customer's message.</summary>
    bool TryEnqueue(WhatsAppAssistantRequest request);

    /// <summary>Requests accepted but not yet finished.</summary>
    int Pending { get; }
}

public sealed class WhatsAppAssistantQueue : IWhatsAppAssistantQueue
{
    private readonly Channel<WhatsAppAssistantRequest> channel = Channel.CreateBounded<WhatsAppAssistantRequest>(
        new BoundedChannelOptions(1000) { FullMode = BoundedChannelFullMode.Wait, SingleReader = true });

    private int pending;

    public ChannelReader<WhatsAppAssistantRequest> Reader => channel.Reader;

    public int Pending => Volatile.Read(ref pending);

    public bool TryEnqueue(WhatsAppAssistantRequest request)
    {
        Interlocked.Increment(ref pending);
        if (channel.Writer.TryWrite(request))
        {
            return true;
        }

        Interlocked.Decrement(ref pending);
        return false;
    }

    /// <summary>Called by the worker when a request has been fully handled or given up on.</summary>
    public void Complete() => Interlocked.Decrement(ref pending);

    /// <summary>Waits until nothing is pending. For tests and health probes; never for a request.</summary>
    public async Task WaitUntilIdleAsync(TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (Pending > 0)
        {
            if (DateTimeOffset.UtcNow > deadline)
            {
                throw new TimeoutException($"{Pending} WhatsApp assistant request(s) were still pending after {timeout}.");
            }

            await Task.Delay(20);
        }
    }
}

public sealed class WhatsAppAssistantWorker(
    WhatsAppAssistantQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<WhatsAppAssistantWorker> logger) : BackgroundService
{
    /// <summary>Long enough for a slow model and a translation each way, short enough that a hung
    /// dependency cannot hold every later customer behind it for long.</summary>
    public static readonly TimeSpan RunTimeout = TimeSpan.FromMinutes(3);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var request in queue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await RunAsync(request, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                queue.Complete();
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception,
                    "Customer assistant failed on a WhatsApp batch. TenantId={TenantId} CorrelationId={CorrelationId}",
                    request.Scope.TenantId, request.CorrelationId);
            }
            finally
            {
                if (!stoppingToken.IsCancellationRequested)
                {
                    queue.Complete();
                }
            }
        }
    }

    /// <summary>Runs one request. Also called inline by the webhook when the queue is full, so the
    /// two paths cannot drift apart.</summary>
    public static async Task RunAsync(
        WhatsAppAssistantRequest request,
        IServiceScopeFactory scopeFactory,
        ILogger logger,
        CancellationToken stoppingToken,
        TimeSpan? timeLimit = null)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var assistant = scope.ServiceProvider.GetRequiredService<ICustomerAssistantService>();

        var budget = timeLimit ?? RunTimeout;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        timeout.CancelAfter(budget);

        try
        {
            var openedCaseIds = await assistant.HandleInboundMessagesAsync(
                request.Scope, request.Messages, request.CorrelationId, request.AutoReplyEnabled, timeout.Token);

            // A claim reported on WhatsApp gets the same immediate agent review as one entered by
            // staff or filed on the claimant portal.
            var triageQueue = scope.ServiceProvider.GetService<IClaimTriageQueue>();
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

    private Task RunAsync(WhatsAppAssistantRequest request, CancellationToken stoppingToken) =>
        RunAsync(request, scopeFactory, logger, stoppingToken);
}
