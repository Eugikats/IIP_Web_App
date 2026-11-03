using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Products;

namespace InsuranceIntelligence.Application.Products;

public sealed class ClaimTypeService(
    IProductRepository productRepository,
    ICaseRepository caseRepository,
    ITenantContextAccessor tenantContextAccessor,
    IPermissionService permissionService,
    IAuditWriter auditWriter) : IClaimTypeService
{
    public async Task<ClaimTypeOptionsDto> ListForProductLineAsync(TenantScope scope, string? productLine, CancellationToken cancellationToken = default)
    {
        var product = await FindPublishedAsync(scope, productLine, cancellationToken);
        var types = product?.ActiveClaimTypes ?? [];
        return new ClaimTypeOptionsDto(
            productLine?.Trim() ?? string.Empty,
            product?.ProductId,
            product?.Name,
            types.Count > 0,
            types.Select(type => new ClaimTypeOptionDto(type.Key, type.Name, type.Description)).ToArray());
    }

    public async Task<ClaimTypeSuggestionDto> SuggestAsync(TenantScope scope, string? productLine, string? text, CancellationToken cancellationToken = default)
    {
        var product = await FindPublishedAsync(scope, productLine, cancellationToken);
        var match = product?.SuggestClaimType(text);
        return new ClaimTypeSuggestionDto(match?.Key, match?.Name, match is not null);
    }

    public async Task<ClaimTypeImportResultDto> ImportExistingAsync(Guid productId, CancellationToken cancellationToken = default)
    {
        await permissionService.RequirePermissionAsync(Permission.ManageProducts, cancellationToken);

        var context = tenantContextAccessor.Current;
        var current = await productRepository.GetCurrentAsync(context.Scope, productId, cancellationToken)
            ?? throw new ProductValidationException("productId", "Product was not found for the current tenant workspace.");
        if (current.Status == ProductStatus.Retired)
        {
            throw new ProductValidationException("status", "A retired product cannot be edited.");
        }

        var cases = (await caseRepository.ListAsync(context.Scope, cancellationToken))
            .Where(claim => claim.ProductId == productId
                || string.Equals(claim.ProductLine, current.ProductClass, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        var types = current.ClaimTypes.ToList();
        var added = new List<string>();
        foreach (var text in cases
                     .Select(claim => claim.ClaimType.Trim())
                     .Where(text => text.Length > 0)
                     .GroupBy(text => ClaimTypeMatching.Normalize(text))
                     .Select(group => group.First())
                     .OrderBy(text => text, StringComparer.OrdinalIgnoreCase))
        {
            if (ClaimTypeMatching.Find(types, text) is not null)
            {
                continue;
            }

            var name = Prettify(text);
            var key = UniqueKey(types, ProductCommandService.Slug(name));
            if (key.Length == 0)
            {
                continue;
            }

            types.Add(new ClaimTypeDefinition(key, name, string.Empty, true, types.Count, [text]));
            added.Add(name);
        }

        var version = current.Version;
        if (added.Count > 0)
        {
            var reason = $"Imported {added.Count} claim type(s) already used on existing claims.";
            var updated = current with
            {
                VersionId = Guid.NewGuid(),
                Version = current.Version + 1,
                ClaimTypes = types,
                Status = current.Status is ProductStatus.Published or ProductStatus.PendingPublishApproval ? ProductStatus.Draft : current.Status,
                UpdatedByUserId = context.UserId,
                UpdatedAt = DateTimeOffset.UtcNow,
                ChangeReason = reason,
                PublishSubmittedByUserId = null,
                PublishSubmittedAt = null,
                PublishApprovedByUserId = null,
                PublishApprovedAt = null,
                PublishRejectedByUserId = null,
                PublishRejectedAt = null,
                PublishRejectionReason = null,
            };
            await productRepository.SaveAsync(updated, cancellationToken);
            await auditWriter.WriteAsync(
                AuditEvent.ProductVersionSaved(
                    updated.Scope, updated.UpdatedByUserId, updated.ProductId, updated.Version,
                    updated.Status.ToString(), "product.claim_types_imported", reason, Guid.NewGuid().ToString("N")),
                cancellationToken);
            version = updated.Version;
        }

        // Link the claims that match to their claim type key. Claims are never changed in any other way.
        var linked = 0;
        foreach (var claim in cases.Where(claim => claim.ClaimTypeKey is null))
        {
            var match = ClaimTypeMatching.Find(types, claim.ClaimType);
            if (match is null)
            {
                continue;
            }

            await caseRepository.SaveAsync(claim with { ClaimTypeKey = match.Key }, cancellationToken);
            linked++;
        }

        return new ClaimTypeImportResultDto(productId, version, added, linked);
    }

    private async Task<Product?> FindPublishedAsync(TenantScope scope, string? productLine, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(productLine))
        {
            return null;
        }

        var products = await productRepository.ListCurrentForAllProductsAsync(scope, cancellationToken);
        return products.FirstOrDefault(product =>
            product.Status == ProductStatus.Published
            && string.Equals(product.ProductClass, productLine.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    private static string UniqueKey(IReadOnlyList<ClaimTypeDefinition> existing, string key)
    {
        if (key.Length == 0)
        {
            return key;
        }

        var candidate = key;
        for (var suffix = 2; existing.Any(type => string.Equals(type.Key, candidate, StringComparison.OrdinalIgnoreCase)); suffix++)
        {
            candidate = $"{key}-{suffix}";
        }

        return candidate;
    }

    /// <summary>"ThirdPartyLiability" becomes "Third party liability"; text that already has spaces
    /// is only sentence-cased.</summary>
    internal static string Prettify(string text)
    {
        var builder = new System.Text.StringBuilder();
        var trimmed = text.Trim();
        for (var i = 0; i < trimmed.Length; i++)
        {
            var character = trimmed[i];
            if (i > 0 && char.IsUpper(character) && char.IsLower(trimmed[i - 1]))
            {
                builder.Append(' ');
                builder.Append(char.ToLowerInvariant(character));
            }
            else
            {
                builder.Append(i == 0 ? char.ToUpperInvariant(character) : character);
            }
        }

        return builder.ToString().Replace('_', ' ');
    }
}
