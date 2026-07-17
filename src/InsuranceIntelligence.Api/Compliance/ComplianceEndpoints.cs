using InsuranceIntelligence.Api.Auth;
using InsuranceIntelligence.Api.Common;
using InsuranceIntelligence.Api.Workflow;
using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Application.Onboarding;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Compliance;
using InsuranceIntelligence.Domain.Documents;
using InsuranceIntelligence.Domain.Knowledge;
using InsuranceIntelligence.Domain.Storage;
using InsuranceIntelligence.Domain.Workflow;
using InsuranceIntelligence.Infrastructure.Configuration;
using System.Text.Json;

namespace InsuranceIntelligence.Api.Compliance;

public static class ComplianceEndpoints
{
    public static async Task<IResult> GetCompliancePosture(
        ITenantConfigurationRepository tenantConfigurationRepository,
        ITenantOnboardingRepository onboardingRepository,
        ICaseRepository caseRepository,
        InfrastructureOptions options,
        ISlaCalculator slaCalculator,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (DemoAuthorization.Require(httpContext, options, Permission.ViewAuditLog) is { } forbidden)
        {
            return forbidden;
        }

        if (!TryResolveScope(httpContext, out var scope, out _, out var validationError))
        {
            return Validation("tenantScope", validationError);
        }

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

        return Results.Ok(new CompliancePostureResponse(
            new RetentionPosture(
                tenantConfiguration.AuditRetentionDays,
                tenantConfiguration.AuditRetentionDays >= 90,
                tenantConfiguration.UpdatedAt.ToString("O")),
            new ConsentPosture(
                matchingOnboarding.Length,
                matchingOnboarding.Count(record => record.Status is TenantOnboardingStatus.Consented or TenantOnboardingStatus.Active),
                matchingOnboarding.Any(record => record.Status is TenantOnboardingStatus.Pending),
                matchingOnboarding.Any(record => record.Status is TenantOnboardingStatus.Blocked),
                matchingOnboarding
                    .Where(record => record.ConsentedAt is not null)
                    .OrderByDescending(record => record.ConsentedAt)
                    .Select(record => record.ConsentedAt!.Value.ToString("O"))
                    .FirstOrDefault()),
            new SlaCompliancePosture(
                cases.Count,
                slaStatuses.Count(status => status == SlaStatus.OnTrack),
                slaStatuses.Count(status => status == SlaStatus.AtRisk),
                slaStatuses.Count(status => status == SlaStatus.Breached)),
            BuildDataResidencyPosture(tenantConfiguration, options),
            new ComplianceProviderPosture(
                tenantConfiguration.ProviderModes.AuthMode,
                tenantConfiguration.ProviderModes.StorageProvider,
                tenantConfiguration.ProviderModes.AiProvider,
                tenantConfiguration.ProviderModes.DocumentProvider,
                tenantConfiguration.ProviderModes.RetrievalProvider,
                tenantConfiguration.ProviderModes.SeedDemoData)));
    }

    public static async Task<IResult> ListAuditEvents(
        IAuditRepository auditRepository,
        InfrastructureOptions options,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (DemoAuthorization.Require(httpContext, options, Permission.ViewAuditLog) is { } forbidden)
        {
            return forbidden;
        }

        if (!TryResolveScope(httpContext, out var scope, out _, out var validationError))
        {
            return Validation("tenantScope", validationError);
        }

        var events = await auditRepository.ListAsync(scope, cancellationToken);
        var filtered = events.AsEnumerable();
        if (httpContext.Request.Query.TryGetValue("caseId", out var caseIdValue)
            && Guid.TryParse(caseIdValue, out var caseId))
        {
            filtered = filtered.Where(auditEvent => auditEvent.CaseId == caseId);
        }

        if (httpContext.Request.Query.TryGetValue("eventType", out var eventType)
            && !string.IsNullOrWhiteSpace(eventType))
        {
            filtered = filtered.Where(auditEvent => auditEvent.ActionName.Contains(eventType.ToString(), StringComparison.OrdinalIgnoreCase));
        }

        if (httpContext.Request.Query.TryGetValue("actor", out var actor)
            && !string.IsNullOrWhiteSpace(actor))
        {
            filtered = filtered.Where(auditEvent => string.Equals(auditEvent.UserId, actor.ToString(), StringComparison.OrdinalIgnoreCase));
        }

        return Results.Ok(filtered.ToArray());
    }

