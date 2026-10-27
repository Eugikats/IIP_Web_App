using InsuranceIntelligence.Application.Products;

namespace InsuranceIntelligence.Api.Products;

/// <summary>
/// Product configuration endpoints (blueprint outcome A/C). An administrator creates a product,
/// edits its claim process and document matrix with no developer and no release, submits it for
/// publish approval, and a second person approves it. Every save is a new version; nothing is
/// overwritten.
/// </summary>
public static class ProductEndpoints
{
    public static async Task<IResult> ListProducts(IProductQueryService queryService, CancellationToken cancellationToken)
    {
        var products = await queryService.ListProductsAsync(cancellationToken);
        return Results.Ok(products);
    }

    public static async Task<IResult> GetDefaultStageLibrary(IProductQueryService queryService, CancellationToken cancellationToken)
    {
        var stages = await queryService.GetDefaultStageLibraryAsync(cancellationToken);
        return Results.Ok(stages);
    }

    public static async Task<IResult> GetCurrent(Guid productId, IProductQueryService queryService, CancellationToken cancellationToken)
    {
        var product = await queryService.GetCurrentAsync(productId, cancellationToken);
        return product is null
            ? Results.NotFound(new { message = "Product was not found for the current tenant workspace." })
            : Results.Ok(product);
    }

    public static async Task<IResult> GetVersion(Guid productId, int version, IProductQueryService queryService, CancellationToken cancellationToken)
    {
        var product = await queryService.GetVersionAsync(productId, version, cancellationToken);
        return product is null
            ? Results.NotFound(new { message = "That product version was not found for the current tenant workspace." })
            : Results.Ok(product);
    }

    public static async Task<IResult> ListVersions(Guid productId, IProductQueryService queryService, CancellationToken cancellationToken)
    {
        var versions = await queryService.ListVersionsAsync(productId, cancellationToken);
        return Results.Ok(versions);
    }

    public static async Task<IResult> CreateDraft(CreateProductRequest request, IProductCommandService commandService, CancellationToken cancellationToken) =>
        await Guarded(async () =>
        {
            var product = await commandService.CreateDraftAsync(request, cancellationToken);
            return Results.Created($"/api/v1/products/{product.ProductId}", product);
        });

    public static async Task<IResult> UpdateDraft(Guid productId, UpdateProductRequest request, IProductCommandService commandService, CancellationToken cancellationToken) =>
        await Guarded(async () => Results.Ok(await commandService.UpdateDraftAsync(productId, request, cancellationToken)));

    public static async Task<IResult> SubmitForPublishApproval(Guid productId, SubmitProductRequest request, IProductCommandService commandService, CancellationToken cancellationToken) =>
        await Guarded(async () => Results.Ok(await commandService.SubmitForPublishApprovalAsync(productId, request, cancellationToken)));

    public static async Task<IResult> ApprovePublish(Guid productId, ApproveProductPublishRequest request, IProductCommandService commandService, CancellationToken cancellationToken) =>
        await Guarded(async () => Results.Ok(await commandService.ApprovePublishAsync(productId, request, cancellationToken)));

    public static async Task<IResult> RejectPublish(Guid productId, RejectProductPublishRequest request, IProductCommandService commandService, CancellationToken cancellationToken) =>
        await Guarded(async () => Results.Ok(await commandService.RejectPublishAsync(productId, request, cancellationToken)));

    public static async Task<IResult> Retire(Guid productId, RetireProductRequest request, IProductCommandService commandService, CancellationToken cancellationToken) =>
        await Guarded(async () => Results.Ok(await commandService.RetireAsync(productId, request, cancellationToken)));

    public static async Task<IResult> ExtractWording(Guid knowledgeDocumentId, IProductWordingExtractionService extraction, CancellationToken cancellationToken) =>
        await WordingGuarded(async () => Results.Ok(await extraction.ExtractAsync(knowledgeDocumentId, cancellationToken)));

    public static async Task<IResult> ListWordingExtractions(Guid? knowledgeDocumentId, IProductWordingExtractionService extraction, CancellationToken cancellationToken) =>
        await WordingGuarded(async () => Results.Ok(await extraction.ListAsync(knowledgeDocumentId, cancellationToken)));

    private static async Task<IResult> WordingGuarded(Func<Task<IResult>> action)
    {
        try
        {
            return await action();
        }
        catch (ProductWordingExtractionException ex)
        {
            var status = ex.Code switch
            {
                "not_found" => StatusCodes.Status404NotFound,
                "not_configured" => StatusCodes.Status503ServiceUnavailable,
                _ => StatusCodes.Status400BadRequest,
            };
            return Results.Problem(ex.Message, statusCode: status, title: ex.Code == "not_configured" ? "Not configured" : "Wording extraction unavailable");
        }
    }

    private static async Task<IResult> Guarded(Func<Task<IResult>> action)
    {
        try
        {
            return await action();
        }
        catch (ProductValidationException ex)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { [ex.Field] = [ex.Message] });
        }
    }
}
