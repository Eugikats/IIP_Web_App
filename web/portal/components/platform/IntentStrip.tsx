"use client";

/** A horizontal strip of the organisation's intents and how each is doing, from its automations' open signals. */

import Link from "next/link";
import { Target } from "lucide-react";
import { intentHealth } from "@/lib/platform/format";
import type { PlatformIntent } from "@/lib/platform/types";
import { StatusPill } from "./ui";

export default function IntentStrip({ intents, href = "/app/platform/automations?tab=intents" }: { intents: PlatformIntent[]; href?: string }) {
  if (intents.length === 0) return null;
  const onTrack = intents.filter((i) => i.health === "on_track").length;
  return (
    <section aria-label="Intents" className="w-full">
      <div className="mb-2 flex items-center justify-between gap-3">
        <p className="flex items-center gap-1.5 text-[11px] font-semibold uppercase tracking-[0.14em] text-dls-text-muted">
          <Target className="h-3.5 w-3.5" aria-hidden /> Intents · {onTrack} of {intents.length} on track
        </p>
        <Link href={href} className="text-[12px] font-semibold text-dls-primary hover:underline">
          View all
        </Link>
      </div>
      <div className="-mx-1 flex snap-x gap-3 overflow-x-auto px-1 pb-1">
        {intents.map((intent) => {
          const health = intentHealth(intent.health);
          return (
            <Link
              key={intent.id}
              href={href}
              className="min-w-[220px] max-w-[280px] flex-1 snap-start rounded-dls-card border border-dls-border-subtle bg-dls-surface p-3 transition hover:border-dls-border-subtle"
            >
              <div className="flex items-start justify-between gap-2">
                <p className="line-clamp-2 text-[13px] font-semibold text-dls-text-primary">{intent.name}</p>
                <StatusPill tone={health.tone} dot className="shrink-0">
                  {health.label}
                </StatusPill>
              </div>
              {intent.target || intent.measure ? (
                <p className="mt-1 truncate text-[12px] text-dls-text-secondary">
                  {[intent.measure, intent.target].filter(Boolean).join(" · ")}
                </p>
              ) : null}
              <div className="mt-2 h-1 w-full overflow-hidden rounded-full bg-dls-bg-page">
                <div
                  className={`h-full rounded-full ${health.bar}`}
                  style={{ width: intent.health === "on_track" ? "100%" : intent.health === "at_risk" ? "60%" : "25%" }}
                />
              </div>
              <p className="mt-1.5 truncate text-[11px] text-dls-text-muted">
                {intent.openItems === 0 ? "No open signals" : `${intent.openItems} open signal${intent.openItems === 1 ? "" : "s"}${intent.latestSignal ? ` · ${intent.latestSignal}` : ""}`}
              </p>
            </Link>
          );
        })}
      </div>
    </section>
  );
}
