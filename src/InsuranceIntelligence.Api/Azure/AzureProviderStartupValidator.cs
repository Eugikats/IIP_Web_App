using InsuranceIntelligence.Infrastructure.Configuration;

namespace InsuranceIntelligence.Api.Azure;

public static class AzureProviderStartupValidator
{
    private static readonly (string SettingName, Func<InfrastructureOptions, string> Selector)[] ProductionProviderModes =
    [
        ("INSURANCEINTELLIGENCE_BLOB_PROVIDER", options => options.BlobProvider),
        ("INSURANCEINTELLIGENCE_DOCUMENT_PROVIDER", options => options.DocumentProvider),
        ("INSURANCEINTELLIGENCE_RETRIEVAL_PROVIDER", options => options.RetrievalProvider),
        ("INSURANCEINTELLIGENCE_AI_PROVIDER", options => options.AiProvider),
        ("INSURANCEINTELLIGENCE_QUEUE_PROVIDER", options => options.QueueProvider),
        ("INSURANCEINTELLIGENCE_EMAIL_PROVIDER", options => options.EmailProvider),
        ("INSURANCEINTELLIGENCE_COMMUNICATION_PROVIDER", options => options.CommunicationProvider),
        ("INSURANCEINTELLIGENCE_NOTIFICATION_PROVIDER", options => options.NotificationProvider)
    ];

