"use client";

import Link from "next/link";
import type { CommandCenterBlock } from "@/lib/commandCenterBlocks";
import { MailWarning } from "lucide-react";

interface Props {
  block: Extract<CommandCenterBlock, { type: "draft_communication" }>;
}

/**
 * Renders a persisted customer-communication draft prepared by AI.
 * Contract invariants: preparation only — the draft is NEVER sent from here.
 * A human must review and approve before any send. No send/execute control is rendered.
 */
export function DraftCommunicationBlock({ block }: Props) {
  return (
    <div className="space-y-2">
      <p className="text-xs font-bold uppercase tracking-[0.12em] text-muted">
        Customer update draft
      </p>
      <div className="rounded-2xl border border-line bg-white p-4 shadow-panel dark:bg-canvas">
        <div className="flex items-start gap-3">
          <MailWarning className="mt-0.5 h-5 w-5 flex-shrink-0 text-muted-soft" aria-hidden="true" />
          <div className="min-w-0 flex-1">
            <p className="whitespace-pre-wrap text-sm leading-relaxed text-ink">{block.preview}</p>
            <div className="mt-3 flex flex-wrap items-center gap-2">
              {block.requiresApproval && (
                <span className="inline-flex items-center rounded-full border border-warning/40 bg-warning/10 px-2.5 py-1 text-xs font-semibold text-warning">
                  Requires human approval before sending
                </span>
              )}
              <span className="font-mono text-xs text-muted">{block.draftId}</span>
            </div>
            <Link
              href="/approvals"
              className="mt-3 inline-flex items-center gap-1.5 rounded-lg border border-line bg-surface-soft px-3 py-1.5 text-xs font-semibold text-ink transition-colors hover:border-accent hover:text-accent"
            >
              Review &amp; approve →
            </Link>
          </div>
        </div>
      </div>
    </div>
  );
}
