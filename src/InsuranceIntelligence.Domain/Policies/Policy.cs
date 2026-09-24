namespace InsuranceIntelligence.Domain.Policies;

/// <summary>Outcome N acceptance: "marked with where it came from." Manual is today's only real
/// path (an officer or a seed script wrote it directly); the other three name the sources this
/// outcome's own three sub-features would produce, so a record created by any of them is honest
/// about its origin, even for the ones not built in this pass.</summary>
public enum PolicyRecordSource
{
    Manual,
    FileImport,
    PartnerApi,
    ExternalLookup,
}

public sealed record Policy(
    string PolicyId,
    string TenantId,
    string WorkspaceId,
    string PolicyNumber,
    string ProductLine,
    string? CoverType,
    string? CustomerId,
    string? BranchId,
    DateOnly? EffectiveFrom,
    DateOnly? EffectiveTo,
    decimal? AnnualPremium = null,
    string? PremiumCurrency = null,
    decimal? SumInsured = null,
    PolicyRecordSource RecordSource = PolicyRecordSource.Manual,
    // Outcome N acceptance: "when it was last refreshed." Null for a record that has never been
    // (re)loaded from an external source, distinct from CreatedAt, which this record does not even
    // carry today (Policy has no audit timestamps at all; adding just this one, rather than a full
    // CreatedAt/UpdatedAt pair, keeps this change to what the acceptance criterion actually needs).
    DateTimeOffset? LastRefreshedAt = null);
