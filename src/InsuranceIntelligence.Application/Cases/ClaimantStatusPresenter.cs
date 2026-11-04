using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Products;

namespace InsuranceIntelligence.Application.Cases;

/// <summary>What a customer is shown about where their claim stands.</summary>
public sealed record ClaimantStatusView(string Headline, string Detail, IReadOnlyList<ClaimantTimelineStep> Timeline);

public interface IClaimantStatusPresenter
{
    /// <summary>The headline, explanation and timeline for a claim, in the claim's own workspace.</summary>
    Task<ClaimantStatusView> BuildAsync(ClaimCase claim, CancellationToken cancellationToken = default);

    /// <summary>Just the customer's short wording for a status in a workspace, for messages that name it.</summary>
    Task<StatusWording> WordingAsync(Domain.Common.TenantScope scope, CaseStatus status, CancellationToken cancellationToken = default);
}

/// <summary>
/// Builds the customer's view of a claim's status (build-loop item 19) from the workspace's own wording
/// (or the defaults) and the claim's pinned product's stages. Customer-facing only: it names no staff
/// role, score or internal state.
/// </summary>
public sealed class ClaimantStatusPresenter(
    ITenantSettingsRepository settingsRepository,
    IProductRepository? productRepository = null) : IClaimantStatusPresenter
{
    public async Task<ClaimantStatusView> BuildAsync(ClaimCase claim, CancellationToken cancellationToken = default)
    {
        var settings = await settingsRepository.GetAsync(claim.Scope, cancellationToken);
        var wording = ClaimantStatusWords.For(claim.Status, settings.ClaimantStatusWording);

        IReadOnlyList<ClaimStageDefinition>? stages = null;
        if (productRepository is not null && claim.ProductId is { } productId && claim.ProductVersion is { } version)
        {
            stages = (await productRepository.GetVersionAsync(claim.Scope, productId, version, cancellationToken))?.Stages;
        }

        return new ClaimantStatusView(wording.Headline, wording.Detail, ClaimantTimeline.Build(claim.Status, stages));
    }

    public async Task<StatusWording> WordingAsync(Domain.Common.TenantScope scope, CaseStatus status, CancellationToken cancellationToken = default) =>
        ClaimantStatusWords.For(status, (await settingsRepository.GetAsync(scope, cancellationToken)).ClaimantStatusWording);
}
