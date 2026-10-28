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
    IReadOnlyCollection<DocumentSummaryDto> Documents,
    decimal? ClaimedAmount = null,
    IReadOnlyCollection<string>? AdvisoryRiskSignals = null,
    // Outcome N: the real Policy record behind this claim's PolicyNumber, when one has been
    // loaded (file import, or any future partner/lookup source). Null, not zeroed dates, when no
    // Policy record exists yet for this number, honest about the gap rather than implying a cover
    // period nobody has actually loaded.
    DateOnly? PolicyEffectiveFrom = null,
    DateOnly? PolicyEffectiveTo = null,
    string? PolicyRecordSource = null,
    DateTimeOffset? PolicyLastRefreshedAt = null,
    // What the customer wrote in their own language, when the description above is a translation of it, and the
    // structured medical claim details. Both were held on the claim but never sent to the screen.
    string? OriginalIncidentText = null,
    string? OriginalIncidentLanguage = null,
    MedicalFnolDetails? MedicalFnolDetails = null);

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
