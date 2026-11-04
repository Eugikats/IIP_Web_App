using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Domain.Documents;

/// <summary>
/// The single PDF built from the pictures in one document slot of one claim (build-loop item 12):
/// pictures of paperwork become pages in the order the customer arranged them. It is a derived copy
/// kept beside the originals, which are never replaced. Rebuilt whenever a page is added, removed or
/// moved, so it always matches what the slot holds now.
/// </summary>
public sealed record SlotPdf
{
    public required Guid CaseId { get; init; }
    public required TenantScope Scope { get; init; }
    public required string SlotKey { get; init; }

    /// <summary>Where the PDF is stored (a derived object, separate from every original).</summary>
    public required string ObjectKey { get; init; }

    public required string Sha256 { get; init; }
    public required long SizeBytes { get; init; }

    /// <summary>The documents that are its pages, first page first.</summary>
    public required IReadOnlyList<Guid> PageDocumentIds { get; init; }

    public required DateTimeOffset BuiltAt { get; init; }

    public int PageCount => PageDocumentIds.Count;
}

public interface ISlotPdfRepository
{
    /// <summary>The slot's current PDF, or null when it has none.</summary>
    Task<SlotPdf?> GetAsync(TenantScope scope, Guid caseId, string slotKey, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SlotPdf>> ListForCaseAsync(TenantScope scope, Guid caseId, CancellationToken cancellationToken = default);

    Task SaveAsync(SlotPdf pdf, CancellationToken cancellationToken = default);

    /// <summary>Forgets the slot's PDF (the slot is now empty or holds something that cannot be one).</summary>
    Task RemoveAsync(TenantScope scope, Guid caseId, string slotKey, CancellationToken cancellationToken = default);
}

public sealed record BuiltPdf(byte[] Content, int PageCount);

/// <summary>Makes one PDF from several pictures, one page each, in the order given. Reads the
/// pictures; never alters them.</summary>
public interface IDocumentPdfBuilder
{
    /// <summary>Null when any of the pictures cannot be read.</summary>
    Task<BuiltPdf?> BuildAsync(IReadOnlyList<(Stream Picture, string FileName)> pages, CancellationToken cancellationToken = default);
}
