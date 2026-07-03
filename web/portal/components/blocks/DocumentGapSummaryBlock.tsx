"use client";

import Link from "next/link";
import type { CommandCenterBlock } from "@/lib/commandCenterBlocks";
import { FileX2 } from "lucide-react";

interface Props {
  block: Extract<CommandCenterBlock, { type: "document_gap_summary" }>;
}

/**
 * Renders missing-document summaries per case.
 * Links to /cases/[caseId]. Shows each missing document as a small tag.
 */
export function DocumentGapSummaryBlock({ block }: Props) {
  return (
    <div className="space-y-2">
      <p className="text-xs font-bold uppercase tracking-[0.12em] text-muted">
        Document gaps
      </p>
      <ul className="space-y-2">
        {block.items.map((item) => (
          <li
            key={item.caseId}
            className="rounded-xl border border-warning/20 bg-warning/5 px-3 py-2.5"
          >
            <div className="flex items-start gap-2">
              <FileX2 className="mt-0.5 h-4 w-4 flex-shrink-0 text-warning" aria-hidden="true" />
              <div className="min-w-0">
                <div className="flex items-center gap-2">
                  <Link
                    href={`/cases/${item.caseId}`}
                    className="font-mono text-xs font-semibold text-accent hover:underline"
                  >
                    {item.caseId}
                  </Link>
                  <span className="text-sm font-semibold text-ink">{item.customer}</span>
                </div>
                <div className="mt-2 flex flex-wrap gap-1.5">
                  {item.missing.map((doc) => (
                    <span
                      key={doc}
                      className="inline-flex items-center rounded-full border border-warning/30 bg-white px-2.5 py-0.5 text-xs font-semibold text-warning dark:bg-canvas"
                    >
                      {doc}
                    </span>
                  ))}
                </div>
              </div>
            </div>
          </li>
        ))}
      </ul>
    </div>
  );
}
