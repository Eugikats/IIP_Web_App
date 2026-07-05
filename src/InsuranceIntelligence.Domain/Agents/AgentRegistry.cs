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
    // The real agent capabilities IIP runs. Adding an agent here surfaces it in
    // the registry with a default-enabled kill switch until an admin toggles it.
    public static readonly IReadOnlyList<AgentDescriptor> Agents =
    [
        new("claim-summary", "Claim Summary Agent", "Summarises a claim with cited evidence for human review.", "Low", HumanApprovalRequired: false),
        new("document-extraction", "Document Extraction Agent", "Extracts fields from claim documents; low-confidence fields are flagged incomplete.", "Medium", HumanApprovalRequired: false),
        new("customer-update-draft", "Customer Update Draft Agent", "Drafts customer-safe updates; every send requires named human approval.", "High", HumanApprovalRequired: true),
        new("policy-qa", "Policy Q&A Agent", "Answers coverage questions grounded only in approved knowledge sources.", "Medium", HumanApprovalRequired: false),
        new("fraud-advisory", "Fraud Advisory Agent", "Raises deterministic advisory fraud signals — never a fraud verdict.", "Medium", HumanApprovalRequired: false),
        new("underwriting-advisory", "Underwriting Advisory Agent", "Surfaces underwriting checks and drafts questions — never decides acceptance or premium.", "Medium", HumanApprovalRequired: false),
        new("missing-information", "Missing Information Agent", "Detects missing documents/fields and prepares request checklists.", "Low", HumanApprovalRequired: false),
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
