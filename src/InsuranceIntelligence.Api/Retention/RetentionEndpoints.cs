using InsuranceIntelligence.Application.Retention;

namespace InsuranceIntelligence.Api.Retention;

/// <summary>Outcome O: "a screen of its own" for the retention watch, plus the reminder
/// draft/approve-and-send actions. Validation exceptions from the Application-layer service map to
/// ValidationProblem the same way PolicyImportEndpoints does.</summary>
public static class RetentionEndpoints
{
    public static async Task<IResult> GetWatch(IRetentionWatchService service, CancellationToken cancellationToken)
    {
        var summary = await service.GetWatchAsync(cancellationToken);
        return Results.Ok(summary);
    }

    public static async Task<IResult> DraftReminder(string policyId, IRetentionWatchService service, CancellationToken cancellationToken)
    {
        try
        {
            var reminder = await service.DraftReminderAsync(policyId, cancellationToken);
            return Results.Created($"/api/v1/retention/reminders/{reminder.ReminderId}", reminder);
        }
        catch (RetentionWatchValidationException ex)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { [ex.Field] = [ex.Message] });
        }
    }

    public static async Task<IResult> ApproveAndSend(Guid reminderId, IRetentionWatchService service, CancellationToken cancellationToken)
    {
        try
        {
            var reminder = await service.ApproveAndSendAsync(reminderId, cancellationToken);
            return Results.Ok(reminder);
        }
        catch (RetentionWatchValidationException ex)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { [ex.Field] = [ex.Message] });
        }
    }
}
