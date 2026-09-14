using InsuranceIntelligence.Domain.AgentWorkflows;

namespace InsuranceIntelligence.Application.AgentWorkflows;

/// <summary>
/// The governed Releases 1-5 shipped with IIP. These are configuration records,
/// not demo business data. Release 1 is executable on the current orchestration
/// path; Releases 2–5 remain visibly held (inspectable, not runnable) until
/// their providers and earlier services are proven, see
/// <see cref="AgentWorkflowReleaseOneBound"/>.
/// </summary>
public static class AgentWorkflowCatalog
{
    private static readonly DateTimeOffset CatalogTimestamp = new(2026, 7, 16, 0, 0, 0, TimeSpan.Zero);

    public static readonly IReadOnlyList<AgentWorkflowDefinition> Releases =
    [
        Build(
            "11111111-1111-4111-8111-111111111111",
            "document-to-decision",
            "Release 1 · Document-to-Decision",
            "Classifies and checks claim documents, records evidence quality, and routes prepared extraction work to a human reviewer.",
            1,
            ["case", "document"],
            [
                Node("trigger", "Document received", "Starts from a real uploaded document reference.", AgentWorkflowNodeType.Trigger, 60, 180),
                Agent("document-check", "Check document", "Classify, inspect extraction, and flag gaps without forwarding raw document content.", "document-checker", 330, 180),
                Decision("quality-gate", "Evidence quality gate", "Evaluate completeness and confidence using bounded structured fields.", 610, 180),
                Review("extraction-review", "Human extraction review", "Confirm, correct, return, or escalate prepared extraction work. This is not a claim decision.", "document-check", 890, 180),
                Complete("complete", "Prepared for downstream work", 1180, 180)
            ],
            [
                Edge("trigger", "document-check"),
                Edge("document-check", "quality-gate"),
                Edge("quality-gate", "extraction-review", "review", "Review required"),
                Edge("quality-gate", "extraction-review", null, "Safe default"),
                Edge("extraction-review", "complete")
            ]),
        Build(
            "22222222-2222-4222-8222-222222222222",
            "fnol-claim-readiness",
            "Release 2 · FNOL & Claim Readiness",
            AgentWorkflowReleaseOneBound.HeldCatalogPreface(2)
            + "Builds a grounded first-notice brief, checks policy and document readiness, and proposes routing for human confirmation.",
            2,
            ["case", "claim"],
            [
                Node("trigger", "FNOL received", "Starts from an existing tenant-scoped case or claim reference.", AgentWorkflowNodeType.Trigger, 60, 180),
                Agent("claim-brief", "Prepare claim brief", "Summarise known claim facts and timeline with evidence references.", "claims-summary", 320, 90),
                Agent("policy-check", "Check policy context", "Retrieve policy context from authoritative records and approved knowledge.", "policy-intelligence", 320, 270),
                Agent("document-readiness", "Check required documents", "Identify missing or low-confidence document information.", "document-checker", 600, 90),
                Agent("routing-proposal", "Propose routing", "Recommend team, priority, and escalation; do not assign automatically.", "routing", 600, 270, AgentWorkflowSideEffect.ApprovalRequired),
                Review("readiness-review", "Human readiness review", "Review the FNOL brief and routing proposal before operational use.", "claim-brief", 900, 180),
                Complete("complete", "Readiness pack accepted", 1190, 180)
            ],
            [
                Edge("trigger", "claim-brief"), Edge("trigger", "policy-check"),
                Edge("claim-brief", "document-readiness"), Edge("policy-check", "routing-proposal"),
                Edge("document-readiness", "readiness-review"), Edge("routing-proposal", "readiness-review"),
                Edge("readiness-review", "complete")
            ]),
        Build(
            "33333333-3333-4333-8333-333333333333",
            "claims-triage-sla",
            "Release 3 · Claims Triage & SLA",
            AgentWorkflowReleaseOneBound.HeldCatalogPreface(3)
            + "Prepares a claim summary, advisory risk signals, and an escalation recommendation for accountable human triage.",
            3,
            ["case", "claim"],
            [
                Node("trigger", "Claim selected", "Starts from an existing tenant-scoped case or claim reference.", AgentWorkflowNodeType.Trigger, 60, 180),
                Agent("claim-summary", "Summarise claim", "Prepare facts, timeline, issues, and evidence.", "claims-summary", 340, 90),
                Agent("risk-signals", "Surface risk signals", "Flag advisory risk indicators without accusation or fraud determination.", "risk-fraud-signal", 340, 270),
                Agent("triage-routing", "Prepare triage recommendation", "Recommend priority, owner, and escalation for human action.", "routing", 650, 180, AgentWorkflowSideEffect.ApprovalRequired),
                Review("triage-review", "Human triage review", "Accept the prepared brief, return it for correction, or escalate. No claim approval or rejection occurs here.", "claim-summary", 940, 180),
                Complete("complete", "Triage brief accepted", 1230, 180)
            ],
            [
                Edge("trigger", "claim-summary"), Edge("trigger", "risk-signals"),
                Edge("claim-summary", "triage-routing"), Edge("risk-signals", "triage-routing"),
                Edge("triage-routing", "triage-review"), Edge("triage-review", "complete")
            ]),
        Build(
            "44444444-4444-4444-8444-444444444444",
            "policy-customer-response",
            "Release 4 · Policy & Customer Response",
            AgentWorkflowReleaseOneBound.HeldCatalogPreface(4)
            + "Grounds a policy explanation and prepares a customer-safe draft that cannot be sent by the workflow engine.",
            4,
            ["case", "claim", "policy", "customer"],
            [
                Node("trigger", "Response requested", "Starts from a real case, claim, policy, or customer reference.", AgentWorkflowNodeType.Trigger, 60, 180),
                Agent("policy-grounding", "Retrieve policy grounding", "Retrieve coverage context from authoritative policy records.", "policy-intelligence", 330, 90),
                Agent("knowledge-grounding", "Retrieve approved knowledge", "Use only approved tenant-scoped knowledge sources.", "knowledge-assistant", 330, 270),
                Agent("draft-response", "Draft customer update", "Prepare a customer-safe draft; never send or commit externally.", "customer-update-drafting", 650, 180, AgentWorkflowSideEffect.DraftOnly),
                Review("communication-review", "Human communication review", "Edit, accept, return, or escalate the draft. Sending remains a separate approved service.", "draft-response", 950, 180),
                Complete("complete", "Draft accepted", 1240, 180)
            ],
            [
                Edge("trigger", "policy-grounding"), Edge("trigger", "knowledge-grounding"),
                Edge("policy-grounding", "draft-response"), Edge("knowledge-grounding", "draft-response"),
                Edge("draft-response", "communication-review"), Edge("communication-review", "complete")
            ]),
        Build(
            "55555555-5555-4555-8555-555555555555",
            "underwriting-management-insight",
            "Release 5 · Underwriting & Management Insight",
            AgentWorkflowReleaseOneBound.HeldCatalogPreface(5)
            + "Combines advisory underwriting review, risk signals, and real operational reporting into a human-reviewed insight pack.",
            5,
            ["submission", "policy", "portfolio", "report"],
            [
                Node("trigger", "Insight requested", "Starts from an existing underwriting submission, policy, portfolio, or report reference.", AgentWorkflowNodeType.Trigger, 60, 180),
                Agent("underwriting-review", "Prepare underwriting review", "Draft questions and referral notes without accepting, rejecting, binding, or pricing.", "underwriting-intelligence", 340, 70),
                Agent("risk-review", "Surface risk signals", "Prepare advisory risk indicators for human interpretation.", "risk-fraud-signal", 340, 180),
                Agent("report-insight", "Prepare management insight", "Summarise real aggregates without estimating or fabricating metrics.", "report-insight", 340, 290),
                Agent("command-brief", "Assemble operational brief", "Assemble the grounded outputs into a structured command-center brief.", "command-center", 690, 180),
                Review("insight-review", "Human insight review", "Review, return, escalate, or accept the prepared advisory pack.", "command-brief", 990, 180),
                Complete("complete", "Insight pack accepted", 1280, 180)
            ],
            [
                Edge("trigger", "underwriting-review"), Edge("trigger", "risk-review"), Edge("trigger", "report-insight"),
                Edge("underwriting-review", "command-brief"), Edge("risk-review", "command-brief"), Edge("report-insight", "command-brief"),
                Edge("command-brief", "insight-review"), Edge("insight-review", "complete")
            ])
    ];

