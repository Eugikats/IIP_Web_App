"use client";

import type { CommandCenterBlock } from "@/lib/commandCenterBlocks";
import { BookOpen } from "lucide-react";

interface Props {
  block: Extract<CommandCenterBlock, { type: "evidence_list" }>;
}

/**
 * Renders the evidence/source citations that back the AI answer.
 * No links externally — refs are internal document identifiers only.
 */
export function EvidenceListBlock({ block }: Props) {
  return (
    <div
      role="region"
      aria-label="Evidence sources"
      className="rounded-2xl border border-line bg-surface p-4 shadow-panel"
    >
      <p className="text-[10px] font-bold uppercase tracking-[0.18em] text-muted">
        {block.sources.length} source{block.sources.length === 1 ? "" : "s"} cited
      </p>
      <ul className="mt-3 space-y-2">
        {block.sources.map((source) => (
          <li
            key={source.ref}
            className="rounded-xl border border-line bg-mist px-3 py-2.5"
          >
            <div className="flex items-start gap-2">
              <BookOpen className="mt-0.5 h-4 w-4 flex-shrink-0 text-muted-soft" aria-hidden="true" />
              <div className="min-w-0">
                <p className="text-xs font-semibold text-ink">{source.label}</p>
                <p className="mt-0.5 font-mono text-[11px] text-muted">{source.documentName}</p>
              </div>
            </div>
          </li>
        ))}
      </ul>
    </div>
  );
}
