"use client";

import { apiBaseUrl, scopedHeaders } from "@/lib/config";

export type RetentionWatchItem = {
  policyId: string;
  policyNumber: string;
  productLine: string;
  customerId: string;
  customerName: string;
  expiryDate: string;
  daysToExpiry: number;
  expiryBucketDays: number;
  claimCountLast12Months: number;
  daysSinceLastContact: number | null;
  annualPremium: number | null;
  premiumCurrency: string | null;
  attentionScore: number;
  latestReminderStatus: string | null;
  latestReminderId: string | null;
};

export type RetentionWatchSummary = {
  policiesAtRisk: number;
  premiumAtRisk: number;
  premiumAtRiskCurrency: string;
  items: RetentionWatchItem[];
};

export type RetentionReminder = {
  reminderId: string;
  policyId: string;
  policyNumber: string;
  customerName: string;
  expiryBucketDays: number;
  channel: string;
  recipient: string;
  messageBody: string;
  status: string;
  deliveryStatus: string | null;
  createdAt: string;
  sentAt: string | null;
};

export type RetentionResult<T> = { ok: true; value: T } | { ok: false; error: string; field?: string };

async function safeError(response: Response, fallback: string): Promise<{ message: string; field?: string }> {
  try {
    const payload = (await response.json()) as { message?: string; errors?: Record<string, string[]> };
    if (payload.errors) {
      const [field, messages] = Object.entries(payload.errors)[0] ?? [];
      if (messages?.[0]) return { message: messages[0], field };
    }
    if (payload.message) return { message: payload.message };
  } catch {
    // Keep the safe fallback for non-JSON errors.
  }
  return { message: fallback };
}

function networkFailure(reason: unknown, fallback: string): string {
  if (reason instanceof TypeError || (reason instanceof Error && /failed to fetch|networkerror/i.test(reason.message))) {
    return "The retention service could not be reached. Check your connection and try again.";
  }
  return fallback;
}

async function call<T>(role: string, input: RequestInfo, init: RequestInit, fallback: string): Promise<RetentionResult<T>> {
  try {
    const response = await fetch(input, { ...init, headers: { ...scopedHeaders(role, ""), ...(init.headers ?? {}) } });
    if (!response.ok) {
      const { message, field } = await safeError(response, fallback);
      return { ok: false, error: message, field };
    }
    if (response.status === 204) return { ok: true, value: undefined as T };
    return { ok: true, value: (await response.json()) as T };
  } catch (reason) {
    return { ok: false, error: networkFailure(reason, fallback) };
  }
}

export function getRetentionWatch(role: string) {
  return call<RetentionWatchSummary>(role, `${apiBaseUrl}/api/v1/retention/watch`, {}, "The retention watch could not be loaded.");
}

export function draftRetentionReminder(role: string, policyId: string) {
  return call<RetentionReminder>(
    role,
    `${apiBaseUrl}/api/v1/retention/policies/${encodeURIComponent(policyId)}/reminders`,
    { method: "POST" },
    "This reminder could not be drafted."
  );
}

export function approveAndSendRetentionReminder(role: string, reminderId: string) {
  return call<RetentionReminder>(
    role,
    `${apiBaseUrl}/api/v1/retention/reminders/${reminderId}/approve-and-send`,
    { method: "POST" },
    "This reminder could not be sent."
  );
}
