/**
 * Command Center `/query` — frontend block types + strict parser.
 *
 * FROZEN CONTRACT: shapes/discriminators must match the backend exactly.
 * Do not add/rename fields without updating both sides + HANDOFF.md.
 * Reference: docs/development-engineering/command-center-query-contract.md
 */

// ---------------------------------------------------------------------------
// Primitives
// ---------------------------------------------------------------------------

export type Tone = "neutral" | "info" | "success" | "warning" | "danger";

export type SlaStatus = "on_track" | "due_today" | "at_risk" | "breached";

// ---------------------------------------------------------------------------
// Sub-shapes (each field validated in parse guards below)
// ---------------------------------------------------------------------------

export interface MetricPill {
  label: string;
  value: string;
  tone: Tone;
  detail?: string;
}

export interface ClaimRow {
  caseId: string;
  customer: string;
  productLine: string;
  status: string;
  slaStatus: SlaStatus;
  completeness: number;
  assignedTo: string;
  href: string;
}

export interface SlaItem {
  caseId: string;
  customer: string;
  slaStatus: SlaStatus;
  dueLabel: string;
}

export interface DocGapItem {
  caseId: string;
  customer: string;
  missing: string[];
}

export interface ApprovalItem {
  approvalId: string;
  caseId: string;
  title: string;
  status: string;
  href: string;
}

export interface ProposedAction {
  label: string;
  description: string;
  targetHref: string;
  requiresHumanApproval: true; // ALWAYS true — contract invariant
}

export interface EvidenceSource {
  label: string;
  documentName: string;
  ref: string;
}

/** Section of a management_summary block. Mirrors the backend SummarySection DTO. */
export interface SummarySection {
  heading: string;
  body: string;
}

// ---------------------------------------------------------------------------
// Block union (discriminated on `type`)
// ---------------------------------------------------------------------------

export type CommandCenterBlock =
  | { type: "text"; markdown: string }
  | { type: "metric_pills"; items: MetricPill[] }
  | { type: "claims_table"; rows: ClaimRow[] }
  | { type: "sla_summary"; items: SlaItem[] }
  | { type: "document_gap_summary"; items: DocGapItem[] }
  | { type: "approval_queue"; items: ApprovalItem[] }
  | { type: "draft_communication"; draftId: string; preview: string; requiresApproval: boolean }
  | { type: "management_summary"; title: string; sections: SummarySection[] }
  | { type: "action_proposal"; actions: ProposedAction[] }
  | { type: "evidence_list"; sources: EvidenceSource[] }
  | { type: "not_configured"; provider: string; adminAction: string }
  | { type: "error"; code: string; safeMessage: string; correlationId: string };

// ---------------------------------------------------------------------------
// Response envelope (mirrors the contract's `canTriggerDecision: false`
// invariant — the parser rejects any envelope where that is not literally
// the boolean `false`).
// ---------------------------------------------------------------------------

export interface CommandCenterQueryResponse {
  blocks: CommandCenterBlock[];
  agentActionLogId: string;
  correlationId: string;
  modelProvider: string;
  promptVersion: string;
  visibility: string;
  canTriggerDecision: false;
  generatedAt: string;
}

// ---------------------------------------------------------------------------
// Strict parser (mirrors `parseAssistantResponse` in AssistantChat.tsx)
// Returns `null` on any unknown/invalid shape rather than rendering garbage.
// ---------------------------------------------------------------------------

export function parseCommandCenterResponse(
  value: unknown
): CommandCenterQueryResponse | null {
  if (
    !isRecord(value) ||
    !Array.isArray(value.blocks) ||
    typeof value.agentActionLogId !== "string" ||
    typeof value.correlationId !== "string" ||
    typeof value.modelProvider !== "string" ||
    typeof value.promptVersion !== "string" ||
    typeof value.visibility !== "string" ||
    value.canTriggerDecision !== false ||
    typeof value.generatedAt !== "string"
  ) {
    return null;
  }

  const parsedBlocks: CommandCenterBlock[] = [];
  for (const raw of value.blocks) {
    const block = parseBlock(raw);
    if (!block) return null; // reject whole response on any bad block
    parsedBlocks.push(block);
  }

  return {
    blocks: parsedBlocks,
    agentActionLogId: value.agentActionLogId,
    correlationId: value.correlationId,
    modelProvider: value.modelProvider,
    promptVersion: value.promptVersion,
    visibility: value.visibility,
    canTriggerDecision: false,
    generatedAt: value.generatedAt,
  };
}

