"use client";
import { ArrowRight, FileText, Sparkles } from "lucide-react";
import ChartCard from "@/components/command-center/ChartCard";
import { Link } from "@/components/iip/NextRouterCompat";
import { MarkdownText } from "@/components/iip/MarkdownText";
import type { CommandCenterBlock } from "@/lib/lovable-parity/data/commandCenterQuery";
import { safeInternalWorkHref } from "@/lib/workItemLinks";
import { claimReference, humanizeIdentifier, isGuid } from "@/lib/format";
import { usePeople } from "@/lib/people";
import { StatusPill, type PillTone } from "@/components/ui/StatusPill";
import { ErrorStateSurface, NotConfiguredStateSurface } from "@/components/ui/StateSurfaces";

// Status vocabulary mirrors iip_workflow_legibility_contract.md section 10.1 and 10.3 exactly. The
// backend sends the raw Domain enum name (CaseStatus.ToString()), never a display label, so every
// screen that shows one must translate it through the same table.
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

// SlaToken is the four-value token CommandCenterQueryPlanner.ResolveSla emits. due_today sits
// between on_track and at_risk and reads as a warning like at_risk does.
const SLA_TONE: Record<string, PillTone> = {
  breached: "danger",
  at_risk: "warning",
  due_today: "warning",
  on_track: "success",
};

const GUID_IN_TEXT = /\b[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}\b/gi;

// Answers saved before claim references existed can still carry a raw id in their prose. Every id
// the assistant writes about is a claim, so it is shown the way the rest of the product names one.
function withReadableReferences(text: string): string {
  return text.replace(GUID_IN_TEXT, (id) => claimReference(id));
}

function caseStatusPill(status: string) {
  const entry = CASE_STATUS_VOCAB[status];
  return <StatusPill tone={entry?.tone ?? "neutral"} label={entry?.label ?? humanizeIdentifier(status)} />;
}

function approvalStatusPill(status: string) {
  const entry = APPROVAL_STATUS_VOCAB[status];
  return <StatusPill tone={entry?.tone ?? "neutral"} label={entry?.label ?? humanizeIdentifier(status)} />;
}

const ROW_LINK =
  "dls-focus-outline group flex items-center gap-3 rounded-dls-tight px-1 py-2.5 transition-colors duration-dls-fast ease-dls hover:bg-dls-bg-page";

function ClaimHref(caseId: string, fallback?: string) {
  return safeInternalWorkHref(fallback ?? (isGuid(caseId) ? `/app/claims/${caseId}` : undefined), "/app/claims");
}

function RowChevron() {
  return <ArrowRight className="h-3.5 w-3.5 shrink-0 text-dls-text-muted transition-colors group-hover:text-dls-primary" aria-hidden="true" />;
}

