using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Domain.Administration;

public sealed record TenantConfiguration
{
    public required TenantScope Scope { get; init; }
    public required string InsurerName { get; init; }
    public string? LogoUrl { get; init; }
    public required string Country { get; init; }
    public required string Region { get; init; }
    public required IReadOnlyCollection<BusinessLineConfiguration> BusinessLines { get; init; }
    public required IReadOnlyCollection<ChannelConfiguration> Channels { get; init; }
    public required IReadOnlyCollection<ApprovalThresholdConfiguration> ApprovalThresholds { get; init; }
    public required IReadOnlyCollection<ChecklistConfiguration> Checklists { get; init; }
    public required ProviderModeConfiguration ProviderModes { get; init; }
    public required int AuditRetentionDays { get; init; }
    public required DateTimeOffset UpdatedAt { get; init; }

    public IReadOnlyCollection<string> Validate()
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(InsurerName))
        {
            errors.Add("Insurer name is required.");
        }

        if (string.IsNullOrWhiteSpace(Country))
        {
            errors.Add("Country is required.");
        }

        if (string.IsNullOrWhiteSpace(Region))
        {
            errors.Add("Region is required.");
        }

        if (!string.IsNullOrWhiteSpace(LogoUrl)
            && (!Uri.TryCreate(LogoUrl, UriKind.Absolute, out var logoUri)
                || logoUri.Scheme != Uri.UriSchemeHttps))
        {
            errors.Add("Logo URL must be an absolute HTTPS URL.");
        }

        if (AuditRetentionDays < 90)
        {
            errors.Add("Audit retention must be at least 90 days.");
        }

        foreach (var checklist in Checklists)
        {
            errors.AddRange(checklist.Validate().Select(error => $"{checklist.ProductLine}: {error}"));
        }

        return errors;
    }

    public static TenantConfiguration Demo() =>
        new()
        {
            Scope = TenantScope.Create("tenant-demo", "workspace-claims"),
            InsurerName = "Demo Insurance Company",
            Country = "Uganda",
            Region = "East Africa",
            BusinessLines =
            [
                new BusinessLineConfiguration("Motor", true),
                new BusinessLineConfiguration("Medical", true),
                new BusinessLineConfiguration("Life", false),
                new BusinessLineConfiguration("Agriculture", false)
            ],
            Channels =
            [
                new ChannelConfiguration("WebPortal", true),
                new ChannelConfiguration("Email", true),
                new ChannelConfiguration("SMS", true),
                new ChannelConfiguration("WhatsApp", false)
            ],
            ApprovalThresholds =
            [
                new ApprovalThresholdConfiguration("Motor", 5000m, "ClaimsSupervisor")
            ],
            Checklists =
            [
                new ChecklistConfiguration("Motor", ["ClaimForm", "PoliceAbstract", "PhotoEvidence"])
            ],
            ProviderModes = ProviderModeConfiguration.LocalDefaults(),
            AuditRetentionDays = 2555,
            UpdatedAt = DateTimeOffset.UtcNow
        };

    public static TenantConfiguration Unconfigured(TenantScope scope) =>
        new()
        {
            Scope = scope,
            InsurerName = string.Empty,
            LogoUrl = null,
            Country = string.Empty,
            Region = string.Empty,
            BusinessLines = [],
            Channels = [],
            ApprovalThresholds = [],
            Checklists = [],
            ProviderModes = ProviderModeConfiguration.Unconfigured(),
            AuditRetentionDays = 365,
            UpdatedAt = DateTimeOffset.UnixEpoch
        };
}

public sealed record BusinessLineConfiguration(string Name, bool Enabled);

public sealed record ChannelConfiguration(string Name, bool Enabled);

public sealed record ApprovalThresholdConfiguration(string ProductLine, decimal Amount, string RequiredRole);

public sealed record ChecklistConfiguration(string ProductLine, IReadOnlyCollection<string> RequiredDocuments)
{
    public IReadOnlyCollection<string> Validate()
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(ProductLine))
        {
            errors.Add("Product line is required.");
        }

        if (RequiredDocuments.Count == 0 || RequiredDocuments.Any(string.IsNullOrWhiteSpace))
        {
            errors.Add("At least one required document is required.");
        }

        return errors;
    }
}

public sealed record ProviderModeConfiguration(
    string StorageProvider,
    string AuthMode,
    string AiProvider,
    string DocumentProvider,
    string RetrievalProvider,
    string EmailProvider,
    string CommunicationProvider,
    string NotificationProvider,
    bool SeedDemoData)
{
    public static ProviderModeConfiguration Unconfigured() =>
        new(
            "NotConfigured",
            "NotConfigured",
            "NotConfigured",
            "NotConfigured",
            "NotConfigured",
            "NotConfigured",
            "NotConfigured",
            "NotConfigured",
            false);

    public static ProviderModeConfiguration LocalDefaults() =>
        new("InMemory", "Development", "Mock", "Mock", "Mock", "Mock", "Mock", "Mock", false);
}