function parseBlock(value: unknown): CommandCenterBlock | null {
  if (!isRecord(value) || typeof value.type !== "string") return null;

  switch (value.type) {
    case "text":
      return typeof value.markdown === "string"
        ? { type: "text", markdown: value.markdown }
        : null;

    case "metric_pills": {
      if (!Array.isArray(value.items)) return null;
      const items: MetricPill[] = [];
      for (const item of value.items) {
        const parsed = parseMetricPill(item);
        if (!parsed) return null;
        items.push(parsed);
      }
      return { type: "metric_pills", items };
    }

    case "claims_table": {
      if (!Array.isArray(value.rows)) return null;
      const rows: ClaimRow[] = [];
      for (const row of value.rows) {
        const parsed = parseClaimRow(row);
        if (!parsed) return null;
        rows.push(parsed);
      }
      return { type: "claims_table", rows };
    }

    case "sla_summary": {
      if (!Array.isArray(value.items)) return null;
      const items: SlaItem[] = [];
      for (const item of value.items) {
        const parsed = parseSlaItem(item);
        if (!parsed) return null;
        items.push(parsed);
      }
      return { type: "sla_summary", items };
    }

    case "document_gap_summary": {
      if (!Array.isArray(value.items)) return null;
      const items: DocGapItem[] = [];
      for (const item of value.items) {
        const parsed = parseDocGapItem(item);
        if (!parsed) return null;
        items.push(parsed);
      }
      return { type: "document_gap_summary", items };
    }

    case "approval_queue": {
      if (!Array.isArray(value.items)) return null;
      const items: ApprovalItem[] = [];
      for (const item of value.items) {
        const parsed = parseApprovalItem(item);
        if (!parsed) return null;
        items.push(parsed);
      }
      return { type: "approval_queue", items };
    }

    case "action_proposal": {
      if (!Array.isArray(value.actions)) return null;
      const actions: ProposedAction[] = [];
      for (const action of value.actions) {
        const parsed = parseProposedAction(action);
        if (!parsed) return null;
        actions.push(parsed);
      }
      return { type: "action_proposal", actions };
    }

    case "evidence_list": {
      if (!Array.isArray(value.sources)) return null;
      const sources: EvidenceSource[] = [];
      for (const source of value.sources) {
        const parsed = parseEvidenceSource(source);
        if (!parsed) return null;
        sources.push(parsed);
      }
      return { type: "evidence_list", sources };
    }

    case "draft_communication":
      return typeof value.draftId === "string" &&
        typeof value.preview === "string" &&
        typeof value.requiresApproval === "boolean"
        ? {
            type: "draft_communication",
            draftId: value.draftId,
            preview: value.preview,
            requiresApproval: value.requiresApproval,
          }
        : null;

    case "management_summary": {
      if (typeof value.title !== "string" || !Array.isArray(value.sections)) return null;
      const sections: SummarySection[] = [];
      for (const section of value.sections) {
        if (
          !isRecord(section) ||
          typeof section.heading !== "string" ||
          typeof section.body !== "string"
        ) {
          return null;
        }
        sections.push({ heading: section.heading, body: section.body });
      }
      return { type: "management_summary", title: value.title, sections };
    }

    case "not_configured":
      return typeof value.provider === "string" &&
        typeof value.adminAction === "string"
        ? { type: "not_configured", provider: value.provider, adminAction: value.adminAction }
        : null;

    case "error":
      return typeof value.code === "string" &&
        typeof value.safeMessage === "string" &&
        typeof value.correlationId === "string"
        ? { type: "error", code: value.code, safeMessage: value.safeMessage, correlationId: value.correlationId }
        : null;

    default:
      // Unknown discriminator → reject
      return null;
  }
}