    public static IReadOnlyCollection<string> Validate(InfrastructureOptions options)
    {
        var errors = new List<string>();

        if (options.BlobProvider.Equals("AzureBlob", StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrWhiteSpace(options.AzureBlob.Endpoint)
            && string.IsNullOrWhiteSpace(options.AzureBlob.AccountName)
            && string.IsNullOrWhiteSpace(options.AzureBlob.ConnectionString))
        {
            errors.Add("AzureBlob mode requires AzureBlob:Endpoint, AzureBlob:AccountName, or AzureBlob:ConnectionString.");
        }

        if (options.DocumentProvider.Equals("AzureDocumentIntelligence", StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrWhiteSpace(options.DocumentIntelligence.Endpoint))
        {
            errors.Add("AzureDocumentIntelligence mode requires DocumentIntelligence:Endpoint.");
        }

        if (options.RetrievalProvider.Equals("AzureAiSearch", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(options.AzureAiSearch.Endpoint))
            {
                errors.Add("AzureAiSearch mode requires AzureAiSearch:Endpoint.");
            }

            if (string.IsNullOrWhiteSpace(options.AzureAiSearch.IndexName))
            {
                errors.Add("AzureAiSearch mode requires AzureAiSearch:IndexName.");
            }
        }

        if (options.AiProvider.Equals("AzureOpenAI", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(options.AzureOpenAI.Endpoint))
            {
                errors.Add("AzureOpenAI mode requires AzureOpenAI:Endpoint.");
            }

            if (string.IsNullOrWhiteSpace(options.AzureOpenAI.ChatDeployment))
            {
                errors.Add("AzureOpenAI mode requires AzureOpenAI:ChatDeployment.");
            }

            if (string.IsNullOrWhiteSpace(options.AzureOpenAI.EmbeddingDeployment))
            {
                errors.Add("AzureOpenAI mode requires AzureOpenAI:EmbeddingDeployment.");
            }
        }

        if (options.QueueProvider.Equals("AzureServiceBus", StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrWhiteSpace(options.ServiceBus.FullyQualifiedNamespace))
        {
            errors.Add("AzureServiceBus mode requires ServiceBus:FullyQualifiedNamespace.");
        }

        if (options.EmailProvider.Equals("Graph", StringComparison.OrdinalIgnoreCase)
            && !HasConfiguredGraphMailboxAccess(options))
        {
            errors.Add("Graph email mode requires GraphMail tenant, client, credential settings, and GraphMail:MailboxAccessScoped=true after mailbox access has been restricted to approved insurer mailboxes.");
        }

        if (options.CommunicationProvider.Equals("GraphEmail", StringComparison.OrdinalIgnoreCase)
            && (!HasConfiguredGraphMailboxAccess(options) || string.IsNullOrWhiteSpace(options.GraphMail.DefaultMailboxAddress)))
        {
            errors.Add("GraphEmail communication mode requires GraphMail client credential settings, GraphMail:MailboxAccessScoped=true, and GraphMail:DefaultMailboxAddress.");
        }

        if (options.NotificationProvider.Equals("GraphTeams", StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("GraphTeams notification mode is not implemented. Configure IncomingWebhook or leave notifications Disabled.");
        }

        return errors;
    }

    public static IReadOnlyCollection<string> ValidateProduction(InfrastructureOptions options, string environmentName)
    {
        var runtimeProfile = ResolveRuntimeProfile(options, environmentName);
        var isProtected = IsProtectedRuntime(runtimeProfile);
        var isDemo = IsDemoRuntime(runtimeProfile);

        // The Demo profile is a deployed, reviewer-safe sandbox: it runs on the same
        // real providers / real identity / real Azure SQL as Production, and differs
        // only in that it is permitted to seed a clearly-labelled synthetic tenant.
        // Every other guardrail applies exactly as in Production so a demo environment
        // can never fall back to simulation or fabricate a success path.
        if (!isProtected && !isDemo)
        {
            return Array.Empty<string>();
        }

        var errors = new List<string>();

        if (options.AuthMode.Equals("Development", StringComparison.OrdinalIgnoreCase))
        {
            errors.Add($"{runtimeProfile} requires INSURANCEINTELLIGENCE_AUTH_MODE to use an external identity mode such as Entra.");
        }

        if (IsForbiddenProvider(options.StorageProvider, includeSqlite: true))
        {
            errors.Add($"{runtimeProfile} requires Azure SQL durable storage; INSURANCEINTELLIGENCE_STORAGE_PROVIDER cannot be {options.StorageProvider}.");
        }
        else if (options.StorageProvider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrWhiteSpace(options.SqlServerConnectionString))
        {
            errors.Add($"{runtimeProfile} requires Azure SQL durable storage; INSURANCEINTELLIGENCE_SQLSERVER_CONNECTION_STRING or ConnectionStrings:InsuranceIntelligenceSql must be configured.");
        }

        // Synthetic seed data is the entire point of the Demo profile, so it is allowed
        // there (and only there among deployed profiles). Production/Staging/Reviewer
        // remain hard-blocked from ever booting with seeded demo data.
        if (options.SeedDemoData && !isDemo)
        {
            errors.Add($"{runtimeProfile} cannot start with INSURANCEINTELLIGENCE_SEED_DEMO_DATA=true.");
        }

        if (options.AllowProductionMockProviders)
        {
            errors.Add($"{runtimeProfile} cannot start with INSURANCEINTELLIGENCE_ALLOW_PRODUCTION_MOCKS=true.");
        }

        foreach (var (settingName, selector) in ProductionProviderModes)
        {
            var value = selector(options);
            if (IsForbiddenProvider(value, includeSqlite: false))
            {
                errors.Add($"{runtimeProfile} requires a real configured provider for {settingName}; {value} is not allowed.");
            }
        }

        if (RequiresProductionResidencyApproval(runtimeProfile))
        {
            errors.AddRange(ValidateProductionResidency(options, runtimeProfile));
        }

        return errors;
    }

    /// <summary>
    /// Classifies the deployment into one of the review modes surfaced to operators:
    /// <c>Production</c> (live customer data), <c>Demo</c> (deployed synthetic sandbox),
    /// or <c>LocalDevelopment</c> (developer machine / LocalTest). This is what drives
    /// the portal's environment banner so synthetic data can never be mistaken for live.
    /// </summary>
    public static string ResolveReviewMode(InfrastructureOptions options, string environmentName)
    {
        var runtimeProfile = ResolveRuntimeProfile(options, environmentName);

        if (IsProtectedRuntime(runtimeProfile))
        {
            return "Production";
        }

        if (IsDemoRuntime(runtimeProfile))
        {
            return "Demo";
        }

        return "LocalDevelopment";
    }

    /// <summary>
    /// True when the environment renders synthetic, non-customer data. Production is
    /// structurally prevented from seeding, so this is effectively "seed enabled and
    /// not Production" — the signal the portal banner uses to warn reviewers.
    /// </summary>
    public static bool UsesSyntheticData(InfrastructureOptions options, string environmentName) =>
        options.SeedDemoData
        && !ResolveReviewMode(options, environmentName).Equals("Production", StringComparison.Ordinal);

    public static string ResolveRuntimeProfile(InfrastructureOptions options, string environmentName)
    {
        if (!string.IsNullOrWhiteSpace(options.RuntimeProfile))
        {
            return options.RuntimeProfile;
        }

        if (environmentName.Equals("Development", StringComparison.OrdinalIgnoreCase))
        {
            return "Development";
        }

        if (environmentName.Equals("Staging", StringComparison.OrdinalIgnoreCase)
            || environmentName.Equals("Production", StringComparison.OrdinalIgnoreCase)
            || environmentName.Equals("ManagedApplicationReviewer", StringComparison.OrdinalIgnoreCase)
            || environmentName.Equals("Demo", StringComparison.OrdinalIgnoreCase)
            || environmentName.Equals("LocalTest", StringComparison.OrdinalIgnoreCase))
        {
            return environmentName;
        }

        return environmentName.Equals("Test", StringComparison.OrdinalIgnoreCase)
            ? "LocalTest"
            : environmentName;
    }

    /// <summary>
    /// True for runtime profiles where live customer data may be present and synthetic
    /// seeding must never be reachable (Staging/Production/ManagedApplicationReviewer).
    /// Exposed publicly so other guarded surfaces (e.g. demo data seeding) can reuse the
    /// exact same classification instead of re-deriving it.
    /// </summary>
    public static bool IsProtectedRuntime(string environmentName) =>
        environmentName.Equals("Staging", StringComparison.OrdinalIgnoreCase)
        || environmentName.Equals("Production", StringComparison.OrdinalIgnoreCase)
        || environmentName.Equals("ManagedApplicationReviewer", StringComparison.OrdinalIgnoreCase);

    private static bool IsDemoRuntime(string environmentName) =>
        environmentName.Equals("Demo", StringComparison.OrdinalIgnoreCase);

    private static bool RequiresProductionResidencyApproval(string runtimeProfile) =>
        runtimeProfile.Equals("Production", StringComparison.OrdinalIgnoreCase)
        || runtimeProfile.Equals("ManagedApplicationReviewer", StringComparison.OrdinalIgnoreCase);

    public static IReadOnlyCollection<string> ValidateProductionResidency(
        InfrastructureOptions options,
        string runtimeProfile)
    {
        var errors = new List<string>();
        RequireResidencySetting(options.DataResidencyCountry, "INSURANCEINTELLIGENCE_DATA_RESIDENCY_COUNTRY", runtimeProfile, errors);
        RequireResidencySetting(options.DataResidencyGeography, "INSURANCEINTELLIGENCE_DATA_RESIDENCY_GEOGRAPHY", runtimeProfile, errors);
        RequireResidencySetting(options.AzureRegion, "INSURANCEINTELLIGENCE_AZURE_REGION", runtimeProfile, errors);
        RequireResidencySetting(options.DataResidencyAllowedAzureRegions, "INSURANCEINTELLIGENCE_DATA_RESIDENCY_ALLOWED_AZURE_REGIONS", runtimeProfile, errors);
        RequireResidencySetting(options.DataResidencyApprovalReference, "INSURANCEINTELLIGENCE_DATA_RESIDENCY_APPROVAL_REFERENCE", runtimeProfile, errors);

        var expectedOpenAiDeploymentType = ExpectedAzureOpenAiDeploymentType(options.DataResidencyMode);
        if (expectedOpenAiDeploymentType is null)
        {
            errors.Add($"{runtimeProfile} has unsupported INSURANCEINTELLIGENCE_DATA_RESIDENCY_MODE '{options.DataResidencyMode}'.");
        }
        else if (options.AiProvider.Equals("AzureOpenAI", StringComparison.OrdinalIgnoreCase))
        {
            RequireResidencySetting(options.AzureOpenAI.DeploymentType, "AzureOpenAI:DeploymentType", runtimeProfile, errors);

            if (!string.IsNullOrWhiteSpace(options.AzureOpenAI.DeploymentType)
                && !options.AzureOpenAI.DeploymentType.Equals(expectedOpenAiDeploymentType, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add(
                    $"{runtimeProfile} data residency mode '{options.DataResidencyMode}' requires AzureOpenAI:DeploymentType "
                    + $"'{expectedOpenAiDeploymentType}', not '{options.AzureOpenAI.DeploymentType}'.");
            }
        }

        if (!string.IsNullOrWhiteSpace(options.AzureRegion)
            && !string.IsNullOrWhiteSpace(options.DataResidencyAllowedAzureRegions)
            && !AllowedAzureRegions(options).Contains(options.AzureRegion.Trim(), StringComparer.OrdinalIgnoreCase))
        {
            errors.Add($"{runtimeProfile} Azure region '{options.AzureRegion}' is not included in INSURANCEINTELLIGENCE_DATA_RESIDENCY_ALLOWED_AZURE_REGIONS.");
        }

        return errors;
    }

    public static string? ExpectedAzureOpenAiDeploymentType(string mode) =>
        mode.Equals("regional", StringComparison.OrdinalIgnoreCase) ? "Standard"
        : mode.Equals("data_zone", StringComparison.OrdinalIgnoreCase) ? "DataZoneStandard"
        : mode.Equals("global", StringComparison.OrdinalIgnoreCase) ? "GlobalStandard"
        : null;

    public static IReadOnlyCollection<string> AllowedAzureRegions(InfrastructureOptions options) =>
        (options.DataResidencyAllowedAzureRegions ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static void RequireResidencySetting(
        string? value,
        string settingName,
        string runtimeProfile,
        ICollection<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add($"{runtimeProfile} requires {settingName} before customer data processing is enabled.");
        }
    }

    private static bool IsForbiddenProvider(string value, bool includeSqlite) =>
        value.Equals("Mock", StringComparison.OrdinalIgnoreCase)
        || value.Equals("Simulated", StringComparison.OrdinalIgnoreCase)
        || value.Equals("Placeholder", StringComparison.OrdinalIgnoreCase)
        || value.Equals("InMemory", StringComparison.OrdinalIgnoreCase)
        || value.Equals("DevelopmentAuth", StringComparison.OrdinalIgnoreCase)
        || value.Equals("Demo", StringComparison.OrdinalIgnoreCase)
        || value.Equals("SeededDemoBusinessData", StringComparison.OrdinalIgnoreCase)
        || value.Equals("FallbackToMock", StringComparison.OrdinalIgnoreCase)
        || (includeSqlite && (value.Equals("SQLite", StringComparison.OrdinalIgnoreCase)
            || value.Equals("EfSqlite", StringComparison.OrdinalIgnoreCase)));

    private static bool HasConfiguredGraphMailboxAccess(InfrastructureOptions options) =>
        HasGraphClientCredential(options)
        && options.GraphMail.MailboxAccessScoped;

    private static bool HasGraphClientCredential(InfrastructureOptions options) =>
        !options.GraphMail.UseManagedIdentity
        && !string.IsNullOrWhiteSpace(options.GraphMail.TenantId)
        && !string.IsNullOrWhiteSpace(options.GraphMail.ClientId)
        && !string.IsNullOrWhiteSpace(options.GraphMail.ClientSecret);

    public static void ThrowIfInvalid(InfrastructureOptions options)
    {
        var errors = Validate(options);
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                "Live Azure provider mode is selected but required configuration is missing. "
                + string.Join(' ', errors)
                + " Mock provider modes do not require Azure credentials.");
        }
    }

    public static void ThrowIfProductionInvalid(InfrastructureOptions options, string environmentName)
    {
        var errors = ValidateProduction(options, environmentName);
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                "Production startup configuration is unsafe. "
                + string.Join(' ', errors)
                + " Test doubles are allowed only in LocalTest or isolated Development execution.");
        }
    }
}
