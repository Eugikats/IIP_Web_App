import { cn } from "@/lib/utils";
import type { ReactNode } from "react";

export function GlassCard({ className, children, level = 2 }: { className?: string; children: ReactNode; level?: 1 | 2 | 3 }) {
  const map = { 1: "glass-subtle", 2: "glass", 3: "glass-elevated" } as const;
  return <div className={cn(map[level], "p-6", className)}>{children}</div>;
}

export function SectionHeader({ eyebrow, title, sub, action }: { eyebrow?: string; title: string; sub?: string; action?: ReactNode }) {
  return (
    <div className="flex items-end justify-between gap-4 mb-6">
      <div>
        {eyebrow && <div className="text-[11px] uppercase tracking-[0.14em] text-stone mb-2">{eyebrow}</div>}
        <h1 className="text-[28px] leading-[36px] font-semibold text-navy">{title}</h1>
        {sub && <p className="text-sm text-stone mt-1.5 max-w-2xl">{sub}</p>}
      </div>
      {action}
    </div>
  );
}

type Tone = "info" | "success" | "warning" | "error" | "violet" | "neutral";
const toneStyles: Record<Tone, string> = {
  info:    "bg-[hsl(var(--info)/0.08)]    text-[hsl(var(--info))]    ring-[hsl(var(--info)/0.2)]",
  success: "bg-[hsl(var(--success)/0.10)] text-[hsl(var(--success))] ring-[hsl(var(--success)/0.22)]",
  warning: "bg-[hsl(var(--warning)/0.12)] text-[hsl(38_92%_38%)]     ring-[hsl(var(--warning)/0.28)]",
  error:   "bg-[hsl(var(--error)/0.10)]   text-[hsl(var(--error))]   ring-[hsl(var(--error)/0.24)]",
  violet:  "bg-[hsl(var(--violet)/0.10)]  text-[hsl(259_60%_50%)]    ring-[hsl(var(--violet)/0.24)]",
  neutral: "bg-cloud/60 text-slate2 ring-cloud",
};

export function Badge({ tone = "neutral", children, className }: { tone?: Tone; children: ReactNode; className?: string }) {
  return (
    <span className={cn("inline-flex items-center gap-1.5 rounded-full px-2.5 py-1 text-[11px] font-medium ring-1 ring-inset", toneStyles[tone], className)}>
      {children}
    </span>
  );
}

export function Dot({ tone = "info" }: { tone?: Tone }) {
  const c = { info: "bg-info", success: "bg-success", warning: "bg-warning", error: "bg-error", violet: "bg-violet2", neutral: "bg-stone" }[tone];
  return <span className={cn("inline-block h-1.5 w-1.5 rounded-full", c)} />;
}

export function StatusBadge({ status }: { status: string }) {
  const map: Record<string, Tone> = {
    "New": "info",
    "Assigned": "info",
    "In Review": "info",
    "InReview": "info",
    "In Progress": "info",
    "Missing Documents": "warning",
    "MissingDocuments": "warning",
    "Needs Review": "warning",
    "Human Review Required": "violet",
    "PendingApproval": "violet",
    "Escalated": "error",
    "Error": "error",
    "Rejected": "error",
    "Paused": "neutral",
    "Ready for Export": "success",
    "ReadyForReview": "success",
    "ReadyForExport": "success",
    "Approved": "success",
    "Connected": "success",
    "Active": "success",
    "Operational": "success",
    "Pending": "warning",
    "Draft": "neutral",
    "Published": "success",
    "Queued": "info",
    "Running": "violet",
    "WaitingForHuman": "warning",
    "RequiresReview": "warning",
    "InProgress": "info",
    "Completed": "success",
    "ReturnedForCorrection": "warning",
    "Cancelled": "neutral",
    "Failed": "error",
    "Skipped": "neutral",
    "Degraded": "warning",
    "Closed": "neutral",
    "Open": "warning",
    "Acknowledged": "info",
    "Resolved": "success",
  };
  return <Badge tone={map[status] ?? "neutral"}><Dot tone={map[status] ?? "neutral"} />{status}</Badge>;
}

export function SLABadge({ sla }: { sla: string }) {
  const map: Record<string, Tone> = { Normal: "success", "At Risk": "warning", Breached: "error" };
  return <Badge tone={map[sla] ?? "neutral"}>SLA · {sla}</Badge>;
}

export function RiskBadge({ risk }: { risk: string }) {
  const map: Record<string, Tone> = { Low: "success", Medium: "warning", High: "error", "Advisory Signal": "violet" };
  return <Badge tone={map[risk] ?? "neutral"}>Risk · {risk}</Badge>;
}

export function ConfidenceBadge({ value }: { value: number }) {
  const pct = Math.round(value * 100);
  const tone: Tone = pct >= 85 ? "success" : pct >= 70 ? "info" : "warning";
  const label = pct >= 85 ? "High" : pct >= 70 ? "Medium" : "Low";
  return <Badge tone={tone}>Confidence · {label} · {pct}%</Badge>;
}
