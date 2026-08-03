using InsuranceIntelligence.Api.Auth;
using InsuranceIntelligence.Api.Common;
using InsuranceIntelligence.Api.Workflow;
using InsuranceIntelligence.Application.Compliance;
using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Infrastructure.Configuration;

namespace InsuranceIntelligence.Api.Compliance;

public static class ComplianceEndpoints
{
    public static async Task<IResult> GetCompliancePosture(
        IComplianceQueryService complianceQueryService,
        InfrastructureOptions options,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (DemoAuthorization.Require(httpContext, options, Permission.ViewAuditLog) is { } forbidden)
        {
            return forbidden;
        }

        if (!TryRequireTenantContext(httpContext, out _))
        {
            return Validation("tenantScope", "Server-resolved tenant context is required.");
        }

        try
        {
            var posture = await complianceQueryService.GetPostureAsync(cancellationToken);
            return Results.Ok(ToResponse(posture));
        }
        catch (MissingTenantContextException)
        {
            return Validation("tenantScope", "Server-resolved tenant context is required.");
        }
        catch (PermissionDeniedException)
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }
    }

    public static async Task<IResult> ListAuditEvents(
        IComplianceQueryService complianceQueryService,
        InfrastructureOptions options,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (DemoAuthorization.Require(httpContext, options, Permission.ViewAuditLog) is { } forbidden)
        {
            return forbidden;
        }

        if (!TryRequireTenantContext(httpContext, out _))
        {
            return Validation("tenantScope", "Server-resolved tenant context is required.");
        }

        Guid? caseId = null;
        if (httpContext.Request.Query.TryGetValue("caseId", out var caseIdValue)
            && Guid.TryParse(caseIdValue, out var parsedCaseId))
        {
            caseId = parsedCaseId;
        }

        string? eventType = null;
        if (httpContext.Request.Query.TryGetValue("eventType", out var eventTypeValue)
            && !string.IsNullOrWhiteSpace(eventTypeValue))
        {
            eventType = eventTypeValue.ToString();
        }

        string? actor = null;
        if (httpContext.Request.Query.TryGetValue("actor", out var actorValue)
            && !string.IsNullOrWhiteSpace(actorValue))
        {
            actor = actorValue.ToString();
        }

        try
        {
            var events = await complianceQueryService.ListAuditEventsAsync(
                new ComplianceAuditEventQuery(caseId, eventType, actor),
                cancellationToken);
            return Results.Ok(events);
        }
        catch (MissingTenantContextException)
        {
            return Validation("tenantScope", "Server-resolved tenant context is required.");
        }
        catch (PermissionDeniedException)
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }
    }

    public static async Task<IResult> GetAuditPack(
        Guid caseId,
        IComplianceQueryService complianceQueryService,
        InfrastructureOptions options,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (DemoAuthorization.Require(httpContext, options, Permission.ViewAuditLog) is { } forbidden)
        {
            return forbidden;
        }

        if (!TryRequireTenantContext(httpContext, out _))
        {
            return Validation("tenantScope", "Server-resolved tenant context is required.");
        }

        try
        {
            var pack = await complianceQueryService.GetAuditPackAsync(caseId, cancellationToken);
            return pack is null
                ? Results.NotFound(new { message = "Case was not found for the requested tenant workspace." })
                : Results.Ok(pack);
        }
        catch (MissingTenantContextException)
        {
            return Validation("tenantScope", "Server-resolved tenant context is required.");
        }
        catch (PermissionDeniedException)
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }
    }

    public static async Task<IResult> ExportAuditPack(
        Guid caseId,
        IComplianceQueryService complianceQueryService,
        InfrastructureOptions options,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (DemoAuthorization.Require(httpContext, options, Permission.ViewAuditLog) is { } forbidden)
        {
            return forbidden;
        }

        if (!TryRequireTenantContext(httpContext, out var tenantContext))
        {
            return Validation("tenantScope", "Server-resolved tenant context is required.");
        }

        // Actor comes from membership-resolved TenantContext only — never from spoofable headers.
        var actor = tenantContext.UserId;
        var auditReason = AuditReasonReader.Read(httpContext);
        var correlationId = WorkflowEndpoints.GetCorrelationId(httpContext);

        try
        {
            var exported = await complianceQueryService.ExportAuditPackAsync(
                new EvidencePackExportCommand(caseId, actor, auditReason ?? string.Empty, correlationId),
                cancellationToken);

            if (exported is null)
            {
                return Results.NotFound(new { message = "Case was not found for the requested tenant workspace." });
            }

            return Results.Ok(new EvidencePackExportResponse(
                exported.CaseId,
                exported.ObjectKey,
                exported.ContentType,
                exported.FileSizeBytes,
                exported.ExportedAt));
        }
        catch (MissingTenantContextException)
        {
            return Validation("tenantScope", "Server-resolved tenant context is required.");
        }
        catch (ComplianceQueryValidationException ex)
        {
            return Validation(ex.Field, ex.Message);
        }
        catch (ComplianceConfigurationException ex)
        {
            return Results.BadRequest(new
            {
                deliveryStatus = "ConfigurationRequired",
                message = ex.Message
            });
        }
        catch (PermissionDeniedException)
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }
    }

    private static CompliancePostureResponse ToResponse(CompliancePostureDto posture) =>
        new(
            new RetentionPosture(
                posture.Retention.AuditRetentionDays,
                posture.Retention.MeetsMinimum,
                posture.Retention.LastUpdatedAt),
            new ConsentPosture(
                posture.Consent.OnboardingRecords,
                posture.Consent.ConsentedRecords,
                posture.Consent.PendingConsent,
                posture.Consent.BlockedConsent,
                posture.Consent.LastConsentedAt),
            new SlaCompliancePosture(
                posture.Sla.TotalOpenClaims,
                posture.Sla.OnTrack,
                posture.Sla.AtRisk,
                posture.Sla.Breached),
            new DataResidencyPosture(
                posture.DataResidency.TenantCountry,
                posture.DataResidency.TenantRegion,
                posture.DataResidency.ResidencyCountry,
                posture.DataResidency.AzureGeography,
                posture.DataResidency.AzureRegion,
                posture.DataResidency.Mode,
                posture.DataResidency.AzureOpenAiDeploymentType,
                posture.DataResidency.AllowedAzureRegions,
                posture.DataResidency.ApprovalReference,
                posture.DataResidency.IsConfigured,
                posture.DataResidency.IsDeploymentRegionApproved,
                posture.DataResidency.IsAzureOpenAiDeploymentTypeApproved,
                posture.DataResidency.Status),
            new ComplianceProviderPosture(
                posture.Providers.AuthMode,
                posture.Providers.StorageProvider,
                posture.Providers.AiProvider,
                posture.Providers.DocumentProvider,
                posture.Providers.RetrievalProvider,
                posture.Providers.SeedDemoData));

    private static IResult Validation(string key, string message) =>
        Results.ValidationProblem(new Dictionary<string, string[]> { [key] = [message] });

    /// <summary>
    /// Compliance scope is membership-resolved TenantContext only. Header/query tenant
    /// authority is never accepted on these deployable paths.
    /// </summary>
    private static bool TryRequireTenantContext(HttpContext httpContext, out TenantContext tenantContext)
    {
        if (httpContext.RequestServices is not null
            && httpContext.RequestServices.GetService(typeof(ITenantContextAccessor)) is ITenantContextAccessor { HasCurrent: true } accessor)
        {
            tenantContext = accessor.Current;
            return true;
        }

        tenantContext = null!;
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
