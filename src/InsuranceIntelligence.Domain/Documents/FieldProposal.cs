using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Domain.Documents;

/// <summary>Which value read from a document feeds which question on the claim. Configured per
/// document type, so an insurer says what its own documents can fill in.</summary>
public sealed record ExtractionFieldSpec(string ExtractedKey, string ClaimFieldKey);

/// <summary>The reading rules a built-in document type has until the insurer sets its own. Kept
/// small on purpose: only mappings that are certainly right for every insurer.</summary>
public static class ExtractionSchemas
{
    public static IReadOnlyList<ExtractionFieldSpec> Defaults(DocumentType type) => type switch
    {
        DocumentType.MedicalReport =>
        [
            new("PatientName", "patientName"),
            new("TreatmentDate", "treatmentDate"),
            new("Facility", "providerName"),
        ],
        _ => [],
    };

    /// <summary>Extracted keys come back as "field.ReportNumber" from some providers and
    /// "ReportNumber" from others; they match without the prefix and without case.</summary>
    public static string Normalise(string extractedKey)
    {
        var key = extractedKey.Trim();
        return (key.StartsWith("field.", StringComparison.OrdinalIgnoreCase) ? key[6..] : key).Trim().ToLowerInvariant();
    }
}

public enum FieldProposalStatus
{
    Proposed,
    Accepted,
    Rejected,

    /// <summary>Replaced by a newer reading of the same document.</summary>
    Superseded,
}

/// <summary>
/// A value read from a document that could fill in one of the claim's questions (build-loop item
/// 15). It is only ever a proposal: it changes nothing on the claim until an officer accepts it
/// (optionally correcting it first). A value a person has already put on the claim is never
/// replaced by a proposal on its own; such a proposal is marked as conflicting and needs an explicit
/// replace.
/// </summary>
public sealed record FieldProposal
{
    public required Guid ProposalId { get; init; }
    public required Guid CaseId { get; init; }
    public required TenantScope Scope { get; init; }
    public required string FieldKey { get; init; }
    public required string Value { get; init; }
    public required Guid SourceDocumentId { get; init; }
    public required string SourceExtractedKey { get; init; }
    public int? Page { get; init; }
    public required decimal Confidence { get; init; }
    public required FieldProposalStatus Status { get; init; }

    /// <summary>The claim already holds a different value for this question, put there by a person.</summary>
    public bool ConflictsWithConfirmed { get; init; }

    /// <summary>Set when the value does not fit the question's type (for example not a date).</summary>
    public string? Warning { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }
    public string? DecidedByUserId { get; init; }
    public DateTimeOffset? DecidedAt { get; init; }

    /// <summary>What was actually put on the claim when accepted, which differs from
    /// <see cref="Value"/> when the officer corrected it.</summary>
    public string? AcceptedValue { get; init; }
}

public interface IFieldProposalRepository
{
    Task SaveAsync(FieldProposal proposal, CancellationToken cancellationToken = default);

    Task<FieldProposal?> GetAsync(TenantScope scope, Guid caseId, Guid proposalId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FieldProposal>> ListForCaseAsync(TenantScope scope, Guid caseId, CancellationToken cancellationToken = default);
}

/// <summary>Where a value on a claim came from and who stands behind it.</summary>
public sealed record FieldProvenance(
    string Source,
    string ConfirmedBy,
    DateTimeOffset ConfirmedAt,
    Guid? DocumentId = null,
    int? Page = null,
    decimal? Confidence = null,
    bool CorrectedByPerson = false);
