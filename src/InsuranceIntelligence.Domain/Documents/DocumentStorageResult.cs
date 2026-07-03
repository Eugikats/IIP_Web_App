namespace InsuranceIntelligence.Domain.Documents;

public sealed record DocumentStorageResult(
    string ObjectKey,
    string ContentType,
    long FileSizeBytes);
