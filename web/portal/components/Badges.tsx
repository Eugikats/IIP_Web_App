import { AlertTriangle, CheckCircle2, Circle, Info, Sparkles, XCircle } from "lucide-react";

type BadgeTone = "neutral" | "success" | "warning" | "danger" | "info" | "ai";

// Token-based tones so badges adapt to light/dark automatically.
const toneClass: Record<BadgeTone, string> = {
  neutral: "bg-mist text-muted border-line",
  success: "bg-success/10 text-success border-success/30",
  warning: "bg-warning/10 text-warning-text border-warning/30",
  danger:  "bg-danger/10 text-danger border-danger/30",
  info:    "bg-info/10 text-info border-info/30",
  ai:      "bg-violet/10 text-violet border-violet/30",
};

const iconClass: Record<BadgeTone, string> = {
  neutral: "text-muted-soft",
  success: "text-success",
  warning: "text-warning",
  danger:  "text-danger",
  info:    "text-info",
  ai:      "text-violet",
};

const IconForTone: Record<BadgeTone, typeof Circle> = {
  neutral: Circle,
  success: CheckCircle2,
  warning: AlertTriangle,
  danger: XCircle,
  info: Info,
  ai: Sparkles,
};

function Badge({ tone, label }: { tone: BadgeTone; label: string }) {
  const Icon = IconForTone[tone];

  return (
    <span
      className={`inline-flex max-w-full items-center gap-1.5 rounded-full border px-2.5 py-1 text-[10px] font-bold uppercase tracking-[0.10em] leading-tight ${toneClass[tone]}`}
    >
      <Icon className={`h-3 w-3 flex-shrink-0 ${iconClass[tone]}`} aria-hidden="true" />
      <span className="min-w-0 break-words">{label}</span>
    </span>
  );
}

export function StatusBadge({ label }: { label?: string }) {
  if (!label) return null;
  const normalized = label.toLowerCase();

  const tone: BadgeTone =
    normalized.includes("missing") || normalized.includes("pending") || normalized.includes("awaiting") ||
      normalized.includes("draft") || normalized.includes("mock") || normalized.includes("not configured") ||
      normalized.includes("degraded") || normalized.includes("not_ready") || normalized.includes("not ready") ||
      normalized.includes("blocked") || normalized.includes("needs review") || normalized.includes("needsreview")
      ? "warning"
    : normalized.includes("ready") || normalized.includes("approved") || normalized.includes("sent") ||
      normalized.includes("enabled") || normalized.includes("healthy") || normalized.includes("indexed") ||
      normalized.includes("connected") || normalized.includes("configured") || normalized.includes("corrected")
      ? "success"
    : normalized.includes("rejected") || normalized.includes("breached") || normalized.includes("disabled") ||
      normalized.includes("failed") || normalized.includes("escalated")
      ? "danger"
    : "info";

  return <Badge tone={tone} label={label} />;
}

export function RiskBadge({ label }: { label?: string }) {
  if (!label) return null;
  const normalized = label.toLowerCase();
  const tone: BadgeTone =
    normalized.includes("critical") || normalized.includes("high")
      ? "danger"
      : normalized.includes("medium")
      ? "warning"
      : "neutral";
  return <Badge tone={tone} label={label} />;
}

export function SlaBadge({ label }: { label?: string }) {
  if (!label) return null;
  const tone: BadgeTone =
    label === "Breached" ? "danger" : label === "AtRisk" ? "warning" : "success";
  return <Badge tone={tone} label={label} />;
}
