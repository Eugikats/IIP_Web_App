// Design language v2. This is the most widely shared legacy component file in the portal (38
// screens import from it) — migrated once, here, rather than patched per screen. Every exported
// signature is unchanged so no caller needs to change.
import { cn } from "@/lib/utils";
import type { ReactNode } from "react";
import { StatusPill, type PillTone } from "@/components/ui/StatusPill";

// `level` is kept for API compatibility (two not-yet-migrated screens pass level={3}) but design
// language v2 has no multi-tier elevation concept — every static card is a flat, bordered,
// shadowless surface (craft guardrails §8: "box-shadow: none on every static card").
export function GlassCard({ className, children }: { className?: string; children: ReactNode; level?: 1 | 2 | 3 }) {
  return <div className={cn("rounded-dls-card border border-dls-border-subtle bg-dls-surface p-6 shadow-dls-none", className)}>{children}</div>;
}

export function SectionHeader({
  eyebrow,
  eyebrowClassName,
  title,
  sub,
  action,
}: {
  eyebrow?: string;
  // Escape hatch for a single screen's eyebrow to use a different, already-AA-compliant token
  // instead of the default text-dls-text-muted (see the /app/claims "Claims Intelligence" label,
  // gate-17) without changing the shared muted token every other of the ~30 eyebrow callers uses.
  eyebrowClassName?: string;
  title: string;
  sub?: string;
  action?: ReactNode;
}) {
  return (
    <div className="mb-6 flex min-w-0 flex-col items-start justify-between gap-4 sm:flex-row sm:items-end">
      <div className="min-w-0">
        {eyebrow && (
          <div className={cn("mb-2 text-[11px] font-bold uppercase tracking-[0.05em]", eyebrowClassName ?? "text-dls-text-muted")}>
            {eyebrow}
          </div>
        )}
        <h1 className="break-words text-[26px] font-extrabold leading-[34px] text-dls-text-primary sm:text-[28px] sm:leading-[36px]">{title}</h1>
        {sub && <p className="mt-1.5 max-w-2xl break-words text-sm text-dls-text-secondary">{sub}</p>}
      </div>
      {action ? <div className="w-full min-w-0 sm:w-auto">{action}</div> : null}
    </div>
  );
}

// Kept for API compatibility only — every new caller should use components/ui/StatusPill
// directly. `error` maps to StatusPill's `danger`; `violet` (never a valid dot colour per design
// language §8.7 — violet marks actions, not status) maps to `info`, the closest neutral-ish
// semantic tone, so any surviving caller degrades to something plausible rather than crashing.
type Tone = "info" | "success" | "warning" | "error" | "violet" | "neutral";
const TONE_TO_PILL: Record<Tone, PillTone> = {
  info: "info",
  success: "success",
  warning: "warning",
  error: "danger",
  violet: "info",
  neutral: "neutral",
};

const DOT_CLASS: Record<PillTone, string> = {
  neutral: "bg-dls-text-faint",
  success: "bg-dls-success-dot",
  warning: "bg-dls-warning-dot",
  danger: "bg-dls-danger-dot",
  info: "bg-dls-info-dot",
};

// Badge takes arbitrary `children` (StatusPill takes only a string `label`), so it cannot simply
// delegate — this renders the same neutral-pill-plus-dot shape by hand.
export function Badge({ tone = "neutral", children, className }: { tone?: Tone; children: ReactNode; className?: string }) {
  return (
    <span className={cn("inline-flex items-center gap-1.5 rounded-dls-pill border border-dls-border-pill px-2.5 py-[3px] text-xs font-medium text-dls-text-primary", className)}>
      <span className={cn("h-1.5 w-1.5 shrink-0 rounded-full", DOT_CLASS[TONE_TO_PILL[tone]])} aria-hidden="true" />
      {children}
    </span>
  );
}

export function Dot({ tone = "info" }: { tone?: Tone }) {
  return <span className={cn("inline-block h-1.5 w-1.5 rounded-full", DOT_CLASS[TONE_TO_PILL[tone]])} aria-hidden="true" />;
}

// Status vocabulary mirrors iip_workflow_legibility_contract.md §10.1-10.3 exactly for the raw
// Domain enum names it covers (CaseStatus, AgentWorkItemStatus, ApprovalStatus). Every other value
// this is called with (agent-flow run states, integration/provider states, document states, etc.)
// is outside that contract's scope and is not a raw enum identifier needing translation — it is
// passed through as its own label.
const STATUS_NEW = { label: "Received", tone: "info" } as const;
const STATUS_IN_REVIEW = { label: "Under review", tone: "info" } as const;
const STATUS_MISSING_DOCS = { label: "Waiting on documents", tone: "warning" } as const;
const STATUS_READY_FOR_REVIEW = { label: "Ready for review", tone: "info" } as const;
const STATUS_PENDING_APPROVAL = { label: "Awaiting approval", tone: "warning" } as const;
const STATUS_READY_FOR_EXPORT = { label: "Ready for export", tone: "info" } as const;
const STATUS_REQUIRES_REVIEW = { label: "Requires review", tone: "danger" } as const;
const STATUS_IN_PROGRESS = { label: "In progress", tone: "info" } as const;

