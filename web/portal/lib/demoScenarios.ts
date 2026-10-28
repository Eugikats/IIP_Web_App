"use client";

import { apiBaseUrl, getActiveOrganisationOverride, getActiveWorkspaceOverride, scopedHeaders, setActiveOrganisationOverride } from "@/lib/config";

export type DemoScenario = {
  id: string;
  title: string;
  summary: string;
  emphasis: string;
  brandName: string;
  shortName: string;
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

/**
 * Makes the WhatsApp assistant speak as this world. The WhatsApp number answers for the workspace it is connected to,
 * which is not the organisation a demonstration lives in, so choosing a world must also set the world's name on that
 * workspace. Uses the ordinary brand endpoint, so it is permission-checked and audited like any change to the
 * assistant's identity. A person without permission there gets a clear answer and the world still opens.
 */
export async function applyWorldToWhatsApp(role: string, scenario: DemoScenario): Promise<DemoScenarioResult<null>> {
  const organisation = getActiveOrganisationOverride();
  const workspace = getActiveWorkspaceOverride();
  // Act in the workspace the number belongs to, not in whichever world was open a moment ago.
  setActiveOrganisationOverride(null, null);
  try {
    const response = await fetch(`${apiBaseUrl}/api/v1/workspace/brand`, {
      method: "PUT",
      headers: {
        ...scopedHeaders(role, ""),
        "Content-Type": "application/json",
        "X-Audit-Reason": `Presenting ${scenario.title}: the WhatsApp assistant follows the demonstration that is open.`,
      },
      body: JSON.stringify({
        organisation: scenario.brandName,
        shortName: scenario.shortName,
        partnerInsurers: scenario.partnerInsurers,
        lineFocus: scenario.lineFocus,
      }),
    });
    if (!response.ok) return failure(response, "The WhatsApp assistant could not be switched to this world.");
    return { ok: true, value: null };
  } catch {
    return network();
  } finally {
    setActiveOrganisationOverride(organisation, workspace);
  }
}
