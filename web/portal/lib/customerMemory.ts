"use client";

import { apiBaseUrl } from "@/lib/config";
import { clientPortalHeaders, type ClientPortalClaim, type ClientPortalSession } from "@/lib/clientPortal";

export type RememberedValue = { key: string; label: string; value: string; fromClaim: string; at: string };
export type DocumentOnFile = { documentId: string; documentTypeKey: string; label: string; fileName: string; fromClaim: string; uploadedAt: string };
export type CustomerMemory = { values: RememberedValue[]; documents: DocumentOnFile[] };

export type MemoryResult<T> = { ok: true; value: T } | { ok: false; error: string };

async function message(response: Response, fallback: string): Promise<string> {
  try {
    const payload = (await response.json()) as { message?: string; errors?: Record<string, string[]> };
    return payload.message ?? Object.values(payload.errors ?? {}).flat()[0] ?? fallback;
  } catch {
    return fallback;
  }
}

const unreachable = "The service could not be reached. Check your connection and try again.";

/** What the customer already gave on earlier claims that this claim asks for. Never throws: a memory problem must not get in the way. */
export async function fetchOnFile(session: ClientPortalSession): Promise<CustomerMemory> {
  if (!session.caseId) return { values: [], documents: [] };
  try {
    const response = await fetch(`${apiBaseUrl}/api/v1/client-portal/claims/${session.caseId}/on-file`, { headers: clientPortalHeaders(session.sessionToken), cache: "no-store" });
    return response.ok ? ((await response.json()) as CustomerMemory) : { values: [], documents: [] };
  } catch {
    return { values: [], documents: [] };
  }
}

/** Before a claim exists: the details to offer in the new-claim form. */
export async function fetchKnownDetails(session: ClientPortalSession, productLine: string, claimType: string): Promise<RememberedValue[]> {
  try {
    const query = `productLine=${encodeURIComponent(productLine)}&claimType=${encodeURIComponent(claimType)}`;
    const response = await fetch(`${apiBaseUrl}/api/v1/client-portal/known-details?${query}`, { headers: clientPortalHeaders(session.sessionToken), cache: "no-store" });
    return response.ok ? ((await response.json()) as CustomerMemory).values : [];
  } catch {
    return [];
  }
}

export async function applyDetailsOnFile(session: ClientPortalSession, keys: string[]): Promise<MemoryResult<CustomerMemory>> {
  try {
    const response = await fetch(`${apiBaseUrl}/api/v1/client-portal/claims/${session.caseId}/on-file/use-details`, {
      method: "POST",
      headers: { ...clientPortalHeaders(session.sessionToken), "Content-Type": "application/json" },
      body: JSON.stringify({ keys }),
    });
    return response.ok ? { ok: true, value: (await response.json()) as CustomerMemory } : { ok: false, error: await message(response, "Those details could not be used.") };
  } catch {
    return { ok: false, error: unreachable };
  }
}

export async function applyDocumentOnFile(session: ClientPortalSession, sourceDocumentId: string, documentTypeKey: string): Promise<MemoryResult<ClientPortalClaim>> {
  try {
    const response = await fetch(`${apiBaseUrl}/api/v1/client-portal/claims/${session.caseId}/on-file/use-document`, {
      method: "POST",
      headers: { ...clientPortalHeaders(session.sessionToken), "Content-Type": "application/json" },
      body: JSON.stringify({ sourceDocumentId, documentTypeKey }),
    });
    return response.ok ? { ok: true, value: (await response.json()) as ClientPortalClaim } : { ok: false, error: await message(response, "That document could not be used.") };
  } catch {
    return { ok: false, error: unreachable };
  }
}
