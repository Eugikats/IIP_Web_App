using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Products;

namespace InsuranceIntelligence.Application.Products;

public sealed class ProductQueryService(
    ITenantContextAccessor tenantContextAccessor,
    IPermissionService permissionService,
    IProductRepository repository) : IProductQueryService
{
    public async Task<ProductDto?> GetCurrentAsync(Guid productId, CancellationToken cancellationToken = default)
    {
        await permissionService.RequirePermissionAsync(Permission.ManageProducts, cancellationToken);

        var context = tenantContextAccessor.Current;
        var product = await repository.GetCurrentAsync(context.Scope, productId, cancellationToken);
        return product is null ? null : ProductCommandService.ToDto(product);
    }

    public async Task<ProductDto?> GetVersionAsync(Guid productId, int version, CancellationToken cancellationToken = default)
    {
        await permissionService.RequirePermissionAsync(Permission.ManageProducts, cancellationToken);

        var context = tenantContextAccessor.Current;
        var product = await repository.GetVersionAsync(context.Scope, productId, version, cancellationToken);
        return product is null ? null : ProductCommandService.ToDto(product);
    }

    public async Task<IReadOnlyList<ProductVersionSummaryDto>> ListVersionsAsync(Guid productId, CancellationToken cancellationToken = default)
    {
        await permissionService.RequirePermissionAsync(Permission.ManageProducts, cancellationToken);

        var context = tenantContextAccessor.Current;
        var versions = await repository.ListVersionsAsync(context.Scope, productId, cancellationToken);
        return versions
            .OrderByDescending(version => version.Version)
            .Select(version => new ProductVersionSummaryDto(
                version.Version,
                version.Status.ToString(),
                version.UpdatedByUserId,
                version.UpdatedAt,
                version.ChangeReason))
            .ToArray();
    }

    public async Task<IReadOnlyList<ProductListItemDto>> ListProductsAsync(CancellationToken cancellationToken = default)
    {
        await permissionService.RequirePermissionAsync(Permission.ManageProducts, cancellationToken);

        var context = tenantContextAccessor.Current;
        var products = await repository.ListCurrentForAllProductsAsync(context.Scope, cancellationToken);
        return products
            .OrderBy(product => product.Name, StringComparer.OrdinalIgnoreCase)
            .Select(product => new ProductListItemDto(
                product.ProductId,
                product.Name,
                product.ProductClass,
                product.Status.ToString(),
                product.Version,
                product.UpdatedAt))
            .ToArray();
    }

    public async Task<IReadOnlyList<ClaimStageDto>> GetDefaultStageLibraryAsync(CancellationToken cancellationToken = default)
    {
        await permissionService.RequirePermissionAsync(Permission.ManageProducts, cancellationToken);

        return await Task.FromResult(
            DefaultClaimStageLibrary.Default()
                .OrderBy(stage => stage.SortOrder)
                .Select(stage => new ClaimStageDto(
                    stage.StageKey,
                    stage.Name,
                    stage.OwnerRole,
                    stage.IsAutomatic,
                    stage.SortOrder,
                    stage.ServiceStandardWorkingDays,
                    stage.ValueTierStandards?.Select(tier => new ValueTierServiceStandardDto(tier.UpToValueUgx, tier.WorkingDays)).ToArray()))
                .ToArray());
    }
}
