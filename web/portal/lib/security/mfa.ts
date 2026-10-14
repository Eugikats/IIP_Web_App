// The second step after sign-in. Every call goes to the API as the signed-in person. The authenticator secret is
// shown once, when it is created; nothing here can read a secret back.

import { apiBaseUrl, scopedHeaders } from "@/lib/config";
import { getEntraApiAccessToken } from "@/lib/entraAuth";

export type MfaMethod = "totp" | "passkey" | "email_otp" | "sms_otp" | "backup_codes";

export type MfaCredential = { method: MfaMethod; id: string; label: string; createdAt: string; lastUsedAt: string | null };

export type MfaStatus = {
  policyRequired: boolean;
  allowSnooze: boolean;
  minMethods: number;
  offeredMethods: MfaMethod[];
  enrolledMethods: MfaMethod[];
  unusedBackupCodes: number;
  satisfied: boolean;
  verified: boolean;
  verifiedUntil: string | null;
  snoozed: boolean;
  snoozeUntil: string | null;
  canSnooze: boolean;
  shouldPrompt: boolean;
  mustVerify: boolean;
  lockedOut: boolean;
  lockedUntil: string | null;
  smsNumberMasked: string | null;
  credentials: MfaCredential[];
};

export type MfaPolicy = { required: boolean; allowSnooze: boolean; minMethods: number; enabledMethods: MfaMethod[]; updatedAt: string | null };

export type MfaMetrics = { peopleWithAMethod: number; totpPeople: number; passkeyPeople: number; emailPeople: number; smsPeople: number };

export class MfaError extends Error {
  constructor(public code: string, message: string, public status: number) {
    super(message);
  }
}

async function call(path: string, init: RequestInit = {}, role = "ClaimsOfficer"): Promise<Response> {
  const token = await getEntraApiAccessToken();
  return fetch(`${apiBaseUrl}/api/v1/security/mfa${path}`, {
    ...init,
    cache: "no-store",
    headers: {
      ...scopedHeaders(role, ""),
      ...(init.body ? { "Content-Type": "application/json" } : {}),
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
    },
  });
}

async function json<T>(response: Response): Promise<T> {
  if (response.ok) return (await response.json()) as T;
  const body = (await response.json().catch(() => ({}))) as { code?: string; message?: string };
  throw new MfaError(body.code ?? "error", body.message ?? "The request could not be completed.", response.status);
}

const post = (path: string, body?: unknown, role?: string) =>
  call(path, { method: "POST", body: body === undefined ? undefined : JSON.stringify(body) }, role);

export const getMfaStatus = (role?: string) => call("/status", {}, role).then((r) => json<MfaStatus>(r));

export const beginTotp = (label: string, role?: string) =>
  post("/totp/begin", { label }, role).then((r) => json<{ id: string; secret: string; provisioningUri: string }>(r));

export const confirmTotp = (id: string, code: string, role?: string) => post("/totp/confirm", { id, code }, role).then((r) => json<{ ok: true }>(r));

export const removeCredential = (method: MfaMethod, id: string, role?: string) =>
  call(`/credentials/${method}/${id}`, { method: "DELETE" }, role).then((r) => json<{ ok: true }>(r));

export const rotateBackupCodes = (role?: string) => post("/backup-codes/rotate", undefined, role).then((r) => json<{ codes: string[] }>(r));

export const sendMfaCode = (channel: "email" | "sms", purpose: "enroll" | "login", phone?: string, role?: string) =>
  post("/codes/send", { channel, purpose, phone }, role).then((r) => json<{ channel: string; destination: string; expiresAt: string }>(r));

export const confirmMfaCode = (channel: "email" | "sms", code: string, role?: string) =>
  post("/codes/confirm", { channel, code }, role).then((r) => json<{ ok: true }>(r));

export const disableMfaChannel = (channel: "email" | "sms", role?: string) =>
  call(`/channels/${channel}`, { method: "DELETE" }, role).then((r) => json<{ ok: true }>(r));

export const verifyMfa = (method: MfaMethod, code: string, role?: string) => post("/verify", { method, code }, role).then((r) => json<{ ok: true }>(r));

export const snoozeMfa = (role?: string) => post("/snooze", undefined, role).then((r) => json<{ ok: true }>(r));

export const signOutMfa = (role?: string) => post("/sign-out", undefined, role).then((r) => json<{ ok: true }>(r));

export const getMfaPolicy = (role?: string) => call("/policy", {}, role).then((r) => json<MfaPolicy>(r));

export const setMfaPolicy = (policy: Omit<MfaPolicy, "updatedAt">, role?: string) =>
  call("/policy", { method: "PUT", body: JSON.stringify(policy) }, role).then((r) => json<MfaPolicy>(r));

export const resetMfaUser = (userId: string, role?: string) => post(`/users/${encodeURIComponent(userId)}/reset`, undefined, role).then((r) => json<{ ok: true }>(r));

export const getMfaMetrics = (role?: string) => call("/metrics", {}, role).then((r) => json<MfaMetrics>(r));

export const METHOD_LABELS: Record<MfaMethod, string> = {
  totp: "Authenticator app",
  passkey: "Passkey or security key",
  email_otp: "Email codes",
  sms_otp: "SMS codes",
  backup_codes: "Backup codes",
};
