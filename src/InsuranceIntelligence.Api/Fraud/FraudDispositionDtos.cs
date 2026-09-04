namespace InsuranceIntelligence.Api.Fraud;

public sealed record TriageFraudSignalRequest(string? RuleId, IReadOnlyList<Guid>? RelatedCaseIds);

public sealed record AssignFraudSignalRequest(string? AssignedToUserId);

public sealed record RecordFraudDispositionOutcomeRequest(string? Outcome, string? Notes);
