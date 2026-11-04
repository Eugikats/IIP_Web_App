using InsuranceIntelligence.Api.Workflow;
using InsuranceIntelligence.Application.DataLifecycle;
using InsuranceIntelligence.Domain.DataLifecycle;

namespace InsuranceIntelligence.Api.DataLifecycle;

public sealed record LegalHoldBody(string? ClaimReference, string? Reason);
public sealed record ErasureBody(string? Phone, string? Email, string? PolicyNumber, string? Reason, string? Confirm);

/// <summary>
/// Retention, legal holds and the erasure of one person's data (build-loop item 34). Every route needs ManageTenantSettings and works only
/// inside the caller's workspace. Erasing asks for a reason and the word ERASE, shows what it will do first, and leaves a record that names
/// what was deleted by kind and number but never the data or the phone or email it was asked about.
/// </summary>
public static class DataLifecycleEndpoints
{
    private static IResult Problem(string message) =>
        Results.ValidationProblem(new Dictionary<string, string[]> { ["data"] = [message] });

    public static async Task<IResult> ListHolds(DataLifecycleService service, HttpContext context, CancellationToken cancellationToken)
    {
        if (!WorkflowEndpoints.TryResolveScope(context, out var scope, out _, out var error))
        {
            return Problem(error);
        }

        return Results.Ok(await service.ListHeldAsync(scope, cancellationToken));
    }

    public static async Task<IResult> PlaceHold(LegalHoldBody body, DataLifecycleService service, HttpContext context, CancellationToken cancellationToken)
    {
        if (!WorkflowEndpoints.TryResolveScope(context, out var scope, out var userId, out var error))
        {
            return Problem(error);
        }

        var (held, problem) = await service.PlaceHoldAsync(scope, userId, body.ClaimReference, body.Reason, context.TraceIdentifier, cancellationToken);
        return held is null ? Problem(problem ?? "The hold could not be placed.") : Results.Ok(held);
    }

    public static async Task<IResult> ReleaseHold(Guid caseId, DataLifecycleService service, HttpContext context, CancellationToken cancellationToken)
    {
        if (!WorkflowEndpoints.TryResolveScope(context, out var scope, out var userId, out var error))
        {
            return Problem(error);
        }

        return await service.ReleaseHoldAsync(scope, userId, caseId, context.TraceIdentifier, cancellationToken)
            ? Results.NoContent()
            : Results.NotFound(new { message = "No claim on legal hold has that id." });
    }

    public static async Task<IResult> PreviewErasure(ErasureBody body, DataLifecycleService service, HttpContext context, CancellationToken cancellationToken)
    {
        if (!WorkflowEndpoints.TryResolveScope(context, out var scope, out _, out var error))
        {
            return Problem(error);
        }

        var (preview, problem) = await service.PreviewErasureAsync(scope, new ErasureSubject(body.Phone, body.Email, body.PolicyNumber), cancellationToken);
        return preview is null ? Problem(problem ?? "Nothing could be previewed.") : Results.Ok(preview);
    }

    public static async Task<IResult> Erase(ErasureBody body, DataLifecycleService service, HttpContext context, CancellationToken cancellationToken)
    {
        if (!WorkflowEndpoints.TryResolveScope(context, out var scope, out var userId, out var error))
        {
            return Problem(error);
        }

        if (!string.Equals(body.Confirm?.Trim(), "ERASE", StringComparison.Ordinal))
        {
            return Problem("Type ERASE to confirm. Erased data cannot be recovered.");
        }

        var (record, problem) = await service.EraseAsync(scope, userId, new ErasureSubject(body.Phone, body.Email, body.PolicyNumber), body.Reason, context.TraceIdentifier, cancellationToken);
        return record is null ? Problem(problem ?? "The erasure could not be done.") : Results.Ok(record);
    }

    public static async Task<IResult> Deletions(DataLifecycleService service, HttpContext context, CancellationToken cancellationToken)
    {
        if (!WorkflowEndpoints.TryResolveScope(context, out var scope, out _, out var error))
        {
            return Problem(error);
        }

        return Results.Ok(await service.ListDeletionsAsync(scope, cancellationToken));
    }

    /// <summary>Runs this workspace's retention periods now, rather than waiting for the next scheduled pass.</summary>
    public static async Task<IResult> RunRetention(DataLifecycleService service, HttpContext context, CancellationToken cancellationToken)
    {
        if (!WorkflowEndpoints.TryResolveScope(context, out var scope, out var userId, out var error))
        {
            return Problem(error);
        }

        return Results.Ok(await service.RunRetentionAsync(scope, userId, context.TraceIdentifier, cancellationToken));
    }
}

/// <summary>
/// The platform's own retention timer, inside the API like the billing timer. It runs the pass for every workspace that set a period, a few times
/// a day; the pass is safe to repeat. Switched off with <c>INSURANCEINTELLIGENCE_RETENTION_WORKER=Off</c>.
/// </summary>
public sealed class RetentionWorker(IServiceScopeFactory scopes, IConfiguration configuration, ILogger<RetentionWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Every = TimeSpan.FromHours(6);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.Equals(configuration["INSURANCEINTELLIGENCE_RETENTION_WORKER"], "Off", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var records = await scope.ServiceProvider.GetRequiredService<DataLifecycleService>().RunRetentionAllAsync(stoppingToken);
                if (records.Count > 0)
                {
                    logger.LogInformation("Retention pass: {Records} deletion record(s), {Total} record(s) deleted.", records.Count, records.Sum(record => record.Total));
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogWarning("The retention pass failed and will be tried again. ErrorType={ErrorType}", exception.GetType().Name);
            }

            try
            {
                await Task.Delay(Every, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }
}
