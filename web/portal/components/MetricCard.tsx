// Migrated onto the design language v2 §8.9 stat tile treatment: the card itself is always the
// same neutral surface (never a filled/tinted red, amber, or green card — §4.2); only the figure
// takes a semantic colour, and only when the number itself is the signal. External API unchanged.
import { type ReactNode } from "react";
import { cn } from "@/lib/utils";

const valueToneClass = {
  neutral: "text-dls-text-primary",
  warning: "text-dls-warning-text",
  danger: "text-dls-danger-text",
  success: "text-dls-success-text",
} as const;

export function MetricCard({
  label,
  value,
  detail,
  tone = "neutral",
  eyebrow,
}: {
  label: string;
  value: string | number;
  detail?: string;
  tone?: "neutral" | "warning" | "danger" | "success";
  eyebrow?: ReactNode;
}) {
  return (
    <div className="flex min-h-[124px] min-w-0 flex-col rounded-dls-card border border-dls-border-subtle bg-dls-surface p-4 shadow-dls-none">
      {eyebrow && (
        <p className="mb-1 truncate text-[11.5px] font-bold uppercase tracking-[0.05em] text-dls-text-muted">{eyebrow}</p>
      )}
      <p className="text-[13.5px] text-dls-text-secondary">{label}</p>
      <p className={cn("mt-2 text-[30px] font-bold leading-[1.1] tabular-nums tracking-[-0.5px] break-words", valueToneClass[tone])}>
        {value}
      </p>
      {detail && <p className="mt-auto line-clamp-2 pt-1.5 text-xs leading-relaxed text-dls-text-muted">{detail}</p>}
    </div>
  );
}
