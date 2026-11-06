using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Products;
using InsuranceIntelligence.Application.Workspaces;

namespace InsuranceIntelligence.Application.CustomerAssistant;

/// <summary>
/// Whose assistant Kato is for one workspace, and which covers he may sell there (build-loop item 27). The
/// organisation's name comes from the workspace's operating settings, the covers from its published products that
/// carry an offer; neither is read from another workspace.
/// </summary>
public interface IAssistantBrandProvider
{
    Task<KatoBrand> ForAsync(TenantScope scope, CancellationToken cancellationToken = default);
}

public static class AssistantBrands
{
    /// <summary>The workspace's brand, or the deployment's default when no provider is set. Bind the result with
    /// <see cref="KatoBrand.Use"/> in the same method that awaits this, so it applies to everything the method does.</summary>
    public static async Task<KatoBrand> ForAsync(IAssistantBrandProvider? provider, TenantScope scope, CancellationToken cancellationToken = default)
    {
        if (provider is null)
        {
            return KatoBrand.Current;
        }

        // A failed workspace lookup cannot become the deployment's default identity: that
        // would answer a real customer as another organisation.
        return await provider.ForAsync(scope, cancellationToken);
    }
}

public sealed class TenantAssistantBrandProvider(
    ITenantSettingsRepository settingsRepository,
    IProductRepository productRepository,
    bool builtInCatalogueFallback,
    WorkspaceBrandService? workspaceBrands = null) : IAssistantBrandProvider
{
    public async Task<KatoBrand> ForAsync(TenantScope scope, CancellationToken cancellationToken = default)
    {
        var settings = await settingsRepository.GetAsync(scope, cancellationToken);
        var brand = !string.IsNullOrWhiteSpace(settings.AssistantOrganisation)
            ? KatoBrand.FromSettings(settings.AssistantOrganisation, settings.AssistantShortName, settings.AssistantPartnerInsurers)
            : (workspaceBrands is null ? null : await workspaceBrands.GetAsync(scope, cancellationToken))?.ToBrand() ?? KatoBrand.Default;

        var offered = (await productRepository.ListCurrentForAllProductsAsync(scope, cancellationToken))
            .Where(product => product.Status == ProductStatus.Published && product.Offer is not null)
            .OrderBy(product => product.Name, StringComparer.OrdinalIgnoreCase)
            .Select(ToCover)
            .ToArray();
        if (offered.Length > 0)
        {
            return brand with { Offered = offered };
        }

        // A workspace with nothing configured sells nothing, honestly. Only a developer or demonstration runtime falls
        // back to the built-in demonstration catalogue, so a real insurer's customer is never offered a cover that
        // insurer does not sell.
        return builtInCatalogueFallback ? brand : brand with { Offered = [] };
    }

    private static MicroinsuranceCatalog.MicroProduct ToCover(Product product)
    {
        var offer = product.Offer!;
        var levels = offer.Levels.Select(level => new MicroinsuranceCatalog.CoverLevel(level.SumInsured, level.MonthlyPremium)).ToArray();
        var top = levels[^1];
        return new MicroinsuranceCatalog.MicroProduct(string.IsNullOrWhiteSpace(offer.CustomerName) ? product.Name : offer.CustomerName!, product.ProductClass, offer.Summary)
        {
            Code = string.IsNullOrWhiteSpace(offer.Code) ? "MI" : offer.Code!,
            Insurer = offer.Insurer,
            PolicyPrefix = string.IsNullOrWhiteSpace(offer.PolicyPrefix) ? "POL" : offer.PolicyPrefix!,
            MonthlyPremium = top.MonthlyPremium,
            SumInsured = top.SumInsured,
            WhoItIsFor = offer.WhoItIsFor,
            WaitingPeriod = offer.WaitingPeriod,
            PapersForAClaim = offer.PapersForAClaim,
            Levels = levels,
        };
    }
}
