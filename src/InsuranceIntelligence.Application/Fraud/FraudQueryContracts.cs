namespace InsuranceIntelligence.Application.Fraud;

public interface IFraudQueryService
{
    Task<IReadOnlyList<FraudSignalDto>> GetSignalsAsync(CancellationToken cancellationToken = default);
}

public sealed record FraudSignalDto(
    Guid SignalId,
    string RuleId,
    string SignalType,
    string Title,
    string Description,
    string Severity,
    IReadOnlyList<string> RelatedCaseIds,
    IReadOnlyList<string> Evidence,
    FraudDispositionDto? Disposition);

public sealed record FraudDispositionDto(
    string Status,
    string? AssignedToUserId,
    string? Outcome,
    string? OutcomeNotes,
    string CreatedByUserId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyCollection<FraudDispositionActionDto> AllowedActions,
    IReadOnlyCollection<FraudDispositionActionDto> BlockedActions);

public sealed record FraudDispositionActionDto(string Action, string Label, string RequiredPermission, string? Reason);
