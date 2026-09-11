using InsuranceIntelligence.Api.Auth;
using InsuranceIntelligence.Api.Common;
using InsuranceIntelligence.Api.Workflow;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Domain.Integrations;
using InsuranceIntelligence.Infrastructure.Configuration;

namespace InsuranceIntelligence.Api.Integrations;

/// <summary>
/// Generic connection settings surface — the /app/admin/connections screen's backend, generalized
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

        var allFields = new Dictionary<string, string>(request.SecretFields, StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in request.NonSecretFields) allFields[key] = value;

        // Guide-through validation: confirm the credentials actually work against the real
        // provider before anything is persisted — never store a connection that is silently broken.
        var validation = await validator.ValidateAsync(allFields, cancellationToken);
        if (!validation.Success)
        {
            return Results.BadRequest(new { deliveryStatus = "ValidationFailed", message = validation.SafeError });
        }

        var (encryptedSecretJson, nonSecretJson) = ConnectionSettingsCodec.Encode(protector, request.SecretFields, request.NonSecretFields);
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
            LastValidationDiscoveredJson = ConnectionSettingsCodec.EncodeDiscovered(validation.DiscoveredSettings),
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
            LastValidationDiscovered: validation.DiscoveredSettings));
    }

    public static async Task<IResult> TestConfiguration(
        string connectionType,
        IIntegrationConnectionRepository repository,
        IConnectionSecretProtector protector,
        IEnumerable<IConnectionValidator> validators,
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

    private static bool TryParseType(string raw, out ConnectionType type) =>
        Enum.TryParse(raw, ignoreCase: true, out type);

    private static string GetCorrelationId(HttpContext httpContext)
    {
        if (httpContext.Request.Headers.TryGetValue("X-Correlation-ID", out var header) && !string.IsNullOrWhiteSpace(header))
        {
            return header.ToString();
        }

        return httpContext.TraceIdentifier;
    }
}

public sealed record SaveConnectionRequest(
    string ProviderKey,
    IReadOnlyDictionary<string, string> SecretFields,
    IReadOnlyDictionary<string, string> NonSecretFields);

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
