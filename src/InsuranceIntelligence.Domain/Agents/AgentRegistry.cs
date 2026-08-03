namespace InsuranceIntelligence.Domain.Agents;

/// <summary>
/// Static description of an AI agent the platform can run. The catalog is code-
/// defined (these are real capabilities), while each agent's enabled/disabled
/// (kill-switch) state is tenant-scoped and persisted. The registry is a control
/// plane: it constrains agents, never grants autonomy (ADR-0002).
/// </summary>
public sealed record AgentDescriptor(
    string Key,
    string Name,
    string Purpose,
    string RiskLevel,
    bool HumanApprovalRequired);

/// <summary>
/// Tenant-scoped, persisted kill-switch (and audit stamp) for a single agent.
/// Absence means "enabled by default".
/// </summary>
public sealed record AgentKillSwitchState(
    string AgentKey,
    bool Enabled,
    string? UpdatedByUserId,
    DateTimeOffset? UpdatedAt,
    string? Reason);

/// <summary>Catalog merged with the tenant's persisted kill-switch state.</summary>
public sealed record AgentRegistryEntry(
    AgentDescriptor Descriptor,
    bool Enabled,
    string? UpdatedByUserId,
    DateTimeOffset? UpdatedAt,
    string? Reason);

public static class AgentCatalog
{
    // The ten canonical agents from the governing architecture. Adding or
    // removing an agent requires an ADR; tenant state may only disable them.
    public static readonly IReadOnlyList<AgentDescriptor> Agents =
    [
        new("command-center", "Command Center Agent", "Prepares grounded operational briefings and structured command responses.", "Medium", HumanApprovalRequired: false),
        new("claims-summary", "Claims Summary Agent", "Summarises claim facts, timelines, issues, and cited evidence for human review.", "Low", HumanApprovalRequired: false),
        new("document-checker", "Document Checker Agent", "Classifies documents, reviews extraction, and identifies missing or low-confidence fields.", "Medium", HumanApprovalRequired: false),
        new("policy-intelligence", "Policy Intelligence Agent", "Explains policy and coverage from approved, tenant-scoped sources only.", "Medium", HumanApprovalRequired: false),
        new("risk-fraud-signal", "Risk & Fraud Signal Agent", "Raises advisory risk and fraud indicators for human review, never accusations or verdicts.", "Medium", HumanApprovalRequired: false),
        new("customer-update-drafting", "Customer Update Drafting Agent", "Drafts customer-safe communications; every external send requires named human approval.", "High", HumanApprovalRequired: true),
        new("routing", "Routing Agent", "Recommends assignment, prioritisation, and escalation without executing consequential actions.", "Medium", HumanApprovalRequired: true),
        new("knowledge-assistant", "Knowledge Assistant", "Answers staff questions from approved, scoped knowledge with source citations.", "Low", HumanApprovalRequired: false),
        new("report-insight", "Report Insight Agent", "Prepares grounded operational and management summaries from real aggregates.", "Medium", HumanApprovalRequired: false),
        new("underwriting-intelligence", "Underwriting Intelligence Agent", "Supports proposal and risk review with referral notes; never accepts, rejects, or prices risk.", "High", HumanApprovalRequired: true),
    ];

    public static AgentDescriptor? Find(string key) =>
        Agents.FirstOrDefault(a => string.Equals(a.Key, key, StringComparison.OrdinalIgnoreCase));
}

/// <summary>
/// Merges the code-defined catalog with persisted kill-switch state. Pure and
/// unit-testable.
/// </summary>
public static class AgentRegistryMerge
{
    public static IReadOnlyList<AgentRegistryEntry> Merge(IReadOnlyList<AgentKillSwitchState> states)
    {
        var byKey = states.ToDictionary(s => s.AgentKey, StringComparer.OrdinalIgnoreCase);
        return AgentCatalog.Agents
            .Select(descriptor =>
            {
                byKey.TryGetValue(descriptor.Key, out var state);
                return new AgentRegistryEntry(
                    descriptor,
                    Enabled: state?.Enabled ?? true,
                    UpdatedByUserId: state?.UpdatedByUserId,
                    UpdatedAt: state?.UpdatedAt,
                    Reason: state?.Reason);
            })
            .ToArray();
    }
}
