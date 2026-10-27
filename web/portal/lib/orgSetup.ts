"use client";

import { apiBaseUrl, scopedHeaders } from "@/lib/config";

export type SetupStep = { key: string; title: string; hint: string; done: boolean; route: string };
export type SetupChecklist = { organisationId: string; completed: number; total: number; steps: SetupStep[] };

export type CompanyProfile = {
  legalName: string;
  tradingName: string | null;
  licenceKind: "Insurer" | "Broker" | "Agent" | "BancassuranceDistributor";
  licenceNumber: string;
  regulator: string;
  country: string;
  contactName: string;
  contactEmail: string;
  contactPhone: string | null;
  website: string | null;
  about: string;
  updatedAt: string;
  updatedBy: string;
};

export type TermsAcceptance = {
  termsVersion: string;
  dataProcessingVersion: string;
  acceptedByUserId: string;
  acceptedByName: string;
  acceptedByTitle: string;
  acceptedAt: string;
};

export type ProfileState = {
  profile: CompanyProfile | null;
  acceptance: TermsAcceptance | null;
  acceptedCurrent: boolean;
  termsVersion: string;
  dataProcessingVersion: string;
};

export type SecurityState = {
  version: string;
  statements: { key: string; title: string; statement: string; status: "InPlace" | "Partly" | "NotOffered" }[];
  acknowledgement: { version: string; acknowledgedByUserId: string; acknowledgedByName: string; acknowledgedAt: string } | null;
  readCurrent: boolean;
};

export type SetupResult<T> = { ok: true; value: T } | { ok: false; error: string; forbidden: boolean };

async function failure(response: Response, fallback: string): Promise<SetupResult<never>> {
  try {
    const payload = (await response.json()) as { detail?: string; message?: string; errors?: Record<string, string[]> };
    return {
      ok: false,
      error: payload.detail || payload.message || (payload.errors ? Object.values(payload.errors).flat()[0] : undefined) || fallback,
      forbidden: response.status === 403,
    };
  } catch {
    return { ok: false, error: fallback, forbidden: response.status === 403 };
  }
}

const NETWORK: SetupResult<never> = { ok: false, error: "The service could not be reached. Check your connection and try again.", forbidden: false };
const headers = (role: string) => ({ "Content-Type": "application/json", ...scopedHeaders(role, "") });

async function call<T>(role: string, path: string, method: string, body: unknown, fallback: string): Promise<SetupResult<T>> {
  try {
    const response = await fetch(`${apiBaseUrl}${path}`, { method, headers: headers(role), body: body === undefined ? undefined : JSON.stringify(body) });
    return response.ok ? { ok: true, value: (await response.json()) as T } : failure(response, fallback);
  } catch {
    return NETWORK;
  }
}

export const getChecklist = (role: string) => call<SetupChecklist>(role, "/api/v1/organisation/onboarding", "GET", undefined, "The set-up steps could not be loaded.");
export const getProfile = (role: string) => call<ProfileState>(role, "/api/v1/organisation/profile", "GET", undefined, "The profile could not be loaded.");
export const saveProfile = (role: string, form: Omit<CompanyProfile, "updatedAt" | "updatedBy">) =>
  call<CompanyProfile>(role, "/api/v1/organisation/profile", "PUT", form, "The profile could not be saved.");
export const acceptTerms = (
  role: string,
  body: { fullName: string; title: string; authorised: boolean; termsVersion: string; dataProcessingVersion: string },
) => call<TermsAcceptance>(role, "/api/v1/organisation/terms", "POST", body, "The acceptance could not be recorded.");

export const getSecurity = (role: string) => call<SecurityState>(role, "/api/v1/organisation/security", "GET", undefined, "The security statements could not be loaded.");
export const acknowledgeSecurity = (role: string, body: { fullName: string; version: string }) =>
  call<SecurityState["acknowledgement"]>(role, "/api/v1/organisation/security-ack", "POST", body, "The acknowledgement could not be recorded.");

export type WhatsAppOnboardingRequest = { id: string; phoneNumber: string; displayName: string; contactEmail: string; status: string; createdAt: string; wabaId?: string | null; phoneNumberId?: string | null; codeMethod?: string | null; registrationSucceeded?: boolean };
export type ManagedAvailability = "Not configured" | "Available";
export const getWhatsAppOnboarding = (role: string) => call<{ status: string; request: WhatsAppOnboardingRequest | null; metaVerification: ManagedAvailability }>(role, "/api/v1/integrations/whatsapp/onboarding", "GET", undefined, "The WhatsApp setup request could not be loaded.");
export const submitWhatsAppOnboarding = (role: string, body: { phoneNumber: string; displayName: string; contactEmail: string; consent: boolean }) =>
  call<WhatsAppOnboardingRequest>(role, "/api/v1/integrations/whatsapp/onboarding", "POST", body, "The WhatsApp setup request could not be submitted.");
export const cancelWhatsAppOnboarding = (role: string) =>
  call<WhatsAppOnboardingRequest>(role, "/api/v1/integrations/whatsapp/onboarding", "DELETE", undefined, "The WhatsApp setup request could not be cancelled.");
export const requestWhatsAppCode = (role: string, method: "SMS" | "VOICE") =>
  call<WhatsAppOnboardingRequest>(role, "/api/v1/integrations/whatsapp/onboarding/request-code", "POST", { method }, "Meta could not send a code.");
export const verifyWhatsAppCode = (role: string, code: string) =>
  call<WhatsAppOnboardingRequest>(role, "/api/v1/integrations/whatsapp/onboarding/verify-code", "POST", { code }, "Meta could not verify the code.");
export const registerWhatsAppNumber = (role: string, pin: string) =>
  call<WhatsAppOnboardingRequest>(role, "/api/v1/integrations/whatsapp/onboarding/register", "POST", { pin }, "Meta could not register the number.");
export type WhatsAppSupportQueue = { items: { tenantId: string; workspaceId: string; request: WhatsAppOnboardingRequest }[]; metaVerification: ManagedAvailability };
export const getWhatsAppSupportQueue = (role: string) => call<WhatsAppSupportQueue>(role, "/api/v1/admin/whatsapp/onboarding", "GET", undefined, "The support queue could not be loaded.");
export const assignWhatsAppNumber = (role: string, tenantId: string, workspaceId: string, wabaId: string, phoneNumberId: string) =>
  call<WhatsAppOnboardingRequest>(role, `/api/v1/admin/whatsapp/onboarding/${encodeURIComponent(tenantId)}/${encodeURIComponent(workspaceId)}/assign`, "POST", { wabaId, phoneNumberId }, "Meta could not confirm the number assignment.");
