"use client";
import { FileText, Sparkles } from "lucide-react";
import { Link } from "@/components/iip/NextRouterCompat";
import { MarkdownText } from "@/components/iip/MarkdownText";
import type { CommandCenterBlock } from "@/lib/lovable-parity/data/commandCenterQuery";
import { safeInternalWorkHref } from "@/lib/workItemLinks";
import { StatusPill, type PillTone } from "@/components/ui/StatusPill";
import { ErrorStateSurface, NotConfiguredStateSurface } from "@/components/ui/StateSurfaces";

// Status vocabulary mirrors iip_workflow_legibility_contract.md §10.1/§10.3 exactly — the backend
// sends the raw Domain enum name (e.g. CaseStatus.ToString()), never a display label, so every
// screen that shows one must translate it through the same table. Keep this in sync with the
// contract; do not invent alternate labels.
const CASE_STATUS_VOCAB: Record<string, { label: string; tone: PillTone }> = {
  New: { label: "Received", tone: "info" },
  Assigned: { label: "Assigned", tone: "info" },
  InReview: { label: "Under review", tone: "info" },
  MissingDocuments: { label: "Waiting on documents", tone: "warning" },
  ReadyForReview: { label: "Ready for review", tone: "info" },
  PendingApproval: { label: "Awaiting approval", tone: "warning" },
  ReadyForExport: { label: "Ready for export", tone: "info" },
  Escalated: { label: "Escalated", tone: "danger" },
  Settled: { label: "Settled", tone: "success" },
  Closed: { label: "Closed", tone: "neutral" },
};

const APPROVAL_STATUS_VOCAB: Record<string, { label: string; tone: PillTone }> = {
  Pending: { label: "Awaiting decision", tone: "warning" },
  Approved: { label: "Approved", tone: "success" },
  Rejected: { label: "Rejected", tone: "danger" },
  Overridden: { label: "Overridden", tone: "warning" },
  Escalated: { label: "Escalated", tone: "danger" },
};

// SlaToken is the four-value token CommandCenterQueryPlanner.ResolveSla emits (not the three-value
// SlaStatus enum in the contract) — due_today sits between on_track and at_risk and reads as a
// warning like at_risk does.
const SLA_TONE: Record<string, PillTone> = {
  breached: "danger",
  at_risk: "warning",
  due_today: "warning",
  on_track: "success",
};

function caseStatusPill(status: string) {
  const entry = CASE_STATUS_VOCAB[status];
  return <StatusPill tone={entry?.tone ?? "neutral"} label={entry?.label ?? status} />;
}

function approvalStatusPill(status: string) {
  const entry = APPROVAL_STATUS_VOCAB[status];
  return <StatusPill tone={entry?.tone ?? "neutral"} label={entry?.label ?? status} />;
}

