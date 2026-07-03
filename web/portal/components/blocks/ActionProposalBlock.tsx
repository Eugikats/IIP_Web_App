"use client";

import Link from "next/link";
import type { CommandCenterBlock } from "@/lib/commandCenterBlocks";
import { ShieldCheck } from "lucide-react";

interface Props {
  block: Extract<CommandCenterBlock, { type: "action_proposal" }>;
}

/**
 * Renders AI-suggested draft actions. Contract invariant: no execute button.
 * Every card is labelled "Human review required" and renders only a link to
 * the governed workflow. `requiresHumanApproval: true` is enforced by the
 * parser — this renderer relies on it being guaranteed by the type system.
 */
export function ActionProposalBlock({ block }: Props) {
  return (
    <div className="space-y-2">
      <div className="flex items-center gap-2">
        <ShieldCheck className="h-4 w-4 text-violet" aria-hidden="true" />
        <p className="text-xs font-bold uppercase tracking-[0.12em] text-violet">
          Suggested actions — Human review required
        </p>
      </div>
      <ul className="space-y-2">
        {block.actions.map((action) => (
          <li
            key={action.label}
            className="rounded-2xl border border-violet/20 bg-surface-ai p-4 shadow-panel"
          >
            <p className="text-sm font-semibold text-ink">{action.label}</p>
            <p className="mt-1.5 text-sm leading-relaxed text-muted">
              {action.description}
            </p>
            <div className="mt-3 flex items-center gap-3">
              {/* The only affordance is a navigation link — NO execute button. */}
              <Link
                href={action.targetHref}
                className="inline-flex items-center gap-1.5 rounded-lg border border-violet/30 bg-violet/10 px-3 py-1.5 text-xs font-semibold text-violet transition-colors hover:bg-violet/20"
              >
                Open in governed workflow →
              </Link>
              <span className="text-[11px] text-muted-soft">
                Requires human approval before any action is taken
              </span>
            </div>
          </li>
        ))}
      </ul>
    </div>
  );
}
