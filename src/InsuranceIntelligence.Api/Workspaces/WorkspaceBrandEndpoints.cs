using InsuranceIntelligence.Api.Auth;
using InsuranceIntelligence.Api.Common;
using InsuranceIntelligence.Api.Workflow;
using InsuranceIntelligence.Application.CustomerAssistant;
using InsuranceIntelligence.Application.Workspaces;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Infrastructure.Configuration;

namespace InsuranceIntelligence.Api.Workspaces;

/// <summary>
/// Whose assistant Kato is for this workspace: the organisation name customers see, the short name used in greetings, the
/// partner insurers when the workspace is a bank selling partner covers, and whether it sells life, general or both.
/// Read and written by the workspace's administrators only, and always for the caller's own workspace.
/// </summary>
public static class WorkspaceBrandEndpoints
{
    public static async Task<IResult> Get(
        WorkspaceBrandService brands,
        InfrastructureOptions options,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (DemoAuthorization.Require(httpContext, options, Permission.ConfigureIntegrations) is { } forbidden)
        {
            return forbidden;
        }

        if (!WorkflowEndpoints.TryResolveScope(httpContext, out var scope, out _, out var validationError))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["tenantScope"] = [validationError] });
        }

        var saved = await brands.GetAsync(scope, cancellationToken);
        var effective = saved ?? WorkspaceBrandSettings.From(KatoBrand.Current);
        return Results.Ok(ToResponse(effective, isDefault: saved is null));
    }

    public static async Task<IResult> Save(
        WorkspaceBrandRequest request,
        WorkspaceBrandService brands,
        Application.Core.IAuditWriter auditWriter,
        InfrastructureOptions options,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (DemoAuthorization.Require(httpContext, options, Permission.ConfigureIntegrations) is { } forbidden)
        {
            return forbidden;
        }

        if (!WorkflowEndpoints.TryResolveScope(httpContext, out var scope, out _, out var validationError))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["tenantScope"] = [validationError] });
        }

        var userId = DemoAuthorization.GetUserId(httpContext, fallback: string.Empty);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["userId"] = ["A signed-in user is required to change the assistant's identity."] });
        }

        var auditReason = AuditReasonReader.Read(httpContext);
        if (string.IsNullOrWhiteSpace(auditReason))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["auditReason"] = ["An audit reason is required to change the assistant's identity."] });
        }

        if (!Enum.TryParse<LineFocus>(request.LineFocus, ignoreCase: true, out var focus))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["lineFocus"] = ["Choose life, general or both."] });
        }

        try
        {
            var existing = await brands.GetAsync(scope, cancellationToken);
            var saved = await brands.SaveAsync(
                scope,
                new WorkspaceBrandSettings(
                    request.Organisation ?? string.Empty,
                    request.ShortName ?? string.Empty,
                    request.PartnerInsurers ?? [],
                    focus,
                    existing?.ScenarioId,
                    existing?.IsDemonstration ?? false),
                cancellationToken);
            await auditWriter.WriteAsync(
                AuditEvent.WorkspaceSettingChanged(scope, userId, "brand", "saved", WorkspaceBrandService.Key, auditReason, httpContext.TraceIdentifier),
                cancellationToken);
            return Results.Ok(ToResponse(saved, isDefault: false));
        }
        catch (WorkspaceBrandException failure)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["brand"] = [failure.Message] });
        }
    }

    private static WorkspaceBrandResponse ToResponse(WorkspaceBrandSettings settings, bool isDefault) => new(
        settings.Organisation,
        settings.ShortName,
        settings.PartnerInsurers,
        settings.LineFocus.ToString(),
        settings.PartnerInsurers.Count > 0,
        settings.ScenarioId,
        settings.IsDemonstration,
        isDefault);
}

public sealed record WorkspaceBrandRequest(string? Organisation, string? ShortName, IReadOnlyList<string>? PartnerInsurers, string? LineFocus);

public sealed record WorkspaceBrandResponse(
    string Organisation,
    string ShortName,
    IReadOnlyList<string> PartnerInsurers,
    string LineFocus,
    bool IsBancassurance,
    string? ScenarioId,
    bool IsDemonstration,
    bool IsStartupDefault);