// ---------------------------------------------------------------------------
// Sub-shape guards
// ---------------------------------------------------------------------------

const VALID_TONES: Tone[] = ["neutral", "info", "success", "warning", "danger"];
const VALID_SLA: SlaStatus[] = ["on_track", "due_today", "at_risk", "breached"];

function isTone(v: unknown): v is Tone {
  return typeof v === "string" && (VALID_TONES as string[]).includes(v);
}

function isSlaStatus(v: unknown): v is SlaStatus {
  return typeof v === "string" && (VALID_SLA as string[]).includes(v);
}

function parseMetricPill(value: unknown): MetricPill | null {
  if (
    !isRecord(value) ||
    typeof value.label !== "string" ||
    typeof value.value !== "string" ||
    !isTone(value.tone)
  )
    return null;
  return {
    label: value.label,
    value: value.value,
    tone: value.tone,
    detail: typeof value.detail === "string" ? value.detail : undefined,
  };
}

function parseClaimRow(value: unknown): ClaimRow | null {
  if (
    !isRecord(value) ||
    typeof value.caseId !== "string" ||
    typeof value.customer !== "string" ||
    typeof value.productLine !== "string" ||
    typeof value.status !== "string" ||
    !isSlaStatus(value.slaStatus) ||
    typeof value.completeness !== "number" ||
    typeof value.assignedTo !== "string" ||
    typeof value.href !== "string"
  )
    return null;
  return {
    caseId: value.caseId,
    customer: value.customer,
    productLine: value.productLine,
    status: value.status,
    slaStatus: value.slaStatus,
    completeness: value.completeness,
    assignedTo: value.assignedTo,
    href: value.href,
  };
}

function parseSlaItem(value: unknown): SlaItem | null {
  if (
    !isRecord(value) ||
    typeof value.caseId !== "string" ||
    typeof value.customer !== "string" ||
    !isSlaStatus(value.slaStatus) ||
    typeof value.dueLabel !== "string"
  )
    return null;
  return {
    caseId: value.caseId,
    customer: value.customer,
    slaStatus: value.slaStatus,
    dueLabel: value.dueLabel,
  };
}

function parseDocGapItem(value: unknown): DocGapItem | null {
  if (
    !isRecord(value) ||
    typeof value.caseId !== "string" ||
    typeof value.customer !== "string" ||
    !Array.isArray(value.missing) ||
    !value.missing.every((m) => typeof m === "string")
  )
    return null;
  return {
    caseId: value.caseId,
    customer: value.customer,
    missing: value.missing as string[],
  };
}

function parseApprovalItem(value: unknown): ApprovalItem | null {
  if (
    !isRecord(value) ||
    typeof value.approvalId !== "string" ||
    typeof value.caseId !== "string" ||
    typeof value.title !== "string" ||
    typeof value.status !== "string" ||
    typeof value.href !== "string"
  )
    return null;
  return {
    approvalId: value.approvalId,
    caseId: value.caseId,
    title: value.title,
    status: value.status,
    href: value.href,
  };
}

function parseProposedAction(value: unknown): ProposedAction | null {
  if (
    !isRecord(value) ||
    typeof value.label !== "string" ||
    typeof value.description !== "string" ||
    typeof value.targetHref !== "string" ||
    value.requiresHumanApproval !== true // contract invariant: ALWAYS true
  )
    return null;
  return {
    label: value.label,
    description: value.description,
    targetHref: value.targetHref,
    requiresHumanApproval: true,
  };
}

function parseEvidenceSource(value: unknown): EvidenceSource | null {
  if (
    !isRecord(value) ||
    typeof value.label !== "string" ||
    typeof value.documentName !== "string" ||
    typeof value.ref !== "string"
  )
    return null;
  return {
    label: value.label,
    documentName: value.documentName,
    ref: value.ref,
  };
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null;
}
