"use client";

import type { CommandCenterBlock, Tone } from "@/lib/commandCenterBlocks";

interface Props {
  block: Extract<CommandCenterBlock, { type: "metric_pills" }>;
}

const toneStyles: Record<Tone, { pill: string; value: string }> = {
  neutral: {
    pill: "border-line bg-surface",
    value: "text-ink",
  },
  info: {
    pill: "border-info/30 bg-info/10",
    value: "text-info",
  },
  success: {
    pill: "border-success/30 bg-success/10",
    value: "text-success",
  },
  warning: {
    pill: "border-warning/30 bg-warning/10",
    value: "text-warning",
  },
  danger: {
    pill: "border-danger/30 bg-danger/10",
    value: "text-danger",
  },
};

/**
 * Renders headline KPI pills — open claims, SLA at risk, pending approvals,
 * incomplete. Each pill is purely presentational; no action is taken from here.
 */
export function MetricPillsBlock({ block }: Props) {
  return (
    <div role="list" aria-label="Key metrics" className="flex flex-wrap gap-3">
      {block.items.map((pill) => {
        const styles = toneStyles[pill.tone];
        return (
          <div
            key={pill.label}
            role="listitem"
            className={`flex min-w-[120px] flex-col rounded-2xl border px-4 py-3 shadow-panel ${styles.pill}`}
          >
            <span className={`text-2xl font-bold tabular-nums leading-none ${styles.value}`}>
              {pill.value}
            </span>
            <span className="mt-1 text-xs font-semibold uppercase tracking-[0.10em] text-muted">
              {pill.label}
            </span>
            {pill.detail ? (
              <span className="mt-1.5 text-[11px] leading-relaxed text-muted-soft">
                {pill.detail}
              </span>
            ) : null}
          </div>
        );
      })}
    </div>
  );
}
