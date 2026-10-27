"use client";

import { apiBaseUrl, scopedHeaders } from "@/lib/config";

export type DemoScenario = {
  id: string;
  title: string;
  summary: string;
  emphasis: string;
  brandName: string;
  isBank: boolean;
  lineFocus: "Both" | "Life" | "General";
  partnerInsurers: string[];
  ready: boolean;
  organisationId: string | null;
  workspaceId: string | null;
};

export type DemoScenarioResult<T> =
  | { ok: true; value: T }
  | { ok: false; error: string; notAvailable: boolean };

async function failure(response: Response, fallback: string): Promise<DemoScenarioResult<never>> {
  let message = fallback;
  try {
    const payload = (await response.json()) as { detail?: string; title?: string; message?: string };
    message = payload.detail || payload.message || payload.title || fallback;
  } catch {
    // Keep the fallback for a response that is not JSON.
  }
  return { ok: false, error: message, notAvailable: response.status === 403 || response.status === 404 };
}

function network(): DemoScenarioResult<never> {
  return { ok: false, error: "The service could not be reached. Check your connection and try again.", notAvailable: false };
}

export async function listDemoScenarios(role: string): Promise<DemoScenarioResult<DemoScenario[]>> {
  try {
    const response = await fetch(`${apiBaseUrl}/api/v1/demo/scenarios`, { headers: scopedHeaders(role, "") });
    if (!response.ok) return failure(response, "The demonstrations could not be loaded.");
    return { ok: true, value: (await response.json()) as DemoScenario[] };
  } catch {
    return network();
  }
}

export async function activateDemoScenario(role: string, scenarioId: string): Promise<DemoScenarioResult<DemoScenario>> {
  try {
    const response = await fetch(`${apiBaseUrl}/api/v1/demo/scenarios/${encodeURIComponent(scenarioId)}/activate`, {
      method: "POST",
      headers: scopedHeaders(role, ""),
    });
    if (!response.ok) return failure(response, "This demonstration could not be set up.");
    return { ok: true, value: (await response.json()) as DemoScenario };
  } catch {
    return network();
  }
}
