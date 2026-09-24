using InsuranceIntelligence.Application.Brief;

namespace InsuranceIntelligence.Api.Brief;

/// <summary>The landing brief (outcome F): at most five ranked claims with a one-sentence reason
/// each, plus what this person has already handled today.</summary>
public static class BriefEndpoints
{
    public static async Task<IResult> GetBrief(IBriefService briefService, CancellationToken cancellationToken)
    {
        var brief = await briefService.GetAsync(cancellationToken);
        return Results.Ok(brief);
    }
}
