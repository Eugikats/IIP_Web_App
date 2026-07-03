namespace InsuranceIntelligence.Domain.Documents;

public sealed record DocumentProcessingRequest(
    Guid DocumentId,
    Guid CaseId,
    string OriginalFileName,
    string ContentType,
    long FileSizeBytes,
    string ObjectKey,
    Func<CancellationToken, Task<Stream>>? OpenContentStreamAsync = null);
