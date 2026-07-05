namespace InsuranceIntelligence.Application.Fraud;

public interface IFraudQueryService
{
    Task<IReadOnlyList<FraudSignalDto>> GetSignalsAsync(CancellationToken cancellationToken = default);
}

public sealed record FraudSignalDto(
    string RuleId,
    string SignalType,
    string Title,
    string Description,
    string Severity,
    IReadOnlyList<string> RelatedCaseIds,
    IReadOnlyList<string> Evidence);
