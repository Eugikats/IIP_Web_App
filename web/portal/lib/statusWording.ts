"use client";

import { apiBaseUrl, scopedHeaders } from "@/lib/config";

export type StatusWordingRow = {
  status: string;
  staffLabel: string;
  defaultHeadline: string;
  defaultDetail: string;
  headline: string;
  detail: string;
  customised: boolean;
};

export type StatusWordingResult<T> = { ok: true; value: T } | { ok: false; error: string };

async function failure(response: Response, fallback: string): Promise<string> {
  try {
    const payload = (await response.json()) as { message?: string; errors?: Record<string, string[]> };
    const first = payload.errors ? Object.values(payload.errors).flat()[0] : null;
    return first ?? payload.message ?? fallback;
  } catch {
    return response.status === 403 ? "You do not have permission to change this wording." : fallback;
  }
}

export async function loadStatusWording(role: string): Promise<StatusWordingResult<StatusWordingRow[]>> {
  try {
    const response = await fetch(`${apiBaseUrl}/api/v1/admin/claim-status-wording`, { headers: scopedHeaders(role, ""), cache: "no-store" });
    if (!response.ok) return { ok: false, error: await failure(response, "The wording could not be loaded.") };
    return { ok: true, value: (await response.json()) as StatusWordingRow[] };
  } catch {
    return { ok: false, error: "The service could not be reached. Check your connection and try again." };
  }
}

/** Sends the wording for every status that differs from its default; the rest go back to the default. */
export async function saveStatusWording(role: string, wording: Record<string, { headline: string; detail: string }>): Promise<StatusWordingResult<StatusWordingRow[]>> {
  try {
    const response = await fetch(`${apiBaseUrl}/api/v1/admin/claim-status-wording`, {
      method: "PUT",
      headers: { ...scopedHeaders(role, ""), "Content-Type": "application/json" },
      body: JSON.stringify({ wording }),
    });
    if (!response.ok) return { ok: false, error: await failure(response, "The wording could not be saved.") };
    return { ok: true, value: (await response.json()) as StatusWordingRow[] };
  } catch {
    return { ok: false, error: "The service could not be reached. Check your connection and try again." };
  }
}
