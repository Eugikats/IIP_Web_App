namespace InsuranceIntelligence.Application.Products;

public sealed record ValueTierServiceStandardDto(decimal? UpToValueUgx, int WorkingDays);

public sealed record ClaimStageDto(
    string StageKey,
    string Name,
    string OwnerRole,
    bool IsAutomatic,
    int SortOrder,
    int? ServiceStandardWorkingDays,
    IReadOnlyList<ValueTierServiceStandardDto>? ValueTierStandards,
    string? CustomerLabel = null,
    bool? CustomerVisible = null,
    string? MilestoneStatus = null);

public sealed record DocumentMatrixEntryDto(
    string DocumentKey,
    string CustomerLabel,
    string Reason,
    bool IsRequired,
    string RequiredAtStageKey,
    bool IsRegulatoryOrWordingMandated,
    IReadOnlyList<string>? ClaimTypeKeys = null,
    string? Kind = null,
    bool AllowsMultiplePages = false,
    int? MaxFiles = null,
    IReadOnlyList<string>? AcceptedFormats = null);

public sealed record ProductWordingFactDto(
    string Category,
    string Label,
    string Value,
    Guid SourceKnowledgeDocumentId,
    string SourceChunkId,
    string SourceQuote,
    decimal Confidence);
public sealed record ClaimTypeDto(
    string? Key,
    string Name,
    string? Description,
    bool Active,
    int SortOrder,
    IReadOnlyList<string>? Aliases);

public sealed record ClaimFieldDto(
    string? Key,
    string Label,
    string Type,
    bool Required,
    IReadOnlyList<string>? Options,
    string? HelpText,
    int SortOrder,
    bool CustomerVisible,
    IReadOnlyList<string>? ClaimTypeKeys);

public sealed record CoverageItemDto(
    string? Key,
    string Kind,
    string Title,
    string? Description,
    string? Clause,
    int? Page,
    string? Quote);

public sealed record ProductDto(
    Guid ProductId,
    int Version,
    string Name,
    string ProductClass,
    string? IraApprovalReference,
    DateOnly? IraApprovalEffectiveDate,
    string? ApprovedWordingKnowledgeDocumentId,
    string Status,
    bool CanPublish,
    IReadOnlyList<ClaimStageDto> Stages,
    IReadOnlyList<DocumentMatrixEntryDto> DocumentMatrix,
    string UpdatedByUserId,
    DateTimeOffset UpdatedAt,
    string ChangeReason,
    string? PublishSubmittedByUserId,
    DateTimeOffset? PublishSubmittedAt,
    string? PublishApprovedByUserId,
    DateTimeOffset? PublishApprovedAt,
    string? PublishRejectedByUserId,
    DateTimeOffset? PublishRejectedAt,
    string? PublishRejectionReason,
    IReadOnlyList<ProductWordingFactDto> WordingFacts,
    IReadOnlyList<ClaimTypeDto>? ClaimTypes = null,
    IReadOnlyList<ClaimFieldDto>? ClaimFields = null,
    IReadOnlyList<CoverageItemDto>? CoverageItems = null,
    ProductOfferDto? Offer = null);

public sealed record ProductOfferDto(
    string? Insurer,
    string? Summary,
    string? WhoItIsFor,
    string? WaitingPeriod,
    string? PapersForAClaim,
    IReadOnlyList<ProductOfferLevelDto>? Levels,
    string? PolicyPrefix = null,
    string? Code = null,
    string? CustomerName = null);

public sealed record ProductOfferLevelDto(decimal SumInsured, decimal MonthlyPremium);

public sealed record ProductVersionSummaryDto(
    int Version,
    string Status,
    string UpdatedByUserId,
    DateTimeOffset UpdatedAt,
    string ChangeReason);

public sealed record ProductListItemDto(
    Guid ProductId,
    string Name,
    string ProductClass,
    string Status,
    int Version,
    DateTimeOffset UpdatedAt);

public sealed record CreateProductRequest(
    string? Name,
    string? ProductClass,
    string? ChangeReason,
    bool UseDefaultStageLibrary = true,
    IReadOnlyList<ProductWordingFactDto>? WordingFacts = null);

