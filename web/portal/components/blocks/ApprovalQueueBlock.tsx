"use client";

import Link from "next/link";
import type { CommandCenterBlock } from "@/lib/commandCenterBlocks";
import { ClipboardList } from "lucide-react";

interface Props {
  block: Extract<CommandCenterBlock, { type: "approval_queue" }>;
}

/**
 * Renders pending approval items. Each row deep-links to /approvals via the
 * contract `href`. No approve/reject buttons — human-only decisions.
 */
export function ApprovalQueueBlock({ block }: Props) {
  return (
    <div className="space-y-2">
      <p className="text-xs font-bold uppercase tracking-[0.12em] text-muted">
        Pending approvals
      </p>
      <ul className="divide-y divide-line overflow-hidden rounded-2xl border border-line shadow-panel">
        {block.items.map((item) => (
          <li key={item.approvalId} className="flex items-center gap-3 bg-white px-4 py-3 transition-colors hover:bg-surface-soft dark:bg-canvas">
            <ClipboardList className="h-4 w-4 flex-shrink-0 text-muted-soft" aria-hidden="true" />
            <div className="min-w-0 flex-1">
              <p className="truncate text-sm font-semibold text-ink">{item.title}</p>
              <p className="mt-0.5 font-mono text-xs text-muted">{item.caseId}</p>
            </div>
            <Link
              href={item.href}
              className="flex-shrink-0 rounded-lg border border-line bg-surface px-3 py-1.5 text-xs font-semibold text-ink transition-colors hover:border-accent hover:text-accent"
            >
              Review →
            </Link>
          </li>
        ))}
      </ul>
    </div>
  );
}
