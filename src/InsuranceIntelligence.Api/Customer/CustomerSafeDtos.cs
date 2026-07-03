namespace InsuranceIntelligence.Api.Customer;

public sealed record CustomerClaimDto(
    Guid ClaimId,
    string ClaimReference,
    string Status,
    string ProductLine,
    string NextStep,
    IReadOnlyCollection<CustomerTimelineItemDto> Timeline,
    IReadOnlyCollection<CustomerRequiredDocumentDto> RequiredDocuments,
    IReadOnlyCollection<CustomerMessageDto> Messages);

public sealed record CustomerTimelineItemDto(
    string Label,
    string Description,
    DateTimeOffset OccurredAt);

public sealed record CustomerRequiredDocumentDto(
    string DocumentType,
    string Label,
    bool Received,
    string CustomerInstruction);

public sealed record CustomerDocumentDto(
    Guid DocumentId,
    string FileName,
    string DocumentType,
    string Status,
    DateTimeOffset UploadedAt);

public sealed record CustomerMessageDto(
    Guid MessageId,
    string Channel,
    string Direction,
    string Body,
    DateTimeOffset CreatedAt);

public sealed record CustomerSupportInsightDto(
    string Summary,
    string SafeNextStep,
    bool HumanHelpAvailable);
