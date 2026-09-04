using InsuranceIntelligence.Api.Auth;
using InsuranceIntelligence.Api.Common;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Documents;

namespace InsuranceIntelligence.Api.Documents;

/// <summary>
/// Tenant rule management for <see cref="DocumentFieldValidationRule"/> — lets a tenant with real
/// regulatory knowledge correct or extend the field-shape checks (the Uganda-verified national-ID
/// length rule and any others) without a deployment. See DocumentFieldValidationRuleDefaults for
/// why this repository starts effectively empty and ResolveRules layers it over the defaults.
/// </summary>
public static class DocumentFieldValidationRuleEndpoints
{
    public static async Task<IResult> ListRules(
        IDocumentFieldValidationRuleRepository repository,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!TenantScopeReader.TryGetFromHeaders(httpContext, out var scope, out var validationError))
        {
            return Validation("tenantScope", validationError);
        }

        var tenantRules = await repository.ListAsync(scope, cancellationToken);
        var resolved = DocumentFieldValidator.ResolveRules(scope, tenantRules)
            .OrderBy(rule => rule.RuleId, StringComparer.Ordinal)
            .Select(ToResponse)
            .ToArray();
        return Results.Ok(resolved);
    }

    public static async Task<IResult> SaveRule(
        DocumentFieldValidationRuleRequest request,
        IDocumentFieldValidationRuleRepository repository,
        IAuditRepository auditRepository,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!TenantScopeReader.TryGetFromHeaders(httpContext, out var scope, out var validationError))
        {
            return Validation("tenantScope", validationError);
        }

        var fieldError = Validate(request);
        if (fieldError is not null)
        {
            return fieldError;
        }

        var userId = DemoAuthorization.GetUserId(httpContext, fallback: string.Empty);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Validation("userId", "Authenticated user context is required to change document validation rules.");
        }

        var auditReason = AuditReasonReader.Read(httpContext);
        if (string.IsNullOrWhiteSpace(auditReason))
        {
            return Validation("auditReason", "An audit reason is required to change document validation rules.");
        }

        var existing = await repository.GetAsync(request.RuleId, scope, cancellationToken);
        var rule = new DocumentFieldValidationRule
        {
            RuleId = request.RuleId,
            Scope = scope,
            DocumentType = request.DocumentType,
            FieldLabel = request.FieldLabel,
            FieldKeyAliases = request.FieldKeyAliases,
            Kind = request.Kind,
            ExactLength = request.ExactLength,
            Pattern = request.Pattern,
            Severity = request.Severity,
            Message = request.Message,
            ApplicableProductLines = request.ApplicableProductLines ?? [],
            Enabled = request.Enabled,
            Source = "tenant",
            CreatedAt = existing?.CreatedAt ?? DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        await repository.SaveAsync(rule, cancellationToken);
        var correlationId = GetCorrelationId(httpContext);
        await auditRepository.WriteAsync(
            AuditEvent.DocumentFieldValidationRuleSaved(rule, userId, correlationId, auditReason),
            cancellationToken);

        return Results.Ok(ToResponse(rule));
    }

    private static IResult? Validate(DocumentFieldValidationRuleRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.RuleId))
        {
            return Validation("ruleId", "A rule ID is required.");
        }

        if (string.IsNullOrWhiteSpace(request.FieldLabel))
        {
            return Validation("fieldLabel", "A field label is required.");
        }

        if (request.FieldKeyAliases is null || request.FieldKeyAliases.Count == 0)
        {
            return Validation("fieldKeyAliases", "At least one field key alias is required to match extracted data.");
        }

        if (request.Kind == DocumentFieldRuleKind.ExactLength && request.ExactLength is null or <= 0)
        {
            return Validation("exactLength", "An exact length greater than zero is required for ExactLength rules.");
        }

        if (request.Kind == DocumentFieldRuleKind.Pattern && string.IsNullOrWhiteSpace(request.Pattern))
        {
            return Validation("pattern", "A pattern is required for Pattern rules.");
        }

        if (string.IsNullOrWhiteSpace(request.Severity))
        {
            return Validation("severity", "A severity is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Message))
        {
            return Validation("message", "A warning message is required.");
        }

        return null;
    }

    private static DocumentFieldValidationRuleResponse ToResponse(DocumentFieldValidationRule rule) => new(
        rule.RuleId,
        rule.DocumentType.ToString(),
        rule.FieldLabel,
        rule.FieldKeyAliases,
        rule.Kind.ToString(),
        rule.ExactLength,
        rule.Pattern,
        rule.Severity,
        rule.Message,
        rule.ApplicableProductLines,
        rule.Enabled,
        rule.Source,
        rule.UpdatedAt);

    private static IResult Validation(string key, string message) =>
        Results.ValidationProblem(new Dictionary<string, string[]>
        {
            [key] = [message]
        });

    private static string GetCorrelationId(HttpContext httpContext)
    {
        if (httpContext.Request.Headers.TryGetValue("X-Correlation-ID", out var header)
            && !string.IsNullOrWhiteSpace(header))
        {
            return header.ToString();
        }

        return httpContext.TraceIdentifier;
    }
}

public sealed record DocumentFieldValidationRuleRequest(
    string RuleId,
    DocumentType DocumentType,
    string FieldLabel,
    IReadOnlyCollection<string> FieldKeyAliases,
    DocumentFieldRuleKind Kind,
    int? ExactLength,
    string? Pattern,
    string Severity,
    string Message,
    IReadOnlyCollection<string>? ApplicableProductLines,
    bool Enabled = true);

public sealed record DocumentFieldValidationRuleResponse(
    string RuleId,
    string DocumentType,
    string FieldLabel,
    IReadOnlyCollection<string> FieldKeyAliases,
    string Kind,
    int? ExactLength,
    string? Pattern,
    string Severity,
    string Message,
    IReadOnlyCollection<string> ApplicableProductLines,
    bool Enabled,
    string? Source,
    DateTimeOffset UpdatedAt);
