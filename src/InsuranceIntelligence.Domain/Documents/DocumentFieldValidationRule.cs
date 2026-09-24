using System.Security.Cryptography;
using System.Text;
using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Domain.Documents;

public enum DocumentFieldRuleKind
{
    /// <summary>Value, trimmed, must be exactly the given length.</summary>
    ExactLength,

    /// <summary>Value, trimmed, must match the given regular expression.</summary>
    Pattern,
}

/// <summary>
/// A field-level validation rule for extracted document data, distinct from
/// <see cref="DocumentChecklistRules"/>, which decides which documents are required.
/// This decides whether a value inside a document that was submitted is shaped correctly
/// (a Ugandan national ID must be 14 characters, for example).
///
/// Data, not code: a rule set is tenant-scoped and stored, so it can be corrected or extended by
/// someone with real regulatory knowledge without a deployment. See
/// docs/03-design-architecture/iip_fnol_six_step_intake_design.md and the claims-workflow gap
/// analysis (Phase 2) for the design this implements.
///
/// Field matching is alias-based rather than exact-key, because Azure Document Intelligence's
/// generic key-value extraction (AzureDocumentIntelligenceProvider.BuildExtraction) derives its
/// field keys from whatever label text appears on the page, "National ID:", "NIN No.", "ID
/// Number" all normalize to different keys. A rule matches any extracted field whose key contains
/// one of its aliases (case-insensitive), not one fixed name.
/// </summary>
public sealed record DocumentFieldValidationRule
{
    public required string RuleId { get; init; }
    public required TenantScope Scope { get; init; }
    public required DocumentType DocumentType { get; init; }
    public required string FieldLabel { get; init; }
    public required IReadOnlyCollection<string> FieldKeyAliases { get; init; }
    public required DocumentFieldRuleKind Kind { get; init; }
    public int? ExactLength { get; init; }
    public string? Pattern { get; init; }
    public required string Severity { get; init; }
    public required string Message { get; init; }

    /// <summary>Empty means every product line.</summary>
    public IReadOnlyCollection<string> ApplicableProductLines { get; init; } = [];
    public bool Enabled { get; init; } = true;
    public string? Source { get; init; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Stable, tenant-unique storage key derived from the human-readable RuleId, same
    /// pattern as FraudSignalDisposition.DeterministicId, so a tenant editing its own copy of a
    /// default rule (same RuleId) always lands on the same row instead of creating a duplicate.</summary>
    public static Guid StorageKey(string ruleId, TenantScope scope)
    {
        var key = $"{scope.TenantId}:{scope.WorkspaceId}:{ruleId}";
        var hash = MD5.HashData(Encoding.UTF8.GetBytes(key));
        return new Guid(hash);
    }
}

public interface IDocumentFieldValidationRuleRepository
{
    /// <summary>
    /// Tenant-configured rules only, never the built-in defaults. Callers combine this with
    /// <see cref="DocumentFieldValidationRuleDefaults.All"/> via
    /// <see cref="DocumentFieldValidator.ResolveRules"/> so a tenant always has at least the
    /// defaults active without needing to seed them.
    /// </summary>
    Task<IReadOnlyCollection<DocumentFieldValidationRule>> ListAsync(TenantScope scope, CancellationToken cancellationToken = default);

    Task SaveAsync(DocumentFieldValidationRule rule, CancellationToken cancellationToken = default);

    Task<DocumentFieldValidationRule?> GetAsync(string ruleId, TenantScope scope, CancellationToken cancellationToken = default);
}
