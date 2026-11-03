using InsuranceIntelligence.Application.Channels;

namespace InsuranceIntelligence.Api.Channels;

public static class ChannelEndpoints
{
    public static async Task<IResult> GetThread(
        string channel,
        string threadId,
        IChannelThreadQueryService queryService,
        CancellationToken cancellationToken)
    {
        var thread = await queryService.GetAsync(channel, threadId, cancellationToken);
        return thread is null ? Results.NotFound() : Results.Ok(thread);
    }

    public static async Task<IResult> GetOverview(
        IChannelQueryService queryService,
        CancellationToken cancellationToken)
    {
        var overview = await queryService.GetOverviewAsync(cancellationToken);
        return Results.Ok(overview);
    }

    public static async Task<IResult> SendMessage(
        string channel,
        string threadId,
        SendChannelMessageRequest request,
        IChannelMessageSendService sendService,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await sendService.SendAsync(new SendChannelMessageCommand(
                channel, threadId, request.Body, request.IdempotencyKey, request.HumanApproved), cancellationToken);
            return result.DeliveryState is "Unknown" or "Queued"
                ? Results.Accepted(value: result)
                : Results.Ok(result);
        }
        catch (ChannelMessagePolicyException exception) when (exception.Code is "RecipientNotVerified" or "ConsentRequired")
        {
            return Results.Conflict(new { code = exception.Code, message = exception.Message });
        }
        catch (ChannelMessagePolicyException exception) when (exception.Code is "ChannelUnavailable" or "ConfigurationRequired" or "ConfigurationChanged")
        {
            return Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "Not configured", detail: exception.Message);
        }
        catch (ChannelMessagePolicyException exception)
        {
            return Results.BadRequest(new { code = exception.Code, message = exception.Message });
        }
    }
}

public sealed record SendChannelMessageRequest(string Body, string IdempotencyKey, bool HumanApproved);