    public static AgentWorkflowDefinition? Find(string key) =>
        Releases.FirstOrDefault(item => string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase));

    private static AgentWorkflowDefinition Build(
        string id,
        string key,
        string name,
        string description,
        int release,
        IReadOnlyList<string> referenceTypes,
        IReadOnlyList<AgentWorkflowNode> nodes,
        IReadOnlyList<AgentWorkflowEdge> edges) =>
        new(
            Guid.Parse(id),
            Scope: null,
            key,
            name,
            description,
            release,
            Version: 1,
            AgentWorkflowDefinitionStatus.Published,
            IsSystemDefinition: true,
            referenceTypes,
            nodes,
            edges,
            CatalogTimestamp,
            CreatedByUserId: "system:iip-release-catalog",
            PublishedAt: CatalogTimestamp,
            PublishedByUserId: "system:iip-release-catalog");

    private static AgentWorkflowNode Node(
        string key,
        string name,
        string description,
        AgentWorkflowNodeType type,
        decimal x,
        decimal y) =>
        new(key, name, description, type, null, null, AgentWorkflowSideEffect.ReadOnly, new(x, y), false, EmptyConfiguration());

    private static AgentWorkflowNode Agent(
        string key,
        string name,
        string description,
        string agentKey,
        decimal x,
        decimal y,
        AgentWorkflowSideEffect sideEffect = AgentWorkflowSideEffect.ReadOnly) =>
        new(key, name, description, AgentWorkflowNodeType.Agent, agentKey, null, sideEffect, new(x, y), sideEffect == AgentWorkflowSideEffect.ApprovalRequired, EmptyConfiguration());

    private static AgentWorkflowNode Decision(string key, string name, string description, decimal x, decimal y) =>
        new(key, name, description, AgentWorkflowNodeType.Decision, null, "workflow.evidence-quality", AgentWorkflowSideEffect.ReadOnly, new(x, y), false, EmptyConfiguration());

    private static AgentWorkflowNode Review(string key, string name, string description, string correctionTarget, decimal x, decimal y) =>
        new(
            key,
            name,
            description,
            AgentWorkflowNodeType.HumanReview,
            null,
            null,
            AgentWorkflowSideEffect.ApprovalRequired,
            new(x, y),
            true,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["correctionTarget"] = correctionTarget });

    private static AgentWorkflowNode Complete(string key, string name, decimal x, decimal y) =>
        new(key, name, "Ends the preparation workflow without executing a consequential insurance decision or external send.", AgentWorkflowNodeType.Complete, null, null, AgentWorkflowSideEffect.ReadOnly, new(x, y), false, EmptyConfiguration());

    private static AgentWorkflowEdge Edge(string source, string target, string? outcome = null, string? label = null) =>
        new(source, target, outcome, label);

    private static IReadOnlyDictionary<string, string> EmptyConfiguration() =>
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}

public static class AgentWorkflowToolCatalog
{
    public static readonly IReadOnlySet<string> Tools = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "claims.read",
        "documents.read",
        "policies.read",
        "knowledge.retrieve",
        "reports.read",
        "workflow.evidence-quality"
    };
}
