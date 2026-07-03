"use client";

import Link from "next/link";
import type { CommandCenterBlock, SlaStatus } from "@/lib/commandCenterBlocks";

interface Props {
  block: Extract<CommandCenterBlock, { type: "claims_table" }>;
}

const slaStyles: Record<SlaStatus, string> = {
  on_track: "text-success",
  due_today: "text-warning",
  at_risk: "text-warning",
  breached: "text-danger font-semibold",
};

const slaLabels: Record<SlaStatus, string> = {
  on_track: "On Track",
  due_today: "Due Today",
  at_risk: "At Risk",
  breached: "Breached",
};

/**
 * Renders the attention-sorted claims table.
 * Each row deep-links to /claims or /cases/[caseId] via the contract `href`.
 * Purely presentational — no actions are performed here.
 */
export function ClaimsTableBlock({ block }: Props) {
  if (block.rows.length === 0) {
    return (
      <p className="text-sm text-muted">No claims to display.</p>
    );
  }

  return (
    <div className="overflow-x-auto rounded-2xl border border-line shadow-panel">
      <table className="min-w-full text-sm" aria-label="Claims requiring attention">
        <thead>
          <tr className="border-b border-line bg-surface-soft">
            <th scope="col" className="px-4 py-3 text-left text-xs font-bold uppercase tracking-[0.10em] text-muted">
              Case
            </th>
            <th scope="col" className="px-4 py-3 text-left text-xs font-bold uppercase tracking-[0.10em] text-muted">
              Customer
            </th>
            <th scope="col" className="hidden px-4 py-3 text-left text-xs font-bold uppercase tracking-[0.10em] text-muted sm:table-cell">
              Product
            </th>
            <th scope="col" className="px-4 py-3 text-left text-xs font-bold uppercase tracking-[0.10em] text-muted">
              Status
            </th>
            <th scope="col" className="px-4 py-3 text-left text-xs font-bold uppercase tracking-[0.10em] text-muted">
              SLA
            </th>
            <th scope="col" className="hidden px-4 py-3 text-right text-xs font-bold uppercase tracking-[0.10em] text-muted lg:table-cell">
              Complete
            </th>
          </tr>
        </thead>
        <tbody className="divide-y divide-line bg-white dark:bg-canvas">
          {block.rows.map((row) => (
            <tr
              key={row.caseId}
              className="transition-colors hover:bg-surface-soft"
            >
              <td className="whitespace-nowrap px-4 py-3">
                <Link
                  href={row.href}
                  className="font-mono text-xs font-semibold text-accent hover:underline"
                >
                  {row.caseId}
                </Link>
              </td>
              <td className="max-w-[160px] truncate px-4 py-3 font-semibold text-ink">
                {row.customer}
              </td>
              <td className="hidden max-w-[140px] truncate px-4 py-3 text-muted sm:table-cell">
                {row.productLine}
              </td>
              <td className="max-w-[140px] truncate px-4 py-3 text-muted">
                {row.status}
              </td>
              <td className={`whitespace-nowrap px-4 py-3 text-xs ${slaStyles[row.slaStatus]}`}>
                {slaLabels[row.slaStatus]}
              </td>
              <td className="hidden whitespace-nowrap px-4 py-3 text-right text-xs text-muted lg:table-cell">
                <span
                  className={
                    row.completeness < 70
                      ? "text-danger"
                      : row.completeness < 100
                      ? "text-warning"
                      : "text-success"
                  }
                >
                  {row.completeness}%
                </span>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
