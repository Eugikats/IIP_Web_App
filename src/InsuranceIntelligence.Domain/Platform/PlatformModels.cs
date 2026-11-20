using InsuranceIntelligence.Domain.Common;

namespace InsuranceIntelligence.Domain.Platform;

// The organisation platform: department workspaces built from templates, governed automations, the Intelligence Inbox
// and intents. The shape follows the CIP platform layer so both products read the same; storage follows IIP (tenant and
// IIP workspace scoped JSON rows behind ports). A "workspace" in this namespace is a department workspace. It lives
// inside an IIP tenant workspace (the data boundary in TenantScope) and never replaces it.

public interface IPlatformRecord
{
    Guid Id { get; }
}

/// <summary>A department workspace in the organisation, built from a template (Claims, Renewals, Bancassurance...).</summary>
public sealed class OrgWorkspace : IPlatformRecord
{
    public Guid Id { get; set; }
    public string TemplateKey { get; set; } = "";
    public string Name { get; set; } = "";
    public string Department { get; set; } = "";
    public string? Purpose { get; set; }
    public string Status { get; set; } = OrgWorkspaceStatuses.AwaitingData;
    public string? Route { get; set; }
    public bool IsPrimary { get; set; }
    public List<string> Kpis { get; set; } = [];
    public List<string> Connectors { get; set; } = [];
    public List<string> Members { get; set; } = [];
    public string? OwnerUserId { get; set; }
    public string? CreatedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public static class OrgWorkspaceStatuses
{
    public const string Live = "live";
    public const string AwaitingData = "awaiting_data";
    public const string Draft = "draft";
    public const string Archived = "archived";
}

/// <summary>An automation a workspace runs on a schedule, governed by test and approval.</summary>
public sealed class PlatformAutomation : IPlatformRecord
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public string TemplateKey { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Objective { get; set; }
    public string TriggerJson { get; set; } = "{}";
    public string LogicJson { get; set; } = "{}";
    public List<string> Outputs { get; set; } = [];
    public List<string> Recipients { get; set; } = [];
    public List<string> Approvers { get; set; } = [];
    public string PolicyJson { get; set; } = "{}";
    public string Status { get; set; } = PlatformAutomationStatuses.Draft;
    public int Version { get; set; } = 1;
    public int? LastTestedVersion { get; set; }
    public string? ApprovedByUserId { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
    public DateTimeOffset? NextRunAt { get; set; }
    public DateTimeOffset? LastRunAt { get; set; }
    public string? LastRunStatus { get; set; }
    public string? LastRunMessage { get; set; }
    public string? CreatedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public static class PlatformAutomationStatuses
{
    public const string Draft = "draft";
    public const string Tested = "tested";
    public const string AwaitingApproval = "awaiting_approval";
    public const string Approved = "approved";
    public const string Active = "active";
    public const string Paused = "paused";
    public const string Failed = "failed";
    public const string Retired = "retired";
}

/// <summary>One test or run of an automation, with the evidence it used.</summary>
public sealed class PlatformAutomationRun : IPlatformRecord
{
    public Guid Id { get; set; }
    public Guid AutomationId { get; set; }
    public int AutomationVersion { get; set; }
    public string Kind { get; set; } = "run";
    public string Status { get; set; } = "succeeded";
    public string? Summary { get; set; }
    public int Findings { get; set; }
    public int NewFindings { get; set; }
    public string EvidenceJson { get; set; } = "[]";
    public string TriggeredBy { get; set; } = "system";
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
}

/// <summary>An item in the Intelligence Inbox: a signal that needs attention, a recommendation or something being monitored.</summary>
public sealed class PlatformInboxItem : IPlatformRecord
{
    public Guid Id { get; set; }
    public Guid? WorkspaceId { get; set; }
    public Guid? AutomationId { get; set; }
    public Guid? IntentId { get; set; }
    public string Kind { get; set; } = "insight";
    public string Status { get; set; } = PlatformInboxStatuses.NeedsAttention;
    public string Severity { get; set; } = "medium";
    public string Title { get; set; } = "";
    public string? Body { get; set; }
    public string EvidenceJson { get; set; } = "[]";
    public string Source { get; set; } = "automation";
    public string? SourceRef { get; set; }
    public string DedupeKey { get; set; } = "";
    public int Occurrences { get; set; } = 1;
    public string? ResolvedByUserId { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
    public string? ResolutionNote { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public static class PlatformInboxStatuses
{
    public const string NeedsAttention = "needs_attention";
    public const string Recommendation = "recommendation";
    public const string Acknowledged = "acknowledged";
    public const string Resolved = "resolved";
    public const string Dismissed = "dismissed";
}

/// <summary>An outcome the organisation is working towards, tracked through linked automations and inbox items.</summary>
public sealed class PlatformIntent : IPlatformRecord
{
    public Guid Id { get; set; }
    public Guid? WorkspaceId { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public string? Measure { get; set; }
    public string? Target { get; set; }
    public string Status { get; set; } = "active";
    public List<Guid> AutomationIds { get; set; } = [];
    public string? OwnerUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>Organisation-level platform settings. One row per tenant workspace.</summary>
public sealed class PlatformSettings : IPlatformRecord
{
    public Guid Id { get; set; }
    public string AccessMode { get; set; } = "open";
    public string? UpdatedByUserId { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>Tenant-scoped persistence for one kind of platform record. Services filter and project in memory over a bounded list.</summary>
public interface IPlatformStore<T> where T : class, IPlatformRecord
{
    /// <summary>The most recently updated records first, at most <paramref name="limit"/> of them.</summary>
    Task<IReadOnlyList<T>> ListAsync(TenantScope scope, int limit = 1000, CancellationToken cancellationToken = default);

    Task<T?> GetAsync(TenantScope scope, Guid id, CancellationToken cancellationToken = default);

    Task SaveAsync(TenantScope scope, T record, CancellationToken cancellationToken = default);
}

public interface IOrgWorkspaceStore : IPlatformStore<OrgWorkspace>
{
    /// <summary>Saves the primary workspace only if the scope has none yet; false when another request created it first.</summary>
    Task<bool> TryCreatePrimaryAsync(TenantScope scope, OrgWorkspace workspace, CancellationToken cancellationToken = default);
}

public interface IPlatformAutomationStore : IPlatformStore<PlatformAutomation>
{
    /// <summary>Saves a batch in one transaction, so a workspace and its suggested drafts appear together.</summary>
    Task SaveManyAsync(TenantScope scope, IReadOnlyCollection<PlatformAutomation> records, CancellationToken cancellationToken = default);
}

public interface IPlatformRunStore : IPlatformStore<PlatformAutomationRun>
{
    Task<IReadOnlyList<PlatformAutomationRun>> ListForAutomationAsync(TenantScope scope, Guid automationId, int limit, CancellationToken cancellationToken = default);
}

public interface IPlatformInboxStore : IPlatformStore<PlatformInboxItem>
{
    /// <summary>The newest item with this deduplication key, whatever its status.</summary>
    Task<PlatformInboxItem?> FindByDedupeKeyAsync(TenantScope scope, string dedupeKey, CancellationToken cancellationToken = default);
}

public interface IPlatformIntentStore : IPlatformStore<PlatformIntent>;

public interface IPlatformSettingsStore : IPlatformStore<PlatformSettings>;
