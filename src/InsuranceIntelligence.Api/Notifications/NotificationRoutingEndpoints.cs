using InsuranceIntelligence.Api.Auth;
using InsuranceIntelligence.Api.Workflow;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Notifications;
using InsuranceIntelligence.Infrastructure.Configuration;
using InsuranceIntelligence.Infrastructure.Notifications;
using Microsoft.Extensions.Options;

namespace InsuranceIntelligence.Api.Notifications;

public sealed record UpdateNotificationRoutingRequest(IReadOnlyCollection<UpdateNotificationRoutingRule>? Rules);

public sealed record UpdateNotificationRoutingRule(
    string? Type,
    IReadOnlyCollection<string>? AdditionalRecipientUserIds,
    IReadOnlyCollection<string>? AdditionalDestinations);

public sealed record NotificationRoutingUser(string UserId, string Name, string Role);

public sealed record NotificationRoutingResponse(
    NotificationRoutingConfigurationResponse Configuration,
    IReadOnlyCollection<NotificationRoutingUser> Users,
    IReadOnlyCollection<string> AvailableDestinations);

public sealed record NotificationRoutingConfigurationResponse(IReadOnlyCollection<NotificationRoutingRuleResponse> Rules);

public sealed record NotificationRoutingRuleResponse(
    string Type,
    IReadOnlyCollection<string> AdditionalRecipientUserIds,
    IReadOnlyCollection<string> AdditionalDestinations);

public static class NotificationRoutingEndpoints
{
    private const int MaxRules = 32;
    private const int MaxRecipientsPerRule = 50;

