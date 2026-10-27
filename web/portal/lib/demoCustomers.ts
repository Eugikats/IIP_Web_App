"use client";

import { apiBaseUrl, scopedHeaders } from "@/lib/config";
import type { DecisionGate } from "@/lib/rules";

export type DemoStatus = { isDemonstration: boolean; scenarioId: string | null; brandName: string | null; isBank: boolean };

export type DemoCustomerForm = {
  name: string;
  phone: string;
  branch: string;
  bankProducts: string[];
  incomeBand: string;
  dependants: string;
  loanAmount?: number;
  loanTermMonths?: number;
  loanDaysAgo?: number;
  consent: "Agreed" | "NotAsked" | "Declined";
};

export type TimelineEntry = {
  at: string;
  kind: "consent" | "event" | "decision" | "message" | "reply";
  title: string;
  detail: string;
  gates: DecisionGate[] | null;
  outcome: string | null;
};

export type PersonTimeline = { phone: string; customerName: string | null; consentSummary: string | null; entries: TimelineEntry[] };

export type DemoResult<T> = { ok: true; value: T } | { ok: false; error: string };

async function failure(response: Response, fallback: string): Promise<DemoResult<never>> {
  try {
    const payload = (await response.json()) as { detail?: string; message?: string; errors?: Record<string, string[]> };
    return { ok: false, error: payload.detail || payload.message || (payload.errors ? Object.values(payload.errors).flat()[0] : undefined) || fallback };
  } catch {
    return { ok: false, error: fallback };
  }
}

const NETWORK: DemoResult<never> = { ok: false, error: "The service could not be reached. Check your connection and try again." };
const headers = (role: string) => ({ "Content-Type": "application/json", ...scopedHeaders(role, "") });

export async function getDemoStatus(role: string): Promise<DemoResult<DemoStatus>> {
  try {
    const response = await fetch(`${apiBaseUrl}/api/v1/demo/status`, { headers: headers(role) });
    return response.ok ? { ok: true, value: (await response.json()) as DemoStatus } : failure(response, "The workspace kind could not be read.");
  } catch {
    return NETWORK;
  }
}

export async function addDemoCustomer(role: string, form: DemoCustomerForm): Promise<DemoResult<{ leadId: string; phone: string; eventRecorded: boolean }>> {
  try {
    const response = await fetch(`${apiBaseUrl}/api/v1/demo/customers`, { method: "POST", headers: headers(role), body: JSON.stringify(form) });
    return response.ok ? { ok: true, value: await response.json() } : failure(response, "The customer could not be added.");
  } catch {
    return NETWORK;
  }
}

export async function getTimeline(role: string, by: { eventId?: string; decisionId?: string }): Promise<DemoResult<PersonTimeline>> {
  try {
    const query = by.eventId ? `eventId=${encodeURIComponent(by.eventId)}` : `decisionId=${encodeURIComponent(by.decisionId ?? "")}`;
    const response = await fetch(`${apiBaseUrl}/api/v1/rule-timeline?${query}`, { headers: headers(role) });
    return response.ok ? { ok: true, value: (await response.json()) as PersonTimeline } : failure(response, "The story could not be loaded.");
  } catch {
    return NETWORK;
  }
}

/** The plan's example customer. The name and number are left for a real teammate who has agreed to take part. */
export const EUGENE: DemoCustomerForm = {
  name: "Eugene",
  phone: "",
  branch: "Ntinda branch",
  bankProducts: ["Salary account", "Savings account", "Salary loan"],
  incomeBand: "About UGX 2.4m a month",
  dependants: "2 children",
  loanAmount: 8_000_000,
  loanTermMonths: 36,
  loanDaysAgo: 2,
  consent: "Agreed",
};
