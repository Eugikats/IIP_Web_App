using System.Text.Json.Serialization;

namespace InsuranceIntelligence.Application.CommandCenter;

/// <summary>
/// Request body for <c>POST /api/v1/command-center/query</c>. Tenant, workspace, and role are
/// resolved server-side from Entra claims or dev headers and are never trusted from this body.
/// </summary>
public sealed record CommandCenterQueryRequest(string? Question);

/// <summary>
/// Governed response envelope for the chat-first Command Center. Reuses the same provenance
/// fields already returned by <c>POST /api/v1/command-center/assistant</c>.
/// <para>
/// Invariant: <see cref="CanTriggerDecision"/> is ALWAYS <c>false</c>. No block conveys a final
/// insurance decision — the Command Center prepares work for a human to decide.
/// </para>
/// </summary>
public sealed record CommandCenterQueryResponse(
    IReadOnlyList<CommandCenterResponseBlock> Blocks,
    Guid AgentActionLogId,
    string CorrelationId,
    string ModelProvider,
    string PromptVersion,
    string Visibility,
    bool CanTriggerDecision,
    DateTimeOffset GeneratedAt);

/// <summary>
/// Discriminated union of Command Center answer blocks. Serialized with a lowercase snake_case
/// <c>type</c> discriminator that MUST match the frozen contract
/// (docs/development-engineering/command-center-query-contract.md) and the frontend parser.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(TextBlock), "text")]
[JsonDerivedType(typeof(MetricPillsBlock), "metric_pills")]
[JsonDerivedType(typeof(ClaimsTableBlock), "claims_table")]
[JsonDerivedType(typeof(SlaSummaryBlock), "sla_summary")]
[JsonDerivedType(typeof(DocumentGapSummaryBlock), "document_gap_summary")]
[JsonDerivedType(typeof(ApprovalQueueBlock), "approval_queue")]
[JsonDerivedType(typeof(DraftCommunicationBlock), "draft_communication")]
[JsonDerivedType(typeof(ActionProposalBlock), "action_proposal")]
[JsonDerivedType(typeof(EvidenceListBlock), "evidence_list")]
[JsonDerivedType(typeof(NotConfiguredBlock), "not_configured")]
[JsonDerivedType(typeof(ErrorBlock), "error")]
public abstract record CommandCenterResponseBlock;

/// <summary>One-paragraph narration over the data blocks. Never the sole content of a data answer.</summary>
public sealed record TextBlock(string Markdown) : CommandCenterResponseBlock;

public sealed record MetricPillsBlock(IReadOnlyList<MetricPill> Items) : CommandCenterResponseBlock;

public sealed record ClaimsTableBlock(IReadOnlyList<ClaimRow> Rows) : CommandCenterResponseBlock;

public sealed record SlaSummaryBlock(IReadOnlyList<SlaItem> Items) : CommandCenterResponseBlock;

public sealed record DocumentGapSummaryBlock(IReadOnlyList<DocGapItem> Items) : CommandCenterResponseBlock;

public sealed record ApprovalQueueBlock(IReadOnlyList<ApprovalItem> Items) : CommandCenterResponseBlock;

/// <summary>Persisted customer-communication draft. Human review is required before send.</summary>
public sealed record DraftCommunicationBlock(
    string DraftId,
    string Preview,
    bool RequiresApproval) : CommandCenterResponseBlock;

/// <summary>Draft-only suggested next steps. Every action requires human approval; never executable.</summary>
public sealed record ActionProposalBlock(IReadOnlyList<ProposedAction> Actions) : CommandCenterResponseBlock;

public sealed record EvidenceListBlock(IReadOnlyList<EvidenceSource> Sources) : CommandCenterResponseBlock;

/// <summary>Honest unavailability for a provider whose health is <c>missing</c>. Never fabricated success.</summary>
public sealed record NotConfiguredBlock(string Provider, string AdminAction) : CommandCenterResponseBlock;

/// <summary>Safe error surface: correlation id, no raw provider error, no secrets.</summary>
public sealed record ErrorBlock(string Code, string SafeMessage, string CorrelationId) : CommandCenterResponseBlock;

public sealed record MetricPill(string Label, string Value, string Tone, string? Detail = null);

public sealed record ClaimRow(
    string CaseId,
    string Customer,
    string ProductLine,
    string Status,
    string SlaStatus,
    decimal Completeness,
    string AssignedTo,
    string Href);

public sealed record SlaItem(string CaseId, string Customer, string SlaStatus, string DueLabel);

public sealed record DocGapItem(string CaseId, string Customer, IReadOnlyList<string> Missing);

public sealed record ApprovalItem(string ApprovalId, string CaseId, string Title, string Status, string Href);

/// <summary>
/// A draft-only proposed next step. <see cref="RequiresHumanApproval"/> is always <c>true</c>;
/// the renderer offers a link into a governed flow, never an execute/confirm control.
/// </summary>
public sealed record ProposedAction(
    string Label,
    string Description,
    string TargetHref,
    bool RequiresHumanApproval = true);

public sealed record EvidenceSource(string Label, string DocumentName, string Ref);

/// <summary>Contract-frozen vocabulary for block fields. Kept in one place to prevent drift.</summary>
public static class CommandCenterBlockVocabulary
{
    public const string ToneNeutral = "neutral";
    public const string ToneInfo = "info";
    public const string ToneSuccess = "success";
    public const string ToneWarning = "warning";
    public const string ToneDanger = "danger";

    public const string SlaOnTrack = "on_track";
    public const string SlaDueToday = "due_today";
    public const string SlaAtRisk = "at_risk";
    public const string SlaBreached = "breached";
}
