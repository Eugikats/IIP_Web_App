using InsuranceIntelligence.Api.Auth;
using InsuranceIntelligence.Api.Common;
using InsuranceIntelligence.Api.Workflow;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Integrations;
using InsuranceIntelligence.Infrastructure.Configuration;
using System.Security.Cryptography;

namespace InsuranceIntelligence.Api.Integrations;

/// <summary>
/// Generic connection settings surface, the /app/admin/connections screen's backend, generalized
/// from WhatsAppConfigurationEndpoints to every ConnectionType (blueprint §3.4). Secret fields are
/// write-only from the browser: GetStatus never returns them, only whether each is present, and
/// SaveConfiguration always validates against the real provider before persisting anything, the
/// same guarantees WhatsApp's dedicated screen already provides.
/// </summary>
public static class ConnectionEndpoints
{
    public static IResult ListProviders(
        string connectionType,
        IEnumerable<IConnectionValidator> validators,
        HttpContext httpContext,
        InfrastructureOptions options)
    {
        if (DemoAuthorization.Require(httpContext, options, Permission.ConfigureIntegrations) is { } forbidden)
        {
            return forbidden;
        }

        if (!TryParseType(connectionType, out var type))
        {
            return Results.NotFound(new { message = $"Unknown connection type '{connectionType}'." });
        }

        var keys = validators.Where(v => v.ConnectionType == type).Select(v => v.ProviderKey).ToArray();
        return Results.Ok(new { connectionType, availableProviders = keys });
    }

