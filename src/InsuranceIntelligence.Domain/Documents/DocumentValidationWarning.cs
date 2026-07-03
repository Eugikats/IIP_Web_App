namespace InsuranceIntelligence.Domain.Documents;

public sealed record DocumentValidationWarning(
    string Code,
    string Message,
    string Severity,
    string? FieldName = null,
    string? ExpectedValue = null,
    string? ObservedValue = null);
