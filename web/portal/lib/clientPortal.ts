"use client";

import { apiBaseUrl, defaultTenantId, defaultWorkspaceId } from "@/lib/config";

const SESSION_STORAGE_KEY = "iip.client-portal-session";

export type ClientPortalSession = {
  sessionToken: string;
  /** Null means: verified policyholder, no claim submitted yet, see createClaim. */
  caseId: string | null;
  policyNumber: string;
  productLine: string | null;
  expiresAt: string;
};

export type ClientPortalDocumentRequirement = {
  documentType: string;
  label: string;
  uploaded: boolean;
  status: string;
};

export type ClientPortalTimelineStep = {
  label: string;
  complete: boolean;
  date: string | null;
};

export type ClientPortalMessage = {
  communicationId: string;
  channel: string;
  direction: string;
  messageBody: string;
  createdAt: string;
  deliveryStatus: string;
};

export type ClientPortalReviewRequestSummary = {
  requestId: string;
  status: string;
  reason: string;
  requestedAt: string;
  resolvedAt: string | null;
};

export type ClientPortalClaim = {
  caseId: string;
  productLine: string;
  claimType: string;
  status: string;
  policyNumber: string;
  incidentDate: string;
  createdAt: string;
  updatedAt: string;
  completenessScore: number;
  requiredDocuments: ClientPortalDocumentRequirement[];
  timeline: ClientPortalTimelineStep[];
  messages: ClientPortalMessage[];
  nextSteps: string[];
  reviewRequests: ClientPortalReviewRequestSummary[];
};

/**
 * X-Tenant-Id/X-Workspace-Id are always sent, in every runtime, unlike scopedHeaders (used by
 * the staff app), which omits them outside Development because staff scope is resolved
 * server-side from the Entra token. A claimant has no Entra token: the portal itself is deployed
 * per-tenant (one insurer per Managed Application), so these two values just say which insurer's
 * portal this is, not who the caller is. That is safe to send unauthenticated; the actual access
 * gate is the magic-link session token below (see ClaimantSessionEndpointFilter).
 */
export function clientPortalHeaders(sessionToken?: string | null): Record<string, string> {
  const headers: Record<string, string> = {};
  if (defaultTenantId) headers["X-Tenant-Id"] = defaultTenantId;
  if (defaultWorkspaceId) headers["X-Workspace-Id"] = defaultWorkspaceId;
  if (sessionToken) headers["X-Client-Portal-Session"] = sessionToken;
  return headers;
}

export function loadStoredSession(): ClientPortalSession | null {
  if (typeof window === "undefined") return null;
  try {
    const raw = window.sessionStorage.getItem(SESSION_STORAGE_KEY);
    if (!raw) return null;
    const parsed = JSON.parse(raw) as ClientPortalSession;
    if (!parsed.sessionToken || !parsed.policyNumber || !parsed.expiresAt) return null;
    if (new Date(parsed.expiresAt).getTime() <= Date.now()) {
      window.sessionStorage.removeItem(SESSION_STORAGE_KEY);
      return null;
    }
    return parsed;
  } catch {
    return null;
  }
}

export function storeSession(session: ClientPortalSession) {
  if (typeof window === "undefined") return;
  try {
    window.sessionStorage.setItem(SESSION_STORAGE_KEY, JSON.stringify(session));
  } catch {
    // A viewer with storage blocked simply has to redeem the link again on their next visit.
  }
}

export function clearStoredSession() {
  if (typeof window === "undefined") return;
  try {
    window.sessionStorage.removeItem(SESSION_STORAGE_KEY);
  } catch {
    // Nothing to clean up if storage was never writable.
  }
}

async function safeError(response: Response, fallback: string): Promise<string> {
  try {
    const payload = (await response.json()) as { message?: string; errors?: Record<string, string[]> };
    if (payload.message) return payload.message;
    const first = payload.errors ? Object.values(payload.errors).flat()[0] : null;
    if (first) return first;
  } catch {
    // Keep the safe fallback for non-JSON errors.
  }
  return fallback;
}

function safeNetworkFailure(reason: unknown, fallback: string): string {
  if (reason instanceof TypeError || (reason instanceof Error && /failed to fetch|networkerror/i.test(reason.message))) {
    return "The claim service could not be reached. Check your connection and try again.";
  }
  return fallback;
}

// sandboxLink is only ever set by the API on the Demo profile, when no SMS or email provider is
// connected to deliver the link; Staging and Production never return it.
export type RequestMagicLinkResult = { ok: true; message: string; sandboxLink: string | null } | { ok: false; error: string };

