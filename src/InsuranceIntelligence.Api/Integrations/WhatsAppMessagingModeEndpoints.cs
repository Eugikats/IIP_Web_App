using InsuranceIntelligence.Api.Auth;
using InsuranceIntelligence.Api.Workflow;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Infrastructure.Configuration;

namespace InsuranceIntelligence.Api.Integrations;

public sealed record UpdateWhatsAppMessagingModeRequest(string Mode);

public static class WhatsAppMessagingModeEndpoints
{
    public static async Task<IResult> Get(
        ITenantConfigurationRepository repository,
        InfrastructureOptions options,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (DemoAuthorization.Require(httpContext, options, Permission.ConfigureIntegrations) is { } forbidden)
            return forbidden;
        if (!WorkflowEndpoints.TryResolveScope(httpContext, out var scope, out _, out var validationError))
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["tenantScope"] = [validationError] });

        var mode = (await repository.GetAsync(scope, cancellationToken)).CustomerMessagingMode;
        return Results.Ok(new WhatsAppMessagingModeResponse(
            mode.ToString(), EffectiveMode(mode, options.WhatsApp.AssistantAutoReplyEnabled),
            options.WhatsApp.AssistantAutoReplyEnabled));
    }

    public static async Task<IResult> Update(
        UpdateWhatsAppMessagingModeRequest request,
        ITenantConfigurationRepository repository,
        IAuditRepository auditRepository,
        InfrastructureOptions options,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (DemoAuthorization.Require(httpContext, options, Permission.ConfigureIntegrations) is { } forbidden)
            return forbidden;
        if (!WorkflowEndpoints.TryResolveScope(httpContext, out var scope, out _, out var validationError))
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["tenantScope"] = [validationError] });
        if (!Enum.TryParse<CustomerMessagingMode>(request.Mode, ignoreCase: true, out var mode))
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["mode"] = ["Choose Review or Autopilot."] });

        var current = await repository.GetAsync(scope, cancellationToken);
        if (current.CustomerMessagingMode == mode)
            return Results.Conflict(new { message = "The selected messaging mode is already active." });

        var actor = DemoAuthorization.GetUserId(httpContext, fallback: string.Empty);
        if (string.IsNullOrWhiteSpace(actor))
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["userId"] = ["Authenticated user context is required."] });
        var auditReason = httpContext.Request.Headers["X-Audit-Reason"].FirstOrDefault()?.Trim();
        if (string.IsNullOrWhiteSpace(auditReason))
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["auditReason"] = ["An audit reason is required to change the customer messaging mode."] });

        var updated = current with { CustomerMessagingMode = mode, UpdatedAt = DateTimeOffset.UtcNow };
        await repository.SaveAsync(updated, cancellationToken);
        await auditRepository.WriteAsync(new AuditEvent(
            Guid.NewGuid(), scope, null, null, actor, ActorType.User,
            "integration.whatsapp.messaging_mode_updated", DateTimeOffset.UtcNow,
            current.CustomerMessagingMode.ToString(), mode.ToString(), ["customerMessagingMode"],
            "Admin", null, "accepted", null, auditReason, httpContext.TraceIdentifier), cancellationToken);

        return Results.Ok(new WhatsAppMessagingModeResponse(
            mode.ToString(), EffectiveMode(mode, options.WhatsApp.AssistantAutoReplyEnabled),
            options.WhatsApp.AssistantAutoReplyEnabled));
    }

    private static string EffectiveMode(CustomerMessagingMode mode, bool deploymentEnabled) =>
        mode == CustomerMessagingMode.Autopilot && deploymentEnabled ? "Autopilot" : "Review";
}

public sealed record WhatsAppMessagingModeResponse(string Mode, string EffectiveMode, bool AutopilotAvailable);
