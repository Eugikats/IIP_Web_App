"use client";

import { apiBaseUrl, scopedHeaders } from "@/lib/config";

export type TenantSettings = {
  requireIraApprovalForPublish: boolean;
  missingDocumentReminderDays: number;
  slaWarningLeadHours: number;
  seatLimit: number;
  claimsMeterLimit: number;
  updatedByUserId: string | null;
  updatedAt: string | null;
  /** The workspace's own privacy notice, or null while the default text is in use. */
  privacyNoticeText: string | null;
  privacyNoticeVersion: number;
  defaultPrivacyNoticeText: string;
  defaults: {
    requireIraApprovalForPublish: boolean;
    missingDocumentReminderDays: number;
    slaWarningLeadHours: number;
    seatLimit: number;
    claimsMeterLimit: number;
  };
};

export type TenantSettingsInput = Partial<Omit<TenantSettings, "updatedByUserId" | "updatedAt" | "defaults" | "privacyNoticeVersion" | "defaultPrivacyNoticeText">>;

export type TenantSettingsResult = { ok: true; value: TenantSettings } | { ok: false; error: string };

async function failure(response: Response, fallback: string): Promise<string> {
  try {
    const payload = (await response.json()) as { message?: string; errors?: Record<string, string[]> };
    const first = payload.errors ? Object.values(payload.errors).flat()[0] : null;
    return first ?? payload.message ?? fallback;
  } catch {
    return response.status === 403 ? "You do not have permission to change these settings." : fallback;
  }
}

export async function loadTenantSettings(role: string): Promise<TenantSettingsResult> {
  try {
    const response = await fetch(`${apiBaseUrl}/api/v1/admin/tenant-settings`, { headers: scopedHeaders(role, ""), cache: "no-store" });
    if (!response.ok) return { ok: false, error: await failure(response, "The settings could not be loaded.") };
    return { ok: true, value: (await response.json()) as TenantSettings };
  } catch {
    return { ok: false, error: "The settings service could not be reached. Check your connection and try again." };
  }
}

export async function saveTenantSettings(role: string, input: TenantSettingsInput): Promise<TenantSettingsResult> {
  try {
    const response = await fetch(`${apiBaseUrl}/api/v1/admin/tenant-settings`, {
      method: "PUT",
      headers: { ...scopedHeaders(role, ""), "Content-Type": "application/json" },
      body: JSON.stringify(input),
    });
    if (!response.ok) return { ok: false, error: await failure(response, "The settings could not be saved.") };
    return { ok: true, value: (await response.json()) as TenantSettings };
  } catch {
    return { ok: false, error: "The settings service could not be reached. Check your connection and try again." };
  }
}
