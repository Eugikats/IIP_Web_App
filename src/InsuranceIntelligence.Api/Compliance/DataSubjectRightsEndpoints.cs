using InsuranceIntelligence.Api.Auth;
using InsuranceIntelligence.Api.Common;
using InsuranceIntelligence.Api.Workflow;
using InsuranceIntelligence.Application.Compliance;
using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Compliance;
using InsuranceIntelligence.Infrastructure.Configuration;
using InsuranceIntelligence.Infrastructure.WhatsApp;

namespace InsuranceIntelligence.Api.Compliance;

/// <summary>Data subject rights (blueprint §14 Phase 6, WO 6.4): export, rectification, and
/// erasure with a documented refusal path, all three are immediate, audited, permission-gated
/// actions, not a ticket-tracking workflow, matching how this codebase already treats the other
/// compliance actions (evidence-pack export, retention enforcement) rather than adding a
/// differently-shaped subsystem for this one.</summary>
public static class DataSubjectRightsEndpoints
{
    public static async Task<IResult> Export(
        string customerId,
        IDataSubjectRightsService service,
        IWhatsAppRuntimeSettingsProvider whatsAppSettingsProvider,
        InfrastructureOptions options,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (DemoAuthorization.Require(httpContext, options, Permission.ManageTenantSettings) is { } forbidden)
        {
            return forbidden;
        }

        if (!TryRequireTenantContext(httpContext, out var tenantContext))
        {
            return Validation("tenantScope", "Server-resolved tenant context is required.");
        }

        var export = await service.ExportAsync(tenantContext.Scope, customerId, whatsAppSettingsProvider.ForScope(tenantContext.Scope)?.PhoneNumberId, cancellationToken);
        return export is null
            ? Results.NotFound(new { message = "Customer was not found for the requested tenant workspace." })
            : Results.Ok(export);
    }

    public static async Task<IResult> Rectify(
        string customerId,
        DataSubjectRectificationRequest request,
        IDataSubjectRightsService service,
        InfrastructureOptions options,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (DemoAuthorization.Require(httpContext, options, Permission.ManageTenantSettings) is { } forbidden)
        {
            return forbidden;
        }

        if (!TryRequireTenantContext(httpContext, out var tenantContext))
        {
            return Validation("tenantScope", "Server-resolved tenant context is required.");
        }

        var auditReason = AuditReasonReader.Read(httpContext);
        if (string.IsNullOrWhiteSpace(auditReason))
        {
            return Validation("auditReason", "An audit reason is required to rectify a customer's data.");
        }

        var correlationId = WorkflowEndpoints.GetCorrelationId(httpContext);
        var rectified = await service.RectifyAsync(tenantContext.Scope, customerId, request, tenantContext.UserId, auditReason, correlationId, cancellationToken);
        return rectified is null
            ? Results.NotFound(new { message = "Customer was not found for the requested tenant workspace." })
            : Results.Ok(rectified);
    }

    public static async Task<IResult> RequestErasure(
        string customerId,
        IDataSubjectRightsService service,
        InfrastructureOptions options,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (DemoAuthorization.Require(httpContext, options, Permission.ManageTenantSettings) is { } forbidden)
        {
            return forbidden;
        }

        if (!TryRequireTenantContext(httpContext, out var tenantContext))
        {
            return Validation("tenantScope", "Server-resolved tenant context is required.");
        }

        var auditReason = AuditReasonReader.Read(httpContext);
        if (string.IsNullOrWhiteSpace(auditReason))
        {
            return Validation("auditReason", "An audit reason is required to request erasure of a customer's data.");
        }

        var correlationId = WorkflowEndpoints.GetCorrelationId(httpContext);
        var result = await service.RequestErasureAsync(tenantContext.Scope, customerId, tenantContext.UserId, auditReason, correlationId, cancellationToken);
        return result is null
            ? Results.NotFound(new { message = "Customer was not found for the requested tenant workspace." })
            : Results.Ok(result);
    }

    private static IResult Validation(string key, string message) =>
        Results.ValidationProblem(new Dictionary<string, string[]> { [key] = [message] });

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