export async function requestMagicLink(policyNumber: string, contactValue: string): Promise<RequestMagicLinkResult> {
  try {
    const response = await fetch(`${apiBaseUrl}/api/v1/client-portal/magic-links`, {
      method: "POST",
      headers: { "Content-Type": "application/json", ...clientPortalHeaders() },
      body: JSON.stringify({ policyNumber, contactValue }),
    });
    if (!response.ok) {
      return { ok: false, error: await safeError(response, "We could not process that request. Try again shortly.") };
    }
    const payload = (await response.json()) as { message: string; sandboxAccessLink?: string | null };
    return { ok: true, message: payload.message, sandboxLink: payload.sandboxAccessLink ?? null };
  } catch (reason) {
    return { ok: false, error: safeNetworkFailure(reason, "We could not process that request. Try again shortly.") };
  }
}

export type RedeemMagicLinkResult = { ok: true; session: ClientPortalSession } | { ok: false; error: string };

export async function redeemMagicLink(token: string): Promise<RedeemMagicLinkResult> {
  try {
    const response = await fetch(`${apiBaseUrl}/api/v1/client-portal/magic-links/redeem`, {
      method: "POST",
      headers: { "Content-Type": "application/json", ...clientPortalHeaders() },
      body: JSON.stringify({ token }),
    });
    if (!response.ok) {
      return { ok: false, error: await safeError(response, "This link is invalid or has expired. Request a new one.") };
    }
    const payload = (await response.json()) as {
      sessionToken: string;
      caseId: string | null;
      expiresAt: string;
      policyNumber: string;
      productLine: string | null;
    };
    return {
      ok: true,
      session: {
        sessionToken: payload.sessionToken,
        caseId: payload.caseId,
        expiresAt: payload.expiresAt,
        policyNumber: payload.policyNumber,
        productLine: payload.productLine,
      },
    };
  } catch (reason) {
    return { ok: false, error: safeNetworkFailure(reason, "This link could not be verified. Try again shortly.") };
  }
}

export type GetClaimResult = { ok: true; claim: ClientPortalClaim } | { ok: false; errorKind: "unauthorized" | "unavailable"; error: string };

export async function getClaim(session: ClientPortalSession): Promise<GetClaimResult> {
  if (!session.caseId) {
    return { ok: false, errorKind: "unavailable", error: "No claim has been submitted on this access link yet." };
  }
  try {
    const response = await fetch(`${apiBaseUrl}/api/v1/client-portal/claims/${session.caseId}`, {
      headers: clientPortalHeaders(session.sessionToken),
    });
    if (response.status === 401 || response.status === 404) {
      return { ok: false, errorKind: "unauthorized", error: "Your access link has expired or is no longer valid. Request a new one." };
    }
    if (!response.ok) {
      return { ok: false, errorKind: "unavailable", error: await safeError(response, `Your claim could not be loaded.`) };
    }
    return { ok: true, claim: (await response.json()) as ClientPortalClaim };
  } catch (reason) {
    return { ok: false, errorKind: "unavailable", error: safeNetworkFailure(reason, "Your claim could not be loaded.") };
  }
}

export type UploadDocumentsResult = { ok: true; claim: ClientPortalClaim } | { ok: false; error: string };

export async function uploadClaimDocuments(session: ClientPortalSession, files: File[]): Promise<UploadDocumentsResult> {
  if (!session.caseId) {
    return { ok: false, error: "No claim has been submitted on this access link yet." };
  }
  try {
    const form = new FormData();
    files.forEach((file) => form.append("files", file));
    const response = await fetch(`${apiBaseUrl}/api/v1/client-portal/claims/${session.caseId}/documents`, {
      method: "POST",
      headers: clientPortalHeaders(session.sessionToken),
      body: form,
    });
    if (!response.ok) {
      return { ok: false, error: await safeError(response, "We could not upload your document. Try again shortly.") };
    }
    return { ok: true, claim: (await response.json()) as ClientPortalClaim };
  } catch (reason) {
    return { ok: false, error: safeNetworkFailure(reason, "We could not upload your document.") };
  }
}

export type RequestReviewResult = { ok: true } | { ok: false; error: string };

export async function requestClaimReview(session: ClientPortalSession, reason: string): Promise<RequestReviewResult> {
  if (!session.caseId) {
    return { ok: false, error: "No claim has been submitted on this access link yet." };
  }
  try {
    const response = await fetch(`${apiBaseUrl}/api/v1/client-portal/claims/${session.caseId}/request-review`, {
      method: "POST",
      headers: { "Content-Type": "application/json", ...clientPortalHeaders(session.sessionToken) },
      body: JSON.stringify({ reason }),
    });
    if (!response.ok) {
      return { ok: false, error: await safeError(response, "We could not submit your request. Try again shortly.") };
    }
    return { ok: true };
  } catch (reason) {
    return { ok: false, error: safeNetworkFailure(reason, "We could not submit your request.") };
  }
}

