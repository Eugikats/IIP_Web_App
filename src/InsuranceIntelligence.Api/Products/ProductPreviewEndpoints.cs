using InsuranceIntelligence.Api.ClientPortal;
using InsuranceIntelligence.Api.Common;
using InsuranceIntelligence.Application.Products;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Compliance;
using InsuranceIntelligence.Domain.Documents;
using InsuranceIntelligence.Domain.Products;

namespace InsuranceIntelligence.Api.Products;

public sealed record CustomerPreviewClaimType(string Key, string Name, string Description);

/// <summary>What a customer would be asked for a product and claim type, in the shape the claimant
/// pages already use, with sample values for everything personal. Built from the product itself, so
/// a draft can be previewed before it is published.</summary>
public sealed record CustomerPreviewResponse(
    Guid ProductId,
    string ProductName,
    string ProductClass,
    string Status,
    bool IsPublished,
    IReadOnlyList<CustomerPreviewClaimType> ClaimTypes,
    string? ClaimTypeKey,
    string ClaimTypeName,
    ClaimFieldFormDto Form,
    IReadOnlyList<ClientPortalDocumentRequirement> Documents,
    string SamplePolicyNumber,
    int PrivacyNoticeVersion,
    string PrivacyNoticeText,
    bool PrivacyNoticeIsDefault);

/// <summary>
/// "Preview as customer" (build-loop item 8). Read only: it resolves the questions and documents for
/// a chosen claim type exactly as a filed claim would, from the product's current version (draft or
/// published), and returns them with nothing stored, no claim created and no message sent. Needs
/// ManageProducts and reads only the caller's own workspace. Being a read it writes no audit event.
/// </summary>
public static class ProductPreviewEndpoints
{
    public const string SamplePolicyNumber = "POL-SAMPLE-0001";

    public static async Task<IResult> CustomerPreview(
        Guid productId,
        string? claimType,
        IProductRepository productRepository,
        ITenantSettingsRepository settingsRepository,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!TenantScopeReader.TryGetFromHeaders(httpContext, out var scope, out var error))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["tenantScope"] = [error] });
        }

        var product = await productRepository.GetCurrentAsync(scope, productId, cancellationToken);
        if (product is null)
        {
            return Results.NotFound(new { message = "Product was not found for the current tenant workspace." });
        }

        var types = product.ActiveClaimTypes;
        var chosen = product.FindClaimType(claimType) ?? (string.IsNullOrWhiteSpace(claimType) ? types.FirstOrDefault() : null);
        if (!string.IsNullOrWhiteSpace(claimType) && chosen is null)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["claimType"] = [$"This product has no claim type called '{claimType}'."] });
        }

        var notice = PrivacyNotices.For(await settingsRepository.GetAsync(scope, cancellationToken));
        var claimTypeName = chosen?.Name ?? string.Empty;
        var ruleSet = DocumentChecklistRules.ForProduct(product, chosen?.Key)
            ?? DocumentChecklistRules.Resolve(product.ProductClass, claimTypeName, null);
        var documents = ruleSet.Slots
            .Select(slot => new ClientPortalDocumentRequirement(
                slot.Key,
                slot.Label,
                slot.Description,
                slot.Required,
                Uploaded: false,
                Status: "Not uploaded",
                FileName: null,
                UploadedAt: null,
                DisagreesWithClassifier: false,
                Kind: slot.Kind.ToString(),
                AllowsMultipleFiles: slot.AllowsMultipleFiles,
                MaxFiles: slot.MaxFiles,
                AcceptedFormats: slot.AcceptedFormats))
            .ToArray();

        return Results.Ok(new CustomerPreviewResponse(
            product.ProductId,
            product.Name,
            product.ProductClass,
            product.Status.ToString(),
            product.Status == ProductStatus.Published,
            types.Select(type => new CustomerPreviewClaimType(type.Key, type.Name, type.Description)).ToArray(),
            chosen?.Key,
            claimTypeName,
            ClaimFieldService.FormFor(product, chosen?.Key),
            documents,
            SamplePolicyNumber,
            notice.Version,
            notice.Text,
            notice.IsDefault));
    }
}
