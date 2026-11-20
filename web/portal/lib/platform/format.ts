/** Pure display helpers for the organisation platform pages. */

export type PillTone = "success" | "warning" | "danger" | "info" | "neutral" | "violet";

const AUTOMATION_STATUS: Record<string, { label: string; tone: PillTone }> = {
  draft: { label: "Draft", tone: "neutral" },
  tested: { label: "Tested", tone: "info" },
  awaiting_approval: { label: "Awaiting approval", tone: "warning" },
  approved: { label: "Approved", tone: "violet" },
  active: { label: "Active", tone: "success" },
  paused: { label: "Paused", tone: "neutral" },
  failed: { label: "Failed", tone: "danger" },
  retired: { label: "Retired", tone: "neutral" },
};

export function automationStatus(status: string): { label: string; tone: PillTone } {
  return AUTOMATION_STATUS[status] ?? { label: humanize(status), tone: "neutral" };
}

const WORKSPACE_STATUS: Record<string, { label: string; tone: PillTone }> = {
  live: { label: "Live", tone: "success" },
  awaiting_data: { label: "Awaiting data", tone: "warning" },
  draft: { label: "Draft", tone: "neutral" },
  archived: { label: "Archived", tone: "neutral" },
};

export function workspaceStatus(status: string): { label: string; tone: PillTone } {
  return WORKSPACE_STATUS[status] ?? { label: humanize(status), tone: "neutral" };
}

const INTENT_HEALTH: Record<string, { label: string; tone: PillTone; bar: string }> = {
  on_track: { label: "On track", tone: "success", bar: "bg-dls-success-dot" },
  at_risk: { label: "At risk", tone: "warning", bar: "bg-dls-warning-dot" },
  off_track: { label: "Off track", tone: "danger", bar: "bg-dls-danger-dot" },
};

export function intentHealth(health: string) {
  return INTENT_HEALTH[health] ?? INTENT_HEALTH.on_track;
}

export function severityTone(severity: string): PillTone {
  if (severity === "critical" || severity === "high") return "danger";
  if (severity === "medium") return "warning";
  return "info";
}

export const INBOX_TABS = [
  { key: "", label: "All open" },
  { key: "needs_attention", label: "Needs attention" },
  { key: "awaiting_approval", label: "Awaiting approval" },
  { key: "recommendation", label: "Recommendations" },
  { key: "monitoring", label: "Monitoring" },
  { key: "resolved", label: "Resolved" },
] as const;

const ACTION_LABELS: Record<string, string> = {
  test: "Run test",
  run: "Run now",
  submit: "Submit for approval",
  approve: "Approve",
  reject: "Send back",
  activate: "Activate",
  pause: "Pause",
  resume: "Resume",
  retire: "Retire",
  acknowledge: "Acknowledge",
  resolve: "Resolve",
  dismiss: "Dismiss",
  reopen: "Reopen",
};

export function actionLabel(action: string): string {
  return ACTION_LABELS[action] ?? humanize(action);
}

/** Steps of the automation lifecycle, for the progress rail on the detail page. */
export const LIFECYCLE = ["draft", "tested", "awaiting_approval", "approved", "active"] as const;

export function lifecycleIndex(status: string): number {
  const i = (LIFECYCLE as readonly string[]).indexOf(status);
  if (i >= 0) return i;
  if (status === "paused" || status === "failed") return LIFECYCLE.length - 1;
  return -1;
}

export function humanize(value: string | null | undefined): string {
  if (!value) return "";
  const text = value.replace(/[_-]+/g, " ").trim();
  return text.charAt(0).toUpperCase() + text.slice(1);
}

export function scheduleLabel(trigger: { type?: string; schedule?: string; time?: string } | null | undefined): string {
  if (!trigger) return "Manual";
  if (trigger.type === "manual") return "Manual";
  const schedule = trigger.schedule ?? "daily";
  if (schedule === "hourly") return "Every hour";
  if (schedule === "weekly") return `Mondays at ${trigger.time ?? "07:00"} UTC`;
  return `Daily at ${trigger.time ?? "07:00"} UTC`;
}

export function formatWhen(value: string | null | undefined, now: Date = new Date()): string {
  if (!value) return "Not yet";
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return "Not yet";
  const diff = (now.getTime() - date.getTime()) / 1000;
  const future = diff < 0;
  const abs = Math.abs(diff);
  if (abs < 60) return future ? "in a moment" : "just now";
  if (abs < 3600) return future ? `in ${Math.round(abs / 60)} min` : `${Math.round(abs / 60)} min ago`;
  if (abs < 86400) return future ? `in ${Math.round(abs / 3600)} h` : `${Math.round(abs / 3600)} h ago`;
  return date.toLocaleDateString(undefined, { day: "numeric", month: "short", year: date.getFullYear() === now.getFullYear() ? undefined : "numeric" });
}

/** Groups items by a key, keeping first-seen order of the groups. */
export function groupBy<T>(items: readonly T[], key: (item: T) => string): [string, T[]][] {
  const map = new Map<string, T[]>();
  for (const item of items) {
    const k = key(item);
    const list = map.get(k);
    if (list) list.push(item);
    else map.set(k, [item]);
  }
  return [...map.entries()];
}

export function greeting(now: Date = new Date()): string {
  const h = now.getHours();
  if (h < 12) return "Good morning";
  if (h < 17) return "Good afternoon";
  return "Good evening";
}
