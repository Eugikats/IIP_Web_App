namespace InsuranceIntelligence.Domain.Documents;

public sealed record DocumentFieldCorrection(
    string FieldName,
    string CorrectedValue,
    string CorrectedByUserId,
    DateTimeOffset CorrectedAt,
    string CorrelationId);