export type MotorFnolDetails = {
  vehicleRegistration?: string;
  accidentTime?: string;
  accidentLocation?: string;
  driverName?: string;
  drivingPermitNumber?: string;
  thirdPartyInvolved?: string;
  injuryIndicator?: string;
  policeReportStatus?: string;
};

export type CreateClaimRequest = {
  productLine: string;
  claimType: string;
  incidentDate: string;
  incidentDescription: string;
  motorFnolDetails?: MotorFnolDetails;
};

export type CreateClaimResult = { ok: true; caseId: string } | { ok: false; error: string };

export async function createClaim(session: ClientPortalSession, request: CreateClaimRequest): Promise<CreateClaimResult> {
  try {
    const response = await fetch(`${apiBaseUrl}/api/v1/client-portal/claims`, {
      method: "POST",
      headers: { "Content-Type": "application/json", ...clientPortalHeaders(session.sessionToken) },
      body: JSON.stringify(request),
    });
    if (!response.ok) {
      return { ok: false, error: await safeError(response, "We could not submit your claim. Try again shortly.") };
    }
    const payload = (await response.json()) as { caseId: string };
    return { ok: true, caseId: payload.caseId };
  } catch (reason) {
    return { ok: false, error: safeNetworkFailure(reason, "We could not submit your claim.") };
  }
}

export type ClientPortalCoverage = {
  name: string;
  limit: string;
  deductible: string;
  insuredObject: string;
  description: string;
};

export type ClientPortalPolicy = {
  policyNumber: string;
  productLine: string;
  coverType: string | null;
  effectiveFrom: string | null;
  effectiveTo: string | null;
  coverStatus: string;
  annualPremium: number | null;
  premiumCurrency: string | null;
  sumInsured: number | null;
  premiumStatus: string;
  coverages: ClientPortalCoverage[];
  recordSource: string;
  lastRefreshedAt: string | null;
};

export type GetPolicyResult = { ok: true; policy: ClientPortalPolicy } | { ok: false; error: string };

export async function getPolicy(session: ClientPortalSession): Promise<GetPolicyResult> {
  try {
    const response = await fetch(`${apiBaseUrl}/api/v1/client-portal/policy`, {
      headers: clientPortalHeaders(session.sessionToken),
    });
    if (!response.ok) {
      return { ok: false, error: await safeError(response, "Your policy could not be loaded.") };
    }
    return { ok: true, policy: (await response.json()) as ClientPortalPolicy };
  } catch (reason) {
    return { ok: false, error: safeNetworkFailure(reason, "Your policy could not be loaded.") };
  }
}

export type PolicyQuestionSource = {
  knowledgeDocumentId: string;
  chunkId: string;
  documentName: string;
  productLine: string;
  version: string;
  excerpt: string;
  label: string;
};

export type AskPolicyQuestionResult =
  | { ok: true; answerText: string; escalationRequired: boolean; sources: PolicyQuestionSource[] }
  | { ok: false; error: string };

export async function askPolicyQuestion(session: ClientPortalSession, question: string): Promise<AskPolicyQuestionResult> {
  try {
    const response = await fetch(`${apiBaseUrl}/api/v1/client-portal/policy-questions`, {
      method: "POST",
      headers: { "Content-Type": "application/json", ...clientPortalHeaders(session.sessionToken) },
      body: JSON.stringify({ question }),
    });
    if (!response.ok) {
      return { ok: false, error: await safeError(response, "We could not answer that question. Try again shortly.") };
    }
    const payload = (await response.json()) as { answerText: string; escalationRequired: boolean; sources: PolicyQuestionSource[] };
    return { ok: true, answerText: payload.answerText, escalationRequired: payload.escalationRequired, sources: payload.sources };
  } catch (reason) {
    return { ok: false, error: safeNetworkFailure(reason, "We could not answer that question.") };
  }
}

export type RequestContactDetailChangeResult = { ok: true; message: string } | { ok: false; error: string };

export async function requestContactDetailChange(
  session: ClientPortalSession,
  email: string | null,
  phone: string | null,
  note: string | null,
): Promise<RequestContactDetailChangeResult> {
  try {
    const response = await fetch(`${apiBaseUrl}/api/v1/client-portal/contact-detail-changes`, {
      method: "POST",
      headers: { "Content-Type": "application/json", ...clientPortalHeaders(session.sessionToken) },
      body: JSON.stringify({ email, phone, note }),
    });
    if (!response.ok) {
      return { ok: false, error: await safeError(response, "We could not submit your request. Try again shortly.") };
    }
    const payload = (await response.json()) as { message: string };
    return { ok: true, message: payload.message };
  } catch (reason) {
    return { ok: false, error: safeNetworkFailure(reason, "We could not submit your request.") };
  }
}