public sealed record UpdateProductRequest(
    string? Name,
    string? ProductClass,
    string? IraApprovalReference,
    DateOnly? IraApprovalEffectiveDate,
    string? ApprovedWordingKnowledgeDocumentId,
    IReadOnlyList<ClaimStageDto>? Stages,
    IReadOnlyList<DocumentMatrixEntryDto>? DocumentMatrix,
    string? ChangeReason,
    IReadOnlyList<ProductWordingFactDto>? WordingFacts = null,
    IReadOnlyList<ClaimTypeDto>? ClaimTypes = null,
    IReadOnlyList<ClaimFieldDto>? ClaimFields = null,
    IReadOnlyList<CoverageItemDto>? CoverageItems = null,
    ProductOfferDto? Offer = null,
    bool RemoveOffer = false);

public sealed record SubmitProductRequest(string? ChangeReason);

public sealed record ApproveProductPublishRequest(string? ChangeReason);

public sealed record RejectProductPublishRequest(string? Reason);

public sealed record RetireProductRequest(string? ChangeReason);

public interface IProductCommandService
{
    Task<ProductDto> CreateDraftAsync(CreateProductRequest request, CancellationToken cancellationToken = default);

    Task<ProductDto> UpdateDraftAsync(Guid productId, UpdateProductRequest request, CancellationToken cancellationToken = default);

    Task<ProductDto> SubmitForPublishApprovalAsync(Guid productId, SubmitProductRequest request, CancellationToken cancellationToken = default);

    Task<ProductDto> ApprovePublishAsync(Guid productId, ApproveProductPublishRequest request, CancellationToken cancellationToken = default);

    Task<ProductDto> RejectPublishAsync(Guid productId, RejectProductPublishRequest request, CancellationToken cancellationToken = default);

    Task<ProductDto> RetireAsync(Guid productId, RetireProductRequest request, CancellationToken cancellationToken = default);
}

public interface IProductQueryService
{
    Task<ProductDto?> GetCurrentAsync(Guid productId, CancellationToken cancellationToken = default);

    Task<ProductDto?> GetVersionAsync(Guid productId, int version, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProductVersionSummaryDto>> ListVersionsAsync(Guid productId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProductListItemDto>> ListProductsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ClaimStageDto>> GetDefaultStageLibraryAsync(CancellationToken cancellationToken = default);
}

public sealed class ProductValidationException(string field, string message) : InvalidOperationException(message)
{
    public string Field { get; } = field;
}

/// <summary>One claim type as offered in a dropdown or to the assistant.</summary>
public sealed record ClaimTypeOptionDto(string Key, string Name, string Description);

/// <summary>The claim types a published product offers for one product line. Configured is false
/// when no published product has any, in which case callers keep the free-text claim type.</summary>
public sealed record ClaimTypeOptionsDto(
    string ProductLine,
    Guid? ProductId,
    string? ProductName,
    bool Configured,
    IReadOnlyList<ClaimTypeOptionDto> ClaimTypes);

public sealed record ClaimTypeSuggestionDto(string? Key, string? Name, bool Matched);

public sealed record ClaimTypeImportResultDto(
    Guid ProductId,
    int ProductVersion,
    IReadOnlyList<string> AddedClaimTypes,
    int ClaimsUpdated);

public interface IClaimTypeService
{
    /// <summary>The active claim types of the Published product for a product line (staff and
    /// claimant dropdowns, and the WhatsApp assistant).</summary>
    Task<ClaimTypeOptionsDto> ListForProductLineAsync(InsuranceIntelligence.Domain.Common.TenantScope scope, string? productLine, CancellationToken cancellationToken = default);

    /// <summary>A suggestion only; the person filing still chooses.</summary>
    Task<ClaimTypeSuggestionDto> SuggestAsync(InsuranceIntelligence.Domain.Common.TenantScope scope, string? productLine, string? text, CancellationToken cancellationToken = default);

    /// <summary>Turns the free-text claim types already used on this product's claims into
    /// configured claim types and links those claims to them. Additive: nothing is removed.</summary>
    Task<ClaimTypeImportResultDto> ImportExistingAsync(Guid productId, CancellationToken cancellationToken = default);
}
