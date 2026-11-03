using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Application.Products;

namespace InsuranceIntelligence.Api.Products;

/// <summary>
/// Policy wording to drafted product setup (build-loop item 7). Every route needs ManageProducts and
/// works only on the caller's own workspace; the service re-checks the permission and the scope. The
/// model only proposes: nothing reaches the product until a person accepts items and applies them,
/// and applying makes a draft version that still needs the usual second approver to publish.
/// </summary>
public static class WordingDraftEndpoints
{
    public static Task<IResult> Create(Guid productId, CreateWordingDraftRequest? request, IWordingDraftService service, CancellationToken cancellationToken) =>
        Run(async () => Results.Created($"/api/v1/products/{productId}/wording-drafts", await service.CreateAsync(productId, request ?? new CreateWordingDraftRequest(null), cancellationToken)));

    public static Task<IResult> List(Guid productId, IWordingDraftService service, CancellationToken cancellationToken) =>
        Run(async () => Results.Ok(await service.ListAsync(productId, cancellationToken)));

    public static Task<IResult> Get(Guid productId, Guid draftId, IWordingDraftService service, CancellationToken cancellationToken) =>
        Run(async () => Results.Ok(await service.GetAsync(productId, draftId, cancellationToken)));

    public static Task<IResult> UpdateItem(Guid productId, Guid draftId, string itemId, UpdateWordingItemRequest request, IWordingDraftService service, CancellationToken cancellationToken) =>
        Run(async () => Results.Ok(await service.UpdateItemAsync(productId, draftId, itemId, request, cancellationToken)));

    public static Task<IResult> Apply(Guid productId, Guid draftId, ApplyWordingDraftRequest? request, IWordingDraftService service, CancellationToken cancellationToken) =>
        Run(async () => Results.Ok(await service.ApplyAsync(productId, draftId, request ?? new ApplyWordingDraftRequest(null), cancellationToken)));

    public static Task<IResult> Discard(Guid productId, Guid draftId, IWordingDraftService service, CancellationToken cancellationToken) =>
        Run(async () => Results.Ok(await service.DiscardAsync(productId, draftId, cancellationToken)));

    private static async Task<IResult> Run(Func<Task<IResult>> action)
    {
        try
        {
            return await action();
        }
        catch (ProductValidationException ex)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { [ex.Field] = [ex.Message] });
        }
        catch (WordingDraftNotFoundException ex)
        {
            return Results.NotFound(new { message = ex.Message });
        }
        catch (WordingExtractionException ex)
        {
            // The message is ours, never the provider's: a raw model or provider error must not
            // reach the browser.
            return Results.Json(new { message = ex.Message }, statusCode: StatusCodes.Status503ServiceUnavailable);
        }
        catch (PermissionDeniedException)
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }
    }
}
