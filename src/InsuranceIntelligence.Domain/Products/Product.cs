using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Domain.Products;

/// <summary>
/// A product's configuration: name, class, IRA approval, its claim process stages and its document
/// matrix, as one versioned snapshot (blueprint outcome A/C, IIP_NEXT_BUILD_PROMPT.md section 5).
/// Every save is a new, append-only version, never overwritten in place (ProductId stable across
/// versions, VersionId and Version change), the same pattern as AgentBehaviourProfile, so a claim
/// can record the exact version in force when it was filed and keep it even after the product is
/// edited or republished. A version cannot move to Published without an IRA approval reference and
/// effective date (see CanPublish), and publishing itself needs a second person, distinct from
/// whoever submitted it, enforced in ProductCommandService.ApprovePublishAsync.
/// </summary>
public sealed record Product
{
    public required Guid ProductId { get; init; }
    public required Guid VersionId { get; init; }
    public required TenantScope Scope { get; init; }
    public required int Version { get; init; }

    public required string Name { get; init; }
    public required string ProductClass { get; init; }

    public string? IraApprovalReference { get; init; }
    public DateOnly? IraApprovalEffectiveDate { get; init; }

    /// <summary>Knowledge document id of the approved wording attached to this product. Null means
    /// no wording has been attached yet, which also blocks publish (see CanPublish).</summary>
    public string? ApprovedWordingKnowledgeDocumentId { get; init; }

    public required ProductStatus Status { get; init; }

    public required IReadOnlyList<ClaimStageDefinition> Stages { get; init; }
    public required IReadOnlyList<ProductDocumentRequirement> DocumentMatrix { get; init; }

    public required string UpdatedByUserId { get; init; }
    public required DateTimeOffset UpdatedAt { get; init; }
    public required string ChangeReason { get; init; }

    public string? PublishSubmittedByUserId { get; init; }
    public DateTimeOffset? PublishSubmittedAt { get; init; }
    public string? PublishApprovedByUserId { get; init; }
    public DateTimeOffset? PublishApprovedAt { get; init; }
    public string? PublishRejectedByUserId { get; init; }
    public DateTimeOffset? PublishRejectedAt { get; init; }
    public string? PublishRejectionReason { get; init; }

    /// <summary>A product cannot be activated without an approval reference and its effective
    /// date. The screen must say so in plain words rather than failing silently (outcome A).</summary>
    public bool CanPublish =>
        !string.IsNullOrWhiteSpace(IraApprovalReference)
        && IraApprovalEffectiveDate is not null
        && Stages.Count > 0;
}

public enum ProductStatus
{
    Draft,
    PendingPublishApproval,
    Published,
    Retired
}

/// <summary>
/// One ordered stage of a product's claim process. The IRA settlement clock depends on the claim's
/// value tier rather than a flat day count, so a stage carries either a flat
/// ServiceStandardWorkingDays or a set of ValueTierStandards, never both meaningfully at once; see
/// ResolveServiceStandardWorkingDays.
/// </summary>
public sealed record ClaimStageDefinition(
    string StageKey,
    string Name,
    string OwnerRole,
    bool IsAutomatic,
    int SortOrder,
    int? ServiceStandardWorkingDays,
    IReadOnlyList<ValueTierServiceStandard>? ValueTierStandards = null)
{
    public int? ResolveServiceStandardWorkingDays(decimal? claimValueUgx)
    {
        if (ValueTierStandards is { Count: > 0 })
        {
            var value = claimValueUgx ?? 0m;
            foreach (var tier in ValueTierStandards.OrderBy(t => t.UpToValueUgx ?? decimal.MaxValue))
            {
                if (tier.UpToValueUgx is null || value <= tier.UpToValueUgx)
                {
                    return tier.WorkingDays;
                }
            }
        }

        return ServiceStandardWorkingDays;
    }
}

/// <summary>One value band of the settlement clock, e.g. "up to UGX 10,000,000 within 10 working
/// days". UpToValueUgx null means the open-ended top tier ("and above").</summary>
public sealed record ValueTierServiceStandard(decimal? UpToValueUgx, int WorkingDays);

public sealed record ProductDocumentRequirement(
    string DocumentKey,
    string CustomerLabel,
    string Reason,
    bool IsRequired,
    string RequiredAtStageKey,
    bool IsRegulatoryOrWordingMandated);

public interface IProductRepository
{
    /// <summary>The highest-Version row for this product, or null if it does not exist.</summary>
    Task<Product?> GetCurrentAsync(TenantScope scope, Guid productId, CancellationToken cancellationToken = default);

    /// <summary>A specific historical version, used to pin a claim to the version in force when it
    /// was filed even after the product moves on to later versions.</summary>
    Task<Product?> GetVersionAsync(TenantScope scope, Guid productId, int version, CancellationToken cancellationToken = default);

    /// <summary>Every saved version for this product, newest first.</summary>
    Task<IReadOnlyCollection<Product>> ListVersionsAsync(TenantScope scope, Guid productId, CancellationToken cancellationToken = default);

    /// <summary>The current version for every product this tenant has ever created.</summary>
    Task<IReadOnlyCollection<Product>> ListCurrentForAllProductsAsync(TenantScope scope, CancellationToken cancellationToken = default);

    /// <summary>Always inserts a new version row, never overwrites a previous one.</summary>
    Task SaveAsync(Product version, CancellationToken cancellationToken = default);
}
