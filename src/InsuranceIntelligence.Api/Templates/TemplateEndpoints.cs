using InsuranceIntelligence.Api.Auth;
using InsuranceIntelligence.Api.Workflow;
using InsuranceIntelligence.Application.Core;
using InsuranceIntelligence.Application.Templates;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Infrastructure.Configuration;

namespace InsuranceIntelligence.Api.Templates;

/// <summary>
/// The workspace's WhatsApp message templates: see them, edit a draft, mark one submitted, and record what WhatsApp decided.
/// Viewing needs the right to manage customer communications; changing needs the right to configure integrations. Every
/// change is audited by name and action, never by content. Nothing here talks to WhatsApp: submission to Meta and its decision
/// are recorded by a person until the provider reports them itself.
/// </summary>
public static class TemplateEndpoints
{
    public static async Task<IResult> List(TemplateCatalogService catalogue, InfrastructureOptions options, HttpContext httpContext, CancellationToken cancellationToken)
    {
        if (DemoAuthorization.Require(httpContext, options, Permission.ManageCustomerCommunications) is { } forbidden)
        {
            return forbidden;
        }

        if (!WorkflowEndpoints.TryResolveScope(httpContext, out var scope, out _, out var scopeError))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["tenantScope"] = [scopeError] });
        }

        return Results.Ok((await catalogue.ListAsync(scope, cancellationToken)).Select(Describe));
    }

    public static Task<IResult> SaveDraft(
        string name, string language, TemplateEditRequest request, TemplateCatalogService catalogue, IAuditWriter audit,
        InfrastructureOptions options, HttpContext httpContext, CancellationToken cancellationToken) =>
        Change(httpContext, options, "saved", name, audit, async (scope, userId) =>
            Describe(await catalogue.SaveDraftAsync(
                scope,
                new MessageTemplate(
                    name, language, ParseCategory(request.Category), TemplateStatus.Draft, request.Purpose ?? string.Empty, request.Body ?? string.Empty,
                    request.Parameters ?? [], request.Buttons ?? [], DateTimeOffset.UtcNow),
                userId, cancellationToken)));

    public static Task<IResult> Submit(
        string name, string language, TemplateCatalogService catalogue, IAuditWriter audit,
        InfrastructureOptions options, HttpContext httpContext, CancellationToken cancellationToken) =>
        Change(httpContext, options, "submitted", name, audit, async (scope, userId) =>
            Describe(await catalogue.SubmitAsync(scope, name, language, userId, cancellationToken)));

    public static Task<IResult> Decide(
        string name, string language, TemplateDecisionRequest request, TemplateCatalogService catalogue, IAuditWriter audit,
        InfrastructureOptions options, HttpContext httpContext, CancellationToken cancellationToken) =>
        Change(httpContext, options, "decided", name, audit, async (scope, userId) =>
        {
            if (!Enum.TryParse<TemplateStatus>(request.Decision, ignoreCase: true, out var decision))
            {
                throw TemplateException.Invalid("A decision is approved, rejected or paused.");
            }

            return Describe(await catalogue.RecordDecisionAsync(scope, name, language, decision, request.Note, userId, cancellationToken));
        });

    public static Task<IResult> Import(
        TemplateImportRequest request, TemplateCatalogService catalogue, IAuditWriter audit,
        InfrastructureOptions options, HttpContext httpContext, CancellationToken cancellationToken) =>
        Change(httpContext, options, "imported", "import", audit, async (scope, userId) =>
        {
            var parsed = new List<MessageTemplate>();
            var refused = new List<string>();
            foreach (var item in request.Templates ?? [])
            {
                if (!Enum.TryParse<TemplateStatus>(item.Status ?? "Draft", ignoreCase: true, out var status))
                {
                    refused.Add($"{item.Name}: unknown status {item.Status}.");
                    continue;
                }

                parsed.Add(new MessageTemplate(
                    item.Name ?? string.Empty, item.Language ?? DefaultTemplates.Language, ParseCategory(item.Category), status,
                    item.Purpose ?? string.Empty, item.Body ?? string.Empty, item.Parameters ?? [], item.Buttons ?? [], DateTimeOffset.UtcNow,
                    DecisionNote: status is TemplateStatus.Approved or TemplateStatus.Rejected ? "Imported with this status." : null,
                    DecidedAt: status is TemplateStatus.Approved or TemplateStatus.Rejected ? DateTimeOffset.UtcNow : null));
            }

            var result = await catalogue.ImportAsync(scope, parsed, userId, cancellationToken);
            return new { stored = result.Stored, refused = refused.Concat(result.Refused).ToArray() };
        });

    private static async Task<IResult> Change(
        HttpContext httpContext, InfrastructureOptions options, string action, string subject, IAuditWriter audit,
        Func<InsuranceIntelligence.Domain.Common.TenantScope, string, Task<object>> work)
    {
        if (DemoAuthorization.Require(httpContext, options, Permission.ConfigureIntegrations) is { } forbidden)
        {
            return forbidden;
        }

        if (!WorkflowEndpoints.TryResolveScope(httpContext, out var scope, out var userId, out var scopeError))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["tenantScope"] = [scopeError] });
        }

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["userId"] = ["A signed-in user is required to change templates."] });
        }

        try
        {
            var result = await work(scope, userId);
            await audit.WriteAsync(
                AuditEvent.WorkspaceSettingChanged(scope, userId, "template", action, subject, $"Template {subject} {action}.", httpContext.TraceIdentifier),
                httpContext.RequestAborted);
            return Results.Ok(result);
        }
        catch (TemplateException failure)
        {
            var status = failure.Code switch
            {
                "not_found" => StatusCodes.Status404NotFound,
                "conflict" => StatusCodes.Status409Conflict,
                _ => StatusCodes.Status400BadRequest,
            };
            return Results.Problem(failure.Message, statusCode: status);
        }
    }

    private static TemplateCategory ParseCategory(string? value) =>
        Enum.TryParse<TemplateCategory>(value, ignoreCase: true, out var category) ? category : TemplateCategory.Utility;

    private static object Describe(MessageTemplate template) => new
    {
        template.Name,
        template.Language,
        category = template.Category.ToString(),
        status = template.Status.ToString(),
        template.Purpose,
        template.Body,
        template.Parameters,
        template.Buttons,
        template.DecisionNote,
        template.DecidedAt,
        updatedAt = template.UpdatedAt == DateTimeOffset.UnixEpoch ? (DateTimeOffset?)null : template.UpdatedAt,
        template.UpdatedBy,
        isSendable = template.IsSendable,
        isEditable = template.IsEditable,
        isStandardDraft = template.UpdatedAt == DateTimeOffset.UnixEpoch,
        problems = template.Problems(),
    };
}

public sealed record TemplateEditRequest(string? Category, string? Purpose, string? Body, IReadOnlyList<string>? Parameters, IReadOnlyList<string>? Buttons);

public sealed record TemplateDecisionRequest(string? Decision, string? Note);

public sealed record TemplateImportItem(
    string? Name, string? Language, string? Category, string? Status, string? Purpose, string? Body, IReadOnlyList<string>? Parameters, IReadOnlyList<string>? Buttons);

public sealed record TemplateImportRequest(IReadOnlyList<TemplateImportItem>? Templates);