    public static async Task<IResult> GetStatus(
        string connectionType,
        IIntegrationConnectionRepository repository,
        HttpContext httpContext,
        InfrastructureOptions options,
        CancellationToken cancellationToken)
    {
        if (DemoAuthorization.Require(httpContext, options, Permission.ConfigureIntegrations) is { } forbidden)
        {
            return forbidden;
        }

        if (!TryParseType(connectionType, out var type))
        {
            return Results.NotFound(new { message = $"Unknown connection type '{connectionType}'." });
        }

        if (!WorkflowEndpoints.TryResolveScope(httpContext, out var scope, out _, out var validationError))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["tenantScope"] = [validationError] });
        }

        var connection = await repository.GetAsync(scope, type, cancellationToken);
        if (connection is null)
        {
            return Results.Ok(ConnectionStatusResponse.NotConnected(connectionType));
        }

        return Results.Ok(new ConnectionStatusResponse(
            connectionType,
            Connected: connection.IsActive,
            ProviderKey: connection.ProviderKey,
            NonSecretSettings: ConnectionSettingsCodec.DecodeNonSecret(connection),
            ConfiguredByUserId: connection.ConfiguredByUserId,
            ConfiguredAt: connection.ConfiguredAt,
            LastValidatedAt: connection.LastValidatedAt,
            LastValidationStatus: connection.LastValidationStatus,
            LastValidationError: connection.LastValidationError,
            LastValidationDiscovered: string.IsNullOrWhiteSpace(connection.LastValidationDiscoveredJson)
                ? null
                : System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(connection.LastValidationDiscoveredJson)));
    }

    public static async Task<IResult> SaveConfiguration(
        string connectionType,
        SaveConnectionRequest request,
        IIntegrationConnectionRepository repository,
        IConnectionSecretProtector protector,
        IEnumerable<IConnectionValidator> validators,
        ITelegramWebhookRegistrar telegramWebhookRegistrar,
        IAuditRepository auditRepository,
        InfrastructureOptions options,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (DemoAuthorization.Require(httpContext, options, Permission.ConfigureIntegrations) is { } forbidden)
        {
            return forbidden;
        }

        if (!TryParseType(connectionType, out var type))
        {
            return Results.NotFound(new { message = $"Unknown connection type '{connectionType}'." });
        }

        if (string.IsNullOrWhiteSpace(request.ProviderKey))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["providerKey"] = ["A provider must be selected."] });
        }

        if (request.SecretFields is null || request.NonSecretFields is null)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["settings"] = ["Connection settings are required."] });
        }
        var fieldError = ConnectionFieldPolicy.Validate(type, request.SecretFields, request.NonSecretFields);
        if (fieldError is not null)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["settings"] = [fieldError] });
        }

        var validator = validators.FirstOrDefault(v => v.ConnectionType == type
            && string.Equals(v.ProviderKey, request.ProviderKey, StringComparison.OrdinalIgnoreCase));
        if (validator is null)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["providerKey"] = [$"'{request.ProviderKey}' is not a supported provider for {connectionType}."]
            });
        }

        if (!WorkflowEndpoints.TryResolveScope(httpContext, out var scope, out _, out var scopeError))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["tenantScope"] = [scopeError] });
        }

        var userId = DemoAuthorization.GetUserId(httpContext, fallback: string.Empty);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["userId"] = ["Authenticated user context is required to configure a connection."] });
        }

        var auditReason = AuditReasonReader.Read(httpContext);
        if (string.IsNullOrWhiteSpace(auditReason))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["auditReason"] = ["An audit reason is required to change connection settings."] });
        }

        if (type == ConnectionType.Telegram
            && (!Uri.TryCreate(options.ApiPublicBaseUrl, UriKind.Absolute, out var publicApiUri)
                || publicApiUri.Scheme != Uri.UriSchemeHttps))
            return Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "Not configured", detail: "A public HTTPS API URL is required before Telegram can be connected.");

        var storedSecretFields = new Dictionary<string, string>(request.SecretFields, StringComparer.OrdinalIgnoreCase);
        var allFields = new Dictionary<string, string>(storedSecretFields, StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in request.NonSecretFields) allFields[key] = value;
        var webhookSecret = type == ConnectionType.Telegram ? CreateWebhookSecret() : null;
        if (webhookSecret is not null) allFields["webhookSecret"] = webhookSecret;

        // Guide-through validation: confirm the credentials actually work against the real
        // provider before anything is persisted, never store a connection that is silently broken.
        var validation = await validator.ValidateAsync(allFields, cancellationToken);
        if (!validation.Success)
        {
            await auditRepository.WriteAsync(
                AuditEvent.ConnectionChecked(scope, userId, connectionType, validator.ProviderKey, false, GetCorrelationId(httpContext)),
                cancellationToken);
            return Results.BadRequest(new { deliveryStatus = "ValidationFailed", message = validation.SafeError });
        }

        IReadOnlyDictionary<string, string>? discoveredSettings = validation.DiscoveredSettings;
        if ((type is ConnectionType.Instagram or ConnectionType.Messenger or ConnectionType.TikTok or ConnectionType.X)
            && validation.DiscoveredSettings is { } discoveredAccount
            && discoveredAccount.TryGetValue("accountId", out var externalAccountId)
            && !string.IsNullOrWhiteSpace(externalAccountId))
        {
            var existingOwner = await repository.FindByExternalAccountAsync(type, externalAccountId, cancellationToken);
            if (existingOwner is not null && !existingOwner.Scope.Matches(scope))
                return Results.Conflict(new { deliveryStatus = "AccountAlreadyConnected", message = "This provider account is already connected to another workspace." });
        }
        if (type == ConnectionType.Telegram)
        {
            var botToken = storedSecretFields["botToken"];
            var botId = validation.DiscoveredSettings?.GetValueOrDefault("accountId");
            if (string.IsNullOrWhiteSpace(botId) || webhookSecret is null)
                return Results.BadRequest(new { deliveryStatus = "ValidationFailed", message = "Telegram did not confirm a bot account ID." });
            var existingOwner = await repository.FindByExternalAccountAsync(type, botId, cancellationToken);
            if (existingOwner is not null && !existingOwner.Scope.Matches(scope))
                return Results.Conflict(new { deliveryStatus = "AccountAlreadyConnected", message = "This Telegram bot is already connected to another workspace." });
            var callbackUrl = $"{options.ApiPublicBaseUrl!.TrimEnd('/')}/api/v1/integrations/telegram/bots/{Uri.EscapeDataString(botId)}/webhook";
            var registration = await telegramWebhookRegistrar.RegisterAsync(botToken, callbackUrl, webhookSecret, cancellationToken);
            if (!registration.Success)
                return Results.BadRequest(new { deliveryStatus = "WebhookRegistrationFailed", message = registration.SafeError });
            storedSecretFields["webhookSecret"] = webhookSecret;
            var discovered = new Dictionary<string, string>(validation.DiscoveredSettings ?? new Dictionary<string, string>(), StringComparer.Ordinal)
            {
                ["messagingStatus"] = "Human replies",
            };
            discoveredSettings = discovered;
        }

        var (encryptedSecretJson, nonSecretJson) = ConnectionSettingsCodec.Encode(protector, storedSecretFields, request.NonSecretFields);
        var now = DateTimeOffset.UtcNow;
        var connection = new IntegrationConnection
        {
            Scope = scope,
            ConnectionType = type,
            ProviderKey = request.ProviderKey,
            EncryptedSecretSettingsJson = encryptedSecretJson,
            NonSecretSettingsJson = nonSecretJson,
            IsActive = true,
            ConfiguredByUserId = userId,
            ConfiguredAt = now,
            LastValidatedAt = now,
            LastValidationStatus = "Success",
            LastValidationError = null,
            LastValidationDiscoveredJson = ConnectionSettingsCodec.EncodeDiscovered(discoveredSettings),
        };
        await repository.SaveAsync(connection, cancellationToken);

        await auditRepository.WriteAsync(
            AuditEvent.ConnectionSaved(scope, userId, connectionType, connection.ProviderKey, auditReason, GetCorrelationId(httpContext)),
            cancellationToken);

        return Results.Ok(new ConnectionStatusResponse(
            connectionType,
            Connected: true,
            ProviderKey: connection.ProviderKey,
            NonSecretSettings: request.NonSecretFields,
            ConfiguredByUserId: connection.ConfiguredByUserId,
            ConfiguredAt: connection.ConfiguredAt,
            LastValidatedAt: connection.LastValidatedAt,
            LastValidationStatus: connection.LastValidationStatus,
            LastValidationError: connection.LastValidationError,
            LastValidationDiscovered: discoveredSettings));
    }

    public static async Task<IResult> TestConfiguration(
        string connectionType,
        IIntegrationConnectionRepository repository,
        IConnectionSecretProtector protector,
        IEnumerable<IConnectionValidator> validators,
        IAuditRepository auditRepository,
        InfrastructureOptions options,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (DemoAuthorization.Require(httpContext, options, Permission.ConfigureIntegrations) is { } forbidden)
        {
            return forbidden;
        }

        if (!TryParseType(connectionType, out var type))
        {
            return Results.NotFound(new { message = $"Unknown connection type '{connectionType}'." });
        }

        if (!WorkflowEndpoints.TryResolveScope(httpContext, out var scope, out _, out var scopeError))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["tenantScope"] = [scopeError] });
        }

        var connection = await repository.GetAsync(scope, type, cancellationToken);
        if (connection is null)
        {
            return Results.NotFound(new { message = $"No {connectionType} connection is saved for this workspace yet." });
        }

        var validator = validators.FirstOrDefault(v => v.ConnectionType == type
            && string.Equals(v.ProviderKey, connection.ProviderKey, StringComparison.OrdinalIgnoreCase));
        if (validator is null)
        {
            return Results.Ok(new { success = false, message = "The saved provider is no longer supported by this deployment." });
        }

        var settings = ConnectionSettingsCodec.Decode(protector, connection);
        var validation = await validator.ValidateAsync(settings, cancellationToken);

        var updated = connection with
        {
            LastValidatedAt = DateTimeOffset.UtcNow,
            LastValidationStatus = validation.Success ? "Success" : "Failed",
            LastValidationError = validation.Success ? null : validation.SafeError,
            LastValidationDiscoveredJson = ConnectionSettingsCodec.EncodeDiscovered(validation.DiscoveredSettings),
        };
        await repository.SaveAsync(updated, cancellationToken);
        await auditRepository.WriteAsync(
            AuditEvent.ConnectionChecked(scope, DemoAuthorization.GetUserId(httpContext, fallback: string.Empty),
                connectionType, connection.ProviderKey, validation.Success, GetCorrelationId(httpContext)),
            cancellationToken);

        return Results.Ok(new { success = validation.Success, message = validation.SafeError, discovered = validation.DiscoveredSettings });
    }

    public static async Task<IResult> DeleteConfiguration(
        string connectionType,
        IIntegrationConnectionRepository repository,
        IAuditRepository auditRepository,
        InfrastructureOptions options,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (DemoAuthorization.Require(httpContext, options, Permission.ConfigureIntegrations) is { } forbidden)
        {
            return forbidden;
        }

        if (!TryParseType(connectionType, out var type))
        {
            return Results.NotFound(new { message = $"Unknown connection type '{connectionType}'." });
        }

        if (!WorkflowEndpoints.TryResolveScope(httpContext, out var scope, out _, out var scopeError))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["tenantScope"] = [scopeError] });
        }

        var userId = DemoAuthorization.GetUserId(httpContext, fallback: string.Empty);
        var auditReason = AuditReasonReader.Read(httpContext);
        if (string.IsNullOrWhiteSpace(auditReason))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["auditReason"] = [$"An audit reason is required to disconnect {connectionType}."] });
        }

        var existing = await repository.GetAsync(scope, type, cancellationToken);
        await repository.DeleteAsync(scope, type, cancellationToken);

        await auditRepository.WriteAsync(
            AuditEvent.ConnectionRemoved(scope, userId, connectionType, existing?.ProviderKey, auditReason, GetCorrelationId(httpContext)),
            cancellationToken);

        return Results.Ok(new { message = $"{connectionType} connection removed." });
    }

    private static bool TryParseType(string raw, out ConnectionType type)
    {
        type = default;
        return !int.TryParse(raw, out _)
            && Enum.TryParse(raw, ignoreCase: true, out type)
            && Enum.IsDefined(type);
    }

    private static string GetCorrelationId(HttpContext httpContext)
    {
        if (httpContext.Request.Headers.TryGetValue("X-Correlation-ID", out var header) && !string.IsNullOrWhiteSpace(header))
        {
            return header.ToString();
        }

        return httpContext.TraceIdentifier;
    }

    private static string CreateWebhookSecret() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
        .TrimEnd('=')
        .Replace('+', '-')
        .Replace('/', '_');
}

