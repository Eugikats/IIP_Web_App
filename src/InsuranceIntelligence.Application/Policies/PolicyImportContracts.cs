namespace InsuranceIntelligence.Application.Policies;

public sealed record PolicyImportCommitRequest(
    IReadOnlyList<string> Headers,
    IReadOnlyDictionary<string, string> ColumnMapping,
    IReadOnlyList<IReadOnlyList<string>> Rows);

public sealed record PolicyImportRowError(int RowNumber, IReadOnlyCollection<string> Errors);

public sealed record PolicyImportResult(
    int TotalRows,
    int ImportedCount,
    int FailedCount,
    IReadOnlyList<PolicyImportRowError> Errors);

public interface IPolicyImportService
{
    Task<PolicyImportResult> CommitAsync(PolicyImportCommitRequest request, CancellationToken cancellationToken = default);
}

public sealed class PolicyImportValidationException(string field, string message) : InvalidOperationException(message)
{
    public string Field { get; } = field;
}
