"use client";

import type { CommandCenterBlock } from "@/lib/commandCenterBlocks";
import { AlertCircle } from "lucide-react";

interface Props {
  block: Extract<CommandCenterBlock, { type: "error" }>;
}

/**
 * Renders a safe error message. The safe message is server-generated — no
 * raw provider errors, no secrets, no stack traces are ever in this block.
 */
export function ErrorBlock({ block }: Props) {
  return (
    <div
      role="alert"
      className="flex items-start gap-3 rounded-2xl border border-danger/30 bg-danger/5 px-4 py-3"
    >
      <AlertCircle className="mt-0.5 h-5 w-5 flex-shrink-0 text-danger" aria-hidden="true" />
      <div className="min-w-0">
        <p className="text-sm font-semibold text-danger">{block.safeMessage}</p>
        <p className="mt-1 font-mono text-[11px] text-muted">
          Code: {block.code} · Ref: {block.correlationId}
        </p>
      </div>
    </div>
  );
}