public sealed record SaveConnectionRequest(
    string ProviderKey,
    IReadOnlyDictionary<string, string> SecretFields,
    IReadOnlyDictionary<string, string> NonSecretFields);

internal static class ConnectionFieldPolicy
{
    private static readonly HashSet<string> SensitiveNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "apiKey", "password", "accessToken", "accessTokenSecret", "consumerSecret", "pageAccessToken", "appSecret", "botToken",
        "authHeaderValue", "clientSecret", "refreshToken", "webhookSecret"
    };

    public static string? Validate(
        ConnectionType type,
        IReadOnlyDictionary<string, string> secretFields,
        IReadOnlyDictionary<string, string> nonSecretFields)
    {
        if (nonSecretFields.Keys.Any(SensitiveNames.Contains))
            return "Credentials must be entered as secret fields.";

        var socialFields = type switch
        {
            ConnectionType.Instagram => (Secrets: new[] { "accessToken", "appSecret" }, Public: new[] { "accountId" }),
            ConnectionType.Messenger => (Secrets: new[] { "pageAccessToken", "appSecret" }, Public: new[] { "pageId" }),
            ConnectionType.TikTok => (Secrets: new[] { "accessToken", "appSecret" }, Public: new[] { "businessId" }),
            ConnectionType.X => (Secrets: new[] { "clientSecret", "accessToken" }, Public: new[] { "userId" }),
            ConnectionType.Telegram => (Secrets: new[] { "botToken" }, Public: Array.Empty<string>()),
            _ => (Secrets: Array.Empty<string>(), Public: Array.Empty<string>()),
        };
        if (socialFields.Secrets.Length == 0) return null;

        if (secretFields.Keys.Any(key => !socialFields.Secrets.Contains(key, StringComparer.Ordinal))
            || nonSecretFields.Keys.Any(key => !socialFields.Public.Contains(key, StringComparer.Ordinal)))
            return "This channel received an unsupported setting.";

        if (socialFields.Secrets.Any(key => !secretFields.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
            || socialFields.Public.Any(key => !nonSecretFields.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value)))
            return "Complete every required account and credential field.";

        return null;
    }
}

public sealed record ConnectionStatusResponse(
    string ConnectionType,
    bool Connected,
    string? ProviderKey,
    IReadOnlyDictionary<string, string>? NonSecretSettings,
    string? ConfiguredByUserId,
    DateTimeOffset? ConfiguredAt,
    DateTimeOffset? LastValidatedAt,
    string? LastValidationStatus,
    string? LastValidationError,
    IReadOnlyDictionary<string, string>? LastValidationDiscovered)
{
    public static ConnectionStatusResponse NotConnected(string connectionType) =>
        new(connectionType, false, null, null, null, null, null, null, null, null);
}
