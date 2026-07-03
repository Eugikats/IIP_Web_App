"use client";

import Link from "next/link";
import type { CommandCenterBlock } from "@/lib/commandCenterBlocks";
import { Settings } from "lucide-react";

interface Props {
  block: Extract<CommandCenterBlock, { type: "not_configured" }>;
}

/**
 * Renders an honest "Not configured" state.
 * Contract invariant: missing capability → not_configured, NEVER fake success.
 * Shows the unconfigured provider name and directs the admin to configure it.
 */
export function NotConfiguredBlock({ block }: Props) {
  return (
    <div
      role="status"
      className="flex items-start gap-3 rounded-2xl border border-dashed border-line bg-surface p-4"
    >
      <Settings className="mt-0.5 h-5 w-5 flex-shrink-0 text-muted-soft" aria-hidden="true" />
      <div className="min-w-0">
        <p className="text-sm font-semibold text-ink">
          Not configured:{" "}
          <span className="font-mono text-xs font-normal text-muted">{block.provider}</span>
        </p>
        <p className="mt-1 text-sm leading-relaxed text-muted">{block.adminAction}</p>
        <Link
          href="/integrations"
          className="mt-3 inline-flex items-center gap-1.5 rounded-lg border border-line bg-surface-soft px-3 py-1.5 text-xs font-semibold text-ink transition-colors hover:border-accent hover:text-accent"
        >
          Go to Integrations →
        </Link>
      </div>
    </div>
  );
}
