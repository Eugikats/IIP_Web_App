// Design language v2 §8.7, the single most important consistency rule in the product: status is
// a neutral pill + a 6px coloured dot + a word. The text is never coloured, the pill is never
// tinted (the one exception is the "positive confirmation of a configured capability" variant).
import { cn } from "@/lib/utils";

export type PillTone = "neutral" | "success" | "warning" | "danger" | "info";

const dotClass: Record<PillTone, string> = {
  neutral: "bg-dls-text-faint",
  success: "bg-dls-success-dot",
  warning: "bg-dls-warning-dot",
  danger: "bg-dls-danger-dot",
  info: "bg-dls-info-dot",
};

export interface StatusPillProps {
  tone: PillTone;
  label: string;
  /** The one permitted tinted variant, a positive confirmation of a configured capability. */
  confirmed?: boolean;
  className?: string;
}

export function StatusPill({ tone, label, confirmed, className }: StatusPillProps) {
  const confirmedTint = confirmed && tone === "success";
  return (
    <span
      className={cn(
        "inline-flex max-w-full items-center gap-1.5 rounded-dls-pill border px-2.5 py-[3px] text-xs font-medium leading-tight",
        confirmedTint
          ? "border-dls-border-pill bg-dls-success-tint text-dls-success-text"
          : "border-dls-border-pill bg-dls-surface text-dls-text-primary",
        className,
      )}
    >
      <span className={cn("h-1.5 w-1.5 shrink-0 rounded-full", dotClass[tone])} aria-hidden="true" />
      <span className="min-w-0 truncate">{label}</span>
    </span>
  );
}
