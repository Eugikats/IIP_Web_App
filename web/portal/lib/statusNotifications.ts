"use client";

import { apiBaseUrl, scopedHeaders } from "@/lib/config";

export type StatusNotificationRow = {
  status: string;
  staffLabel: string;
  sendsMessage: boolean;
  needsApproval: boolean;
  whatsAppTemplateName: string | null;
  whatsAppTemplateLanguage: string | null;
};

export type StatusNotificationSettings = {
  notifyCustomerOnStatusChange: boolean;
  autoSendStatusMessages: boolean;
  statuses: StatusNotificationRow[];
};

export type StatusNotificationResult = { ok: true; value: StatusNotificationSettings } | { ok: false; error: string };

async function failure(response: Response, fallback: string): Promise<string> {
  try {
    const payload = (await response.json()) as { message?: string; errors?: Record<string, string[]> };
    const first = payload.errors ? Object.values(payload.errors).flat()[0] : null;
    return first ?? payload.message ?? fallback;
  } catch {
    return response.status === 403 ? "You do not have permission to change this." : fallback;
  }
}

export async function loadStatusNotifications(role: string): Promise<StatusNotificationResult> {
  try {
    const response = await fetch(`${apiBaseUrl}/api/v1/admin/status-notifications`, { headers: scopedHeaders(role, ""), cache: "no-store" });
    if (!response.ok) return { ok: false, error: await failure(response, "The settings could not be loaded.") };
    return { ok: true, value: (await response.json()) as StatusNotificationSettings };
  } catch {
    return { ok: false, error: "The service could not be reached. Check your connection and try again." };
  }
}

export async function saveStatusNotifications(
  role: string,
  input: { notifyCustomerOnStatusChange: boolean; autoSendStatusMessages: boolean; whatsAppTemplates: Record<string, { name: string; language: string }> },
): Promise<StatusNotificationResult> {
  try {
    const response = await fetch(`${apiBaseUrl}/api/v1/admin/status-notifications`, {
      method: "PUT",
      headers: { ...scopedHeaders(role, ""), "Content-Type": "application/json" },
      body: JSON.stringify(input),
    });
    if (!response.ok) return { ok: false, error: await failure(response, "The settings could not be saved.") };
    return { ok: true, value: (await response.json()) as StatusNotificationSettings };
  } catch {
    return { ok: false, error: "The service could not be reached. Check your connection and try again." };
  }
}
