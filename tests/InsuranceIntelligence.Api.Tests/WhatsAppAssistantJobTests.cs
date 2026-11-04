using System.Text.Json;
using InsuranceIntelligence.Api.Agents;
using InsuranceIntelligence.Api.Integrations;
using InsuranceIntelligence.Api.Jobs;
using InsuranceIntelligence.Api.Tests.Support;
using InsuranceIntelligence.Application.CustomerAssistant;
using InsuranceIntelligence.Application.WhatsApp;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Integrations;
using InsuranceIntelligence.Domain.Jobs;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;

namespace InsuranceIntelligence.Api.Tests;

/// <summary>
/// The WhatsApp webhook used to await the assistant inside the request, and then handed it to an in-process queue.
/// These tests pin the durable job that replaced the queue (build-loop item 27): Meta is answered at once, the work
/// runs afterwards from the stored messages (the job carries identifiers, never what the customer wrote), a claim the
/// assistant opens still gets its automatic review, a batch that keeps failing is dead-lettered where an administrator
/// can see it, and a batch that outlives its time is handed to a person rather than lost.
/// </summary>
public sealed class WhatsAppAssistantJobTests(MockedApiFactory factory) : IClassFixture<MockedApiFactory>
{
    private static readonly string[] Queues = [BackgroundQueues.CustomerAssistant];
    private const string Phone = "phone-job-1";
    private const string Customer = "256772118001";

