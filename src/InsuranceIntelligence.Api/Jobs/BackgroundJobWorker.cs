using InsuranceIntelligence.Domain.Jobs;

namespace InsuranceIntelligence.Api.Jobs;

/// <summary>
/// The local worker for durable jobs (build-loop item 17): a few loops that take due jobs from the
/// database and run them. The queue is in the database, not in memory, so work waiting when the API
/// stops is still there when it starts. Document processing gets most of the loops; claim review one.
/// Switched off by configuration for tests that drive the processor by hand.
/// </summary>
public sealed class BackgroundJobWorker(
    IBackgroundJobProcessor processor,
    BackgroundPipelineOptions options,
    ILogger<BackgroundJobWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.WorkerEnabled)
        {
            logger.LogInformation("The background job worker is switched off by configuration.");
            return;
        }

        var loops = new List<Task>();
        for (var index = 0; index < options.Workers; index++)
        {
            var owner = $"{Environment.MachineName}:{Environment.ProcessId}:doc-{index}";
            loops.Add(Loop([BackgroundQueues.DocumentProcessing, BackgroundQueues.ClaimReview], owner, stoppingToken));
        }

        loops.Add(Loop([BackgroundQueues.ClaimReview], $"{Environment.MachineName}:{Environment.ProcessId}:review", stoppingToken));
        await Task.WhenAll(loops);
    }

    private async Task Loop(IReadOnlyCollection<string> queues, string owner, CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!await processor.ProcessNextAsync(queues, owner, stoppingToken))
                {
                    await Task.Delay(options.PollMilliseconds, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                // The loop itself must never die: a database blip is waited out.
                logger.LogError(exception, "The background job loop hit an error and will try again. Owner={Owner}", owner);
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }
}
