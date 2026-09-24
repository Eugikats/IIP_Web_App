using InsuranceIntelligence.Application.Documents;

namespace InsuranceIntelligence.Api.Documents;

public static class DocumentQualityEndpoints
{
    public static async Task<IResult> GetAccuracyByType(IDocumentQualityService qualityService, CancellationToken cancellationToken)
    {
        var accuracy = await qualityService.GetAccuracyByTypeAsync(cancellationToken);
        return Results.Ok(accuracy);
    }
}