    private (WebApplicationFactory<Program> Host, RecordingAssistant Assistant, RecordingTriageQueue Triage) Start(int maxAttempts = 3, RecordingAssistant? assistant = null)
    {
        assistant ??= new RecordingAssistant();
        var triage = new RecordingTriageQueue();
        var host = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.Replace(ServiceDescriptor.Singleton(new BackgroundPipelineOptions
            {
                Inline = false,
                WorkerEnabled = false,
                MaxAttempts = maxAttempts,
                BackoffSeconds = [0],
                ReviewSettleSeconds = 0,
            }));
            services.Replace(ServiceDescriptor.Scoped<ICustomerAssistantService>(_ => assistant));
            services.Replace(ServiceDescriptor.Singleton<IClaimTriageQueue>(triage));
        }));
        return (host, assistant, triage);
    }

    private static async Task<(WhatsAppInboundMessage Inbound, Guid StoredId)> StoreInboundAsync(
        WebApplicationFactory<Program> host, TenantScope scope, string text, string? mediaId = null, string? profileName = null)
    {
        using var services = host.Services.CreateScope();
        var repository = services.ServiceProvider.GetRequiredService<IWhatsAppMessageRepository>();
        var inbound = new WhatsAppInboundMessage($"wamid.{Guid.NewGuid():N}", Phone, Customer, WhatsAppContentType.Text, text, null, DateTimeOffset.UtcNow)
        {
            MediaId = mediaId,
            ProfileName = profileName,
        };
        var stored = new WhatsAppMessage
        {
            MessageId = Guid.NewGuid(),
            Scope = scope,
            ThreadId = WhatsAppMessagingService.BuildThreadId(Phone, Customer),
            BusinessPhoneNumberId = Phone,
            ContactWaId = Customer,
            Direction = WhatsAppMessageDirection.Inbound,
            ContentType = inbound.ContentType,
            CustomerSafeBody = text,
            ProviderMessageId = inbound.ProviderMessageId,
            MediaId = mediaId,
            ProfileName = profileName,
            DeliveryState = WhatsAppDeliveryState.Received,
            CreatedAt = inbound.ReceivedAt,
            UpdatedAt = inbound.ReceivedAt,
            CorrelationId = "corr",
        };
        await repository.SaveAsync(stored);
        return (inbound, stored.MessageId);
    }

    private static async Task<bool> EnqueueAsync(WebApplicationFactory<Program> host, TenantScope scope, params WhatsAppInboundMessage[] messages)
    {
        using var services = host.Services.CreateScope();
        return await WhatsAppAssistantJobs.TryEnqueueAsync(
            scope, messages, "corr-job", autoReplyEnabled: true,
            services.ServiceProvider.GetRequiredService<IWhatsAppMessageRepository>(),
            services.ServiceProvider.GetRequiredService<IBackgroundJobStore>(),
            services.ServiceProvider.GetRequiredService<BackgroundPipelineOptions>(),
            CancellationToken.None);
    }

    private static Task<bool> StepAsync(WebApplicationFactory<Program> host, TenantScope scope) =>
        host.Services.GetRequiredService<IBackgroundJobProcessor>().ProcessNextAsync(Queues, "test-worker", CancellationToken.None, scope);

    private static async Task<IReadOnlyList<BackgroundJob>> JobsAsync(WebApplicationFactory<Program> host, TenantScope scope)
    {
        using var services = host.Services.CreateScope();
        return await services.ServiceProvider.GetRequiredService<IBackgroundJobStore>().ListAsync(scope, null, null, 50);
    }

    [Fact]
    public async Task TheWebhookOnlyQueuesTheWork_AndTheJobHoldsIdentifiersNotTheCustomersWords()
    {
        var (host, assistant, _) = Start();
        using var _h = host;
        var scope = TenantScope.Create(MockedApiFactory.NewTenant(), "workspace-main");
        var (inbound, storedId) = await StoreInboundAsync(host, scope, "My boda was hit at Wandegeya, please help");

        Assert.True(await EnqueueAsync(host, scope, inbound));

        Assert.Empty(assistant.Handled); // nothing ran inside the request
        var job = Assert.Single(await JobsAsync(host, scope));
        Assert.Equal(BackgroundJobTypes.CustomerAssistantRespond, job.Type);
        Assert.Equal(BackgroundJobStatus.Pending, job.Status);
        Assert.Contains(storedId.ToString(), job.PayloadJson);
        Assert.DoesNotContain("boda", job.PayloadJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Wandegeya", job.PayloadJson);
        Assert.DoesNotContain(Customer, job.PayloadJson);
    }

    [Fact]
    public async Task TheJobRebuildsTheMessagesFromTheStoreAndRunsTheAssistant()
    {
        var (host, assistant, _) = Start();
        using var _h = host;
        var scope = TenantScope.Create(MockedApiFactory.NewTenant(), "workspace-main");
        var (inbound, _) = await StoreInboundAsync(host, scope, "Where is my claim?", mediaId: "media-9", profileName: "Amina Okello");
        await EnqueueAsync(host, scope, inbound);

        Assert.True(await StepAsync(host, scope));

        var handled = Assert.Single(assistant.Handled);
        Assert.Equal(scope, handled.Scope);
        var message = Assert.Single(handled.Messages);
        Assert.Equal("Where is my claim?", message.CustomerSafeBody);
        Assert.Equal(inbound.ProviderMessageId, message.ProviderMessageId);
        Assert.Equal("media-9", message.MediaId);
        Assert.Equal("Amina Okello", message.ProfileName);
        Assert.True(handled.AutoReply);
        Assert.Equal(BackgroundJobStatus.Succeeded, Assert.Single(await JobsAsync(host, scope)).Status);
    }

    [Fact]
    public async Task AClaimTheAssistantOpens_IsHandedToTheAutomaticReview()
    {
        var caseId = Guid.NewGuid();
        var (host, _, triage) = Start(assistant: new RecordingAssistant { Opens = [caseId] });
        using var _h = host;
        var scope = TenantScope.Create(MockedApiFactory.NewTenant(), "workspace-main");
        var (inbound, _) = await StoreInboundAsync(host, scope, "I had an accident");
        await EnqueueAsync(host, scope, inbound);

        await StepAsync(host, scope);

        var review = Assert.Single(triage.Enqueued);
        Assert.Equal(caseId, review.CaseId);
        Assert.Equal("whatsapp_assistant", review.Trigger);
    }

    [Fact]
    public async Task ABatchThatThrows_IsRetriedAndThenAnswered()
    {
        var (host, assistant, _) = Start(assistant: new RecordingAssistant { ThrowTimes = 1 });
        using var _h = host;
        var scope = TenantScope.Create(MockedApiFactory.NewTenant(), "workspace-main");
        var (inbound, _) = await StoreInboundAsync(host, scope, "first");
        await EnqueueAsync(host, scope, inbound);

        Assert.True(await StepAsync(host, scope));
        Assert.Equal(BackgroundJobStatus.Pending, Assert.Single(await JobsAsync(host, scope)).Status);

        Assert.True(await StepAsync(host, scope));
        Assert.Equal(BackgroundJobStatus.Succeeded, Assert.Single(await JobsAsync(host, scope)).Status);
        Assert.Equal(2, assistant.Handled.Count);
    }

    [Fact]
    public async Task ABatchThatKeepsFailing_IsDeadLetteredWhereAnAdministratorCanSeeItWithAPlainName()
    {
        var (host, _, _) = Start(maxAttempts: 2, assistant: new RecordingAssistant { ThrowTimes = 10 });
        using var _h = host;
        var tenant = MockedApiFactory.NewTenant();
        var scope = TenantScope.Create(tenant, "workspace-main");
        var (inbound, _) = await StoreInboundAsync(host, scope, "help");
        await EnqueueAsync(host, scope, inbound);

        await StepAsync(host, scope);
        await StepAsync(host, scope);

        Assert.Equal(BackgroundJobStatus.DeadLettered, Assert.Single(await JobsAsync(host, scope)).Status);
        using var staff = host.CreateScopedClient(tenant);
        var listing = await MockedApiFactory.ReadJsonAsync(await staff.GetAsync("/api/v1/admin/background-jobs?status=DeadLettered"));
        Assert.Contains(listing.GetProperty("jobs").EnumerateArray(), job => job.GetProperty("what").GetString() == "Answering a customer on WhatsApp");
    }

    [Fact]
    public async Task AJobNeverReadsAMessageFromAnotherWorkspace()
    {
        var (host, assistant, _) = Start();
        using var _h = host;
        var scopeA = TenantScope.Create(MockedApiFactory.NewTenant(), "workspace-main");
        var scopeB = TenantScope.Create(MockedApiFactory.NewTenant(), "workspace-main");
        var (_, storedInB) = await StoreInboundAsync(host, scopeB, "Private words of workspace B");

        // A job in workspace A that names the stored id of a message in workspace B.
        using (var services = host.Services.CreateScope())
        {
            var now = DateTimeOffset.UtcNow;
            await services.ServiceProvider.GetRequiredService<IBackgroundJobStore>().EnqueueAsync(new BackgroundJob
            {
                JobId = Guid.NewGuid(),
                Scope = scopeA,
                Queue = BackgroundQueues.CustomerAssistant,
                Type = BackgroundJobTypes.CustomerAssistantRespond,
                Key = "crossed",
                PayloadJson = JsonSerializer.Serialize(new { messageIds = new[] { storedInB }, autoReply = true }),
                Status = BackgroundJobStatus.Pending,
                MaxAttempts = 3,
                NotBefore = now,
                CreatedAt = now,
                UpdatedAt = now,
                CorrelationId = "corr",
            });
        }

        await StepAsync(host, scopeA);

        Assert.Empty(assistant.Handled);
        var job = Assert.Single(await JobsAsync(host, scopeA));
        Assert.Equal(BackgroundJobStatus.DeadLettered, job.Status);
        Assert.DoesNotContain("Private words", job.LastError ?? string.Empty);
    }

    [Fact]
    public async Task ARedeliveredBatchIsOneJob()
    {
        var (host, _, _) = Start();
        using var _h = host;
        var scope = TenantScope.Create(MockedApiFactory.NewTenant(), "workspace-main");
        var (inbound, _) = await StoreInboundAsync(host, scope, "hello");

        await EnqueueAsync(host, scope, inbound);
        await EnqueueAsync(host, scope, inbound);

        Assert.Single(await JobsAsync(host, scope));
    }

    [Fact]
    public async Task ABatchWithNothingStored_IsNotQueued_SoTheCallerHandlesItItself()
    {
        var (host, _, _) = Start();
        using var _h = host;
        var scope = TenantScope.Create(MockedApiFactory.NewTenant(), "workspace-main");
        var unsaved = new WhatsAppInboundMessage("wamid.never-saved", Phone, Customer, WhatsAppContentType.Text, "hello", null, DateTimeOffset.UtcNow);

        Assert.False(await EnqueueAsync(host, scope, unsaved));
        Assert.Empty(await JobsAsync(host, scope));
    }

    [Fact]
    public async Task ABatchThatRunsOutOfTime_IsHandedToAPersonWithAFreshToken()
    {
        var assistant = new RecordingAssistant { NeverFinishes = true };
        using var services = new ServiceCollection().AddSingleton<ICustomerAssistantService>(assistant).BuildServiceProvider();
        var request = new WhatsAppAssistantRequest(
            TenantScope.Create("tenant-q", "workspace-q"),
            [new WhatsAppInboundMessage("wamid.stuck", Phone, Customer, WhatsAppContentType.Text, "stuck", null, DateTimeOffset.UtcNow)],
            "corr-stuck",
            AutoReplyEnabled: false);

        using var stopping = new CancellationTokenSource();
        await WhatsAppAssistantRunner
            .RunAsync(request, services.GetRequiredService<IServiceScopeFactory>(), NullLogger.Instance, stopping.Token, TimeSpan.FromMilliseconds(50))
            .WaitAsync(TimeSpan.FromSeconds(5));

        var escalation = Assert.Single(assistant.Escalated);
        Assert.IsType<TimeoutException>(escalation.Reason);
        Assert.False(escalation.TokenWasCancelled); // the token that ran out cannot be used to say so
    }

    private sealed record Handled(TenantScope Scope, IReadOnlyList<WhatsAppInboundMessage> Messages, bool AutoReply);

    private sealed class RecordingAssistant : ICustomerAssistantService
    {
        private int thrown;

        public List<Handled> Handled { get; } = [];
        public List<(Exception Reason, bool TokenWasCancelled)> EscalatedLog { get; } = [];
        public IReadOnlyList<(Exception Reason, bool TokenWasCancelled)> Escalated => EscalatedLog;
        public IReadOnlyList<Guid> Opens { get; init; } = [];
        public int ThrowTimes { get; init; }
        public bool NeverFinishes { get; init; }

        public async Task<IReadOnlyList<Guid>> HandleInboundMessagesAsync(
            TenantScope scope, IReadOnlyList<WhatsAppInboundMessage> messages, string correlationId, bool autoReplyEnabled, CancellationToken cancellationToken = default)
        {
            lock (Handled)
            {
                Handled.Add(new Handled(scope, messages, autoReplyEnabled));
            }

            if (NeverFinishes)
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }

            if (Interlocked.Increment(ref thrown) <= ThrowTimes)
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

        public Task EnqueueAsync(TenantScope scope, Guid caseId, string correlationId, string trigger, CancellationToken cancellationToken = default)
        {
            Enqueued.Add((caseId, trigger));
            return Task.CompletedTask;
        }
    }
}
