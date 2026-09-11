using System.Text.Json;
using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Application.Onboarding;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Approvals;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Compliance;
using InsuranceIntelligence.Domain.Documents;
using InsuranceIntelligence.Domain.Knowledge;
using InsuranceIntelligence.Domain.Storage;
using InsuranceIntelligence.Domain.Workflow;

namespace InsuranceIntelligence.Application.Compliance;

public sealed class ComplianceQueryService(
    ITenantContextAccessor tenantContextAccessor,
    IPermissionService permissionService,
    ITenantConfigurationRepository tenantConfigurationRepository,
    ITenantOnboardingRepository onboardingRepository,
    ICaseRepository caseRepository,
    IAuditRepository auditRepository,
    ICaseDocumentRepository documentRepository,
    IAgentActionLogRepository actionLogRepository,
    IApprovalRepository approvalRepository,
    ICustomerUpdateRepository updateRepository,
    IEvidencePackStorageProvider storageProvider,
    ISlaCalculator slaCalculator,
    IComplianceResidencySettings residencySettings) : IComplianceQueryService
{
    public async Task<CompliancePostureDto> GetPostureAsync(CancellationToken cancellationToken = default)
    {
        var scope = RequireScope();
        await permissionService.RequirePermissionAsync(Permission.ViewAuditLog, cancellationToken);

        var tenantConfiguration = await tenantConfigurationRepository.GetAsync(scope, cancellationToken);
        var onboardingRecords = await onboardingRepository.ListAsync(cancellationToken);
        var matchingOnboarding = onboardingRecords
            .Where(record => string.Equals(record.InternalTenantId, scope.TenantId, StringComparison.OrdinalIgnoreCase)
                && (record.TargetWorkspaces.Count == 0
                    || record.TargetWorkspaces.Any(workspace => string.Equals(workspace, scope.WorkspaceId, StringComparison.OrdinalIgnoreCase))))
            .ToArray();
        var cases = await caseRepository.ListAsync(scope, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var slaStatuses = cases.Select(claimCase => slaCalculator.Calculate(claimCase, now)).ToArray();

        return new CompliancePostureDto(
            new RetentionPostureDto(
                tenantConfiguration.AuditRetentionDays,
                tenantConfiguration.AuditRetentionDays >= 90,
                tenantConfiguration.UpdatedAt.ToString("O")),
            new ConsentPostureDto(
                matchingOnboarding.Length,
                matchingOnboarding.Count(record => record.Status is TenantOnboardingStatus.Consented or TenantOnboardingStatus.Active),
                matchingOnboarding.Any(record => record.Status is TenantOnboardingStatus.Pending),
                matchingOnboarding.Any(record => record.Status is TenantOnboardingStatus.Blocked),
                matchingOnboarding
                    .Where(record => record.ConsentedAt is not null)
                    .OrderByDescending(record => record.ConsentedAt)
                    .Select(record => record.ConsentedAt!.Value.ToString("O"))
                    .FirstOrDefault()),
            new SlaCompliancePostureDto(
                cases.Count,
                slaStatuses.Count(status => status == SlaStatus.OnTrack),
                slaStatuses.Count(status => status == SlaStatus.AtRisk),
                slaStatuses.Count(status => status == SlaStatus.Breached)),
            BuildDataResidencyPosture(tenantConfiguration),
            new ComplianceProviderPostureDto(
                tenantConfiguration.ProviderModes.AuthMode,
                tenantConfiguration.ProviderModes.StorageProvider,
                tenantConfiguration.ProviderModes.AiProvider,
                tenantConfiguration.ProviderModes.DocumentProvider,
                tenantConfiguration.ProviderModes.RetrievalProvider,
                tenantConfiguration.ProviderModes.SeedDemoData));
    }

    public async Task<IReadOnlyList<AuditEvent>> ListAuditEventsAsync(
        ComplianceAuditEventQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var scope = RequireScope();
        await permissionService.RequirePermissionAsync(Permission.ViewAuditLog, cancellationToken);

        var events = await auditRepository.ListAsync(scope, cancellationToken);
        var filtered = events.AsEnumerable();

        if (query.CaseId is { } caseId)
        {
            filtered = filtered.Where(auditEvent => auditEvent.CaseId == caseId);
        }

        if (!string.IsNullOrWhiteSpace(query.EventType))
        {
            filtered = filtered.Where(auditEvent =>
                auditEvent.ActionName.Contains(query.EventType, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(query.Actor))
        {
            filtered = filtered.Where(auditEvent =>
                string.Equals(auditEvent.UserId, query.Actor, StringComparison.OrdinalIgnoreCase));
        }

        return filtered.ToArray();
    }

    public async Task<AuditPack?> GetAuditPackAsync(Guid caseId, CancellationToken cancellationToken = default)
    {
        var scope = RequireScope();
        await permissionService.RequirePermissionAsync(Permission.ViewAuditLog, cancellationToken);

        var claimCase = await caseRepository.GetAsync(caseId, cancellationToken);
        if (claimCase is null || !claimCase.Scope.Matches(scope))
        {
            return null;
        }

        return await BuildAuditPackAsync(claimCase, scope, cancellationToken);
    }

    public async Task<EvidencePackExportDto?> ExportAuditPackAsync(
        EvidencePackExportCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (string.IsNullOrWhiteSpace(command.ActorUserId))
        {
            throw new ComplianceQueryValidationException("userId", "Authenticated user context is required to export an evidence pack.");
        }

        if (string.IsNullOrWhiteSpace(command.AuditReason))
        {
            throw new ComplianceQueryValidationException("auditReason", "An audit reason is required to export an evidence pack.");
        }

        var scope = RequireScope();
        await permissionService.RequirePermissionAsync(Permission.ExportEvidencePack, cancellationToken);

        var claimCase = await caseRepository.GetAsync(command.CaseId, cancellationToken);
        if (claimCase is null || !claimCase.Scope.Matches(scope))
        {
            return null;
        }

        try
        {
            ClaimAuthorityPolicy.EnsureMayExportEvidencePack(
                claimCase,
                ApprovalDecisionGuard.ResolveActorType(command.ActorUserId));
        }
        catch (DomainException exception)
        {
            throw new ComplianceQueryValidationException("status", exception.Message);
        }

        var pack = await BuildAuditPackAsync(claimCase, scope, cancellationToken);
        await using var content = new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(pack));
        var fileName = $"case-{command.CaseId:N}-evidence-pack-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}.json";

        try
        {
            var stored = await storageProvider.StoreAsync(
                new EvidencePackStorageRequest(scope, command.CaseId, fileName, "application/json", content),
                cancellationToken);

            await auditRepository.WriteAsync(
                AuditEvent.EvidencePackExported(
                    scope,
                    command.CaseId,
                    command.ActorUserId,
                    stored.ObjectKey,
                    stored.FileSizeBytes,
                    command.AuditReason,
                    command.CorrelationId),
                cancellationToken);

            return new EvidencePackExportDto(
                command.CaseId,
                stored.ObjectKey,
                stored.ContentType,
                stored.FileSizeBytes,
                DateTimeOffset.UtcNow);
        }
        catch (InvalidOperationException ex) when (ex is not ComplianceQueryValidationException and not MissingTenantContextException)
        {
            // Never surface raw provider/storage exception text to API clients.
            throw new ComplianceConfigurationException(
                "Evidence pack storage is unavailable. Verify blob identity, container configuration, and network access.");
        }
    }

    public async Task<EvidencePackDownloadDto?> DownloadAuditPackAsync(
        Guid caseId,
        string objectKey,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(objectKey))
        {
            throw new ComplianceQueryValidationException("objectKey", "An object key is required to download an evidence pack.");
        }

        var scope = RequireScope();
        await permissionService.RequirePermissionAsync(Permission.ExportEvidencePack, cancellationToken);

        var claimCase = await caseRepository.GetAsync(caseId, cancellationToken);
        if (claimCase is null || !claimCase.Scope.Matches(scope))
        {
            return null;
        }

        // The audit trail is the authorization check, not a client-trusted object key alone —
        // only a key this tenant's own export actually produced for this case is downloadable.
        var auditEvents = await auditRepository.ListForCaseAsync(caseId, cancellationToken);
        var exportEvent = auditEvents.FirstOrDefault(
            e => e.ActionName == "evidence_pack.exported" && e.EvidenceIds.Contains(objectKey));
        if (exportEvent is null)
        {
            return null;
        }

        try
        {
            var content = await storageProvider.ReadAsync(scope, objectKey, cancellationToken);
            var fileName = objectKey.Split('/').Last();
            return new EvidencePackDownloadDto(content, "application/json", fileName);
        }
        catch (InvalidOperationException ex) when (ex is not ComplianceQueryValidationException and not MissingTenantContextException)
        {
            throw new ComplianceConfigurationException(
                "Evidence pack storage is unavailable. Verify blob identity, container configuration, and network access.");
        }
    }

    private TenantScope RequireScope()
    {
        if (!tenantContextAccessor.HasCurrent)
        {
            throw new MissingTenantContextException();
        }

        return tenantContextAccessor.Current.Scope;
    }

    private async Task<AuditPack> BuildAuditPackAsync(
        ClaimCase claimCase,
        TenantScope scope,
        CancellationToken cancellationToken) =>
        AuditPack.Build(
            claimCase,
            await auditRepository.ListForCaseAsync(claimCase.CaseId, cancellationToken),
            await documentRepository.ListForCaseAsync(claimCase.CaseId, scope, cancellationToken),
            await actionLogRepository.ListForCaseAsync(claimCase.CaseId, scope, cancellationToken),
            await approvalRepository.ListForCaseAsync(claimCase.CaseId, scope, cancellationToken),
            await updateRepository.ListForCaseAsync(claimCase.CaseId, scope, cancellationToken));

    private DataResidencyPostureDto BuildDataResidencyPosture(TenantConfiguration tenantConfiguration)
    {
        var allowedAzureRegions = ParseAllowedAzureRegions(residencySettings.DataResidencyAllowedAzureRegions);
        var expectedOpenAiDeploymentType = ExpectedAzureOpenAiDeploymentType(residencySettings.DataResidencyMode);
        var openAiDeploymentTypeRequired = residencySettings.AiProvider.Equals("AzureOpenAI", StringComparison.OrdinalIgnoreCase);
        var configured = !string.IsNullOrWhiteSpace(residencySettings.DataResidencyCountry)
            && !string.IsNullOrWhiteSpace(residencySettings.DataResidencyGeography)
            && !string.IsNullOrWhiteSpace(residencySettings.AzureRegion)
            && allowedAzureRegions.Count > 0
            && !string.IsNullOrWhiteSpace(residencySettings.DataResidencyApprovalReference)
            && expectedOpenAiDeploymentType is not null
            && (!openAiDeploymentTypeRequired || !string.IsNullOrWhiteSpace(residencySettings.AzureOpenAiDeploymentType));
        var deploymentRegionApproved = configured
            && allowedAzureRegions.Contains(residencySettings.AzureRegion!.Trim(), StringComparer.OrdinalIgnoreCase);
        var openAiDeploymentTypeApproved = configured
            && (!openAiDeploymentTypeRequired
                || residencySettings.AzureOpenAiDeploymentType!.Equals(expectedOpenAiDeploymentType, StringComparison.OrdinalIgnoreCase));
        var compliant = deploymentRegionApproved && openAiDeploymentTypeApproved;

        return new DataResidencyPostureDto(
            string.IsNullOrWhiteSpace(tenantConfiguration.Country) ? "Not configured" : tenantConfiguration.Country,
            string.IsNullOrWhiteSpace(tenantConfiguration.Region) ? "Not configured" : tenantConfiguration.Region,
            string.IsNullOrWhiteSpace(residencySettings.DataResidencyCountry) ? "Not configured" : residencySettings.DataResidencyCountry,
            string.IsNullOrWhiteSpace(residencySettings.DataResidencyGeography) ? "Not configured" : residencySettings.DataResidencyGeography,
            string.IsNullOrWhiteSpace(residencySettings.AzureRegion) ? "Not configured" : residencySettings.AzureRegion,
            residencySettings.DataResidencyMode,
            string.IsNullOrWhiteSpace(residencySettings.AzureOpenAiDeploymentType) ? "Not configured" : residencySettings.AzureOpenAiDeploymentType,
            allowedAzureRegions,
            string.IsNullOrWhiteSpace(residencySettings.DataResidencyApprovalReference) ? "Not configured" : residencySettings.DataResidencyApprovalReference,
            configured,
            deploymentRegionApproved,
            openAiDeploymentTypeApproved,
            !configured ? "Not configured" : compliant ? "Compliant" : "Mismatch");
    }

    private static string? ExpectedAzureOpenAiDeploymentType(string mode) =>
        mode.Equals("regional", StringComparison.OrdinalIgnoreCase) ? "Standard"
        : mode.Equals("data_zone", StringComparison.OrdinalIgnoreCase) ? "DataZoneStandard"
        : mode.Equals("global", StringComparison.OrdinalIgnoreCase) ? "GlobalStandard"
        : null;

    private static IReadOnlyCollection<string> ParseAllowedAzureRegions(string? value) =>
        (value ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
}
