using InsuranceIntelligence.Infrastructure.Persistence;

namespace InsuranceIntelligence.Api.Admin;

/// <summary>
/// Fills the lookup columns on rows saved before they existed (build-loop item 35), once, shortly after start-up. Until it finishes the
/// lookups still find those rows, only more slowly. Switched off with <c>INSURANCEINTELLIGENCE_LOOKUP_BACKFILL=Off</c>.
/// </summary>
public sealed class LookupBackfillWorker(IServiceScopeFactory scopes, IConfiguration configuration, ILogger<LookupBackfillWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.Equals(configuration["INSURANCEINTELLIGENCE_LOOKUP_BACKFILL"], "Off", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken);
            using var scope = scopes.CreateScope();
            var filled = await scope.ServiceProvider.GetRequiredService<LookupKeyBackfill>().RunAsync(stoppingToken);
            if (filled > 0)
            {
                logger.LogInformation("Lookup backfill: filled the lookup columns on {Rows} existing row(s).", filled);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            // The lookups still work without it; the next start tries again.
            logger.LogWarning("The lookup backfill could not finish. ErrorType={ErrorType}", exception.GetType().Name);
        }
    }
}