const STATUS_VOCAB: Record<string, { label: string; tone: PillTone }> = {
  New: STATUS_NEW,
  Received: STATUS_NEW,
  Assigned: { label: "Assigned", tone: "info" },
  InReview: STATUS_IN_REVIEW,
  "In Review": STATUS_IN_REVIEW,
  MissingDocuments: STATUS_MISSING_DOCS,
  "Missing Documents": STATUS_MISSING_DOCS,
  "Awaiting Documents": STATUS_MISSING_DOCS,
  ReadyForReview: STATUS_READY_FOR_REVIEW,
  "Ready for Review": STATUS_READY_FOR_REVIEW,
  // Two case-detail phrasings this codebase uses for "a human needs to review this claim now" —
  // neither is a literal CaseStatus enum name, but both describe the same ReadyForReview state.
  "Human Review Required": STATUS_READY_FOR_REVIEW,
  "Needs Review": STATUS_READY_FOR_REVIEW,
  PendingApproval: STATUS_PENDING_APPROVAL,
  "Pending Approval": STATUS_PENDING_APPROVAL,
  ReadyForExport: STATUS_READY_FOR_EXPORT,
  "Ready for Export": STATUS_READY_FOR_EXPORT,
  Settled: { label: "Settled", tone: "success" },
  Closed: { label: "Closed", tone: "neutral" },
  RequiresReview: STATUS_REQUIRES_REVIEW,
  "Requires Review": STATUS_REQUIRES_REVIEW,
  InProgress: STATUS_IN_PROGRESS,
  "In Progress": STATUS_IN_PROGRESS,
  Completed: { label: "Completed", tone: "success" },
  ReturnedForCorrection: { label: "Returned for correction", tone: "warning" },
  Cancelled: { label: "Cancelled", tone: "neutral" },
  Escalated: { label: "Escalated", tone: "danger" },
  Pending: { label: "Awaiting decision", tone: "warning" },
  Approved: { label: "Approved", tone: "success" },
  Rejected: { label: "Rejected", tone: "danger" },
  Overridden: { label: "Overridden", tone: "warning" },
  OnTrack: { label: "On track", tone: "success" },
  AtRisk: { label: "At risk", tone: "warning" },
  Breached: { label: "Breached", tone: "danger" },
};

// Non-contract statuses (agent flows, integrations, documents, etc.) that already read as plain
// English keep their own text; only the tone is decided here.
const OTHER_STATUS_TONE: Record<string, PillTone> = {
  Error: "danger",
  Failed: "danger",
  Degraded: "warning",
  Paused: "neutral",
  Draft: "neutral",
  Published: "success",
  Queued: "info",
  Running: "info",
  WaitingForHuman: "warning",
  Skipped: "neutral",
  Open: "warning",
  Acknowledged: "info",
  Resolved: "success",
  Connected: "success",
  Active: "success",
  Operational: "success",
};

export function StatusBadge({ status }: { status: string }) {
  const contract = STATUS_VOCAB[status];
  if (contract) return <StatusPill tone={contract.tone} label={contract.label} />;
  return <StatusPill tone={OTHER_STATUS_TONE[status] ?? "neutral"} label={status} />;
}

export function SLABadge({ sla }: { sla: string }) {
  const map: Record<string, PillTone> = { Normal: "success", "At Risk": "warning", Breached: "danger", OnTrack: "success", AtRisk: "warning" };
  return <StatusPill tone={map[sla] ?? "neutral"} label={`SLA · ${sla}`} />;
}

export function RiskBadge({ risk }: { risk: string }) {
  const map: Record<string, PillTone> = { Low: "success", Medium: "warning", High: "danger", "Advisory Signal": "info" };
  return <StatusPill tone={map[risk] ?? "neutral"} label={`Risk · ${risk}`} />;
}

export function ConfidenceBadge({ value }: { value: number }) {
  // Design language v2 §8.22 — never a coloured bar alone: numeric value plus a word.
  const pct = Math.round(value * 100);
  const tone: PillTone = pct >= 85 ? "success" : pct >= 70 ? "info" : "warning";
  const label = pct >= 85 ? "High" : pct >= 70 ? "Medium" : "Low";
  return <StatusPill tone={tone} label={`${(value).toFixed(2)} · ${label}`} />;
}
