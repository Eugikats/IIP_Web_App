using InsuranceIntelligence.Application.Channels;

namespace InsuranceIntelligence.Api.Channels;

public static class ChannelIdentityEndpoints
{
    public static async Task<IResult> GetForThread(
        string channel, string threadId, IChannelIdentityService service, CancellationToken cancellationToken) =>
        Results.Ok(await service.GetForThreadAsync(channel, threadId, cancellationToken));

    public static async Task<IResult> Link(
        string channel, string threadId, LinkChannelIdentityRequest request,
        IChannelIdentityService service, CancellationToken cancellationToken)
    {
        try
        {
            return Results.Ok(await service.LinkAsync(new LinkChannelIdentityCommand(
                channel, threadId, request.CustomerId, request.CustomerConsentedToContext,
                request.EvidenceReference, request.AuditReason), cancellationToken));
        }
        catch (ChannelIdentityPolicyException exception) when (exception.Code is "IdentityAlreadyLinked" or "RelinkRequiresReview")
        {
            return Results.Conflict(new { code = exception.Code, message = exception.Message });
        }
        catch (ChannelIdentityPolicyException exception) when (exception.Code is "CustomerNotFound" or "InboundConversationRequired" or "AccountNotActive")
        {
            return Results.BadRequest(new { code = exception.Code, message = exception.Message });
        }
        catch (ChannelIdentityPolicyException exception) when (exception.Code == "ConsentRequired")
        {
            return Results.Conflict(new { code = exception.Code, message = exception.Message });
        }
        catch (ArgumentException exception)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { [exception.ParamName ?? "request"] = [exception.Message] });
        }
    }

    public static async Task<IResult> Revoke(
        Guid linkId, RevokeChannelIdentityRequest request,
        IChannelIdentityService service, CancellationToken cancellationToken)
    {
        try
        {
            return Results.Ok(await service.RevokeAsync(linkId, request.AuditReason, cancellationToken));
        }
        catch (ChannelIdentityPolicyException exception) when (exception.Code is "IdentityLinkNotFound" or "IdentityLinkAlreadyRevoked")
        {
            return Results.NotFound(new { code = exception.Code, message = exception.Message });
        }
        catch (ArgumentException exception)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { [exception.ParamName ?? "request"] = [exception.Message] });
        }
    }

    public static async Task<IResult> ListForCustomer(
        string customerId, IChannelIdentityService service, CancellationToken cancellationToken) =>
        Results.Ok(await service.ListForCustomerAsync(customerId, cancellationToken));

    public static async Task<IResult> GetCustomerTimeline(
        string customerId, IChannelIdentityService service, CancellationToken cancellationToken,
        int page = 1, int pageSize = 50)
    {
        try
        {
            var timeline = await service.GetCustomerTimelineAsync(customerId, page, pageSize, cancellationToken);
            return timeline is null
                ? Results.NotFound(new { message = "Customer was not found in this workspace." })
                : Results.Ok(timeline);
        }
        catch (ArgumentException exception)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { [exception.ParamName ?? "request"] = [exception.Message] });
        }
    }
}

public sealed record LinkChannelIdentityRequest(
    string CustomerId, bool CustomerConsentedToContext, string EvidenceReference, string AuditReason);

public sealed record RevokeChannelIdentityRequest(string AuditReason);