    public static async Task<IResult> Get(
        ITenantConfigurationRepository configurationRepository,
        IUserAccountRepository userRepository,
        InfrastructureOptions options,
        HttpContext httpContext,
        CancellationToken cancellationToken,
        IOptions<IncomingWebhookTeamsProviderOptions>? teamsOptions = null)
    {
        if (DemoAuthorization.Require(httpContext, options, Permission.ConfigureIntegrations) is { } forbidden)
            return forbidden;
        if (!WorkflowEndpoints.TryResolveScope(httpContext, out var scope, out _, out var error))
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["tenantScope"] = [error] });

        var configuration = await configurationRepository.GetAsync(scope, cancellationToken);
        var users = (await userRepository.ListAsync(scope, cancellationToken))
            .Where(user => string.Equals(user.Status, "Active", StringComparison.OrdinalIgnoreCase))
            .OrderBy(user => user.Name, StringComparer.OrdinalIgnoreCase)
            .Select(user => new NotificationRoutingUser(user.UserId, user.Name, user.Role.ToString()))
            .ToArray();
        var available = new List<string> { NotificationDestination.InApp.ToString() };
        if (IsTeamsAvailable(options, teamsOptions?.Value))
            available.Add(NotificationDestination.MicrosoftTeams.ToString());

        return Results.Ok(new NotificationRoutingResponse(ToResponse(configuration.NotificationRouting), users, available));
    }

    public static async Task<IResult> Update(
        UpdateNotificationRoutingRequest request,
        ITenantConfigurationRepository configurationRepository,
        IUserAccountRepository userRepository,
        IAuditRepository auditRepository,
        InfrastructureOptions options,
        HttpContext httpContext,
        CancellationToken cancellationToken,
        IOptions<IncomingWebhookTeamsProviderOptions>? teamsOptions = null)
    {
        if (DemoAuthorization.Require(httpContext, options, Permission.ConfigureIntegrations) is { } forbidden)
            return forbidden;
        if (!WorkflowEndpoints.TryResolveScope(httpContext, out var scope, out _, out var error))
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["tenantScope"] = [error] });
        if (request.Rules is null || request.Rules.Count > MaxRules)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["rules"] = [$"Provide at most {MaxRules} routing rules."] });

        var domainRules = ParseRules(
            request.Rules,
            IsTeamsAvailable(options, teamsOptions?.Value),
            out var ruleErrors);
        if (ruleErrors.Count > 0 || domainRules is null)
            return Results.ValidationProblem(ruleErrors);

        var selectedUserIds = domainRules.SelectMany(rule => rule.AdditionalRecipientUserIds)
            .Distinct(StringComparer.Ordinal).ToArray();
        var activeUsers = (await userRepository.ListAsync(scope, cancellationToken))
            .Where(user => string.Equals(user.Status, "Active", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var activeUserIds = activeUsers.Select(user => user.UserId).ToHashSet(StringComparer.Ordinal);
        var unknown = selectedUserIds.Where(userId => !activeUserIds.Contains(userId)).ToArray();
        if (unknown.Length > 0)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["rules"] = ["Select active team members from this workspace only."] });

        var actor = DemoAuthorization.GetUserId(httpContext, fallback: string.Empty);
        if (string.IsNullOrWhiteSpace(actor))
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["userId"] = ["Authenticated user context is required."] });
        var reason = httpContext.Request.Headers["X-Audit-Reason"].FirstOrDefault()?.Trim();
        if (string.IsNullOrWhiteSpace(reason))
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["auditReason"] = ["An audit reason is required to change notification routing."] });

        var current = await configurationRepository.GetAsync(scope, cancellationToken);
        if (RulesEqual(current.NotificationRouting.Rules, domainRules))
            return Results.Conflict(new { message = "The notification routing rules are already up to date." });

        var updated = current with
        {
            NotificationRouting = new NotificationRoutingConfiguration(domainRules.ToArray()),
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        await configurationRepository.SaveAsync(updated, cancellationToken);
        await auditRepository.WriteAsync(new AuditEvent(
            Guid.NewGuid(), scope, null, null, actor, ActorType.User,
            "admin.notification_routing.updated", DateTimeOffset.UtcNow,
            "Configured", "Configured", ["ruleCount", "recipientCount"],
            "Admin", null, "accepted", null, reason, httpContext.TraceIdentifier), cancellationToken);

        var available = new List<string> { NotificationDestination.InApp.ToString() };
        if (IsTeamsAvailable(options, teamsOptions?.Value))
            available.Add(NotificationDestination.MicrosoftTeams.ToString());
        var responseUsers = activeUsers.OrderBy(user => user.Name, StringComparer.OrdinalIgnoreCase)
            .Select(user => new NotificationRoutingUser(user.UserId, user.Name, user.Role.ToString())).ToArray();
        return Results.Ok(new NotificationRoutingResponse(ToResponse(updated.NotificationRouting), responseUsers, available));
    }

    private static IReadOnlyCollection<NotificationRoutingRule>? ParseRules(
        IReadOnlyCollection<UpdateNotificationRoutingRule> requested,
        bool teamsAvailable,
        out Dictionary<string, string[]> errors)
    {
        errors = new Dictionary<string, string[]>();
        var parsed = new List<NotificationRoutingRule>();
        foreach (var rule in requested)
        {
            if (!Enum.TryParse<NotificationType>(rule.Type, ignoreCase: false, out var type) || !Enum.IsDefined(type))
            {
                errors["rules"] = ["Choose a supported notification type."];
                continue;
            }
            if (rule.AdditionalRecipientUserIds is null || rule.AdditionalDestinations is null)
            {
                errors["rules"] = ["Recipient and destination lists are required for each rule."];
                continue;
            }

            var destinations = new List<NotificationDestination>();
            foreach (var destination in rule.AdditionalDestinations)
            {
                if (!Enum.TryParse<NotificationDestination>(destination, ignoreCase: false, out var parsedDestination)
                    || !Enum.IsDefined(parsedDestination))
                {
                    errors["destinations"] = ["Choose a supported notification destination."];
                    continue;
                }
                destinations.Add(parsedDestination);
            }
            parsed.Add(new NotificationRoutingRule(type, rule.AdditionalRecipientUserIds.ToArray(), destinations));
        }

        if (parsed.Select(rule => rule.Type).Distinct().Count() != requested.Count)
            errors["rules"] = ["Each supported notification type can appear once."];
        if (parsed.Any(rule => rule.AdditionalRecipientUserIds.Count > MaxRecipientsPerRule
            || rule.AdditionalRecipientUserIds.Any(string.IsNullOrWhiteSpace)
            || rule.AdditionalRecipientUserIds.Distinct(StringComparer.Ordinal).Count() != rule.AdditionalRecipientUserIds.Count))
            errors["recipients"] = [$"Choose up to {MaxRecipientsPerRule} distinct active team members per rule."];
        if (parsed.Any(rule => rule.AdditionalDestinations.Count == 0
            || rule.AdditionalDestinations.Distinct().Count() != rule.AdditionalDestinations.Count))
            errors["destinations"] = ["Choose one or more supported destinations for every rule."];
        if (parsed.Any(rule => rule.AdditionalDestinations.Contains(NotificationDestination.InApp)
            && rule.AdditionalRecipientUserIds.Count == 0))
            errors["recipients"] = ["Select at least one active recipient for each in-app routing rule."];
        if (!teamsAvailable && parsed.Any(rule => rule.AdditionalDestinations.Contains(NotificationDestination.MicrosoftTeams)))
            errors["destinations"] = ["Microsoft Teams is Not configured for this deployment."];
        return errors.Count == 0 ? parsed : null;
    }

    private static NotificationRoutingConfigurationResponse ToResponse(NotificationRoutingConfiguration configuration) =>
        new(configuration.Rules.Select(rule => new NotificationRoutingRuleResponse(
            rule.Type.ToString(),
            rule.AdditionalRecipientUserIds,
            rule.AdditionalDestinations.Select(destination => destination.ToString()).ToArray())).ToArray());

    private static bool IsTeamsAvailable(InfrastructureOptions options, IncomingWebhookTeamsProviderOptions? teamsOptions) =>
        ProviderModeSupport.IsLiveProviderMode(options.NotificationProvider, "IncomingWebhook")
        && Uri.TryCreate(teamsOptions?.WebhookUrl, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps;

    private static bool RulesEqual(
        IReadOnlyCollection<NotificationRoutingRule> current,
        IReadOnlyCollection<NotificationRoutingRule> requested)
    {
        if (current.Count != requested.Count) return false;
        var currentByType = current.ToDictionary(rule => rule.Type);
        return requested.All(rule => currentByType.TryGetValue(rule.Type, out var existing)
            && existing.AdditionalRecipientUserIds.Order(StringComparer.Ordinal).SequenceEqual(rule.AdditionalRecipientUserIds.Order(StringComparer.Ordinal), StringComparer.Ordinal)
            && existing.AdditionalDestinations.Order().SequenceEqual(rule.AdditionalDestinations.Order()));
    }
}
