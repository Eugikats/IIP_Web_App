import { apiBaseUrl, scopedHeaders } from "@/lib/config";
import { getEntraApiAccessToken } from "@/lib/entraAuth";

export type CreditsSummary = {
  metered: boolean;
  period: string;
  planName: string;
  included: number;
  used: number;
  remaining: number;
  percentUsed: number;
  state: "ok" | "near_limit" | "exhausted" | "not_metered";
  calls: number;
  byFeature: { feature: string; label: string; credits: number; calls: number }[];
  byDay: { day: string; credits: number }[];
  costUsd: number | null;
  canAddCredits: boolean;
};

export type CreditsResult = { ok: true; summary: CreditsSummary } | { ok: false; kind: "no_permission" | "unavailable"; message: string };

export async function getCreditsSummary(role: string): Promise<CreditsResult> {
  try {
    const token = await getEntraApiAccessToken();
    const response = await fetch(`${apiBaseUrl}/api/v1/billing/credits`, {
      cache: "no-store",
      headers: { ...scopedHeaders(role, ""), ...(token ? { Authorization: `Bearer ${token}` } : {}) },
    });
    if (response.status === 403) return { ok: false, kind: "no_permission", message: "Your role cannot see AI credits." };
    if (!response.ok) return { ok: false, kind: "unavailable", message: "AI credits could not be read just now." };
    return { ok: true, summary: (await response.json()) as CreditsSummary };
  } catch {
    return { ok: false, kind: "unavailable", message: "AI credits could not be read just now." };
  }
}
