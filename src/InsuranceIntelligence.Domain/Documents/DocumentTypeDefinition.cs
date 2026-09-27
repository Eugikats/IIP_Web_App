using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Domain.Documents;

/// <summary>
/// A document type an insurer's workspace actually recognises, owned by the tenant and versioned
/// the same way as <c>CommunicationTemplate</c> and <c>Product</c>: every save appends a version,
/// nothing is overwritten in place.
///
/// This sits on top of the fixed <see cref="Documents.DocumentType"/> enum rather than replacing
/// it (claim intake build prompt outcome A). Classification, extraction and the completeness score
/// stay built on that enum, so a catalog entry whose <see cref="BuiltInType"/> is set is simply a
/// customer-facing name and reason for one of those eight values. A catalog entry with no
/// <see cref="BuiltInType"/> is one the insurer added that the classifier does not know: it is
/// still trackable as required or received (see <see cref="CustomDocumentRequirement"/>), by the
/// key the customer or an officer chose, but nothing recognises it automatically until it has real
/// examples to learn from.
///
/// The product document matrix's <c>DocumentKey</c> is validated against a tenant's active catalog
/// at save time (<c>ProductCommandService</c>), which is what makes a document type
/// disappearing on a typo (the silent-drop the earlier gap analysis found) impossible: an unknown
/// key is now refused, not skipped.
/// </summary>
public sealed record DocumentTypeDefinition
{
    public required Guid DefinitionId { get; init; }
    public required Guid VersionId { get; init; }
    public required TenantScope Scope { get; init; }
    public required int Version { get; init; }

    /// <summary>A stable, tenant-chosen key, unique per tenant regardless of case. Equal to the
    /// enum name for a built-in type, so existing stored documents (which hold the enum name as
    /// their document type) resolve against the default catalog with no migration.</summary>
    public required string Key { get; init; }

    public required string CustomerLabel { get; init; }

    /// <summary>Why this is asked for, in words a customer or an officer would read, not an
    /// internal justification.</summary>
    public required string Description { get; init; }

    /// <summary>Set when this key names one of the eight values the classifier can actually
    /// recognise. Null for a type the insurer added that starts out unrecognised.</summary>
    public DocumentType? BuiltInType { get; init; }

    /// <summary>An inactive definition is kept for history (a document filed against it stays
    /// readable) but is no longer offered when configuring a product.</summary>
    public required bool Active { get; init; }

    public required string UpdatedByUserId { get; init; }
    public required DateTimeOffset UpdatedAt { get; init; }
    public required string ChangeReason { get; init; }
}

public interface IDocumentTypeCatalogRepository
{
    Task<DocumentTypeDefinition?> GetCurrentAsync(TenantScope scope, string key, CancellationToken cancellationToken = default);

    /// <summary>The current (highest-version) row for every key this tenant has ever saved,
    /// active or not.</summary>
    Task<IReadOnlyCollection<DocumentTypeDefinition>> ListCurrentAsync(TenantScope scope, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<DocumentTypeDefinition>> ListVersionsAsync(TenantScope scope, string key, CancellationToken cancellationToken = default);

    Task SaveAsync(DocumentTypeDefinition version, CancellationToken cancellationToken = default);
}

/// <summary>The eight built-in types, as the catalog every tenant starts with. A workspace that has
/// never saved its own catalog reads this; the first save copies it in, since nothing here is
/// itself persisted.</summary>
public static class DefaultDocumentTypeCatalog
{
    public static IReadOnlyList<(string Key, string CustomerLabel, string Description, DocumentType BuiltInType)> Defaults() =>
    [
        ("ClaimForm", "Claim form", "The completed claim form for this loss.", Documents.DocumentType.ClaimForm),
        ("PoliceAbstract", "Police abstract", "Confirms the incident was reported to the police.", Documents.DocumentType.PoliceAbstract),
        ("Invoice", "Invoice", "Shows the cost of repair, replacement or treatment.", Documents.DocumentType.Invoice),
        ("MedicalReport", "Medical report", "A doctor's account of the injury or treatment.", Documents.DocumentType.MedicalReport),
        ("PhotoEvidence", "Photos of the damage", "Photos showing what happened.", Documents.DocumentType.PhotoEvidence),
        ("CustomerId", "Your ID", "Confirms the claim is yours.", Documents.DocumentType.CustomerId),
        ("GarageEstimate", "Garage estimate", "A garage's estimate to repair the vehicle.", Documents.DocumentType.GarageEstimate),
    ];

    public static IReadOnlyCollection<DocumentTypeDefinition> BuildFor(TenantScope scope, string systemActorId, DateTimeOffset now) =>
        Defaults()
            .Select(entry => new DocumentTypeDefinition
            {
                DefinitionId = Guid.NewGuid(),
                VersionId = Guid.NewGuid(),
                Scope = scope,
                Version = 1,
                Key = entry.Key,
                CustomerLabel = entry.CustomerLabel,
                Description = entry.Description,
                BuiltInType = entry.BuiltInType,
                Active = true,
                UpdatedByUserId = systemActorId,
                UpdatedAt = now,
                ChangeReason = "Default document type.",
            })
            .ToArray();
}

/// <summary>A document a product requires or accepts that is not one of the eight built-in types.
/// Tracked by key rather than by <see cref="Documents.DocumentType"/>: satisfied when a case
/// document's key matches, whether or not the classifier recognised it.</summary>
public sealed record CustomDocumentRequirement(string Key, string Label, string Description);
