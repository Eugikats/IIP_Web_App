"use client";

import type { CommandCenterBlock } from "@/lib/commandCenterBlocks";
import { FileText } from "lucide-react";

interface Props {
  block: Extract<CommandCenterBlock, { type: "management_summary" }>;
}

/**
 * Renders an AI-prepared management summary: a titled set of headed sections.
 * Read-only preparation for a human — conveys no final decision.
 */
export function ManagementSummaryBlock({ block }: Props) {
  return (
    <div className="space-y-2">
      <div className="flex items-center gap-2">
        <FileText className="h-4 w-4 flex-shrink-0 text-muted-soft" aria-hidden="true" />
        <p className="text-xs font-bold uppercase tracking-[0.12em] text-muted">{block.title}</p>
      </div>
      <div className="space-y-3 rounded-2xl border border-line bg-white p-4 shadow-panel dark:bg-canvas">
        {block.sections.map((section, index) => (
          <div key={`${section.heading}-${index}`}>
            <p className="text-sm font-semibold text-ink">{section.heading}</p>
            <p className="mt-1 whitespace-pre-wrap text-sm leading-relaxed text-muted">{section.body}</p>
          </div>
        ))}
      </div>
    </div>
  );
}
