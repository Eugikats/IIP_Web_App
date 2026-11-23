"use client";

import { useEffect, useState } from "react";
import { apiBaseUrl, scopedHeaders } from "@/lib/config";

/**
 * The brief the agent gives when the workspace opens: what needs a person, counted from the same
 * metrics the Command Center uses. Every line is a real count; a count of zero is left out, and a
 * workspace with nothing waiting says so rather than inventing something to report.
 */

export type BriefItem = { key: string; sentence: string; href: string; tone: "danger" | "warning" | "info" };

export type BriefState =
  | { status: "loading" }
  | { status: "error" }
  | { status: "ready"; items: BriefItem[] };

type Metrics = {
  pendingApprovals?: number;
  slaAtRisk?: number;
  slaBreached?: number;
  incompleteClaims?: number;
  newClaimsToday?: number;
  customerUpdatesPending?: number;
};

const plural = (count: number, one: string, many: string) => `${count} ${count === 1 ? one : many}`;

export function briefFrom(metrics: Metrics): BriefItem[] {
  const items: BriefItem[] = [];
  const add = (key: string, count: number | undefined, tone: BriefItem["tone"], href: string, sentence: (n: number) => string) => {
    if (typeof count === "number" && count > 0) items.push({ key, sentence: sentence(count), href, tone });
  };
  add("breached", metrics.slaBreached, "danger", "/app/claims", (n) => `${plural(n, "claim is", "claims are")} past its service deadline.`);
  add("at-risk", metrics.slaAtRisk, "warning", "/app/claims", (n) => `${plural(n, "claim is", "claims are")} close to its service deadline.`);
  add("approvals", metrics.pendingApprovals, "warning", "/app/work-items", (n) => `${plural(n, "approval is", "approvals are")} waiting for a person to decide.`);
  add("incomplete", metrics.incompleteClaims, "info", "/app/claims", (n) => `${plural(n, "claim is", "claims are")} missing documents.`);
  add("updates", metrics.customerUpdatesPending, "info", "/app/claims", (n) => `${plural(n, "customer update is", "customer updates are")} waiting to be sent.`);
  add("new", metrics.newClaimsToday, "info", "/app/claims", (n) => `${plural(n, "new claim", "new claims")} came in today.`);
  return items;
}

export function useAgentBrief(enabled: boolean, role: string): BriefState {
  const [state, setState] = useState<BriefState>({ status: "loading" });

  useEffect(() => {
    if (!enabled) return;
    let cancelled = false;
    setState({ status: "loading" });
    (async () => {
      try {
        const response = await fetch(`${apiBaseUrl}/api/v1/command-center/metrics`, { headers: scopedHeaders(role, "") });
        if (!response.ok) throw new Error("metrics");
        const metrics = (await response.json()) as Metrics;
        if (!cancelled) setState({ status: "ready", items: briefFrom(metrics) });
      } catch {
        if (!cancelled) setState({ status: "error" });
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [enabled, role]);

  return state;
}
