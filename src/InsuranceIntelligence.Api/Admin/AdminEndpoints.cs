using InsuranceIntelligence.Api.Auth;
using InsuranceIntelligence.Api.Common;
using InsuranceIntelligence.Domain.Administration;
using InsuranceIntelligence.Domain.Audit;
using InsuranceIntelligence.Domain.Cases;
using InsuranceIntelligence.Infrastructure.Configuration;

namespace InsuranceIntelligence.Api.Admin;

public static class AdminEndpoints
{
    private const string UpdateUserRoleAction = "updateUserRole";

    public static async Task<IResult> GetTenantConfiguration(
        ITenantConfigurationRepository repository,
        InfrastructureOptions options,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (DemoAuthorization.Require(httpContext, options, Permission.ManageTenantSettings) is { } forbidden)
        {
            return forbidden;
        }

        if (!TenantScopeReader.TryGetFromHeadersOrQuery(httpContext, out var scope, out var validationError))
        {
            return Validation("tenantScope", validationError);
        }

        return Results.Ok(await repository.GetAsync(scope, cancellationToken));
    }

    public static async Task<IResult> UpdateTenantConfiguration(
        UpdateTenantConfigurationRequest request,
        ITenantConfigurationRepository repository,
        IAuditRepository auditRepository,
        InfrastructureOptions options,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (DemoAuthorization.Require(httpContext, options, Permission.ManageTenantSettings) is { } forbidden)
        {
            return forbidden;
        }

        if (!TenantScopeReader.TryGetFromHeadersOrQuery(httpContext, out var scope, out var validationError))
        {
            return Validation("tenantScope", validationError);
        }

        var current = await repository.GetAsync(scope, cancellationToken);
        var updated = current with
        {
            InsurerName = request.InsurerName,
            LogoUrl = request.LogoUrl,
            Country = request.Country,
            Region = request.Region,
            BusinessLines = request.BusinessLines,
            Channels = request.Channels,
            ApprovalThresholds = request.ApprovalThresholds,
            Checklists = request.Checklists,
            AuditRetentionDays = request.AuditRetentionDays,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        var errors = updated.Validate();
        if (errors.Count > 0)
        {
            return Validation("tenantConfiguration", string.Join(" ", errors));
        }

        var changedFields = ChangedTenantConfigurationFields(current, updated);
        if (changedFields.Count == 0)
        {
            return Results.Conflict(new { message = "The submitted tenant configuration does not change the current configuration." });
        }

        var actor = DemoAuthorization.GetUserId(httpContext, fallback: string.Empty);
        if (string.IsNullOrWhiteSpace(actor))
        {
            return Validation("userId", "Authenticated user context is required to update tenant configuration.");
        }

        var auditReason = AuditReasonReader.Read(httpContext);
        if (string.IsNullOrWhiteSpace(auditReason))
        {
            return Validation("auditReason", "An audit reason is required to update tenant configuration.");
        }

        await repository.SaveAsync(updated, cancellationToken);
        await auditRepository.WriteAsync(
            new AuditEvent(
                Guid.NewGuid(),
                scope,
                CaseId: null,
                DocumentId: null,
                UserId: actor,
                ActorType.User,
                ActionName: "admin.tenant_configuration.updated",
                DateTimeOffset.UtcNow,
                BeforeStatus: IsTenantConfigured(current) ? "Configured" : "NotConfigured",
                AfterStatus: "Configured",
                EvidenceIds: changedFields,
                ModelProvider: "Admin",
                Confidence: null,
                ValidationResult: "accepted",
                ApprovalId: null,
                AuditReason: auditReason,
                CorrelationId: httpContext.TraceIdentifier),
            cancellationToken);
        return Results.Ok(updated);
    }

    public static IResult GetProviderModes(InfrastructureOptions options, HttpContext httpContext)
    {
        if (DemoAuthorization.Require(httpContext, options, Permission.ManageTenantSettings) is { } forbidden)
        {
            return forbidden;
        }

        return Results.Ok(new ProviderModeConfiguration(
            options.StorageProvider,
            options.AuthMode,
            options.AiProvider,
            options.DocumentProvider,
            options.RetrievalProvider,
            options.EmailProvider,
            options.CommunicationProvider,
            options.NotificationProvider,
            options.SeedDemoData));
    }

    public static IResult GetProviderHealth(InfrastructureOptions options, HttpContext httpContext)
    {
        if (DemoAuthorization.Require(httpContext, options, Permission.ManageTenantSettings) is { } forbidden)
        {
            return forbidden;
        }

        return Results.Ok(BuildProviderHealth(options));
    }

    public static ProviderHealthResponse BuildProviderHealth(InfrastructureOptions options)
    {
        var items = new List<ProviderHealthItem>
        {
            Health("database", options.StorageProvider, DatabaseStatus(options), DatabaseDetail(options)),
            Health("blobStorage", options.BlobProvider, BlobStatus(options), BlobDetail(options)),
            Health("documentIntelligence", options.DocumentProvider, RequiredStatus(options, options.DocumentProvider, "AzureDocumentIntelligence", options.DocumentIntelligence.Endpoint), RequiredDetail(options, options.DocumentProvider, "AzureDocumentIntelligence", "DocumentIntelligence:Endpoint")),
            Health("aiSearch", options.RetrievalProvider, RequiredStatus(options, options.RetrievalProvider, "AzureAiSearch", options.AzureAiSearch.Endpoint, options.AzureAiSearch.IndexName), RequiredDetail(options, options.RetrievalProvider, "AzureAiSearch", "AzureAiSearch:Endpoint and AzureAiSearch:IndexName")),
            Health("azureOpenAI", options.AiProvider, RequiredStatus(options, options.AiProvider, "AzureOpenAI", options.AzureOpenAI.Endpoint, options.AzureOpenAI.ChatDeployment, options.AzureOpenAI.EmbeddingDeployment), RequiredDetail(options, options.AiProvider, "AzureOpenAI", "AzureOpenAI:Endpoint, AzureOpenAI:ChatDeployment, and AzureOpenAI:EmbeddingDeployment")),
            Health("serviceBus", options.QueueProvider, RequiredStatus(options, options.QueueProvider, "AzureServiceBus", options.ServiceBus.FullyQualifiedNamespace), RequiredDetail(options, options.QueueProvider, "AzureServiceBus", "ServiceBus:FullyQualifiedNamespace")),
            Health("graphMail", options.EmailProvider, GraphStatus(options), GraphDetail(options)),
            Health("communications", options.CommunicationProvider, CommunicationStatus(options), CommunicationDetail(options)),
            Health("notifications", options.NotificationProvider, NotificationStatus(options), NotificationDetail(options))
        };

        if (options.AuthMode.Equals("Development", StringComparison.OrdinalIgnoreCase))
        {
            items.Add(Health("mockProviders", "Mock", "healthy", "Mock providers remain available for local development."));
        }

        var overall = items.Any(item => item.Status == "missing") ? "degraded" : "healthy";
        return new ProviderHealthResponse(overall, items);
    }

    public static IResult GetRoles(InfrastructureOptions options, HttpContext httpContext)
    {
        if (DemoAuthorization.Require(httpContext, options, Permission.ManageTenantSettings) is { } forbidden)
        {
            return forbidden;
        }

        var roles = Enum.GetValues<PlatformRole>()
            .Select(role => new AdminRoleResponse(
                role.ToString(),
                RolePermissionMap.ForRole(role).Select(permission => permission.ToString()).ToArray(),
                ApprovalLimit: null,
                CaseVisibilityScope: role is PlatformRole.Admin or PlatformRole.ClaimsSupervisor or PlatformRole.Compliance ? "all" : "assigned"))
            .ToArray();
        return Results.Ok(roles);
    }

    public static async Task<IResult> GetUsers(
        IUserAccountRepository repository,
        InfrastructureOptions options,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (DemoAuthorization.Require(httpContext, options, Permission.ManageTenantSettings) is { } forbidden)
        {
            return forbidden;
        }

        if (!TenantScopeReader.TryGetFromHeadersOrQuery(httpContext, out var scope, out var validationError))
        {
            return Validation("tenantScope", validationError);
        }

        var users = await repository.ListAsync(scope, cancellationToken);
        return Results.Ok(MapUsers(users));
    }

    public static async Task<IResult> CreateUser(
        CreateUserRequest request,
        IUserAccountRepository repository,
        InfrastructureOptions options,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (DemoAuthorization.Require(httpContext, options, Permission.ManageTenantSettings) is { } forbidden)
        {
            return forbidden;
        }

        if (!TenantScopeReader.TryGetFromHeadersOrQuery(httpContext, out var scope, out var validationError))
        {
            return Validation("tenantScope", validationError);
        }

        if (!Enum.TryParse<PlatformRole>(request.Role, true, out var role))
        {
            return Validation("role", "A supported role is required.");
        }

        var user = new InsuranceUserAccount
        {
            UserId = request.UserId,
            Scope = scope,
            Name = request.Name,
            Email = request.Email,
            Phone = request.Phone,
            Role = role,
            Department = request.Department,
            Branch = request.Branch
        };
        await repository.SaveAsync(user, cancellationToken);
        var users = await repository.ListAsync(scope, cancellationToken);
        return Results.Created($"/api/v1/admin/users/{user.UserId}", MapUser(user, ActiveAdminCount(users)));
    }

    public static async Task<IResult> UpdateUserRole(
        string userId,
        UpdateUserRoleRequest request,
        IUserAccountRepository repository,
        IAuditRepository auditRepository,
        InfrastructureOptions options,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (DemoAuthorization.Require(httpContext, options, Permission.ManageTenantSettings) is { } forbidden)
        {
            return forbidden;
        }

        if (!TenantScopeReader.TryGetFromHeadersOrQuery(httpContext, out var scope, out var validationError))
        {
            return Validation("tenantScope", validationError);
        }

        var user = await repository.GetAsync(userId, scope, cancellationToken);
        if (user is null)
        {
            return Results.NotFound(new { message = "User was not found for the requested tenant workspace." });
        }

        if (!Enum.TryParse<PlatformRole>(request.Role, true, out var role))
        {
            return Validation("role", "A supported role is required.");
        }

        if (user.Role == role)
        {
            return Results.Conflict(new { message = "The user already has the requested role.", blockedAction = UpdateUserRoleAction });
        }

        var users = await repository.ListAsync(scope, cancellationToken);
        if (IsLastActiveAdmin(user, users) && role != PlatformRole.Admin)
        {
            return Results.Conflict(new
            {
                message = "The last active tenant administrator cannot be demoted. Assign another active administrator first.",
                blockedAction = UpdateUserRoleAction
            });
        }

        var actor = DemoAuthorization.GetUserId(httpContext, fallback: string.Empty);
        if (string.IsNullOrWhiteSpace(actor))
        {
            return Validation("userId", "Authenticated user context is required to change a tenant role.");
        }

        var auditReason = AuditReasonReader.Read(httpContext);
        if (string.IsNullOrWhiteSpace(auditReason))
        {
            return Validation("auditReason", "An audit reason is required to change a tenant role.");
        }

        var updated = user with { Role = role };
        await repository.SaveAsync(updated, cancellationToken);
        await auditRepository.WriteAsync(
            new AuditEvent(
                Guid.NewGuid(),
                scope,
                CaseId: null,
                DocumentId: updated.UserId,
                UserId: actor,
                ActorType.User,
                ActionName: "admin.user_role.updated",
                DateTimeOffset.UtcNow,
                BeforeStatus: user.Role.ToString(),
                AfterStatus: updated.Role.ToString(),
                EvidenceIds: [updated.UserId],
                ModelProvider: "Admin",
                Confidence: null,
                ValidationResult: "accepted",
                ApprovalId: null,
                AuditReason: auditReason,
                CorrelationId: httpContext.TraceIdentifier),
            cancellationToken);
        var updatedUsers = users.Select(item => item.UserId == updated.UserId ? updated : item).ToArray();
        return Results.Ok(MapUser(updated, ActiveAdminCount(updatedUsers)));
    }

    private static IReadOnlyCollection<AdminUserResponse> MapUsers(IReadOnlyCollection<InsuranceUserAccount> users)
    {
        var activeAdminCount = ActiveAdminCount(users);
        return users.Select(user => MapUser(user, activeAdminCount)).ToArray();
    }

    private static AdminUserResponse MapUser(InsuranceUserAccount user, int activeAdminCount)
    {
        var blockedReason = user.Role == PlatformRole.Admin
                            && user.Status.Equals("Active", StringComparison.OrdinalIgnoreCase)
                            && activeAdminCount <= 1
            ? "The last active tenant administrator cannot be demoted. Assign another active administrator first."
            : null;
        var action = new ActionStateResponse(
            UpdateUserRoleAction,
            "Change user role",
            Permission.ManageTenantSettings.ToString(),
            blockedReason);

        return new AdminUserResponse(
            user.UserId,
            user.Name,
            user.Email,
            user.Phone,
            user.Role.ToString(),
            user.Department,
            user.Branch,
            user.Status,
            user.LastLogin,
            blockedReason is null ? [action] : Array.Empty<ActionStateResponse>(),
            blockedReason is null ? Array.Empty<ActionStateResponse>() : [action]);
    }

    private static int ActiveAdminCount(IEnumerable<InsuranceUserAccount> users) =>
        users.Count(user => user.Role == PlatformRole.Admin && user.Status.Equals("Active", StringComparison.OrdinalIgnoreCase));

    private static bool IsLastActiveAdmin(InsuranceUserAccount user, IEnumerable<InsuranceUserAccount> users) =>
        user.Role == PlatformRole.Admin
        && user.Status.Equals("Active", StringComparison.OrdinalIgnoreCase)
        && ActiveAdminCount(users) <= 1;

    private static IReadOnlyCollection<string> ChangedTenantConfigurationFields(
        TenantConfiguration current,
        TenantConfiguration updated)
    {
        var changedFields = new List<string>();
        AddChanged(changedFields, "insurerProfile",
            !string.Equals(current.InsurerName, updated.InsurerName, StringComparison.Ordinal)
            || !string.Equals(current.LogoUrl, updated.LogoUrl, StringComparison.Ordinal)
            || !string.Equals(current.Country, updated.Country, StringComparison.Ordinal)
            || !string.Equals(current.Region, updated.Region, StringComparison.Ordinal));
        AddChanged(changedFields, "businessLines", !current.BusinessLines.SequenceEqual(updated.BusinessLines));
        AddChanged(changedFields, "channels", !current.Channels.SequenceEqual(updated.Channels));
        AddChanged(changedFields, "approvalThresholds", !current.ApprovalThresholds.SequenceEqual(updated.ApprovalThresholds));
        AddChanged(changedFields, "documentChecklists", !ChecklistsEqual(current.Checklists, updated.Checklists));
        AddChanged(changedFields, "auditRetention", current.AuditRetentionDays != updated.AuditRetentionDays);
        return changedFields;
    }

    private static bool IsTenantConfigured(TenantConfiguration configuration) =>
        !string.IsNullOrWhiteSpace(configuration.InsurerName)
        && !string.IsNullOrWhiteSpace(configuration.Country)
        && !string.IsNullOrWhiteSpace(configuration.Region);

    private static void AddChanged(ICollection<string> changedFields, string field, bool changed)
    {
        if (changed)
        {
            changedFields.Add(field);
        }
    }

    private static bool ChecklistsEqual(
        IReadOnlyCollection<ChecklistConfiguration> current,
        IReadOnlyCollection<ChecklistConfiguration> updated) =>
        current.Count == updated.Count
        && current.Zip(updated).All(pair =>
            string.Equals(pair.First.ProductLine, pair.Second.ProductLine, StringComparison.Ordinal)
            && pair.First.RequiredDocuments.SequenceEqual(pair.Second.RequiredDocuments, StringComparer.Ordinal));

    private static IResult Validation(string key, string message) =>
        Results.ValidationProblem(new Dictionary<string, string[]> { [key] = [message] });

    private static ProviderHealthItem Health(string name, string mode, string status, string detail) =>
        new(name, mode, status, detail);

    private static string GraphStatus(InfrastructureOptions options)
    {
        if (!options.EmailProvider.Equals("Graph", StringComparison.OrdinalIgnoreCase))
        {
            return IsDevelopmentAuth(options) ? "healthy" : "missing";
        }

        return HasConfiguredGraphMailboxAccess(options) ? "configured" : "missing";
    }

    private static string GraphDetail(InfrastructureOptions options)
    {
        if (!options.EmailProvider.Equals("Graph", StringComparison.OrdinalIgnoreCase))
        {
            return IsDevelopmentAuth(options)
                ? "Mock email ingestion is available only for local development."
                : "Graph email must be configured for deployed runtime modes.";
        }

        return HasConfiguredGraphMailboxAccess(options)
            ? "Graph mail provider is configured for app-only access with mailbox scoping acknowledged."
            : "Graph mail mode requires GraphMail tenant, client, credential, GraphMail:MailboxAccessScoped=true, and tenant mailbox configuration.";
    }

    private static string CommunicationStatus(InfrastructureOptions options)
    {
        if (options.CommunicationProvider.Equals("Mock", StringComparison.OrdinalIgnoreCase))
        {
            return IsDevelopmentAuth(options) ? "healthy" : "missing";
        }

        if (options.CommunicationProvider.Equals("GraphEmail", StringComparison.OrdinalIgnoreCase))
        {
            return HasConfiguredGraphMailboxAccess(options) && !string.IsNullOrWhiteSpace(options.GraphMail.DefaultMailboxAddress)
                ? "configured"
                : "missing";
        }

        return "missing";
    }

    private static string CommunicationDetail(InfrastructureOptions options)
    {
        if (options.CommunicationProvider.Equals("Mock", StringComparison.OrdinalIgnoreCase))
        {
            return IsDevelopmentAuth(options)
                ? "Mock communications are available only for local development."
                : "A real communication provider must be configured or the feature must be unavailable.";
        }

        if (options.CommunicationProvider.Equals("GraphEmail", StringComparison.OrdinalIgnoreCase))
        {
            return CommunicationStatus(options) == "configured"
                ? "GraphEmail send mode is configured. Only approved or low-risk customer updates may be sent."
                : "GraphEmail mode requires GraphMail client settings, mailbox access scoping, and a default sender mailbox.";
        }

        return "Customer communication sending is not configured. Configure GraphEmail with scoped mailbox access before enabling customer sends.";
    }

    private static string NotificationStatus(InfrastructureOptions options) =>
        options.NotificationProvider.Equals("Mock", StringComparison.OrdinalIgnoreCase) && !IsDevelopmentAuth(options)
            ? "missing"
            : options.NotificationProvider.Equals("Mock", StringComparison.OrdinalIgnoreCase)
                ? "healthy"
                : "missing";

    private static string NotificationDetail(InfrastructureOptions options) =>
        options.NotificationProvider.Equals("Mock", StringComparison.OrdinalIgnoreCase)
            ? IsDevelopmentAuth(options)
                ? "Mock notifications are available only for local development."
                : "A real notification provider must be configured or notification features must be unavailable."
            : "Notification provider is not configured.";

    private static bool HasGraphClientCredential(InfrastructureOptions options) =>
        !options.GraphMail.UseManagedIdentity
        && !string.IsNullOrWhiteSpace(options.GraphMail.TenantId)
        && !string.IsNullOrWhiteSpace(options.GraphMail.ClientId)
        && !string.IsNullOrWhiteSpace(options.GraphMail.ClientSecret);

    private static bool HasConfiguredGraphMailboxAccess(InfrastructureOptions options) =>
        HasGraphClientCredential(options)
        && options.GraphMail.MailboxAccessScoped;

    private static string DatabaseStatus(InfrastructureOptions options)
    {
        if (!options.StorageProvider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
        {
            return IsDevelopmentAuth(options) ? "configured" : "missing";
        }

        return string.IsNullOrWhiteSpace(options.SqlServerConnectionString) ? "missing" : "configured";
    }

    private static string DatabaseDetail(InfrastructureOptions options) =>
        options.StorageProvider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase)
            ? "SQL Server mode selected. Connection string must come from secure configuration or Key Vault reference."
            : IsDevelopmentAuth(options)
                ? $"{options.StorageProvider} mode selected for local development."
                : "Azure SQL durable storage must be configured for deployed runtime modes.";

    private static string BlobStatus(InfrastructureOptions options)
    {
        if (!options.BlobProvider.Equals("AzureBlob", StringComparison.OrdinalIgnoreCase))
        {
            return IsDevelopmentAuth(options) ? "healthy" : "missing";
        }

        return string.IsNullOrWhiteSpace(options.AzureBlob.Endpoint)
               && string.IsNullOrWhiteSpace(options.AzureBlob.AccountName)
               && string.IsNullOrWhiteSpace(options.AzureBlob.ConnectionString)
            ? "missing"
            : "configured";
    }

    private static string BlobDetail(InfrastructureOptions options) =>
        options.BlobProvider.Equals("AzureBlob", StringComparison.OrdinalIgnoreCase)
            ? "Azure Blob mode selected. Storage endpoint/account must be configured; sensitive values are not returned."
            : IsDevelopmentAuth(options)
                ? "Mock/local blob storage is active only for local development."
                : "Azure Blob storage must be configured for deployed runtime modes.";

    private static string RequiredStatus(InfrastructureOptions options, string selectedMode, string liveMode, params string?[] required)
    {
        if (!selectedMode.Equals(liveMode, StringComparison.OrdinalIgnoreCase))
        {
            return IsDevelopmentAuth(options) && selectedMode.Equals("Mock", StringComparison.OrdinalIgnoreCase) ? "healthy" : "missing";
        }

        return required.Any(string.IsNullOrWhiteSpace) ? "missing" : "configured";
    }

    private static string RequiredDetail(InfrastructureOptions options, string selectedMode, string liveMode, string settingNames) =>
        selectedMode.Equals(liveMode, StringComparison.OrdinalIgnoreCase)
            ? $"Live provider mode selected. Required settings: {settingNames}. Sensitive values are never returned."
            : IsDevelopmentAuth(options)
                ? "Mock provider mode active for local development."
                : $"{liveMode} must be configured for deployed runtime modes.";

    private static bool IsDevelopmentAuth(InfrastructureOptions options) =>
        options.AuthMode.Equals("Development", StringComparison.OrdinalIgnoreCase);
}
