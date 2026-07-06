using InsuranceIntelligence.Application.Documents;

namespace InsuranceIntelligence.Api.Documents;

public static class DocumentLibraryEndpoints
{
    public static async Task<IResult> ListDocuments(
        IDocumentQueryService service,
        CancellationToken cancellationToken)
    {
        var documents = await service.ListAsync(cancellationToken);
        return Results.Ok(documents);
    }
}
