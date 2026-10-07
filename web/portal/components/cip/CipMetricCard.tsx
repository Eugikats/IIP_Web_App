// Ported from the CIP web app (apps/web/components/dashboard/ui) so IIP and CIP share one look.
// Only the import of `cn` and a trend check that refuses to draw a flat line were changed.
import type { ComponentType, ReactNode } from "react";
import type { LucideProps } from "lucide-react";
import DashboardCard from "./DashboardCard";
import PremiumSparkline, { type PremiumSparklineTrend } from "./PremiumSparkline";
export type TrendDirection = "up" | "down" | "flat";
import { cn } from "@/lib/utils";
/** A trend is drawn only from a real series: at least two days, with some movement. An all-zero week
 *  says "No trend" rather than drawing a flat line that looks like data. */
export function hasSeriesData(values: number[] | null | undefined): boolean {
  if (!values || values.length < 2) return false;
  return values.some((value) => Number.isFinite(value) && value !== 0);
}

type MetricCardProps = {
  label: ReactNode;
  value: ReactNode;
  delta?: string;
  trendDirection?: TrendDirection;
  trendLabel?: string;
  positiveWhenDown?: boolean;
  sparklineData?: number[];
  helperText?: string;
  icon?: ComponentType<LucideProps>;
  accent?: "blue" | "violet" | "emerald" | "amber" | "slate";
  className?: string;
  /** Compact sparkline-first layout (no comparison copy / helper / icon chrome). */
  compact?: boolean;
};

function sparklineTrend(direction: TrendDirection, positiveWhenDown: boolean): PremiumSparklineTrend {
  if (direction === "flat") return "neutral";
  if (direction === "down") return positiveWhenDown ? "positive" : "negative";
  return "positive";
}

export default function MetricCard({
  label,
  value,
  delta,
  trendDirection = "up",
  trendLabel,
  positiveWhenDown = false,
  sparklineData,
  helperText,
  icon: Icon,
  accent = "violet",
  className,
  compact = false,
}: MetricCardProps) {
  if (compact) {
    return (
      <DashboardCard className={cn("flex h-full min-h-[5.5rem] flex-col justify-center", className)} padding="sm">
        <p className="text-[11px] font-semibold leading-4 text-slate-500">{label}</p>
        <div className="mt-1.5 flex items-end justify-between gap-3">
          <p className="min-w-0 whitespace-nowrap text-[20px] font-bold leading-none tracking-tight text-[#0B1640]">
            {value}
          </p>
          {hasSeriesData(sparklineData) ? (
            <PremiumSparkline
              data={sparklineData ?? []}
              trend={sparklineTrend(trendDirection, positiveWhenDown)}
              width={112}
              height={40}
              ariaLabel={`${String(label)} trend`}
            />
          ) : (
            <div className="flex h-10 w-[7rem] shrink-0 items-center justify-center rounded-md border border-dashed border-slate-200 text-[9px] font-semibold text-slate-400">
              No trend
            </div>
          )}
        </div>
        {helperText ? (
          <p className="mt-2 text-[11px] font-medium leading-snug text-slate-500">{helperText}</p>
        ) : null}
      </DashboardCard>
    );
  }

  return null;
}