// Renders one governed answer from POST /api/v1/command-center/query. Every block type has an
// explicit renderer; anything unknown falls through to a safe note (never raw JSON). Every row that
// names a claim is a link to that claim: an answer that lists work a person cannot open is a dead end.
function Block({ block, historical = false }: { block: CommandCenterBlock; historical?: boolean }) {
  const { describe } = usePeople();

  switch (block.type) {
    case "text":
      return (
        <MarkdownText
          className="text-[13.5px] leading-relaxed text-dls-text-secondary"
          content={withReadableReferences(String((block as { markdown?: string }).markdown ?? ""))}
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
            <Link key={r.caseId} to={ClaimHref(r.caseId, r.href)} className={ROW_LINK}>
              <div className="w-28 shrink-0 text-[12px] font-semibold tabular-nums text-dls-text-primary">{claimReference(r.caseId)}</div>
              <div className="min-w-0 flex-1">
                <div className="truncate text-[12.5px] text-dls-text-primary">{r.customer}</div>
                <div className="truncate text-[11px] text-dls-text-muted">
                  {r.productLine} · {r.assignedTo ? describe(r.assignedTo) : "Unassigned"}
                </div>
              </div>
              {caseStatusPill(r.status)}
              <RowChevron />
            </Link>
          ))}
        </div>
      );
    case "sla_summary":
      return (
        <div className="divide-y divide-dls-border-subtle">
          {(block as Extract<CommandCenterBlock, { type: "sla_summary" }>).items.map((s) => (
            <Link key={s.caseId} to={ClaimHref(s.caseId)} className={ROW_LINK}>
              <div className="w-28 shrink-0 text-[12px] font-semibold tabular-nums text-dls-text-primary">{claimReference(s.caseId)}</div>
              <div className="min-w-0 flex-1 truncate text-[12.5px] text-dls-text-primary">{s.customer}</div>
              <StatusPill tone={SLA_TONE[s.slaStatus] ?? "neutral"} label={s.dueLabel} />
              <RowChevron />
            </Link>
          ))}
        </div>
      );
    case "document_gap_summary":
      return (
        <div className="divide-y divide-dls-border-subtle">
          {(block as Extract<CommandCenterBlock, { type: "document_gap_summary" }>).items.map((d) => (
            <Link key={d.caseId} to={ClaimHref(d.caseId)} className={ROW_LINK}>
              <div className="w-28 shrink-0 text-[12px] font-semibold tabular-nums text-dls-text-primary">{claimReference(d.caseId)}</div>
              <div className="min-w-0 flex-1">
                <div className="truncate text-[12.5px] text-dls-text-primary">{d.customer}</div>
                <div className="text-[11.5px] text-dls-text-muted">Still needed: {d.missing.join(", ")}</div>
              </div>
              <RowChevron />
            </Link>
          ))}
        </div>
      );
    case "approval_queue":
      return (
        <div className="divide-y divide-dls-border-subtle">
          {(block as Extract<CommandCenterBlock, { type: "approval_queue" }>).items.map((a) => (
            <Link key={a.approvalId} to={safeInternalWorkHref(a.href, "/app/workflows")} className={ROW_LINK}>
              <div className="min-w-0 flex-1">
                <div className="truncate text-[12.5px] text-dls-text-primary">
                  {/\s/.test(a.title) ? withReadableReferences(a.title) : humanizeIdentifier(a.title)}
                </div>
                {a.caseId ? <div className="text-[11px] text-dls-text-muted">Claim {claimReference(a.caseId)}</div> : null}
              </div>
              {approvalStatusPill(a.status)}
              <RowChevron />
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
              {/* Section bodies are model prose from the Report Insight agent and carry the same
                  markdown the "text" block above already renders, so both are rendered the same way. */}
              <MarkdownText className="text-[12.5px] text-dls-text-secondary" content={withReadableReferences(s.body)} />
            </div>
          ))}
        </div>
      );
    case "draft_communication": {
      const b = block as Extract<CommandCenterBlock, { type: "draft_communication" }>;
      return (
        <div className="rounded-dls-card border border-dls-border-subtle bg-dls-surface p-3">
          <div className="mb-1.5 flex flex-wrap items-center gap-2">
            <Sparkles className="h-3.5 w-3.5 text-dls-primary" aria-hidden="true" />
            <span className="text-[11px] font-bold uppercase tracking-[0.05em] text-dls-text-muted">Draft message</span>
            {b.requiresApproval ? <StatusPill tone="warning" label="Not sent: needs approval" /> : null}
          </div>
          <div className="whitespace-pre-wrap text-[12.5px] text-dls-text-secondary">{b.preview}</div>
        </div>
      );
    }
    case "action_proposal":
      // Design language section 8.21, action card. Proposals that need a person's approval say so;
      // plain navigation (open a claim) does not wear an approval pill it has no reason to carry.
      return (
        <div className="space-y-2">
          {(block as Extract<CommandCenterBlock, { type: "action_proposal" }>).actions.map((a, i) => (
            <div key={i} className="rounded-dls-card border border-dls-border-subtle bg-dls-surface p-3.5">
              <div className="flex items-start justify-between gap-3">
                <div className="min-w-0">
                  <div className="text-[12.5px] font-semibold text-dls-text-primary">{withReadableReferences(a.label)}</div>
                  <div className="mt-0.5 text-[11.5px] text-dls-text-muted">{withReadableReferences(a.description)}</div>
                </div>
                {a.requiresHumanApproval && <StatusPill tone="warning" label="Requires approval" />}
              </div>
              <div className="mt-2.5">
                {historical ? (
                  <StatusPill tone="neutral" label="Earlier suggestion" />
                ) : (
                  <Link
                    to={safeInternalWorkHref(a.targetHref, "/app")}
                    className="dls-focus-outline inline-flex items-center justify-center gap-1.5 rounded-dls-control bg-dls-primary px-4 py-[9px] text-[13px] font-bold text-dls-text-inverse shadow-dls-primary transition-colors duration-dls-fast ease-dls hover:bg-dls-primary-hover active:bg-dls-primary-active"
                  >
                    {a.requiresHumanApproval ? "Review" : "Open"}
                  </Link>
                )}
              </div>
            </div>
          ))}
        </div>
      );
    case "evidence_list":
      // Design language section 8.20, citation chip. `ref` is an internal evidence reference, not a
      // URL, and there is no side drawer yet to open it into, so these render as labelled chips.
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
      return <NotConfiguredStateSurface what={`${b.provider}. ${b.adminAction}`} />;
    }
    case "chart":
      return <ChartCard block={block as Extract<CommandCenterBlock, { type: "chart" }>} />;
    case "credits_notice": {
      const b = block as Extract<CommandCenterBlock, { type: "credits_notice" }>;
      return (
        <div role="status" className="rounded-xl border border-amber-100 bg-amber-50 px-3.5 py-3 text-[13px] leading-5 text-amber-900">
          <p className="m-0 font-semibold">AI credits are used up</p>
          <p className="m-0 mt-0.5">{b.message}</p>
        </div>
      );
    }
    case "link": {
      const b = block as Extract<CommandCenterBlock, { type: "link" }>;
      return (
        <Link
          to={safeInternalWorkHref(b.href, "/app")}
          className="group inline-flex items-center gap-2 rounded-full bg-[#1769FF] px-4 py-2 text-[13px] font-semibold text-white shadow-[0_6px_16px_rgba(23,105,255,0.3)] transition-colors hover:bg-[#1258d6]"
        >
          {b.label}
          <ArrowRight className="h-3.5 w-3.5 transition-transform group-hover:translate-x-0.5" aria-hidden="true" />
        </Link>
      );
    }
    case "error": {
      const b = block as Extract<CommandCenterBlock, { type: "error" }>;
      return <ErrorStateSurface message={b.safeMessage} correlationId={b.correlationId} />;
    }
    default:
      return <div className="text-[12px] italic text-dls-text-muted">This part of the answer could not be shown.</div>;
  }
}

// Renders just the governed answer blocks (no outer card), used inside chat bubbles.
export function CommandCenterBlocks({ blocks, historical = false }: { blocks: CommandCenterBlock[]; historical?: boolean }) {
  return <div className="space-y-4">{blocks.map((b, i) => <Block key={i} block={b} historical={historical} />)}</div>;
}
