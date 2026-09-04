using System.Text.Json;
using InsuranceIntelligence.Api.Documents;
using InsuranceIntelligence.LocalTestDoubles;
using InsuranceIntelligence.Domain.Documents;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace InsuranceIntelligence.Api.Tests;

public sealed class DocumentFieldValidationRuleEndpointsTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task ListRules_ReturnsTheBuiltInDefaultWithNoTenantConfiguration()
    {
        var repository = new InMemoryDocumentFieldValidationRuleRepository();

        var result = await DocumentFieldValidationRuleEndpoints.ListRules(repository, ScopedContext(), CancellationToken.None);
        var (statusCode, response) = await ExecuteAsync<DocumentFieldValidationRuleResponse[]>(result);

        Assert.Equal(StatusCodes.Status200OK, statusCode);
        var rule = Assert.Single(response!, rule => rule.RuleId == DocumentFieldValidationRuleDefaults.NationalIdRuleId);
        Assert.Equal("CustomerId", rule.DocumentType);
        Assert.Equal(14, rule.ExactLength);
        Assert.True(rule.Enabled);
    }

    [Fact]
    public async Task SaveRule_PersistsATenantRuleAndWritesAnAuditEvent()
    {
        var repository = new InMemoryDocumentFieldValidationRuleRepository();
        var auditRepository = new InMemoryAuditRepository();
        var request = new DocumentFieldValidationRuleRequest(
            "tenant.policy-number-format",
            DocumentType.ClaimForm,
            "Policy number",
            ["policy_number", "policyno"],
            DocumentFieldRuleKind.Pattern,
            null,
            "^[A-Z]{3}/[A-Z]{2}/\\d{4}/\\d{5}$",
            "warning",
            "Policy number does not match the expected format.",
            null);

        var result = await DocumentFieldValidationRuleEndpoints.SaveRule(
            request, repository, auditRepository, ScopedContext(auditReason: "Adding tenant-specific policy number format check."), CancellationToken.None);
        var (statusCode, response) = await ExecuteAsync<DocumentFieldValidationRuleResponse>(result);

        Assert.Equal(StatusCodes.Status200OK, statusCode);
        Assert.Equal("tenant.policy-number-format", response!.RuleId);
        Assert.Equal("tenant", response.Source);

        var stored = await repository.ListAsync(Scope(), CancellationToken.None);
        Assert.Contains(stored, rule => rule.RuleId == "tenant.policy-number-format");

        var auditEvents = await auditRepository.ListAsync(Scope(), CancellationToken.None);
        Assert.Contains(auditEvents, auditEvent =>
            auditEvent.ActionName == "document_field_validation_rule.saved"
            && auditEvent.AuditReason == "Adding tenant-specific policy number format check.");
    }

    [Fact]
    public async Task SaveRule_RequiresAnAuditReason()
    {
        var repository = new InMemoryDocumentFieldValidationRuleRepository();
        var auditRepository = new InMemoryAuditRepository();
        var request = new DocumentFieldValidationRuleRequest(
            "tenant.policy-number-format",
            DocumentType.ClaimForm,
            "Policy number",
            ["policy_number"],
            DocumentFieldRuleKind.Pattern,
            null,
            "^POL-\\d+$",
            "warning",
            "Unexpected policy number format.",
            null);

        var result = await DocumentFieldValidationRuleEndpoints.SaveRule(
            request, repository, auditRepository, ScopedContext(), CancellationToken.None);
        var (statusCode, _) = await ExecuteAsync<JsonElement>(result);

        Assert.Equal(StatusCodes.Status400BadRequest, statusCode);
        Assert.Empty(await repository.ListAsync(Scope(), CancellationToken.None));
    }

    [Fact]
    public async Task SaveRule_RequiresAnExactLengthForExactLengthRules()
    {
        var repository = new InMemoryDocumentFieldValidationRuleRepository();
        var auditRepository = new InMemoryAuditRepository();
        var request = new DocumentFieldValidationRuleRequest(
            "tenant.bad-rule",
            DocumentType.CustomerId,
            "Some field",
            ["some_field"],
            DocumentFieldRuleKind.ExactLength,
            ExactLength: null,
            Pattern: null,
            "warning",
            "Message",
            null);

        var result = await DocumentFieldValidationRuleEndpoints.SaveRule(
            request, repository, auditRepository, ScopedContext(auditReason: "test"), CancellationToken.None);
        var (statusCode, _) = await ExecuteAsync<JsonElement>(result);

        Assert.Equal(StatusCodes.Status400BadRequest, statusCode);
    }

    [Fact]
    public async Task SaveRule_OverwritingAnExistingRuleReusesItsCreatedAtAndKeepsOneStoredRow()
    {
        var repository = new InMemoryDocumentFieldValidationRuleRepository();
        var auditRepository = new InMemoryAuditRepository();
        var firstRequest = new DocumentFieldValidationRuleRequest(
            "tenant.rule-a",
            DocumentType.CustomerId,
            "Field A",
            ["field_a"],
            DocumentFieldRuleKind.ExactLength,
            5,
            null,
            "warning",
            "First version.",
            null);
        await DocumentFieldValidationRuleEndpoints.SaveRule(
            firstRequest, repository, auditRepository, ScopedContext(auditReason: "first"), CancellationToken.None);

        var secondRequest = firstRequest with { Message = "Second version.", ExactLength = 7 };
        await DocumentFieldValidationRuleEndpoints.SaveRule(
            secondRequest, repository, auditRepository, ScopedContext(auditReason: "second"), CancellationToken.None);

        var stored = await repository.ListAsync(Scope(), CancellationToken.None);
        var rule = Assert.Single(stored, rule => rule.RuleId == "tenant.rule-a");
        Assert.Equal("Second version.", rule.Message);
        Assert.Equal(7, rule.ExactLength);
    }

    [Fact]
    public async Task ListRules_DoesNotLeakAnotherTenantsRuleOverride()
    {
        var repository = new InMemoryDocumentFieldValidationRuleRepository();
        var auditRepository = new InMemoryAuditRepository();
        var request = new DocumentFieldValidationRuleRequest(
            DocumentFieldValidationRuleDefaults.NationalIdRuleId,
            DocumentType.CustomerId,
            "National ID number (NIN)",
            ["national_id"],
            DocumentFieldRuleKind.ExactLength,
            9,
            null,
            "warning",
            "Overridden for this tenant only.",
            null);
        await DocumentFieldValidationRuleEndpoints.SaveRule(
            request, repository, auditRepository, ScopedContext(auditReason: "override"), CancellationToken.None);

        var otherTenantResult = await DocumentFieldValidationRuleEndpoints.ListRules(
            repository, ScopedContext(tenantId: "tenant-other"), CancellationToken.None);
        var (_, otherTenantRules) = await ExecuteAsync<DocumentFieldValidationRuleResponse[]>(otherTenantResult);

        var defaultRule = Assert.Single(otherTenantRules!, rule => rule.RuleId == DocumentFieldValidationRuleDefaults.NationalIdRuleId);
        Assert.Equal(14, defaultRule.ExactLength);
    }

    private static InsuranceIntelligence.Domain.Common.TenantScope Scope() =>
        InsuranceIntelligence.Domain.Common.TenantScope.Create("tenant-demo", "workspace-claims");

    private static DefaultHttpContext ScopedContext(
        string? tenantId = "tenant-demo",
        string? workspaceId = "workspace-claims",
        string? auditReason = null)
    {
        var context = new DefaultHttpContext { TraceIdentifier = "trace-rule-001" };
        context.Request.Headers["X-Tenant-Id"] = tenantId;
        context.Request.Headers["X-Workspace-Id"] = workspaceId;
        context.Request.Headers["X-User-Id"] = "user-claims-1";
        context.Request.Headers["x-demo-user-id"] = "user-claims-1";
        context.Request.Headers["x-demo-role"] = "ITAdmin";
        context.Request.Headers["X-Correlation-ID"] = "corr-rule-001";
        if (auditReason is not null)
        {
            context.Request.Headers["X-Audit-Reason"] = auditReason;
        }

        return context;
    }

    private static async Task<(int? StatusCode, T? Body)> ExecuteAsync<T>(IResult result)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.RequestServices = new ServiceCollection()
            .AddLogging()
            .AddProblemDetails()
            .BuildServiceProvider();
        httpContext.Response.Body = new MemoryStream();

        await result.ExecuteAsync(httpContext);

        httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        var body = httpContext.Response.Body.Length == 0
            ? default
            : await JsonSerializer.DeserializeAsync<T>(httpContext.Response.Body, JsonOptions);

        return (httpContext.Response.StatusCode, body);
    }
}
