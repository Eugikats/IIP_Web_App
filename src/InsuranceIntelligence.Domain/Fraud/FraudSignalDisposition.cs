using System.Security.Cryptography;
using System.Text;
using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Domain.Fraud;

// WO-19: FraudRuleEngine recomputes signals fresh from live claim data on every read — there is no
// persisted signal row to attach a workflow to. A disposition only exists once a human triages the
// signal, keyed by a deterministic id derived from the same RuleId + RelatedCaseIds the engine
// already produces, so the same underlying pattern always maps to the same disposition record
// without the client ever computing or guessing an id.
public enum FraudDispositionStatus
{
    Triaged,
    UnderInvestigation,
    OutcomeRecorded,
    Closed,
}

// Process outcomes only — never an adverse fraud determination (I4, DEC-014). IIP is a triage and
// evidence-preparation tool; if a concern is substantiated, that determination is made by a human
// investigator/SIU process outside this system, not recorded here as "confirmed fraud".
public enum FraudDispositionOutcome
{
    NoFurtherActionRequired,
    ReferredForSpecialInvestigation,
    ClaimContinuesUnderNormalReview,
    UnableToSubstantiateConcern,
}

public sealed record FraudSignalDisposition
{
    public required Guid SignalId { get; init; }
    public required TenantScope Scope { get; init; }
    public required string RuleId { get; init; }
    public required IReadOnlyList<Guid> RelatedCaseIds { get; init; }
    public required FraudDispositionStatus Status { get; init; }
    public string? AssignedToUserId { get; init; }
    public FraudDispositionOutcome? Outcome { get; init; }
    public string? OutcomeNotes { get; init; }
    public required string CreatedByUserId { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required DateTimeOffset UpdatedAt { get; init; }
    public required string CorrelationId { get; init; }

    /// <summary>Deterministic id for a signal instance — the same RuleId + RelatedCaseIds always
    /// produces the same id, regardless of which request first triages it or when it is
    /// recomputed. Not a security boundary (scope isolation is still enforced by TenantScope on
    /// every read/write), just a stable join key between the live rule engine output and the
    /// persisted disposition.</summary>
    public static Guid DeterministicId(string ruleId, IEnumerable<Guid> relatedCaseIds)
    {
        var key = $"{ruleId}:{string.Join(",", relatedCaseIds.OrderBy(id => id))}";
        var hash = MD5.HashData(Encoding.UTF8.GetBytes(key));
        return new Guid(hash);
    }
}

public interface IFraudSignalDispositionRepository
{
    Task SaveAsync(FraudSignalDisposition disposition, CancellationToken cancellationToken = default);

    Task<FraudSignalDisposition?> GetAsync(Guid signalId, TenantScope scope, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<FraudSignalDisposition>> ListAsync(TenantScope scope, CancellationToken cancellationToken = default);
}
