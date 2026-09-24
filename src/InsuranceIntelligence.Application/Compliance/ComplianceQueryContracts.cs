using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Compliance;

namespace InsuranceIntelligence.Application.Compliance;

public interface IComplianceQueryService
{
    Task<CompliancePostureDto> GetPostureAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AuditEvent>> ListAuditEventsAsync(
        ComplianceAuditEventQuery query,
        CancellationToken cancellationToken = default);

    Task<AuditPack?> GetAuditPackAsync(Guid caseId, CancellationToken cancellationToken = default);

    Task<EvidencePackExportDto?> ExportAuditPackAsync(
        EvidencePackExportCommand command,
        CancellationToken cancellationToken = default);

    /// <summary>Reads back a previously exported evidence pack (blueprint §14 Phase 6, WO 6.2).
    /// Returns null if the case doesn't exist/belong to this tenant, or if objectKey was never
    /// actually recorded as exported for this case, the audit trail is the authorization check,
    /// not a client-trusted object key alone.</summary>
    Task<EvidencePackDownloadDto?> DownloadAuditPackAsync(
        Guid caseId,
        string objectKey,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Deployment residency posture inputs. Mapped from InfrastructureOptions at composition root,
/// Application never reads Infrastructure configuration types directly.
/// </summary>
public interface IComplianceResidencySettings
{
    string? DataResidencyCountry { get; }
    string? DataResidencyGeography { get; }
    string? AzureRegion { get; }
    string DataResidencyMode { get; }
    string? AzureOpenAiDeploymentType { get; }
    string? DataResidencyAllowedAzureRegions { get; }
    string? DataResidencyApprovalReference { get; }
    string AiProvider { get; }
}

public sealed record ComplianceResidencySettings(
    string? DataResidencyCountry,
    string? DataResidencyGeography,
    string? AzureRegion,
    string DataResidencyMode,
    string? AzureOpenAiDeploymentType,
    string? DataResidencyAllowedAzureRegions,
    string? DataResidencyApprovalReference,
    string AiProvider) : IComplianceResidencySettings;

public sealed record ComplianceAuditEventQuery(
    Guid? CaseId = null,
    string? EventType = null,
    string? Actor = null);

public sealed record EvidencePackExportCommand(
    Guid CaseId,
    string ActorUserId,
    string AuditReason,
    string CorrelationId);

public sealed record EvidencePackExportDto(
    Guid CaseId,
    string ObjectKey,
    string ContentType,
    long FileSizeBytes,
    DateTimeOffset ExportedAt);

public sealed record EvidencePackDownloadDto(
    Stream Content,
    string ContentType,
    string FileName);

public sealed record CompliancePostureDto(
    RetentionPostureDto Retention,
    ConsentPostureDto Consent,
    SlaCompliancePostureDto Sla,
    DataResidencyPostureDto DataResidency,
    ComplianceProviderPostureDto Providers);

public sealed record RetentionPostureDto(
    int AuditRetentionDays,
    bool MeetsMinimum,
    string LastUpdatedAt);

public sealed record ConsentPostureDto(
    int OnboardingRecords,
    int ConsentedRecords,
    bool PendingConsent,
    bool BlockedConsent,
    string? LastConsentedAt);

public sealed record SlaCompliancePostureDto(
    int TotalOpenClaims,
    int OnTrack,
    int AtRisk,
    int Breached);

public sealed record DataResidencyPostureDto(
    string TenantCountry,
    string TenantRegion,
    string ResidencyCountry,
    string AzureGeography,
    string AzureRegion,
    string Mode,
    string AzureOpenAiDeploymentType,
    IReadOnlyCollection<string> AllowedAzureRegions,
    string ApprovalReference,
    bool IsConfigured,
    bool IsDeploymentRegionApproved,
    bool IsAzureOpenAiDeploymentTypeApproved,
    string Status);

public sealed record ComplianceProviderPostureDto(
    string AuthMode,
    string StorageProvider,
    string AiProvider,
    string DocumentProvider,
    string RetrievalProvider,
    bool SeedDemoData);

public sealed class ComplianceQueryValidationException(string field, string message)
    : InvalidOperationException(message)
{
    public string Field { get; } = field;
}

public sealed class ComplianceConfigurationException(string message) : InvalidOperationException(message);

public sealed class MissingTenantContextException()
    : InvalidOperationException("Server-resolved tenant context is required.");
