using System.Collections.Concurrent;
using InsuranceIntelligence.Application.Agents;
using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Application.SalesLeads;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Api.Agents;

/// <summary>
/// Reads a WhatsApp conversation again once it goes quiet, so the prospect's record and insights are
/// current when an officer opens them, without the customer's reply ever waiting on it.
///
/// Requests for the same conversation coalesce: a customer who sends five messages in a minute gets
/// one read, 20 seconds after the last one. In-process like <see cref="ClaimTriageQueue"/>, for the
/// same reasons; a request lost to a restart only means the officer presses Refresh.
/// </summary>
public sealed class ConversationInsightsQueue : IConversationInsightsQueue
{
    public static readonly TimeSpan QuietPeriod = TimeSpan.FromSeconds(20);
    private const int MaxPending = 500;

    private readonly ConcurrentDictionary<string, Pending> pending = new(StringComparer.Ordinal);

    public sealed record Pending(TenantScope Scope, string WaId, string ThreadId, string CorrelationId, DateTimeOffset DueAt);

    public void Enqueue(TenantScope scope, string waId, string threadId, string correlationId)
    {
        if (pending.Count >= MaxPending && !pending.ContainsKey(Key(scope, threadId)))
        {
            return;
        }

        pending[Key(scope, threadId)] = new Pending(scope, waId, threadId, correlationId, DateTimeOffset.UtcNow + QuietPeriod);
    }

    /// <summary>Removes and returns the requests whose quiet period has passed.</summary>
    public IReadOnlyList<Pending> TakeDue(DateTimeOffset now)
    {
        var due = new List<Pending>();
        foreach (var (key, request) in pending)
        {
            if (request.DueAt <= now && pending.TryRemove(new KeyValuePair<string, Pending>(key, request)))
            {
                due.Add(request);
            }
        }

        return due;
    }

    private static string Key(TenantScope scope, string threadId) => $"{scope.TenantId}|{scope.WorkspaceId}|{threadId}";
}

public sealed class ConversationInsightsWorker(
    ConversationInsightsQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<ConversationInsightsWorker> logger) : BackgroundService
{
    public const string SystemActorId = "system:conversation-insights";
    private static readonly TimeSpan Tick = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan RunTimeout = TimeSpan.FromMinutes(2);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Tick);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            foreach (var request in queue.TakeDue(DateTimeOffset.UtcNow))
            {
                try
                {
                    await RunAsync(request, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception exception)
                {
                    logger.LogWarning(exception,
                        "Conversation insights failed. ThreadId={ThreadId} CorrelationId={CorrelationId}",
                        request.ThreadId, request.CorrelationId);
                }
            }
        }
    }

    private async Task RunAsync(ConversationInsightsQueue.Pending request, CancellationToken stoppingToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var insights = services.GetService<IConversationInsightsService>();
        if (insights is null)
        {
            return;
        }

        services.GetRequiredService<ITenantContextSetter>().SetCurrent(new TenantContext(
            request.Scope.TenantId,
            request.Scope.WorkspaceId,
            SystemActorId,
            [PlatformRole.CustomerService.ToString()]));

        var gate = await services.GetRequiredService<IAgentGate>().CheckAsync(request.Scope, ConversationInsightsService.AgentKey, stoppingToken);
        if (!gate.Enabled)
        {
            return;
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        timeout.CancelAfter(RunTimeout);
        await insights.RefreshConversationAsync(request.Scope, request.WaId, request.ThreadId, SystemActorId, request.CorrelationId, timeout.Token);
    }
}
