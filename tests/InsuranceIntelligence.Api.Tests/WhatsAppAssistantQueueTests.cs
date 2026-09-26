using InsuranceIntelligence.Api.Agents;
using InsuranceIntelligence.Api.Integrations;
using InsuranceIntelligence.Application.CustomerAssistant;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Integrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace InsuranceIntelligence.Api.Tests;

/// <summary>
/// The WhatsApp webhook used to await the assistant inside the request. These tests pin the
/// behaviour that replaced it: Meta is answered at once, the work runs in the background on a token
/// that a dropped connection cannot cancel, a claim the assistant opens still gets its automatic
/// review, and a batch that outlives its time is handed to a person rather than lost.
/// </summary>
public sealed class WhatsAppAssistantQueueTests
{
    private static readonly TenantScope Scope = TenantScope.Create("tenant-q", "workspace-q");

    [Fact]
    public async Task Enqueueing_ReturnsAtOnceEvenWhileTheAssistantIsStillWorking()
    {
        var assistant = new BlockingAssistant();
        await using var host = Host.Start(assistant);

        var accepted = host.Queue.TryEnqueue(Request("hello"));

        Assert.True(accepted);
        await assistant.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(1, host.Queue.Pending); // the assistant is mid-work and the caller is long gone

        assistant.Release.SetResult();
        await host.Queue.WaitUntilIdleAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(0, host.Queue.Pending);
        Assert.Single(assistant.Handled);
    }

    [Fact]
    public async Task AClaimTheAssistantOpens_IsHandedToTheAutomaticReview()
    {
        var caseId = Guid.NewGuid();
        var assistant = new BlockingAssistant { Opens = [caseId] };
        var triage = new RecordingTriageQueue();
        await using var host = Host.Start(assistant, triage);
        assistant.Release.SetResult();

        host.Queue.TryEnqueue(Request("I had an accident"));
        await host.Queue.WaitUntilIdleAsync(TimeSpan.FromSeconds(5));

        var review = Assert.Single(triage.Enqueued);
        Assert.Equal(caseId, review.CaseId);
        Assert.Equal("whatsapp_assistant", review.Trigger);
    }

    [Fact]
    public async Task ABatchThatThrows_DoesNotStopTheWorkerAnsweringTheNextOne()
    {
        var assistant = new BlockingAssistant { ThrowOnFirst = true };
        await using var host = Host.Start(assistant);
        assistant.Release.SetResult();

        host.Queue.TryEnqueue(Request("first"));
        host.Queue.TryEnqueue(Request("second"));
        await host.Queue.WaitUntilIdleAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(2, assistant.Handled.Count);
    }

    [Fact]
    public async Task ABatchThatRunsOutOfTime_IsHandedToAPersonWithAFreshToken()
    {
        var assistant = new BlockingAssistant { NeverFinishes = true };
        using var services = new ServiceCollection().AddSingleton<ICustomerAssistantService>(assistant).BuildServiceProvider();

        using var stopping = new CancellationTokenSource();
        await WhatsAppAssistantWorker
            .RunAsync(Request("stuck"), services.GetRequiredService<IServiceScopeFactory>(), NullLogger.Instance, stopping.Token, TimeSpan.FromMilliseconds(50))
            .WaitAsync(TimeSpan.FromSeconds(5));

        var escalation = Assert.Single(assistant.Escalated);
        Assert.IsType<TimeoutException>(escalation.Reason);
        Assert.False(escalation.TokenWasCancelled); // the token that ran out cannot be used to say so
    }

    [Fact]
    public void AFullQueue_RefusesRatherThanDroppingSilently()
    {
        var queue = new WhatsAppAssistantQueue();
        for (var i = 0; i < 1000; i++)
        {
            Assert.True(queue.TryEnqueue(Request($"m{i}")));
        }

        Assert.False(queue.TryEnqueue(Request("one too many")));
        Assert.Equal(1000, queue.Pending);
    }

    private static WhatsAppAssistantRequest Request(string text) =>
        new(Scope,
            [new WhatsAppInboundMessage($"wamid.{Guid.NewGuid():N}", "phone-1", "256772118001", WhatsAppContentType.Text, text, null, DateTimeOffset.UtcNow)],
            $"corr-{Guid.NewGuid():N}",
            AutoReplyEnabled: false);

    private sealed class Host : IAsyncDisposable
    {
        private readonly ServiceProvider services;
        private readonly WhatsAppAssistantWorker worker;

        private Host(ServiceProvider services, WhatsAppAssistantQueue queue, WhatsAppAssistantWorker worker)
        {
            this.services = services;
            Queue = queue;
            this.worker = worker;
        }

        public WhatsAppAssistantQueue Queue { get; }

        public static Host Start(BlockingAssistant assistant, RecordingTriageQueue? triage = null)
        {
            var collection = new ServiceCollection();
            collection.AddSingleton<ICustomerAssistantService>(assistant);
            if (triage is not null)
            {
                collection.AddSingleton<IClaimTriageQueue>(triage);
            }

            var provider = collection.BuildServiceProvider();
            var queue = new WhatsAppAssistantQueue();
            var worker = new WhatsAppAssistantWorker(queue, provider.GetRequiredService<IServiceScopeFactory>(), NullLogger<WhatsAppAssistantWorker>.Instance);
            worker.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
            return new Host(provider, queue, worker);
        }

        public async ValueTask DisposeAsync()
        {
            await worker.StopAsync(CancellationToken.None);
            await services.DisposeAsync();
        }
    }

    private sealed class BlockingAssistant : ICustomerAssistantService
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public List<string> Handled { get; } = [];
        public List<(Exception Reason, bool TokenWasCancelled)> EscalatedLog { get; } = [];
        public IReadOnlyList<(Exception Reason, bool TokenWasCancelled)> Escalated => EscalatedLog;
        public IReadOnlyList<Guid> Opens { get; init; } = [];
        public bool ThrowOnFirst { get; init; }
        public bool NeverFinishes { get; init; }

        public async Task<IReadOnlyList<Guid>> HandleInboundMessagesAsync(
            TenantScope scope, IReadOnlyList<WhatsAppInboundMessage> messages, string correlationId, bool autoReplyEnabled, CancellationToken cancellationToken = default)
        {
            Started.TrySetResult();
            int order;
            lock (Handled)
            {
                Handled.Add(messages[0].CustomerSafeBody);
                order = Handled.Count;
            }

            if (NeverFinishes)
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }

            await Release.Task;
            if (ThrowOnFirst && order == 1)
            {
                throw new InvalidOperationException("boom");
            }

            return Opens;
        }

        public Task EscalateUnhandledAsync(
            TenantScope scope, IReadOnlyList<WhatsAppInboundMessage> messages, string correlationId, Exception reason, CancellationToken cancellationToken = default)
        {
            EscalatedLog.Add((reason, cancellationToken.IsCancellationRequested));
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingTriageQueue : IClaimTriageQueue
    {
        public List<(Guid CaseId, string Trigger)> Enqueued { get; } = [];

        public void Enqueue(TenantScope scope, Guid caseId, string correlationId, string trigger) => Enqueued.Add((caseId, trigger));
    }
}