// Renders one governed answer from POST /api/v1/command-center/query. Every block type has an
// explicit renderer; anything unknown falls through to a safe "unsupported" note (never raw JSON).
function Block({ block, historical = false }: { block: CommandCenterBlock; historical?: boolean }) {
  switch (block.type) {
    case "text":
      return (
        <MarkdownText
          className="text-[13.5px] leading-relaxed text-dls-text-secondary"
          content={String((block as { markdown?: string }).markdown ?? "")}
        />
      );
    case "metric_pills":
      return (
        <div className="grid grid-cols-2 gap-2.5 md:grid-cols-4">
          {(block as Extract<CommandCenterBlock, { type: "metric_pills" }>).items.map((m, i) => (
            <div key={i} className="rounded-dls-card border border-dls-border-subtle bg-dls-surface p-3">
              <div className="text-[11px] text-dls-text-muted">{m.label}</div>
              <div className="mt-1 text-[20px] font-bold tabular-nums text-dls-text-primary">{m.value}</div>
              {m.detail ? <div className="mt-0.5 text-[10.5px] text-dls-text-muted">{m.detail}</div> : null}
            </div>
          ))}
        </div>
      );
    case "claims_table":
      return (
        <div className="divide-y divide-dls-border-subtle">
          {(block as Extract<CommandCenterBlock, { type: "claims_table" }>).rows.map((r) => (
            <Link
              key={r.caseId}
              to={safeInternalWorkHref(r.href, "/app/claims")}
              className="dls-focus-outline flex items-center gap-3 rounded-dls-tight px-1 py-2.5 hover:bg-dls-bg-page"
            >
              <div className="w-28 shrink-0 truncate text-[12px] font-semibold text-dls-text-primary">{r.caseId}</div>
              <div className="min-w-0 flex-1 truncate text-[12.5px] text-dls-text-primary">{r.customer}</div>
              <div className="hidden text-[11.5px] text-dls-text-muted md:block">{r.productLine}</div>
              {caseStatusPill(r.status)}
            </Link>
          ))}
        </div>
      );
    case "sla_summary":
      return (
        <div className="space-y-1.5">
          {(block as Extract<CommandCenterBlock, { type: "sla_summary" }>).items.map((s) => (
            <div key={s.caseId} className="flex items-center justify-between text-[12.5px]">
              <span className="text-dls-text-primary">{s.caseId} · {s.customer}</span>
              <StatusPill tone={SLA_TONE[s.slaStatus] ?? "neutral"} label={s.dueLabel} />
            </div>
          ))}
        </div>
      );
    case "document_gap_summary":
      return (
        <div className="space-y-2">
          {(block as Extract<CommandCenterBlock, { type: "document_gap_summary" }>).items.map((d) => (
            <div key={d.caseId} className="text-[12.5px]">
              <span className="font-semibold text-dls-text-primary">{d.caseId} · {d.customer}</span>
              <span className="text-dls-text-muted"> — missing: {d.missing.join(", ")}</span>
            </div>
          ))}
        </div>
      );
    case "approval_queue":
      return (
        <div className="divide-y divide-dls-border-subtle">
          {(block as Extract<CommandCenterBlock, { type: "approval_queue" }>).items.map((a) => (
            <Link
              key={a.approvalId}
              to={safeInternalWorkHref(a.href, "/app/work-items")}
              className="dls-focus-outline flex items-center justify-between rounded-dls-tight px-1 py-2.5 hover:bg-dls-bg-page"
            >
              <span className="text-[12.5px] text-dls-text-primary">{a.title}</span>
              {approvalStatusPill(a.status)}
            </Link>
          ))}
        </div>
      );
    case "management_summary":
      return (
        <div>
          <div className="mb-2 text-[13px] font-bold text-dls-text-primary">
            {(block as Extract<CommandCenterBlock, { type: "management_summary" }>).title}
          </div>
          {(block as Extract<CommandCenterBlock, { type: "management_summary" }>).sections.map((s, i) => (
            <div key={i} className="mb-2">
              <div className="text-[12px] font-semibold text-dls-text-primary">{s.heading}</div>
              <div className="text-[12.5px] text-dls-text-secondary">{s.body}</div>
            </div>
          ))}
        </div>
      );
    case "draft_communication": {
      const b = block as Extract<CommandCenterBlock, { type: "draft_communication" }>;
      return (
        <div className="rounded-dls-card border border-dls-border-subtle bg-dls-surface p-3">
          <div className="mb-1.5 flex items-center gap-2">
            <Sparkles className="h-3.5 w-3.5 text-dls-primary" aria-hidden="true" />
            <span className="text-[11px] font-bold uppercase tracking-[0.05em] text-dls-text-muted">Draft communication</span>
            {b.requiresApproval ? <StatusPill tone="warning" label="Human approval required" /> : null}
          </div>
          <div className="whitespace-pre-wrap text-[12.5px] text-dls-text-secondary">{b.preview}</div>
        </div>
      );
    }
    case "action_proposal":
      // Design language §8.21 — action card. targetHref/requiresHumanApproval are the only object
      // facts this block type carries; there is no separate object-status field to show alongside
      // the approval pill without a data-shape change (out of scope for a presentation migration).
      return (
        <div className="space-y-2">
          {(block as Extract<CommandCenterBlock, { type: "action_proposal" }>).actions.map((a, i) => (
            <div key={i} className="rounded-dls-card border border-dls-border-subtle bg-dls-surface p-3.5">
              <div className="flex items-start justify-between gap-3">
                <div className="min-w-0">
                  <div className="text-[12.5px] font-semibold text-dls-text-primary">{a.label}</div>
                  <div className="mt-0.5 text-[11.5px] text-dls-text-muted">{a.description}</div>
                </div>
                {a.requiresHumanApproval && <StatusPill tone="warning" label="Requires approval" />}
              </div>
              <div className="mt-2.5">
                {historical ? (
                  <StatusPill tone="neutral" label="Historical proposal" />
                ) : (
                  <Link
                    to={safeInternalWorkHref(a.targetHref, "/app")}
                    className="dls-focus-outline inline-flex items-center justify-center gap-1.5 rounded-dls-control bg-dls-primary px-4 py-[9px] text-[13px] font-bold text-dls-text-inverse shadow-dls-primary transition-colors duration-dls-fast ease-dls hover:bg-dls-primary-hover active:bg-dls-primary-active"
                  >
                    Review
                  </Link>
                )}
              </div>
            </div>
          ))}
        </div>
      );
    case "evidence_list":
      // Design language §8.20 — citation chip. Visual contract only: `ref` is an internal evidence
      // reference, not a URL, and there is no side-drawer primitive (§8.16) yet to open it into, so
      // these render as labelled, non-interactive chips rather than a fabricated click target.
      return (
        <div className="flex flex-wrap gap-1.5">
          {(block as Extract<CommandCenterBlock, { type: "evidence_list" }>).sources.map((s, i) => (
            <span
              key={i}
              className="inline-flex items-center gap-1.5 rounded-dls-pill border border-dls-border-subtle bg-dls-bg-page px-2 py-0.5 text-[11.5px] font-semibold text-dls-text-secondary"
            >
              <FileText className="h-3 w-3 text-dls-text-muted" aria-hidden="true" />
              {s.label ? `${s.label}: ${s.documentName}` : s.documentName}
            </span>
          ))}
        </div>
      );
    case "not_configured": {
      const b = block as Extract<CommandCenterBlock, { type: "not_configured" }>;
      return <NotConfiguredStateSurface what={`${b.provider} — ${b.adminAction}`} />;
    }
    case "error": {
      const b = block as Extract<CommandCenterBlock, { type: "error" }>;
      return <ErrorStateSurface message={b.safeMessage} correlationId={b.correlationId} />;
    }
    default:
      return <div className="text-[12px] italic text-dls-text-muted">Unsupported response block ({String(block.type)}) — nothing rendered.</div>;
  }
}

// Renders just the governed answer blocks (no outer card) — used inside chat bubbles.
export function CommandCenterBlocks({ blocks, historical = false }: { blocks: CommandCenterBlock[]; historical?: boolean }) {
  return <div className="space-y-4">{blocks.map((b, i) => <Block key={i} block={b} historical={historical} />)}</div>;
}
