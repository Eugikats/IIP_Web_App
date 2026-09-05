using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Documents;
using InsuranceIntelligence.Domain.Workflow;

namespace InsuranceIntelligence.Application.Claims;

public sealed record ClaimsQuery(
    string? Search = null,
    string? Status = null,
    string? RiskLevel = null,
    string? Channel = null,
    string? AssignedUserId = null,
    bool? AttentionOnly = null,
    int Skip = 0,
    int Take = 50);

public sealed record ClaimListItemDto(
    Guid ClaimId,
    string CustomerName,
    string PolicyNumber,
    string ProductLine,
    string ClaimType,
    string Channel,
    string Status,
    decimal CompletenessScore,
    string RiskLevel,
    string Priority,
    string SlaStatus,
    DateTimeOffset? SlaDueAt,
    bool RequiresConfirmation,
    string? AssignedUserId,
    string? AssignedTeam,
    DateTimeOffset UpdatedAt);

public sealed record ClaimDetailDto(
    Guid ClaimId,
    string TenantId,
    string WorkspaceId,
    string CustomerName,
    string? CustomerEmail,
    string? CustomerPhone,
    string PolicyNumber,
    string ProductLine,
    string ClaimType,
    string Channel,
    DateOnly IncidentDate,
    string IncidentDescription,
    MotorFnolDetails? MotorFnolDetails,
    string Status,
    decimal CompletenessScore,
    string RiskLevel,
    string Priority,
    DateTimeOffset? SlaDueAt,
    bool RequiresConfirmation,
    string? AssignedUserId,
    string? AssignedTeam,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyCollection<DocumentSummaryDto> Documents);

public sealed record DocumentSummaryDto(
    Guid DocumentId,
    string DocumentType,
    string ProcessingStatus,
    decimal ClassificationConfidence);

public sealed record ClaimsDailySummaryDto(
    DateOnly Date,
    int NewClaims,
    int OpenClaims,
    int AttentionClaims,
    int MissingDocumentClaims,
    int SlaBreaches);

public sealed record ClaimDraftContextDto(
    Guid ClaimId,
    string CustomerName,
    string PolicyNumber,
    string Status,
    string Summary,
    IReadOnlyCollection<string> EvidenceIds,
    IReadOnlyCollection<string> Guardrails);

public interface IClaimsReadRepository
{
    Task<IReadOnlyCollection<ClaimCase>> ListAsync(ClaimsQuery query, CancellationToken cancellationToken = default);

    Task<ClaimCase?> GetAsync(Guid claimId, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<CaseDocument>> ListDocumentsAsync(Guid claimId, CancellationToken cancellationToken = default);
}

public interface IClaimsQueryService
{
    Task<IReadOnlyCollection<ClaimListItemDto>> ListAsync(ClaimsQuery query, CancellationToken cancellationToken = default);

    Task<ClaimDetailDto?> GetAsync(Guid claimId, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<ClaimListItemDto>> GetAttentionAsync(CancellationToken cancellationToken = default);

    Task<ClaimsDailySummaryDto> GetDailySummaryAsync(DateOnly date, CancellationToken cancellationToken = default);

    Task<ClaimDraftContextDto?> GetDraftContextAsync(Guid claimId, CancellationToken cancellationToken = default);
}
