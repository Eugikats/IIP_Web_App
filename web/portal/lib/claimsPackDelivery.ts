"use client";

import { apiBaseUrl, scopedHeaders } from "@/lib/config";

export type ClaimsPackDelivery = {
  recipients: string[];
  trigger: "Manual" | "Complete" | "Status";
  triggerStatus: string | null;
  maxRecipients: number;
  maxAttachmentBytes: number;
  linkDays: number;
};

export type ClaimsPackHistoryEntry = {
  at: string;
  reason: string;
  sentBy: string;
  recipients: string[];
  mode: "Attached" | "Link" | "NotSent";
  outcome: string;
  files: number;
  bytes: number;
  linkExpiresAt: string | null;
};

export type DeliveryResult<T> = { ok: true; value: T } | { ok: false; error: string };

async function failure(response: Response, fallback: string): Promise<string> {
  try {
    const payload = (await response.json()) as { message?: string; errors?: Record<string, string[]> };
    const first = payload.errors ? Object.values(payload.errors).flat()[0] : null;
    return first ?? payload.message ?? fallback;
  } catch {
    return response.status === 403 ? "You do not have permission to do this." : fallback;
  }
}

const unreachable = "The service could not be reached. Check your connection and try again.";

export async function loadDelivery(role: string): Promise<DeliveryResult<ClaimsPackDelivery>> {
  try {
    const response = await fetch(`${apiBaseUrl}/api/v1/admin/claims-pack-delivery`, { headers: scopedHeaders(role, ""), cache: "no-store" });
    return response.ok ? { ok: true, value: (await response.json()) as ClaimsPackDelivery } : { ok: false, error: await failure(response, "The settings could not be loaded.") };
  } catch {
    return { ok: false, error: unreachable };
  }
}

export async function saveDelivery(role: string, input: { recipients: string[]; trigger: string; triggerStatus: string | null }): Promise<DeliveryResult<ClaimsPackDelivery>> {
  try {
    const response = await fetch(`${apiBaseUrl}/api/v1/admin/claims-pack-delivery`, {
      method: "PUT",
      headers: { ...scopedHeaders(role, ""), "Content-Type": "application/json" },
      body: JSON.stringify(input),
    });
    return response.ok ? { ok: true, value: (await response.json()) as ClaimsPackDelivery } : { ok: false, error: await failure(response, "The settings could not be saved.") };
  } catch {
    return { ok: false, error: unreachable };
  }
}

export async function loadHistory(role: string, caseId: string): Promise<DeliveryResult<ClaimsPackHistoryEntry[]>> {
  try {
    const response = await fetch(`${apiBaseUrl}/api/v1/compliance/cases/${caseId}/claims-pack/history`, { headers: scopedHeaders(role, ""), cache: "no-store" });
    return response.ok ? { ok: true, value: (await response.json()) as ClaimsPackHistoryEntry[] } : { ok: false, error: await failure(response, "The history could not be loaded.") };
  } catch {
    return { ok: false, error: unreachable };
  }
}

export async function sendPackNow(role: string, caseId: string): Promise<DeliveryResult<{ mode: string; recipients: number; linkExpiresAt: string | null }>> {
  try {
    const response = await fetch(`${apiBaseUrl}/api/v1/compliance/cases/${caseId}/claims-pack/send`, { method: "POST", headers: scopedHeaders(role, "") });
    return response.ok ? { ok: true, value: (await response.json()) as { mode: string; recipients: number; linkExpiresAt: string | null } } : { ok: false, error: await failure(response, "The pack could not be sent.") };
  } catch {
    return { ok: false, error: unreachable };
  }
}
