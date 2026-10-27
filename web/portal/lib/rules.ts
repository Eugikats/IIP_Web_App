"use client";

import { apiBaseUrl, scopedHeaders } from "@/lib/config";

export type OutreachRule = {
  ruleId: string;
  name: string;
  version: number;
  eventType: string;
  eventLabel: string;
  minAmount: number | null;
  minTermMonths: number | null;
  waitDays: number;
  allowedProductLines: string[];
  templateName: string;
  language: string;
  fromHour: number;
  toHour: number;
  onePerPersonDays: number;
  sameCoverDays: number;
  dailyLimit: number;
  followUpAfterDays: number | null;
  followUpTemplate: string | null;
  status: "Draft" | "Approved" | "Paused";
  isLive: boolean;
  changedSinceApproval: boolean;
  createdBy: string;
  updatedAt: string;
  approvedBy: string | null;
  approvedByRole: string | null;
  approvedAt: string | null;
};

export type RulesOverview = {
  rules: OutreachRule[];
  approval: { allowedRoles: string[]; requireDifferentApprover: boolean; youMayApprove: boolean };
  events: { code: string; label: string; suggestedOffer: string }[];
  covers: { line: string; name: string; insurer: string; insuranceClass: string }[];
  templates: { name: string; language: string; category: string; status: string; purpose: string }[];
};

export type RuleForm = {
  name: string;
  eventType: string;
  minAmount?: number | null;
  minTermMonths?: number | null;
  waitDays: number;
  allowedProductLines: string[];
  templateName: string;
  language: string;
  fromHour: number;
  toHour: number;
  onePerPersonDays: number;
  sameCoverDays: number;
  dailyLimit: number;
  followUpAfterDays?: number | null;
  followUpTemplate?: string | null;
};

export type DecisionGate = { name: string; passed: boolean; detail: string };

export type RuleDecision = {
  decisionId: string;
  ruleId: string;
  ruleName: string;
  ruleVersion: number;
  eventType: string;
  phone: string;
  customerName: string | null;
  leadId: string | null;
  outcome: "Sent" | "DemoSent" | "Skipped" | "Scheduled" | "Blocked";
  reason: string;
  decidedAt: string;
  notBefore: string | null;
  cover: string | null;
  insurer: string | null;
  templateName: string | null;
  kind: string;
  gates: DecisionGate[];
};

export type RulesResult<T> = { ok: true; value: T } | { ok: false; error: string; forbidden: boolean };

async function failure(response: Response, fallback: string): Promise<RulesResult<never>> {
  let message = fallback;
  try {
    const payload = (await response.json()) as { detail?: string; message?: string; errors?: Record<string, string[]> };
    message = payload.detail || payload.message || (payload.errors ? Object.values(payload.errors).flat()[0] : undefined) || fallback;
  } catch {
    // Keep the fallback.
  }
  return { ok: false, error: message, forbidden: response.status === 403 };
}

const NETWORK: RulesResult<never> = { ok: false, error: "The service could not be reached. Check your connection and try again.", forbidden: false };
const headers = (role: string) => ({ "Content-Type": "application/json", ...scopedHeaders(role, "") });

async function call<T>(role: string, path: string, method: string, body: unknown, fallback: string): Promise<RulesResult<T>> {
  try {
    const response = await fetch(`${apiBaseUrl}${path}`, { method, headers: headers(role), body: body === undefined ? undefined : JSON.stringify(body) });
    return response.ok ? { ok: true, value: (await response.json()) as T } : failure(response, fallback);
  } catch {
    return NETWORK;
  }
}

export const getRules = (role: string) => call<RulesOverview>(role, "/api/v1/rules", "GET", undefined, "The rules could not be loaded.");
export const saveRule = (role: string, ruleId: string | null, form: RuleForm) =>
  call<OutreachRule>(role, ruleId ? `/api/v1/rules/${encodeURIComponent(ruleId)}` : "/api/v1/rules", ruleId ? "PUT" : "POST", form, "The rule could not be saved.");
export const approveRule = (role: string, ruleId: string) => call<OutreachRule>(role, `/api/v1/rules/${encodeURIComponent(ruleId)}/approve`, "POST", undefined, "The rule could not be approved.");
export const pauseRule = (role: string, ruleId: string) => call<OutreachRule>(role, `/api/v1/rules/${encodeURIComponent(ruleId)}/pause`, "POST", undefined, "The rule could not be paused.");
export const resumeRule = (role: string, ruleId: string) => call<OutreachRule>(role, `/api/v1/rules/${encodeURIComponent(ruleId)}/resume`, "POST", undefined, "The rule could not be resumed.");
export const saveApprovalSettings = (role: string, allowedRoles: string[], requireDifferentApprover: boolean) =>
  call<{ allowedRoles: string[]; requireDifferentApprover: boolean }>(role, "/api/v1/rules-approval", "PUT", { allowedRoles, requireDifferentApprover }, "The approvers could not be saved.");
export const runRules = (role: string) =>
  call<{ considered: number; sent: number; demoSent: number; skipped: number; scheduled: number; blocked: number }>(role, "/api/v1/rules-run", "POST", undefined, "The rules could not be run.");
export const getDecisions = (role: string, outcome?: string) =>
  call<RuleDecision[]>(role, `/api/v1/rule-decisions${outcome ? `?outcome=${encodeURIComponent(outcome)}` : ""}`, "GET", undefined, "The decision log could not be loaded.");

export function ruleSentence(rule: OutreachRule, coverNames: Record<string, string>): string {
  const conditions = [
    rule.minAmount ? `UGX ${rule.minAmount.toLocaleString("en-UG")} or more` : null,
    rule.minTermMonths ? `${rule.minTermMonths} months or more` : null,
  ].filter(Boolean);
  const covers = rule.allowedProductLines.map((line) => coverNames[line] ?? line);
  return (
    `When: ${rule.eventLabel.toLowerCase()}${conditions.length ? ` (${conditions.join(", ")})` : ""}. ` +
    `Wait ${rule.waitDays === 0 ? "no time" : `${rule.waitDays} day${rule.waitDays === 1 ? "" : "s"}`}, then offer ${covers.length > 1 ? "the best fit of " : ""}${covers.join(", ")} ` +
    `with the template ${rule.templateName}, between ${String(rule.fromHour).padStart(2, "0")}:00 and ${String(rule.toHour).padStart(2, "0")}:00 East Africa time. ` +
    `No more than one offer to a person in ${rule.onePerPersonDays} days, the same cover once in ${rule.sameCoverDays} days, ${rule.dailyLimit} people a day.`
  );
}
