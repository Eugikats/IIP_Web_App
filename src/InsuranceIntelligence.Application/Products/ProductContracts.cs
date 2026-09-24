namespace InsuranceIntelligence.Application.Products;

public sealed record ValueTierServiceStandardDto(decimal? UpToValueUgx, int WorkingDays);

public sealed record ClaimStageDto(
    string StageKey,
    string Name,
    string OwnerRole,
    bool IsAutomatic,
    int SortOrder,
    int? ServiceStandardWorkingDays,
    IReadOnlyList<ValueTierServiceStandardDto>? ValueTierStandards);

public sealed record DocumentMatrixEntryDto(
    string DocumentKey,
    string CustomerLabel,
    string Reason,
    bool IsRequired,
    string RequiredAtStageKey,
    bool IsRegulatoryOrWordingMandated);

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
    string? PublishRejectionReason);

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
    bool UseDefaultStageLibrary = true);

public sealed record UpdateProductRequest(
    string? Name,
    string? ProductClass,
    string? IraApprovalReference,
    DateOnly? IraApprovalEffectiveDate,
    string? ApprovedWordingKnowledgeDocumentId,
    IReadOnlyList<ClaimStageDto>? Stages,
    IReadOnlyList<DocumentMatrixEntryDto>? DocumentMatrix,
    string? ChangeReason);

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
