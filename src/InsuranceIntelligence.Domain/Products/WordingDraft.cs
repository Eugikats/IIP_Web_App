using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Domain.Products;

public enum WordingItemKind
{
    Covered,
    Exclusion,
    ClaimType,
    Document,
    Field,
}

public enum WordingItemDecision
{
    Pending,
    Accepted,
    Rejected,
}

public enum WordingDraftStatus
{
    InReview,
    Applied,
    Discarded,
}

/// <summary>Where in the wording an item came from. <see cref="Quote"/> is the clause text itself;
/// <see cref="Verified"/> is true only when that text was found in the wording, so a quote the model
/// invented or paraphrased is visible as such and cannot be accepted.</summary>
public sealed record WordingCitation(string Quote, string? Clause, int? Page, bool Verified);

/// <summary>
/// One thing the model proposes to configure from a policy wording, with the clause it relies on.
/// It does nothing until a person accepts it and the draft is applied. Only the properties that fit
/// the <see cref="Kind"/> are used: a claim type uses aliases, a document uses the document key,
/// claim type names and whether it is required, a field uses the field type, options and required.
/// </summary>
public sealed record WordingDraftItem
{
    public required string ItemId { get; init; }
    public required WordingItemKind Kind { get; init; }
    public required string Title { get; init; }
    public string Detail { get; init; } = string.Empty;
    public required WordingCitation Citation { get; init; }
    public WordingItemDecision Decision { get; init; } = WordingItemDecision.Pending;

    /// <summary>True once a person changed the model's wording of this item.</summary>
    public bool Edited { get; init; }

    /// <summary>Why this item cannot be accepted as it stands (for example the quoted clause is not
    /// in the wording, or no document type with that name exists). Null when it can.</summary>
    public string? Problem { get; init; }

    /// <summary>Something worth knowing that does not block acceptance, for example that the
    /// product already has this claim type.</summary>
    public string? Note { get; init; }

    public IReadOnlyList<string> Aliases { get; init; } = [];
    public IReadOnlyList<string> ClaimTypeNames { get; init; } = [];
    public bool Required { get; init; } = true;
    public string? DocumentKey { get; init; }
    public string? FieldType { get; init; }
    public IReadOnlyList<string> Options { get; init; } = [];
}

/// <summary>
/// The model's reading of a product's policy wording, held for a person to review item by item
/// (build-loop item 7). Nothing in it is part of the product until <see cref="WordingDraftStatus.Applied"/>,
/// which writes the accepted items into a new draft version of the product; publishing that version
/// still needs the usual second approver. It records the document and provider it came from, never
/// the wording text itself.
/// </summary>
public sealed record WordingDraft
{
    public required Guid DraftId { get; init; }
    public required TenantScope Scope { get; init; }
    public required Guid ProductId { get; init; }
    public required Guid KnowledgeDocumentId { get; init; }
    public required string DocumentName { get; init; }
    public required WordingDraftStatus Status { get; init; }
    public required IReadOnlyList<WordingDraftItem> Items { get; init; }

    public required string Provider { get; init; }
    public string? ModelVersion { get; init; }

    /// <summary>True when the wording was longer than the model could be given, so the items cover
    /// only the start of it. The review screen says so.</summary>
    public bool TextTruncated { get; init; }
    public int CharactersRead { get; init; }

    public required string CreatedByUserId { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public string? AppliedByUserId { get; init; }
    public DateTimeOffset? AppliedAt { get; init; }
    public int? AppliedProductVersion { get; init; }

    public int AcceptedCount => Items.Count(item => item.Decision == WordingItemDecision.Accepted);
}

public interface IWordingDraftRepository
{
    Task SaveAsync(WordingDraft draft, CancellationToken cancellationToken = default);

    Task<WordingDraft?> GetAsync(TenantScope scope, Guid draftId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WordingDraft>> ListForProductAsync(TenantScope scope, Guid productId, CancellationToken cancellationToken = default);
}
