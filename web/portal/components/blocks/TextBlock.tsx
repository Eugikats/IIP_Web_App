"use client";

import type { CommandCenterBlock } from "@/lib/commandCenterBlocks";

interface Props {
  block: Extract<CommandCenterBlock, { type: "text" }>;
}

/**
 * Renders the one-paragraph narrative summary that narrates (never replaces)
 * the structured data blocks that follow it.
 */
export function TextBlock({ block }: Props) {
  return (
    <div className="rounded-2xl border border-ai-border bg-ai-surface px-4 py-3 text-sm leading-relaxed text-ink whitespace-pre-wrap">
      {block.markdown}
    </div>
  );
}
