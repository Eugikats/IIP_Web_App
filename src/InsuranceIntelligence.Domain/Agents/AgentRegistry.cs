namespace InsuranceIntelligence.Domain.Agents;

/// <summary>
/// WO-11: what an agent actually is, verified against real call sites (not the target-state
/// architecture doc, which describes what the platform should eventually have). ModelBacked means
/// a real, gated chatCompletionProvider/IAiAssistantProvider/ICustomerUpdateDraftingProvider call
/// site exists for this agent key. AiServiceBacked means a real cloud AI service is called (e.g.
/// Azure Document Intelligence) but no chat/LLM completion, a genuinely different capability from
/// a rule engine, since output quality depends on a live AI service, not fixed logic. RuleBased
/// means deterministic business rules with no AI service call at all. NotImplemented means the
/// catalog entry exists but nothing in the codebase executes it, it cannot be enabled.
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
    // The agents the platform can run (the count is asserted in AgentWorkflowCatalogTests, not
    // repeated here). Adding or removing an agent requires an ADR; tenant state may only disable them.
    public static readonly IReadOnlyList<AgentDescriptor> Agents =
    [
        new("command-center", "Command Center Agent", "Prepares grounded operational briefings and structured command responses.", "Medium", HumanApprovalRequired: false,
            AgentImplementationKind.ModelBacked, ["claims.attention.read", "claims.sla.read", "documents.gaps.read", "approvals.queue.read", "knowledge.search", "provider_health.read", "metrics.read"]),
        new("claims-summary", "Claims Summary Agent", "Summarises claim facts, timelines, issues, and cited evidence for human review.", "Low", HumanApprovalRequired: false,
            AgentImplementationKind.ModelBacked, ["claim.read", "document.read", "policy.read", "knowledge.search", "claims.attention.read", "audit.read"]),
        // Briefs staff on one customer from that customer's own records only (policies, claims,
        // messages, documents, consent), citing the record behind every point. No knowledge base.
        new("customer-summary", "Customer Summary Agent", "Summarises one customer's policies, claims, messages and documents for staff, citing the record behind each point.", "Low", HumanApprovalRequired: false,
            AgentImplementationKind.ModelBacked, ["customer.profile.read"]),
        // Document Checker calls a real Azure AI service (Document Intelligence: OCR + field
        // extraction) but never a chat/LLM completion, a genuinely different capability from a
        // rule engine, since its output quality depends on a live AI service, not fixed logic.
        new("document-checker", "Document Checker Agent", "Classifies documents, reviews extraction, and identifies missing or low-confidence fields.", "Medium", HumanApprovalRequired: false,
            AgentImplementationKind.AiServiceBacked, ["document.read", "documents.gaps.read", "provider_health.read"]),
        new("policy-intelligence", "Policy Intelligence Agent", "Explains policy and coverage from approved, tenant-scoped sources only.", "Medium", HumanApprovalRequired: false,
            AgentImplementationKind.ModelBacked, ["knowledge.search", "policy.read"]),
        // Model-backed (2026-11-10): FraudAgentService reads a claim against the Uganda fraud playbook
        // (FraudPlaybook) and writes findings that each cite the claim facts they rest on. The fixed
        // FraudRuleEngine rules (duplicate contact, claim velocity, duplicate policy, early claim, high
        // value, low completeness) still run and are given to the agent as facts. Findings are advisory.
        new("risk-fraud-signal", "Risk & Fraud Signal Agent", "Reads a claim against a written playbook of how insurance fraud shows up in Uganda and raises advisory risk indicators, each citing the claim facts it rests on, for human review, never accusations or verdicts.", "Medium", HumanApprovalRequired: false,
            AgentImplementationKind.ModelBacked, ["claim.read", "document.read", "audit.read"]),
        // Writes a short plain explanation of the claim recommendation from facts the platform has already
        // worked out (score, missing documents, picture checks). Its wording is checked and dropped if it
        // sounds like a decision or an accusation. The recommendation itself is rules, not this agent.
        new("claim-recommendation", "Claim Recommendation Agent", "Explains the recommended next step on a claim from the facts already worked out, in advisory wording, for a person to agree or disagree with.", "Medium", HumanApprovalRequired: false,
            AgentImplementationKind.ModelBacked, ["claim.read", "document.read"]),
        new("customer-update-drafting", "Customer Update Drafting Agent", "Drafts customer-safe communications; every external send requires named human approval.", "High", HumanApprovalRequired: true,
            AgentImplementationKind.ModelBacked, ["claim.read"]),
        // Rule-based: ClaimRoutingPolicy. No chat/LLM call site exists.
        new("routing", "Routing Agent", "Recommends assignment, prioritisation, and escalation without executing consequential actions.", "Medium", HumanApprovalRequired: true,
            AgentImplementationKind.RuleBased, ["claims.attention.read", "claims.sla.read", "claim.read"]),
        // Not implemented: no code path in this repository invokes a model, a provider, or any
        // service under this agent key. Staff policy/knowledge questions are answered by the
        // policy-intelligence agent instead (via the same Command Center query path), this
        // catalog entry currently has no distinct implementation of its own.
        new("knowledge-assistant", "Knowledge Assistant", "Answers staff questions from approved, scoped knowledge with source citations.", "Low", HumanApprovalRequired: false,
            AgentImplementationKind.NotImplemented, []),
        new("report-insight", "Report Insight Agent", "Prepares grounded operational and management summaries from real aggregates.", "Medium", HumanApprovalRequired: false,
            AgentImplementationKind.ModelBacked, ["claims.sla.read", "audit.read", "provider_health.read", "metrics.read"]),
        // Rule-based: UnderwritingRuleEngine. No chat/LLM call site exists.
        new("underwriting-intelligence", "Underwriting Intelligence Agent", "Supports proposal and risk review with referral notes; never accepts, rejects, or prices risk.", "High", HumanApprovalRequired: true,
            AgentImplementationKind.RuleBased, ["knowledge.search", "document.read", "policy.read"]),
        // Customer-facing, not staff-facing, a different category from the staff agents above (see
        // docs/03-design-architecture/iip_ai_agent_architecture_tool_registry_updated.md's
        // Customer-Facing Agents section). Intent classification is deterministic
        // (KeywordCustomerIntentClassifier), but Kato's replies are produced by a tool-calling model
        // loop (CustomerAssistantService.Agent.cs) whenever an AI provider is configured, which is
        // why this is ModelBacked: the output quality depends on a live model, not fixed logic.
        // RiskLevel is High and HumanApprovalRequired is true because this is the
        // one agent in the catalog that can act on an unauthenticated inbound message with no
        // staff user in the loop at all, disabling it (Admin > Agents) is the fail-closed switch:
        // CustomerAssistantService checks it before processing any inbound message and escalates
        // to human instead when disabled.
        // Reads a prospect's WhatsApp conversation after the reply has gone and keeps their record
        // current. Every point cites the customer message behind it; facts must echo the customer's
        // own words and come from an allowed list, so nothing sensitive or invented is stored.
        new("conversation-insights", "Conversation Insights Agent", "Reads a prospect's WhatsApp conversation after each reply and records what they said about themselves, the covers they are interested in, their problems and doubts, and a suggested next step, citing the message behind each point.", "Low", HumanApprovalRequired: false,
            AgentImplementationKind.ModelBacked, ["conversation.read", "lead.update"]),
        // Scores each prospect from what is recorded about them and names the two covers that fit best,
        // with reasons quoted from the record. A ranking for staff to act on, never a decision.
        new("prospect-triage", "Prospect Triage Agent", "Scores each prospect from the facts recorded about them, names the two covers that fit best with the reasons and the insurer's own summary of each cover, and ranks who to contact first. It never contacts anyone or decides cover.", "Low", HumanApprovalRequired: false,
            AgentImplementationKind.RuleBased, ["lead.read", "lead.update", "knowledge.search"]),
        // Drafts a personal message for each person with a recommended cover, and follow-ups for people who
        // have gone quiet or whose policy is ending. It only drafts: a named person approves every send.
        new("outreach", "Outreach Agent", "Drafts a personal message for each person with a recommended cover, and follow-ups for people who have gone quiet or whose policy is ending. It only drafts: a named person reads, edits and approves every message, and each send is checked for consent, sending hours and WhatsApp rules.", "Medium", HumanApprovalRequired: true,
            AgentImplementationKind.RuleBased, ["lead.read", "lead.update", "message.draft"]),
        new("customer-assistant", "Customer Assistant (WhatsApp)", "Classifies inbound WhatsApp messages into six intents; answers general questions from approved knowledge, opens provisional claims, and captures sales leads, never discloses case-specific detail without a matched policy number.", "High", HumanApprovalRequired: true,
            AgentImplementationKind.ModelBacked, ["customer.intent.classify", "knowledge.search", "case.create", "lead.create"]),
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
