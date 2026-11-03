using InsuranceIntelligence.Domain.Common;
using InsuranceIntelligence.Domain.Fraud;
using InsuranceIntelligence.Domain.Notifications;

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
    // Outcome K, layer 2: below this, a document's classification or extraction needs a person
    // to decide rather than being treated as reliable. A tenant setting, not a fixed literal
    // (AGENTS.md non-negotiable 11), defaulting to the platform's long-standing 0.8.
    public decimal DocumentClassificationConfidenceThreshold { get; init; } = 0.8m;
    public FraudRuleOptions FraudRules { get; init; } = FraudRuleOptions.Default;
    /// <summary>Enabled non-English customer assistant languages, stored as normalized language
    /// codes. Null preserves the prior deployment-level setting during configuration migration;
    /// an empty collection explicitly means English only.</summary>
    public IReadOnlyCollection<string>? AssistantLanguages { get; init; }
    /// <summary>Outbound customer messaging defaults to human review. Autopilot is only effective
    /// when the deployment has also enabled the WhatsApp assistant capability.</summary>
    public CustomerMessagingMode CustomerMessagingMode { get; init; } = CustomerMessagingMode.Review;
    public NotificationRoutingConfiguration NotificationRouting { get; init; } = NotificationRoutingConfiguration.Empty;
    public required DateTimeOffset UpdatedAt { get; init; }

    public IReadOnlyCollection<string> Validate()
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(InsurerName))
        {
            errors.Add("Insurer name is required.");
        }

        if (DocumentClassificationConfidenceThreshold is < 0m or > 1m)
        {
            errors.Add("Document classification confidence threshold must be between 0 and 1.");
        }

        errors.AddRange(FraudRules.Validate().Select(error => $"Fraud rules: {error}"));

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

public enum NotificationDestination
{
    InApp,
    MicrosoftTeams,
}

/// <summary>Optional, tenant-scoped routing overrides for real staff notifications. When no
/// rule exists, existing in-app delivery to the event's assigned recipient is preserved.</summary>
public sealed record NotificationRoutingConfiguration(IReadOnlyCollection<NotificationRoutingRule> Rules)
{
    public static readonly NotificationRoutingConfiguration Empty = new([]);
}

public sealed record NotificationRoutingRule(
    NotificationType Type,
    IReadOnlyCollection<string> AdditionalRecipientUserIds,
    IReadOnlyCollection<NotificationDestination> AdditionalDestinations);

public sealed record BusinessLineConfiguration(string Name, bool Enabled);

public sealed record ChannelConfiguration(string Name, bool Enabled);

public enum CustomerMessagingMode
{
    Review,
    Autopilot,
}

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
