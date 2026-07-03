"use client";

import Link from "next/link";
import type { CommandCenterBlock, SlaStatus } from "@/lib/commandCenterBlocks";
import { Clock, AlertTriangle, XCircle, CheckCircle2 } from "lucide-react";

interface Props {
  block: Extract<CommandCenterBlock, { type: "sla_summary" }>;
}

const slaConfig: Record<
  SlaStatus,
  { Icon: typeof Clock; rowClass: string; chipClass: string; label: string }
> = {
  on_track: {
    Icon: CheckCircle2,
    rowClass: "border-success/20 bg-success/5",
    chipClass: "text-success",
    label: "On Track",
  },
  due_today: {
    Icon: Clock,
    rowClass: "border-warning/20 bg-warning/5",
    chipClass: "text-warning",
    label: "Due Today",
  },
  at_risk: {
    Icon: AlertTriangle,
    rowClass: "border-warning/30 bg-warning/10",
    chipClass: "text-warning font-semibold",
    label: "At Risk",
  },
  breached: {
    Icon: XCircle,
    rowClass: "border-danger/30 bg-danger/10",
    chipClass: "text-danger font-semibold",
    label: "Breached",
  },
};

/**
 * Renders the SLA risk list. Links to /cases/[caseId] for each item.
 */
export function SlaSummaryBlock({ block }: Props) {
  return (
    <div className="space-y-2">
      <p className="text-xs font-bold uppercase tracking-[0.12em] text-muted">
        SLA status
      </p>
      <ul className="space-y-2">
        {block.items.map((item) => {
          const cfg = slaConfig[item.slaStatus];
          const { Icon } = cfg;
          return (
            <li
              key={item.caseId}
              className={`flex items-center gap-3 rounded-xl border px-3 py-2.5 ${cfg.rowClass}`}
            >
              <Icon className={`h-4 w-4 flex-shrink-0 ${cfg.chipClass}`} aria-hidden="true" />
              <div className="min-w-0 flex-1">
                <Link
                  href={`/cases/${item.caseId}`}
                  className="font-mono text-xs font-semibold text-accent hover:underline"
                >
                  {item.caseId}
                </Link>
                <span className="ml-2 text-sm text-ink">{item.customer}</span>
              </div>
              <span className={`flex-shrink-0 text-xs ${cfg.chipClass}`}>
                {item.dueLabel}
              </span>
            </li>
          );
        })}
      </ul>
    </div>
  );
}
