"use client";

import { apiBaseUrl, scopedHeaders } from "@/lib/config";

export type BankEventType = {
  code: string;
  label: string;
  reason: string;
  suggestedOffer: string;
  suggestedTiming: string;
  isTimeTrigger: boolean;
};

export type BankEvent = {
  eventId: string;
  eventType: string;
  label: string;
  phone: string;
  occurredAt: string;
  receivedAt: string;
  source: "Recorded" | "FileImport" | "Feed" | "PolicyDates";
  sourceReference: string | null;
  details: Record<string, string>;
  state: "Pending" | "Processed";
  processedAt: string | null;
};

export type EventInputForm = {
  eventType: string;
  phone: string;
  occurredAt: string;
  amount?: number;
  termMonths?: number;
  branch?: string;
};

export type EventImportOutcome = {
  rowsRead: number;
  created: number;
  duplicates: number;
  rejected: { row: number; reason: string }[];
  ignoredColumns: string[];
};

export type BankEventResult<T> = { ok: true; value: T } | { ok: false; error: string; forbidden: boolean };

async function failure(response: Response, fallback: string): Promise<BankEventResult<never>> {
  let message = fallback;
  try {
    const payload = (await response.json()) as { detail?: string; message?: string; errors?: Record<string, string[]> };
    message = payload.detail || payload.message || (payload.errors ? Object.values(payload.errors).flat()[0] : undefined) || fallback;
  } catch {
    // Keep the fallback.
  }
  return { ok: false, error: message, forbidden: response.status === 403 };
}

const NETWORK: BankEventResult<never> = { ok: false, error: "The service could not be reached. Check your connection and try again.", forbidden: false };
const headers = (role: string) => ({ "Content-Type": "application/json", ...scopedHeaders(role, "") });

export async function listEventTypes(role: string): Promise<BankEventResult<BankEventType[]>> {
  try {
    const response = await fetch(`${apiBaseUrl}/api/v1/events/types`, { headers: headers(role) });
    return response.ok ? { ok: true, value: (await response.json()) as BankEventType[] } : failure(response, "The event types could not be loaded.");
  } catch {
    return NETWORK;
  }
}

export async function listEvents(role: string): Promise<BankEventResult<BankEvent[]>> {
  try {
    const response = await fetch(`${apiBaseUrl}/api/v1/events?take=200`, { headers: headers(role) });
    return response.ok ? { ok: true, value: (await response.json()) as BankEvent[] } : failure(response, "The events could not be loaded.");
  } catch {
    return NETWORK;
  }
}

export async function recordEvent(role: string, input: EventInputForm): Promise<BankEventResult<{ created: boolean; event: BankEvent }>> {
  try {
    const response = await fetch(`${apiBaseUrl}/api/v1/events`, { method: "POST", headers: headers(role), body: JSON.stringify(input) });
    return response.ok ? { ok: true, value: await response.json() } : failure(response, "The event could not be recorded.");
  } catch {
    return NETWORK;
  }
}

export async function importEventFile(role: string, fileName: string, csv: string): Promise<BankEventResult<EventImportOutcome>> {
  try {
    const response = await fetch(`${apiBaseUrl}/api/v1/events/import`, { method: "POST", headers: headers(role), body: JSON.stringify({ fileName, csv }) });
    return response.ok ? { ok: true, value: (await response.json()) as EventImportOutcome } : failure(response, "The file could not be loaded.");
  } catch {
    return NETWORK;
  }
}
