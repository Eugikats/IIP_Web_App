"use client";

import { apiBaseUrl, scopedHeaders } from "@/lib/config";
import { clientPortalHeaders, type ClientPortalSession } from "@/lib/clientPortal";

export type ClaimFieldType = "Text" | "Number" | "Date" | "Select" | "Phone" | "Currency" | "YesNo";

/** A question as asked of a person, with the claim's current answer if there is one. */
export type ClaimFieldValue = {
  key: string;
  label: string;
  type: ClaimFieldType;
  required: boolean;
  options: string[];
  helpText: string | null;
  sortOrder: number;
  customerVisible: boolean;
  value: string | null;
};

export type ClaimFieldForm = {
  claimTypeKey: string | null;
  /** True when the product has configured its own fields; false means the built-in questions. */
  configured: boolean;
  fields: ClaimFieldValue[];
};

export type ClaimFieldResult<T> = { ok: true; value: T } | { ok: false; error: string; fieldErrors: Record<string, string> };

async function readFailure(response: Response, fallback: string): Promise<{ error: string; fieldErrors: Record<string, string> }> {
  try {
    const payload = (await response.json()) as { message?: string; errors?: Record<string, string[]> };
    const fieldErrors: Record<string, string> = {};
    let first: string | null = null;
    for (const [name, messages] of Object.entries(payload.errors ?? {})) {
      const message = messages?.[0];
      if (!message) continue;
      first ??= message;
      fieldErrors[name.replace(/^fieldValues\./, "")] = message;
    }
    return { error: first ?? payload.message ?? fallback, fieldErrors };
  } catch {
    return { error: fallback, fieldErrors: {} };
  }
}

async function call<T>(input: string, init: RequestInit, fallback: string): Promise<ClaimFieldResult<T>> {
  try {
    const response = await fetch(input, { cache: "no-store", ...init });
    if (!response.ok) return { ok: false, ...(await readFailure(response, fallback)) };
    return { ok: true, value: (await response.json()) as T };
  } catch {
    return { ok: false, error: "The connection failed. Check it and try again.", fieldErrors: {} };
  }
}

const json = { "Content-Type": "application/json" };

export function fetchClaimFieldForm(role: string, productLine: string, claimType: string) {
  const query = new URLSearchParams({ productLine, claimType });
  return call<ClaimFieldForm>(`${apiBaseUrl}/api/v1/claim-fields?${query}`, { headers: scopedHeaders(role, "") }, "The claim questions could not be loaded.");
}

export function fetchClaimFields(role: string, caseId: string) {
  return call<ClaimFieldForm>(`${apiBaseUrl}/api/v1/cases/${caseId}/fields`, { headers: scopedHeaders(role, "") }, "The claim details could not be loaded.");
}

export function saveClaimFieldValues(role: string, caseId: string, values: Record<string, string>) {
  return call<ClaimFieldForm>(
    `${apiBaseUrl}/api/v1/cases/${caseId}/field-values`,
    { method: "PUT", headers: { ...scopedHeaders(role, ""), ...json }, body: JSON.stringify({ values }) },
    "The details could not be saved.",
  );
}

export function fetchClaimantFieldForm(session: ClientPortalSession, productLine: string, claimType: string) {
  const query = new URLSearchParams({ productLine, claimType });
  return call<ClaimFieldForm>(
    `${apiBaseUrl}/api/v1/client-portal/claim-fields?${query}`,
    { headers: clientPortalHeaders(session.sessionToken) },
    "The questions could not be loaded.",
  );
}

export function fetchClaimantClaimFields(session: ClientPortalSession, caseId: string) {
  return call<ClaimFieldForm>(
    `${apiBaseUrl}/api/v1/client-portal/claims/${caseId}/fields`,
    { headers: clientPortalHeaders(session.sessionToken) },
    "Your details could not be loaded.",
  );
}

export function saveClaimantFieldValues(session: ClientPortalSession, caseId: string, values: Record<string, string>) {
  return call<ClaimFieldForm>(
    `${apiBaseUrl}/api/v1/client-portal/claims/${caseId}/field-values`,
    { method: "PUT", headers: { ...clientPortalHeaders(session.sessionToken), ...json }, body: JSON.stringify({ values }) },
    "Your details could not be saved.",
  );
}

/** Required questions that have no answer yet, by label. */
export function missingRequired(fields: ClaimFieldValue[], values: Record<string, string>): string[] {
  return fields.filter((field) => field.required && !(values[field.key] ?? "").trim()).map((field) => field.label);
}
