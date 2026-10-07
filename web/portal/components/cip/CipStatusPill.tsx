// Ported from the CIP web app (apps/web/components/dashboard/ui) so IIP and CIP share one look.
// Only the import of `cn` and a trend check that refuses to draw a flat line were changed.
import type { ReactNode } from "react";
import { cn } from "@/lib/utils";

type StatusTone = "success" | "warning" | "danger" | "info" | "neutral" | "violet";

type CipStatusPillProps = {
  children: ReactNode;
  tone?: StatusTone;
  dot?: boolean;
  className?: string;
};

const toneClasses: Record<StatusTone, string> = {
  success: "border-emerald-100 bg-emerald-50 text-emerald-700",
  warning: "border-amber-100 bg-amber-50 text-amber-700",
  danger: "border-red-100 bg-red-50 text-red-700",
  info: "border-blue-100 bg-blue-50 text-blue-700",
  neutral: "border-slate-200 bg-slate-50 text-slate-600",
  violet: "border-sky-100 bg-sky-50 text-sky-700",
};

const dotClasses: Record<StatusTone, string> = {
  success: "bg-emerald-500",
  warning: "bg-amber-500",
  danger: "bg-red-500",
  info: "bg-blue-500",
  neutral: "bg-slate-400",
  violet: "bg-sky-500",
};

export default function CipStatusPill({
  children,
  tone = "neutral",
  dot = false,
  className,
}: CipStatusPillProps) {
  return (
    <span
      className={cn(
        "inline-flex items-center gap-1.5 whitespace-nowrap rounded-full border px-2.5 py-1 text-[11px] font-bold leading-none",
        toneClasses[tone],
        className
      )}
    >
      {dot ? <span className={cn("h-1.5 w-1.5 rounded-full", dotClasses[tone])} /> : null}
      {children}
    </span>
  );
}
