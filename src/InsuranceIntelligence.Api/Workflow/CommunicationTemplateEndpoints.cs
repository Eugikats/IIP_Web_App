using InsuranceIntelligence.Api.Common;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Workflow;

namespace InsuranceIntelligence.Api.Workflow;

/// <summary>
/// The message template library (outcome D). A template belongs to a reason category and a
/// channel, holds a subject, a long body and a short SMS body, and is versioned exactly like
/// AgentBehaviourProfile and Product, every save is a new row, never an overwrite.
/// </summary>
public static class CommunicationTemplateEndpoints
{
    public static async Task<IResult> ListTemplates(
        ICommunicationTemplateRepository repository,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!WorkflowEndpoints.TryResolveScope(httpContext, out var scope, out _, out var validationError))
        {
            return WorkflowEndpoints.Validation("tenantScope", validationError);
        }

        var templates = await repository.ListCurrentForAllTemplatesAsync(scope, cancellationToken);
        return Results.Ok(templates.OrderBy(t => t.ReasonCategory).ThenBy(t => t.Channel).Select(ToResponse).ToArray());
    }

    public static async Task<IResult> GetCurrent(
        string templateKey,
        ICommunicationTemplateRepository repository,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!WorkflowEndpoints.TryResolveScope(httpContext, out var scope, out _, out var validationError))
        {
            return WorkflowEndpoints.Validation("tenantScope", validationError);
        }

        var template = await repository.GetCurrentAsync(scope, templateKey, cancellationToken);
        return template is null
            ? Results.NotFound(new { message = "That template was not found for the current tenant workspace." })
            : Results.Ok(ToResponse(template));
    }

    public static async Task<IResult> ListVersions(
        string templateKey,
        ICommunicationTemplateRepository repository,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!WorkflowEndpoints.TryResolveScope(httpContext, out var scope, out _, out var validationError))
        {
            return WorkflowEndpoints.Validation("tenantScope", validationError);
        }

        var versions = await repository.ListVersionsAsync(scope, templateKey, cancellationToken);
        return Results.Ok(versions.Select(ToResponse).ToArray());
    }

    public static async Task<IResult> SaveVersion(
        string templateKey,
        SaveCommunicationTemplateRequest request,
        ICommunicationTemplateRepository repository,
        IAuditRepository auditRepository,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!WorkflowEndpoints.TryResolveScope(httpContext, out var scope, out _, out var validationError))
        {
            return WorkflowEndpoints.Validation("tenantScope", validationError);
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return WorkflowEndpoints.Validation("name", "A template name is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Body))
        {
            return WorkflowEndpoints.Validation("body", "A message body is required.");
        }

        if (string.IsNullOrWhiteSpace(request.ShortBody))
        {
            return WorkflowEndpoints.Validation("shortBody", "A short, SMS-safe body is required, most phones in this market are not smartphones.");
        }

        if (!Enum.TryParse<CustomerUpdateReasonCategory>(request.ReasonCategory, true, out var reasonCategory))
        {
            return WorkflowEndpoints.Validation("reasonCategory", "A supported reason category is required.");
        }

        if (!Enum.TryParse<CommunicationChannel>(request.Channel, true, out var channel))
        {
            return WorkflowEndpoints.Validation("channel", "A supported communication channel is required.");
        }

        var actor = WorkflowEndpoints.GetActor(httpContext, null);
        if (string.IsNullOrWhiteSpace(actor))
        {
            return WorkflowEndpoints.Validation("userId", "Authenticated user context is required to change a template.");
        }

        var auditReason = AuditReasonReader.Read(httpContext);
        if (string.IsNullOrWhiteSpace(auditReason))
        {
            return WorkflowEndpoints.Validation("auditReason", "A reason is required to change a template.");
        }

        var current = await repository.GetCurrentAsync(scope, templateKey, cancellationToken);
        var version = new CommunicationTemplate
        {
            TemplateId = current?.TemplateId ?? Guid.NewGuid(),
            VersionId = Guid.NewGuid(),
            Scope = scope,
            Version = (current?.Version ?? 0) + 1,
            TemplateKey = templateKey,
            Name = request.Name.Trim(),
            ReasonCategory = reasonCategory,
            Channel = channel,
            Language = string.IsNullOrWhiteSpace(request.Language) ? "en" : request.Language.Trim(),
            Subject = string.IsNullOrWhiteSpace(request.Subject) ? null : request.Subject.Trim(),
            Body = request.Body.Trim(),
            ShortBody = request.ShortBody.Trim(),
            Active = request.Active,
            UpdatedByUserId = actor,
            UpdatedAt = DateTimeOffset.UtcNow,
            ChangeReason = auditReason,
        };

        await repository.SaveAsync(version, cancellationToken);
        await auditRepository.WriteAsync(
            AuditEvent.CommunicationTemplateSaved(scope, actor, templateKey, version.Version, version.Active, auditReason, httpContext.TraceIdentifier),
            cancellationToken);

        return Results.Ok(ToResponse(version));
    }

    /// <summary>Seeds the library from the messages already hardcoded in
    /// MockCustomerUpdateDraftingProvider (outcome D), as version 1 of each default template.
    /// Idempotent: a default whose TemplateKey already exists for this tenant is left alone, so
    /// running this again after a tenant has customized a template never overwrites their edit.</summary>
    public static async Task<IResult> SeedDefaults(
        ICommunicationTemplateRepository repository,
        IAuditRepository auditRepository,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!WorkflowEndpoints.TryResolveScope(httpContext, out var scope, out _, out var validationError))
        {
            return WorkflowEndpoints.Validation("tenantScope", validationError);
        }

        var actor = WorkflowEndpoints.GetActor(httpContext, null);
        if (string.IsNullOrWhiteSpace(actor))
        {
            return WorkflowEndpoints.Validation("userId", "Authenticated user context is required to seed templates.");
        }

        var existing = await repository.ListCurrentForAllTemplatesAsync(scope, cancellationToken);
        var existingKeys = existing.Select(t => t.TemplateKey).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var created = new List<CommunicationTemplateResponse>();
        foreach (var defaultTemplate in DefaultCommunicationTemplates.Defaults())
        {
            if (existingKeys.Contains(defaultTemplate.TemplateKey))
            {
                continue;
            }

            var version = new CommunicationTemplate
            {
                TemplateId = Guid.NewGuid(),
                VersionId = Guid.NewGuid(),
                Scope = scope,
                Version = 1,
                TemplateKey = defaultTemplate.TemplateKey,
                Name = defaultTemplate.Name,
                ReasonCategory = defaultTemplate.ReasonCategory,
                Channel = defaultTemplate.Channel,
                Subject = defaultTemplate.Subject,
                Body = defaultTemplate.Body,
                ShortBody = defaultTemplate.ShortBody,
                Active = true,
                UpdatedByUserId = actor,
                UpdatedAt = DateTimeOffset.UtcNow,
                ChangeReason = "Seeded from the platform's default message library.",
            };
            await repository.SaveAsync(version, cancellationToken);
            await auditRepository.WriteAsync(
                AuditEvent.CommunicationTemplateSaved(scope, actor, version.TemplateKey, version.Version, version.Active, version.ChangeReason, httpContext.TraceIdentifier),
                cancellationToken);
            created.Add(ToResponse(version));
        }

        return Results.Ok(new { seeded = created.Count, templates = created });
    }

    private static CommunicationTemplateResponse ToResponse(CommunicationTemplate template) =>
        new(
            template.TemplateId,
            template.TemplateKey,
            template.Version,
            template.Name,
            template.ReasonCategory.ToString(),
            template.Channel.ToString(),
            template.Language,
            template.Subject,
            template.Body,
            template.ShortBody,
            template.Active,
            template.UpdatedByUserId,
            template.UpdatedAt,
            template.ChangeReason);
}

public sealed record SaveCommunicationTemplateRequest(
    string? Name,
    string? ReasonCategory,
    string? Channel,
    string? Language,
    string? Subject,
    string? Body,
    string? ShortBody,
    bool Active);

public sealed record CommunicationTemplateResponse(
    Guid TemplateId,
    string TemplateKey,
    int Version,
    string Name,
    string ReasonCategory,
    string Channel,
    string Language,
    string? Subject,
    string Body,
    string ShortBody,
    bool Active,
    string UpdatedByUserId,
    DateTimeOffset UpdatedAt,
    string ChangeReason);
