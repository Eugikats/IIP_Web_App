namespace InsuranceIntelligence.Application.Products;

public sealed record WordingCitationDto(string Quote, string? Clause, int? Page, bool Verified);

public sealed record WordingDraftItemDto(
    string ItemId,
    string Kind,
    string Title,
    string Detail,
    WordingCitationDto Citation,
    string Decision,
    bool Edited,
    string? Problem,
    string? Note,
    IReadOnlyList<string> Aliases,
    IReadOnlyList<string> ClaimTypeNames,
    bool Required,
    string? DocumentKey,
    string? FieldType,
    IReadOnlyList<string> Options);

public sealed record WordingDraftDto(
    Guid DraftId,
    Guid ProductId,
    Guid KnowledgeDocumentId,
    string DocumentName,
    string Status,
    IReadOnlyList<WordingDraftItemDto> Items,
    string Provider,
    string? ModelVersion,
    bool TextTruncated,
    int CharactersRead,
    string CreatedByUserId,
    DateTimeOffset CreatedAt,
    string? AppliedByUserId,
    DateTimeOffset? AppliedAt,
    int? AppliedProductVersion,
    int AcceptedCount,
    IReadOnlyList<string> DocumentTypeKeys);

public sealed record CreateWordingDraftRequest(Guid? KnowledgeDocumentId);

/// <summary>The decision and any changes a person makes to one item. Only the supplied properties
/// change. Editing an item the model proposed marks it as edited.</summary>
public sealed record UpdateWordingItemRequest(
    string? Decision,
    string? Title,
    string? Detail,
    IReadOnlyList<string>? Aliases,
    IReadOnlyList<string>? ClaimTypeNames,
    bool? Required,
    string? DocumentKey,
    string? FieldType,
    IReadOnlyList<string>? Options);

public sealed record ApplyWordingDraftRequest(string? ChangeReason);

public sealed record WordingApplyResultDto(
    WordingDraftDto Draft,
    int ProductVersion,
    int CoverageAdded,
    int ClaimTypesAdded,
    int DocumentsAdded,
    int FieldsAdded,
    IReadOnlyList<WordingSkippedItem> Skipped);

public sealed class WordingDraftNotFoundException(string message) : InvalidOperationException(message);

public interface IWordingDraftService
{
    /// <summary>Reads the product's approved wording (or the one named) and drafts the setup it
    /// implies. Nothing is applied.</summary>
    Task<WordingDraftDto> CreateAsync(Guid productId, CreateWordingDraftRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WordingDraftDto>> ListAsync(Guid productId, CancellationToken cancellationToken = default);

    Task<WordingDraftDto> GetAsync(Guid productId, Guid draftId, CancellationToken cancellationToken = default);

    Task<WordingDraftDto> UpdateItemAsync(Guid productId, Guid draftId, string itemId, UpdateWordingItemRequest request, CancellationToken cancellationToken = default);

    /// <summary>Writes the accepted items into a new draft version of the product. Publishing that
    /// version still needs the usual second approver.</summary>
    Task<WordingApplyResultDto> ApplyAsync(Guid productId, Guid draftId, ApplyWordingDraftRequest request, CancellationToken cancellationToken = default);

    Task<WordingDraftDto> DiscardAsync(Guid productId, Guid draftId, CancellationToken cancellationToken = default);
}