    public static async Task<IResult> GetAuditPack(
        Guid caseId,
        ICaseRepository caseRepository,
        IAuditRepository auditRepository,
        ICaseDocumentRepository documentRepository,
        IAgentActionLogRepository actionLogRepository,
        IApprovalRepository approvalRepository,
        ICustomerUpdateRepository updateRepository,
        InfrastructureOptions options,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (DemoAuthorization.Require(httpContext, options, Permission.ViewAuditLog) is { } forbidden)
        {
            return forbidden;
        }

        if (!TryResolveScope(httpContext, out var scope, out _, out var validationError))
        {
            return Validation("tenantScope", validationError);
        }

        var claimCase = await caseRepository.GetAsync(caseId, cancellationToken);
        if (claimCase is null || !claimCase.Scope.Matches(scope))
        {
            return Results.NotFound(new { message = "Case was not found for the requested tenant workspace." });
        }

        var pack = await BuildAuditPackAsync(
            claimCase,
            scope,
            auditRepository,
            documentRepository,
            actionLogRepository,
            approvalRepository,
            updateRepository,
            cancellationToken);

        return Results.Ok(pack);
    }

    public static async Task<IResult> ExportAuditPack(
        Guid caseId,
        ICaseRepository caseRepository,
        IAuditRepository auditRepository,
        ICaseDocumentRepository documentRepository,
        IAgentActionLogRepository actionLogRepository,
        IApprovalRepository approvalRepository,
        ICustomerUpdateRepository updateRepository,
        IEvidencePackStorageProvider storageProvider,
        InfrastructureOptions options,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (DemoAuthorization.Require(httpContext, options, Permission.ViewAuditLog) is { } forbidden)
        {
            return forbidden;
        }

        if (!TryResolveScope(httpContext, out var scope, out _, out var validationError))
        {
            return Validation("tenantScope", validationError);
        }

        var claimCase = await caseRepository.GetAsync(caseId, cancellationToken);
        if (claimCase is null || !claimCase.Scope.Matches(scope))
        {
            return Results.NotFound(new { message = "Case was not found for the requested tenant workspace." });
        }

        var actor = WorkflowEndpoints.GetActor(httpContext, null);
        if (string.IsNullOrWhiteSpace(actor))
        {
            return Validation("userId", "Authenticated user context is required to export an evidence pack.");
        }

        var auditReason = AuditReasonReader.Read(httpContext);
        if (string.IsNullOrWhiteSpace(auditReason))
        {
            return Validation("auditReason", "An audit reason is required to export an evidence pack.");
        }

        var pack = await BuildAuditPackAsync(
            claimCase,
            scope,
            auditRepository,
            documentRepository,
            actionLogRepository,
            approvalRepository,
            updateRepository,
            cancellationToken);

        await using var content = new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(pack));
        var fileName = $"case-{caseId:N}-evidence-pack-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}.json";

