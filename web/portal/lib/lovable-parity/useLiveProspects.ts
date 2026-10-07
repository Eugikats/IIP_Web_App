"use client";

import { useEffect, useRef, useState } from "react";
import type { SalesLeadView } from "@/lib/lovable-parity/data/salesLeads";

// Watches the prospect list for real changes and reports them, so the dashboard can show them:
// someone new, a score that moved, an interest that was picked up. Nothing here is invented; every
// event is a difference between two reads of the same records.

export type LiveEvent = { id: string; at: number; text: string };
export type LiveMark = { kind: "new" | "scored" | "interest"; until: number; from?: number };

const MARK_MS = 6000;
const FEED_MAX = 4;

export function useLiveProspects(leads: SalesLeadView[], refresh: () => void | Promise<void>, enabled: boolean, intervalMs = 3000) {
  const previous = useRef<Map<string, SalesLeadView> | null>(null);
  const [marks, setMarks] = useState<Record<string, LiveMark>>({});
  const [feed, setFeed] = useState<LiveEvent[]>([]);
  const [now, setNow] = useState(() => Date.now());

  useEffect(() => {
    if (!enabled) return;
    const timer = window.setInterval(() => {
      if (document.visibilityState === "visible") void refresh();
    }, intervalMs);
    return () => window.clearInterval(timer);
  }, [enabled, refresh, intervalMs]);

  // A clock that only runs while something is marked, so marks expire without a poll.
  useEffect(() => {
    if (Object.keys(marks).length === 0) return;
    const timer = window.setInterval(() => setNow(Date.now()), 1000);
    return () => window.clearInterval(timer);
  }, [marks]);

  useEffect(() => {
    const before = previous.current;
    const current = new Map(leads.map((lead) => [lead.leadId, lead]));
    previous.current = current;
    if (!before || before.size === 0) return;

    const at = Date.now();
    const nextMarks: Record<string, LiveMark> = {};
    const events: LiveEvent[] = [];
    for (const lead of leads) {
      const old = before.get(lead.leadId);
      if (!old) {
        nextMarks[lead.leadId] = { kind: "new", until: at + MARK_MS };
        events.push({ id: `${lead.leadId}-new-${at}`, at, text: `${lead.customerName} is new. The triage agent is reading what they said.` });
        continue;
      }
      if (lead.triage && old.triage && lead.triage.score !== old.triage.score) {
        nextMarks[lead.leadId] = { kind: "scored", until: at + MARK_MS, from: old.triage.score };
        events.push({ id: `${lead.leadId}-s-${at}`, at, text: `Triage agent scored ${lead.customerName}: ${old.triage.score} to ${lead.triage.score}.` });
      } else if (lead.triage && !old.triage) {
        nextMarks[lead.leadId] = { kind: "scored", until: at + MARK_MS, from: 0 };
        events.push({ id: `${lead.leadId}-s-${at}`, at, text: `Triage agent scored ${lead.customerName}: ${lead.triage.score}, ${lead.triage.bandLabel}.` });
      } else if ((lead.productInterest ?? "") !== (old.productInterest ?? "") && lead.productInterest) {
        nextMarks[lead.leadId] = { kind: "interest", until: at + MARK_MS };
        events.push({ id: `${lead.leadId}-i-${at}`, at, text: `${lead.customerName} showed interest in ${lead.productInterest}.` });
      }
    }
    if (events.length > 0) {
      setMarks((existing) => ({ ...existing, ...nextMarks }));
      setFeed((existing) => [...events, ...existing].slice(0, FEED_MAX));
      setNow(at);
    }
  }, [leads]);

  const active = (leadId: string): LiveMark | null => {
    const mark = marks[leadId];
    return mark && mark.until > now ? mark : null;
  };

  /** True when the person has been in touch since they were last scored: the score is being worked out. */
  const scoring = (lead: SalesLeadView) =>
    !!lead.triage && !!lead.lastInteractionAt && lead.lastInteractionAt > lead.triage.scoredAt && Date.now() - new Date(lead.lastInteractionAt).getTime() < 90_000;

  return { active, feed, scoring };
}
