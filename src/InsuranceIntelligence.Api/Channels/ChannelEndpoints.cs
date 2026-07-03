using InsuranceIntelligence.Application.Channels;

namespace InsuranceIntelligence.Api.Channels;

public static class ChannelEndpoints
{
    public static async Task<IResult> GetOverview(
        IChannelQueryService queryService,
        CancellationToken cancellationToken)
    {
        var overview = await queryService.GetOverviewAsync(cancellationToken);
        return Results.Ok(overview);
    }
}
