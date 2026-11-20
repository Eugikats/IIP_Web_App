"use client";

/**
 * Command Center cards from the organisation platform: inbox notifications (approvals, signals needing attention,
 * intents off course, automations watching). Each is a bordered row with a neutral icon; tone is a 6px dot and a word,
 * never a tinted surface.
 */

import Link from "next/link";
import { Bell, Bot, ChevronRight, FileText, LineChart, ShieldAlert, Target, type LucideIcon } from "lucide-react";
import type { PlatformCard } from "@/lib/platform/types";

const ICONS: Record<string, LucideIcon> = {
  approval: ShieldAlert,
  risk: Bell,
  target: Target,
  automation: Bot,
  portfolio: LineChart,
  health: LineChart,
  applications: FileText,
  documents: FileText,
};

const DOT: Record<PlatformCard["tone"], string> = {
  blue: "bg-dls-info-dot",
  amber: "bg-dls-warning-dot",
  red: "bg-dls-danger-dot",
  green: "bg-dls-success-dot",
};

function iconFor(card: PlatformCard): LucideIcon {
  const key = card.iconKey.toLowerCase();
  return ICONS[key] ?? Object.entries(ICONS).find(([k]) => key.includes(k))?.[1] ?? LineChart;
}

export function IntelligenceCard({ card, onAsk }: { card: PlatformCard; onAsk?: (prompt: string) => void }) {
  const Icon = iconFor(card);
  const content = (
    <>
      <span className="flex h-11 w-11 shrink-0 items-center justify-center rounded-dls-control border border-dls-border-subtle bg-dls-bg-page">
        <Icon className="h-5 w-5 text-dls-text-secondary" aria-hidden />
      </span>
      <span className="min-w-0 flex-1 text-left">
        <span className="flex items-center gap-1.5 truncate text-[10px] font-semibold uppercase tracking-[0.12em] text-dls-text-muted">
          <span className={`h-1.5 w-1.5 shrink-0 rounded-full ${DOT[card.tone] ?? DOT.blue}`} aria-hidden />
          {card.kicker}
          {card.count && card.kind !== "workspace" ? <span className="ml-1 text-dls-text-primary">{card.count > 99 ? "99+" : card.count}</span> : null}
        </span>
        <span className="block text-[15px] font-semibold text-dls-text-primary">{card.title}</span>
        <span className="mt-0.5 line-clamp-2 block text-[13px] leading-snug text-dls-text-secondary">{card.summary}</span>
      </span>
      <ChevronRight className="h-5 w-5 shrink-0 text-dls-text-faint" aria-hidden />
    </>
  );
  const className =
    "flex w-full items-center gap-3 rounded-dls-card border border-dls-border-subtle bg-dls-surface px-4 py-3.5 text-left transition-colors hover:border-dls-border-strong";

  if (card.askPrompt && onAsk && card.kind === "workspace") {
    return (
      <button type="button" className={className} onClick={() => onAsk(card.askPrompt!)}>
        {content}
      </button>
    );
  }
  if (card.href) {
    return (
      <Link href={card.href} className={className}>
        {content}
      </Link>
    );
  }
  return <div className={className}>{content}</div>;
}

export default function IntelligenceCards({
  cards,
  loading,
  onAsk,
  limit = 6,
}: {
  cards: PlatformCard[];
  loading?: boolean;
  onAsk?: (prompt: string) => void;
  limit?: number;
}) {
  if (loading) {
    return (
      <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-3" role="status" aria-label="Loading">
        {[0, 1, 2].map((i) => (
          <div key={i} className="h-[84px] animate-pulse rounded-dls-card bg-dls-bg-page" />
        ))}
      </div>
    );
  }
  if (cards.length === 0) return null;
  return (
    <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-3">
      {cards.slice(0, limit).map((card) => (
        <IntelligenceCard key={card.id} card={card} onAsk={onAsk} />
      ))}
    </div>
  );
}
