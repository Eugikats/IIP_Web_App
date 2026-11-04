"use client";

import { apiBaseUrl, scopedHeaders } from "@/lib/config";

export type PortalIdentity = {
  slug: string | null;
  displayName: string | null;
  accentColour: string | null;
  logoUrl: string | null;
  supportPhone: string | null;
  supportEmail: string | null;
  hostname: string | null;
  hostnameVerified: boolean;
  /** What the insurer must publish at the challenge path on its own address to prove it controls it. */
  hostnameChallenge: string | null;
  challengePath: string;
  smsWebhookConfigured: boolean;
  exampleLink: string | null;
};

export type PortalIdentityInput = {
  slug: string;
  displayName: string;
  accentColour: string;
  logoUrl: string;
  supportPhone: string;
  supportEmail: string;
  hostname: string;
};

export type PortalResult<T> = { ok: true; value: T } | { ok: false; error: string };

async function failure(response: Response, fallback: string): Promise<string> {
  try {
    const payload = (await response.json()) as { message?: string; errors?: Record<string, string[]> };
    const first = payload.errors ? Object.values(payload.errors).flat()[0] : null;
    return first ?? payload.message ?? fallback;
  } catch {
    return response.status === 403 ? "You do not have permission to change the customer portal." : fallback;
  }
}

const unreachable = "The settings service could not be reached. Check your connection and try again.";

export async function loadPortalIdentity(role: string): Promise<PortalResult<PortalIdentity>> {
  try {
    const response = await fetch(`${apiBaseUrl}/api/v1/admin/portal-identity`, { headers: scopedHeaders(role, ""), cache: "no-store" });
    return response.ok ? { ok: true, value: (await response.json()) as PortalIdentity } : { ok: false, error: await failure(response, "The customer portal details could not be loaded.") };
  } catch {
    return { ok: false, error: unreachable };
  }
}

export async function savePortalIdentity(role: string, input: PortalIdentityInput): Promise<PortalResult<PortalIdentity>> {
  try {
    const response = await fetch(`${apiBaseUrl}/api/v1/admin/portal-identity`, {
      method: "PUT",
      headers: { ...scopedHeaders(role, ""), "Content-Type": "application/json" },
      body: JSON.stringify(input),
    });
    return response.ok ? { ok: true, value: (await response.json()) as PortalIdentity } : { ok: false, error: await failure(response, "The customer portal details could not be saved.") };
  } catch {
    return { ok: false, error: unreachable };
  }
}

export async function verifyPortalHostname(role: string): Promise<PortalResult<PortalIdentity>> {
  try {
    const response = await fetch(`${apiBaseUrl}/api/v1/admin/portal-identity/verify-hostname`, { method: "POST", headers: scopedHeaders(role, "") });
    return response.ok ? { ok: true, value: (await response.json()) as PortalIdentity } : { ok: false, error: await failure(response, "The web address could not be verified.") };
  } catch {
    return { ok: false, error: unreachable };
  }
}

export async function rotateSmsWebhook(role: string): Promise<PortalResult<{ callbackUrl: string }>> {
  try {
    const response = await fetch(`${apiBaseUrl}/api/v1/admin/portal-identity/sms-webhook`, { method: "POST", headers: scopedHeaders(role, "") });
    return response.ok ? { ok: true, value: (await response.json()) as { callbackUrl: string } } : { ok: false, error: await failure(response, "The callback address could not be made.") };
  } catch {
    return { ok: false, error: unreachable };
  }
}
