"use client";

import { apiBaseUrl, scopedHeaders } from "@/lib/config";

export type Outcome<T> = { ok: true; value: T } | { ok: false; error: string; forbidden?: boolean };

export type HeldClaim = { caseId: string; reference: string; status: string; reason: string; placedBy: string; placedAt: string };
export type MatchedClaim = { caseId: string; reference: string; status: string; onLegalHold: boolean; legalHoldReason: string | null; alreadyErased: boolean };
export type ErasurePreview = { claims: MatchedClaim[]; counts: Record<string, number>; claimsThatWillBeErased: number; claimsOnHold: number; nothingFound: boolean };
export type Deletion = {
  deletionId: string; kind: "Retention" | "Erasure"; subject: string; reason: string; actor: string; at: string;
  counts: Record<string, number>; storedFilesDeleted: number; heldClaims: string[]; failures: string[]; total: number;
};
export type Subject = { phone: string; email: string; policyNumber: string };

const unreachable = "The service could not be reached. Check your connection and try again.";

async function failure(response: Response, fallback: string): Promise<string> {
  try {
    const payload = (await response.json()) as { message?: string; errors?: Record<string, string[]> };
    return Object.values(payload.errors ?? {})[0]?.[0] ?? payload.message ?? fallback;
  } catch {
    return response.status === 403 ? "You do not have permission to do this." : fallback;
  }
}

async function call<T>(role: string, method: string, path: string, body: unknown, fallback: string): Promise<Outcome<T>> {
  try {
    const response = await fetch(`${apiBaseUrl}/api/v1/admin/data${path}`, {
      method,
      cache: "no-store",
      headers: { ...scopedHeaders(role, ""), ...(body === undefined ? {} : { "Content-Type": "application/json" }) },
      body: body === undefined ? undefined : JSON.stringify(body),
    });
    if (!response.ok) return { ok: false, error: await failure(response, fallback), forbidden: response.status === 403 };
    return { ok: true, value: (response.status === 204 ? undefined : await response.json()) as T };
  } catch {
    return { ok: false, error: unreachable };
  }
}

const clean = (subject: Subject) => ({ phone: subject.phone.trim() || null, email: subject.email.trim() || null, policyNumber: subject.policyNumber.trim() || null });

export const listHolds = (role: string) => call<HeldClaim[]>(role, "GET", "/legal-holds", undefined, "Legal holds could not be loaded.");
export const placeHold = (role: string, claimReference: string, reason: string) => call<HeldClaim>(role, "POST", "/legal-holds", { claimReference, reason }, "The hold could not be placed.");
export const releaseHold = (role: string, caseId: string) => call<void>(role, "DELETE", `/legal-holds/${caseId}`, undefined, "The hold could not be released.");
export const previewErasure = (role: string, subject: Subject) => call<ErasurePreview>(role, "POST", "/erasure/preview", clean(subject), "Nothing could be previewed.");
export const erase = (role: string, subject: Subject, reason: string, confirm: string) => call<Deletion>(role, "POST", "/erasure", { ...clean(subject), reason, confirm }, "The erasure could not be done.");
export const listDeletions = (role: string) => call<Deletion[]>(role, "GET", "/deletions", undefined, "The evidence could not be loaded.");
export const runRetention = (role: string) => call<Deletion[]>(role, "POST", "/retention/run", undefined, "Retention could not be run.");

export const categoryLabels: Record<string, string> = {
  claims: "Claims (kept without the person)",
  documents: "Documents and stored files",
  communications: "Messages about claims",
  "customer-updates": "Customer updates",
  "reviews-and-tasks": "Reviews, tasks and approvals",
  "ai-records": "AI summaries, checks and stored copies",
  "claims-packs": "Claims packs",
  "unfinished-uploads": "Unfinished uploads",
  "whatsapp-conversations": "WhatsApp conversations",
  "whatsapp-messages": "WhatsApp messages",
  consents: "Consent and language records",
  "customer-portal-access": "Customer portal links and sessions",
  "customer-profiles": "Customer profiles",
  "sales-leads": "Sales leads",
  emails: "Emails",
};