        try
        {
            var stored = await storageProvider.StoreAsync(
                new EvidencePackStorageRequest(scope, caseId, fileName, "application/json", content),
                cancellationToken);

            var correlationId = WorkflowEndpoints.GetCorrelationId(httpContext);
            await auditRepository.WriteAsync(
                AuditEvent.EvidencePackExported(scope, caseId, actor, stored.ObjectKey, stored.FileSizeBytes, auditReason, correlationId),
                cancellationToken);

            return Results.Ok(new EvidencePackExportResponse(
                caseId,
                stored.ObjectKey,
                stored.ContentType,
                stored.FileSizeBytes,
                DateTimeOffset.UtcNow));
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new
            {
                deliveryStatus = "ConfigurationRequired",
                message = ex.Message
            });
        }
    }

    private static async Task<AuditPack> BuildAuditPackAsync(
        ClaimCase claimCase,
        TenantScope scope,
        IAuditRepository auditRepository,
        ICaseDocumentRepository documentRepository,
        IAgentActionLogRepository actionLogRepository,
        IApprovalRepository approvalRepository,
        ICustomerUpdateRepository updateRepository,
        CancellationToken cancellationToken) =>
        AuditPack.Build(
            claimCase,
            await auditRepository.ListForCaseAsync(claimCase.CaseId, cancellationToken),
            await documentRepository.ListForCaseAsync(claimCase.CaseId, scope, cancellationToken),
            await actionLogRepository.ListForCaseAsync(claimCase.CaseId, scope, cancellationToken),
            await approvalRepository.ListForCaseAsync(claimCase.CaseId, scope, cancellationToken),
            await updateRepository.ListForCaseAsync(claimCase.CaseId, scope, cancellationToken));

    private static DataResidencyPosture BuildDataResidencyPosture(
        TenantConfiguration tenantConfiguration,
        InfrastructureOptions options)
    {
        var allowedAzureRegions = Azure.AzureProviderStartupValidator.AllowedAzureRegions(options);
        var expectedOpenAiDeploymentType = Azure.AzureProviderStartupValidator.ExpectedAzureOpenAiDeploymentType(options.DataResidencyMode);
        var openAiDeploymentTypeRequired = options.AiProvider.Equals("AzureOpenAI", StringComparison.OrdinalIgnoreCase);
        var configured = !string.IsNullOrWhiteSpace(options.DataResidencyCountry)
            && !string.IsNullOrWhiteSpace(options.DataResidencyGeography)
            && !string.IsNullOrWhiteSpace(options.AzureRegion)
            && allowedAzureRegions.Count > 0
            && !string.IsNullOrWhiteSpace(options.DataResidencyApprovalReference)
            && expectedOpenAiDeploymentType is not null
            && (!openAiDeploymentTypeRequired || !string.IsNullOrWhiteSpace(options.AzureOpenAI.DeploymentType));
        var deploymentRegionApproved = configured
            && allowedAzureRegions.Contains(options.AzureRegion!.Trim(), StringComparer.OrdinalIgnoreCase);
        var openAiDeploymentTypeApproved = configured
            && (!openAiDeploymentTypeRequired
                || options.AzureOpenAI.DeploymentType!.Equals(expectedOpenAiDeploymentType, StringComparison.OrdinalIgnoreCase));
        var compliant = deploymentRegionApproved && openAiDeploymentTypeApproved;

        return new DataResidencyPosture(
            string.IsNullOrWhiteSpace(tenantConfiguration.Country) ? "Not configured" : tenantConfiguration.Country,
            string.IsNullOrWhiteSpace(tenantConfiguration.Region) ? "Not configured" : tenantConfiguration.Region,
            string.IsNullOrWhiteSpace(options.DataResidencyCountry) ? "Not configured" : options.DataResidencyCountry,
            string.IsNullOrWhiteSpace(options.DataResidencyGeography) ? "Not configured" : options.DataResidencyGeography,
            string.IsNullOrWhiteSpace(options.AzureRegion) ? "Not configured" : options.AzureRegion,
            options.DataResidencyMode,
            string.IsNullOrWhiteSpace(options.AzureOpenAI.DeploymentType) ? "Not configured" : options.AzureOpenAI.DeploymentType,
            allowedAzureRegions,
            string.IsNullOrWhiteSpace(options.DataResidencyApprovalReference) ? "Not configured" : options.DataResidencyApprovalReference,
            configured,
            deploymentRegionApproved,
            openAiDeploymentTypeApproved,
            !configured ? "Not configured" : compliant ? "Compliant" : "Mismatch");
    }

    private static IResult Validation(string key, string message) =>
        Results.ValidationProblem(new Dictionary<string, string[]> { [key] = [message] });

    private static bool TryResolveScope(
        HttpContext httpContext,
        out TenantScope scope,
        out string userId,
        out string validationError)
    {
        if (httpContext.RequestServices is not null
            && httpContext.RequestServices.GetService(typeof(ITenantContextAccessor)) is ITenantContextAccessor { HasCurrent: true } tenantContext)
        {
            var current = tenantContext.Current;
            scope = TenantScope.Create(current.TenantId, current.WorkspaceId);
            userId = current.UserId;
            validationError = string.Empty;
            return true;
        }

        if (TenantScopeReader.TryGetFromHeadersOrQuery(httpContext, out scope, out validationError))
        {
            userId = WorkflowEndpoints.GetActor(httpContext, null);
            return true;
        }

        userId = string.Empty;
        return false;
    }
}

public sealed record EvidencePackExportResponse(
    Guid CaseId,
    string ObjectKey,
    string ContentType,
    long FileSizeBytes,
    DateTimeOffset ExportedAt);

public sealed record CompliancePostureResponse(
    RetentionPosture Retention,
    ConsentPosture Consent,
    SlaCompliancePosture Sla,
    DataResidencyPosture DataResidency,
    ComplianceProviderPosture Providers);

public sealed record RetentionPosture(
    int AuditRetentionDays,
    bool MeetsMinimum,
    string LastUpdatedAt);

public sealed record ConsentPosture(
    int OnboardingRecords,
    int ConsentedRecords,
    bool PendingConsent,
    bool BlockedConsent,
    string? LastConsentedAt);

public sealed record SlaCompliancePosture(
    int TotalOpenClaims,
    int OnTrack,
    int AtRisk,
    int Breached);

public sealed record DataResidencyPosture(
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

public sealed record ComplianceProviderPosture(
    string AuthMode,
    string StorageProvider,
    string AiProvider,
    string DocumentProvider,
    string RetrievalProvider,
    bool SeedDemoData);
