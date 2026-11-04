"use client";

import { apiBaseUrl, platformSessionKey, scopedHeaders, setApiAccessToken } from "@/lib/config";

/** What this deployment offers on the sign-in and sign-up pages. */
export type AuthCapabilities = { signUp: boolean; passwordSignIn: boolean; entra: boolean };

export type PlatformUser = { userId: string; name: string; email: string; tenantId: string; workspaceId: string; role: string };

export type WorkspaceChoice = { tenantId: string; workspaceId: string; name: string };

export type AuthResult<T> = { ok: true; value: T } | { ok: false; error: string; fields?: Record<string, string> };

const SESSION_KEY = platformSessionKey;

type StoredSession = { accessToken: string; expiresAt: string; user: PlatformUser };

async function readError(response: Response, fallback: string): Promise<{ error: string; fields?: Record<string, string> }> {
  try {
    const payload = (await response.json()) as { message?: string; detail?: string; errors?: Record<string, string[]> };
    const fields: Record<string, string> = {};
    for (const [key, value] of Object.entries(payload.errors ?? {})) fields[key] = value[0];
    const first = Object.values(fields)[0];
    return { error: payload.message ?? first ?? payload.detail ?? fallback, fields: Object.keys(fields).length > 0 ? fields : undefined };
  } catch {
    return { error: fallback };
  }
}

const unreachable = "The service could not be reached. Check your connection and try again.";

async function postJson<T>(path: string, body: unknown, fallback: string): Promise<AuthResult<T>> {
  try {
    const response = await fetch(`${apiBaseUrl}${path}`, { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify(body) });
    if (!response.ok) return { ok: false, ...(await readError(response, fallback)) };
    return { ok: true, value: (await response.json().catch(() => ({}))) as T };
  } catch {
    return { ok: false, error: unreachable };
  }
}

export async function loadCapabilities(): Promise<AuthCapabilities> {
  try {
    const response = await fetch(`${apiBaseUrl}/api/v1/auth/capabilities`, { cache: "no-store" });
    return response.ok ? ((await response.json()) as AuthCapabilities) : { signUp: false, passwordSignIn: false, entra: false };
  } catch {
    return { signUp: false, passwordSignIn: false, entra: false };
  }
}

export const signUp = (body: { companyName: string; adminName: string; email: string; password: string; acceptTerms: boolean; website: string }) =>
  postJson<{ message: string }>("/api/v1/signup", body, "Sign-up could not be started.");

export const verifySignUp = (token: string) =>
  postJson<{ email: string; message: string }>("/api/v1/signup/verify", { token }, "The confirmation link could not be used.");

export const requestPasswordReset = (email: string) =>
  postJson<{ message: string }>("/api/v1/auth/password/forgot", { email }, "The reset email could not be sent.");

export const resetPassword = (token: string, password: string) =>
  postJson<{ message: string }>("/api/v1/auth/password/reset", { token, password }, "The password could not be changed.");

export type InvitationPreview = { workspaceName: string; email: string; role: string; hasAccount: boolean };

export async function previewInvitation(token: string): Promise<AuthResult<InvitationPreview>> {
  try {
    const response = await fetch(`${apiBaseUrl}/api/v1/invitations/preview?token=${encodeURIComponent(token)}`, { cache: "no-store" });
    if (!response.ok) return { ok: false, ...(await readError(response, "This invitation is no longer valid.")) };
    return { ok: true, value: (await response.json()) as InvitationPreview };
  } catch {
    return { ok: false, error: unreachable };
  }
}

export const acceptInvitation = (body: { token: string; name: string; password: string }) =>
  postJson<{ email: string; message: string }>("/api/v1/invitations/accept", body, "The invitation could not be accepted.");

