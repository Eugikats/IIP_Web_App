// Migrated onto components/ui/StatusPill (design language v2 §8.7): a neutral pill, a 6px
// coloured dot, and a word, never a tinted pill, never coloured pill text. External API
// unchanged so every existing caller keeps working.
import { StatusPill, type PillTone } from "@/components/ui/StatusPill";

export function StatusBadge({ label }: { label?: string }) {
  if (!label) return null;
  const normalized = label.toLowerCase();

  const tone: PillTone =
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

  // The one permitted tinted variant: a positive confirmation of a configured capability.
  const confirmed = tone === "success" && (normalized.includes("enabled") || normalized.includes("connected") || normalized.includes("configured"));

  return <StatusPill tone={tone} label={label} confirmed={confirmed} />;
}

export function RiskBadge({ label }: { label?: string }) {
  if (!label) return null;
  const normalized = label.toLowerCase();
  const tone: PillTone =
    normalized.includes("critical") || normalized.includes("high")
      ? "danger"
      : normalized.includes("medium")
      ? "warning"
      : "neutral";
  return <StatusPill tone={tone} label={label} />;
}

export function SlaBadge({ label }: { label?: string }) {
  if (!label) return null;
  const tone: PillTone = label === "Breached" ? "danger" : label === "AtRisk" ? "warning" : "success";
  return <StatusPill tone={tone} label={label} />;
}
