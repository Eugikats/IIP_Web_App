using InsuranceIntelligence.Application.Fraud;

namespace InsuranceIntelligence.Api.Fraud;

public static class FraudEndpoints
{
    public static async Task<IResult> GetSignals(
        IFraudQueryService queryService,
        CancellationToken cancellationToken)
    {
        var signals = await queryService.GetSignalsAsync(cancellationToken);
        return Results.Ok(signals);
    }
}