/** Signs in. More than one workspace comes back as a choice for the person to make. */
export async function signIn(email: string, password: string, workspace?: WorkspaceChoice): Promise<AuthResult<{ user: PlatformUser } | { choose: WorkspaceChoice[] }>> {
  try {
    const response = await fetch(`${apiBaseUrl}/api/v1/auth/sign-in`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ email, password, workspaceId: workspace?.workspaceId, tenantId: workspace?.tenantId }),
    });
    if (response.status === 409) {
      const payload = (await response.json()) as { workspaces?: WorkspaceChoice[] };
      return { ok: true, value: { choose: payload.workspaces ?? [] } };
    }

    if (!response.ok) return { ok: false, ...(await readError(response, "Sign-in did not work. Try again.")) };
    const payload = (await response.json()) as { accessToken: string; expiresAt: string; user: PlatformUser };
    storePlatformSession({ accessToken: payload.accessToken, expiresAt: payload.expiresAt, user: payload.user });
    return { ok: true, value: { user: payload.user } };
  } catch {
    return { ok: false, error: unreachable };
  }
}

export function storePlatformSession(session: StoredSession) {
  try {
    window.sessionStorage.setItem(SESSION_KEY, JSON.stringify(session));
  } catch {
    // Without storage the person signs in again on the next page load; nothing else breaks.
  }

  setApiAccessToken(session.accessToken);
}

/** The platform's own access token for this tab, when one is held and has not run out. */
export function platformAccessToken(): string | null {
  if (typeof window === "undefined") return null;
  try {
    const raw = window.sessionStorage.getItem(SESSION_KEY);
    if (!raw) return null;
    const session = JSON.parse(raw) as StoredSession;
    if (!session.accessToken || new Date(session.expiresAt).getTime() <= Date.now()) {
      window.sessionStorage.removeItem(SESSION_KEY);
      return null;
    }

    return session.accessToken;
  } catch {
    return null;
  }
}

export function platformUser(): PlatformUser | null {
  try {
    const raw = typeof window === "undefined" ? null : window.sessionStorage.getItem(SESSION_KEY);
    return raw && platformAccessToken() ? (JSON.parse(raw) as StoredSession).user : null;
  } catch {
    return null;
  }
}

export function clearPlatformSession() {
  try {
    window.sessionStorage.removeItem(SESSION_KEY);
  } catch {
    // Nothing to clear.
  }

  setApiAccessToken(null);
}

// ---- invitations (administrator) ----

export type InvitationView = { invitationId: string; email: string; role: string; status: string; createdAt: string; expiresAt: string; invitedBy: string };

export async function listInvitations(role: string): Promise<AuthResult<InvitationView[]>> {
  try {
    const response = await fetch(`${apiBaseUrl}/api/v1/admin/invitations`, { headers: scopedHeaders(role, ""), cache: "no-store" });
    if (!response.ok) return { ok: false, ...(await readError(response, "Invitations could not be loaded.")) };
    return { ok: true, value: (await response.json()) as InvitationView[] };
  } catch {
    return { ok: false, error: unreachable };
  }
}

export async function inviteColleague(role: string, email: string, roleToGive: string): Promise<AuthResult<InvitationView>> {
  try {
    const response = await fetch(`${apiBaseUrl}/api/v1/admin/invitations`, {
      method: "POST",
      headers: { ...scopedHeaders(role, ""), "Content-Type": "application/json" },
      body: JSON.stringify({ email, role: roleToGive }),
    });
    if (!response.ok) return { ok: false, ...(await readError(response, "The invitation could not be sent.")) };
    return { ok: true, value: (await response.json()) as InvitationView };
  } catch {
    return { ok: false, error: unreachable };
  }
}

export async function revokeInvitation(role: string, invitationId: string): Promise<AuthResult<true>> {
  try {
    const response = await fetch(`${apiBaseUrl}/api/v1/admin/invitations/${invitationId}`, { method: "DELETE", headers: scopedHeaders(role, "") });
    if (!response.ok) return { ok: false, ...(await readError(response, "The invitation could not be withdrawn.")) };
    return { ok: true, value: true };
  } catch {
    return { ok: false, error: unreachable };
  }
}
