namespace InsuranceIntelligence.Domain.Agents;

/// <summary>
/// WO-11: what an agent actually is, verified against real call sites (not the target-state
/// architecture doc, which describes what the platform should eventually have). ModelBacked means
/// a real, gated chatCompletionProvider/IAiAssistantProvider/ICustomerUpdateDraftingProvider call
/// site exists for this agent key. AiServiceBacked means a real cloud AI service is called (e.g.
/// Azure Document Intelligence) but no chat/LLM completion — a genuinely different capability from
/// a rule engine, since output quality depends on a live AI service, not fixed logic. RuleBased
/// means deterministic business rules with no AI service call at all. NotImplemented means the
/// catalog entry exists but nothing in the codebase executes it — it cannot be enabled.
/// </summary>
public enum AgentImplementationKind
{
    ModelBacked,
    AiServiceBacked,
    RuleBased,
    NotImplemented,
}

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
    bool HumanApprovalRequired,
    AgentImplementationKind ImplementationKind,
    IReadOnlyCollection<string> Tools);

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
        new("command-center", "Command Center Agent", "Prepares grounded operational briefings and structured command responses.", "Medium", HumanApprovalRequired: false,
            AgentImplementationKind.ModelBacked, ["claims.attention.read", "claims.sla.read", "documents.gaps.read", "approvals.queue.read", "knowledge.search", "provider_health.read", "metrics.read"]),
        new("claims-summary", "Claims Summary Agent", "Summarises claim facts, timelines, issues, and cited evidence for human review.", "Low", HumanApprovalRequired: false,
            AgentImplementationKind.ModelBacked, ["claim.read", "document.read", "policy.read", "knowledge.search", "claims.attention.read", "audit.read"]),
        // Document Checker calls a real Azure AI service (Document Intelligence: OCR + field
        // extraction) but never a chat/LLM completion — a genuinely different capability from a
        // rule engine, since its output quality depends on a live AI service, not fixed logic.
        new("document-checker", "Document Checker Agent", "Classifies documents, reviews extraction, and identifies missing or low-confidence fields.", "Medium", HumanApprovalRequired: false,
            AgentImplementationKind.AiServiceBacked, ["document.read", "documents.gaps.read", "provider_health.read"]),
        new("policy-intelligence", "Policy Intelligence Agent", "Explains policy and coverage from approved, tenant-scoped sources only.", "Medium", HumanApprovalRequired: false,
            AgentImplementationKind.ModelBacked, ["knowledge.search", "policy.read"]),
        // Rule-based: FraudRuleEngine (duplicate contact, claim velocity, duplicate policy, low
        // completeness signals). No chat/LLM call site exists.
        new("risk-fraud-signal", "Risk & Fraud Signal Agent", "Raises advisory risk and fraud indicators for human review, never accusations or verdicts.", "Medium", HumanApprovalRequired: false,
            AgentImplementationKind.RuleBased, ["claim.read", "document.read", "audit.read"]),
        new("customer-update-drafting", "Customer Update Drafting Agent", "Drafts customer-safe communications; every external send requires named human approval.", "High", HumanApprovalRequired: true,
            AgentImplementationKind.ModelBacked, ["claim.read"]),
        // Rule-based: ClaimRoutingPolicy. No chat/LLM call site exists.
        new("routing", "Routing Agent", "Recommends assignment, prioritisation, and escalation without executing consequential actions.", "Medium", HumanApprovalRequired: true,
            AgentImplementationKind.RuleBased, ["claims.attention.read", "claims.sla.read", "claim.read"]),
        // Not implemented: no code path in this repository invokes a model, a provider, or any
        // service under this agent key. Staff policy/knowledge questions are answered by the
        // policy-intelligence agent instead (via the same Command Center query path) — this
        // catalog entry currently has no distinct implementation of its own.
        new("knowledge-assistant", "Knowledge Assistant", "Answers staff questions from approved, scoped knowledge with source citations.", "Low", HumanApprovalRequired: false,
            AgentImplementationKind.NotImplemented, []),
        new("report-insight", "Report Insight Agent", "Prepares grounded operational and management summaries from real aggregates.", "Medium", HumanApprovalRequired: false,
            AgentImplementationKind.ModelBacked, ["claims.sla.read", "audit.read", "provider_health.read", "metrics.read"]),
        // Rule-based: UnderwritingRuleEngine. No chat/LLM call site exists.
        new("underwriting-intelligence", "Underwriting Intelligence Agent", "Supports proposal and risk review with referral notes; never accepts, rejects, or prices risk.", "High", HumanApprovalRequired: true,
            AgentImplementationKind.RuleBased, ["knowledge.search", "document.read", "policy.read"]),
        // Customer-facing, not staff-facing — a different category from the ten above (see
        // docs/03-design-architecture/iip_ai_agent_architecture_tool_registry_updated.md's
        // Customer-Facing Agents section). Intent classification is deterministic/rule-based
        // (KeywordCustomerIntentClassifier); grounded GeneralQuestion answers are produced by
        // delegating to policy-intelligence's own model-backed path, not a separate model call
        // this agent owns. RiskLevel is High and HumanApprovalRequired is true because this is the
        // one agent in the catalog that can act on an unauthenticated inbound message with no
        // staff user in the loop at all — disabling it (Admin > Agents) is the fail-closed switch:
        // CustomerAssistantService checks it before processing any inbound message and escalates
        // to human instead when disabled.
        new("customer-assistant", "Customer Assistant (WhatsApp)", "Classifies inbound WhatsApp messages into six intents; answers general questions from approved knowledge, opens provisional claims, and captures sales leads — never discloses case-specific detail without a matched policy number.", "High", HumanApprovalRequired: true,
            AgentImplementationKind.RuleBased, ["customer.intent.classify", "knowledge.search", "case.create", "lead.create"]),
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
