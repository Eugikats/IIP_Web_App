"use client";

import { apiBaseUrl } from "@/lib/config";
import { clientPortalHeaders, type ClientPortalSession } from "@/lib/clientPortal";

export type PrivacyConsent = {
  state: "NotGiven" | "Given" | "Withdrawn" | "NoticeChanged";
  noticeVersion: number;
  noticeText: string;
  noticeIsDefault: boolean;
  recordedAt: string | null;
  consentedVersion: string | null;
};

export type PrivacyConsentResult = { ok: true; consent: PrivacyConsent } | { ok: false; error: string };

async function failure(response: Response, fallback: string): Promise<string> {
  try {
    const payload = (await response.json()) as { message?: string };
    if (payload.message) return payload.message;
  } catch {
    // Keep the plain fallback for a response that is not JSON.
  }
  return fallback;
}

async function call(session: ClientPortalSession, path: string, init: RequestInit, fallback: string): Promise<PrivacyConsentResult> {
  try {
    const response = await fetch(`${apiBaseUrl}${path}`, {
      ...init,
      headers: { ...(init.body ? { "Content-Type": "application/json" } : {}), ...clientPortalHeaders(session.sessionToken) },
      cache: "no-store",
    });
    if (!response.ok) return { ok: false, error: await failure(response, fallback) };
    return { ok: true, consent: (await response.json()) as PrivacyConsent };
  } catch {
    return { ok: false, error: "The claim service could not be reached. Check your connection and try again." };
  }
}

/** The privacy notice in force and whether this claimant has agreed to it. */
export function fetchPrivacyConsent(session: ClientPortalSession) {
  return call(session, "/api/v1/client-portal/privacy-notice", {}, "We could not load the privacy notice. Try again shortly.");
}

export function agreeToPrivacyNotice(session: ClientPortalSession, noticeVersion: number) {
  return call(session, "/api/v1/client-portal/consent", { method: "POST", body: JSON.stringify({ noticeVersion: `v${noticeVersion}` }) }, "We could not record your agreement. Try again shortly.");
}

export function withdrawPrivacyConsent(session: ClientPortalSession) {
  return call(session, "/api/v1/client-portal/consent/withdraw", { method: "POST" }, "We could not withdraw your permission. Try again shortly.");
}
