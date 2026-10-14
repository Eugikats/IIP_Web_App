// Passkeys and security keys in the browser. The API makes the challenge; the person's device makes or uses the key
// pair; the private key never leaves the device. Nothing here handles a secret.

import { apiBaseUrl, scopedHeaders } from "@/lib/config";
import { getEntraApiAccessToken } from "@/lib/entraAuth";
import { MfaError } from "@/lib/security/mfa";

function toBase64Url(buffer: ArrayBuffer): string {
  let binary = "";
  for (const byte of new Uint8Array(buffer)) binary += String.fromCharCode(byte);
  return btoa(binary).replace(/\+/g, "-").replace(/\//g, "_").replace(/=+$/, "");
}

function fromBase64Url(value: string): ArrayBuffer {
  const padded = value.replace(/-/g, "+").replace(/_/g, "/");
  const binary = atob(padded + "=".repeat((4 - (padded.length % 4)) % 4));
  const bytes = new Uint8Array(binary.length);
  for (let i = 0; i < binary.length; i++) bytes[i] = binary.charCodeAt(i);
  return bytes.buffer;
}

async function post(path: string, body?: unknown, role = "ClaimsOfficer"): Promise<unknown> {
  const token = await getEntraApiAccessToken();
  const response = await fetch(`${apiBaseUrl}/api/v1/security/mfa/passkey${path}`, {
    method: "POST",
    cache: "no-store",
    headers: {
      ...scopedHeaders(role, ""),
      ...(body === undefined ? {} : { "Content-Type": "application/json" }),
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
    },
    body: body === undefined ? undefined : JSON.stringify(body),
  });
  if (response.ok) return response.json();
  const failure = (await response.json().catch(() => ({}))) as { code?: string; message?: string };
  throw new MfaError(failure.code ?? "error", failure.message ?? "The request could not be completed.", response.status);
}

export function passkeysSupported(): boolean {
  return typeof window !== "undefined" && typeof window.PublicKeyCredential !== "undefined" && Boolean(navigator.credentials);
}

/** A cancelled or timed-out prompt is not an error worth showing in red. */
export function passkeyCancelled(failure: unknown): boolean {
  return failure instanceof DOMException && (failure.name === "NotAllowedError" || failure.name === "AbortError");
}

export async function registerPasskey(nickname: string, role?: string): Promise<void> {
  const raw = (await post("/register/options", undefined, role)) as Record<string, unknown>;
  const user = raw.user as { id: string; name: string; displayName: string };
  const exclude = (raw.excludeCredentials as { id: string; type?: string; transports?: string[] }[] | undefined) ?? [];
  const options = {
    ...raw,
    challenge: fromBase64Url(raw.challenge as string),
    user: { ...user, id: fromBase64Url(user.id) },
    excludeCredentials: exclude.map((credential) => ({
      type: (credential.type ?? "public-key") as PublicKeyCredentialType,
      id: fromBase64Url(credential.id),
      transports: credential.transports as AuthenticatorTransport[] | undefined,
    })),
  } as unknown as PublicKeyCredentialCreationOptions;

  const credential = (await navigator.credentials.create({ publicKey: options })) as PublicKeyCredential | null;
  if (!credential) throw new DOMException("No passkey was made.", "NotAllowedError");
  const attestation = credential.response as AuthenticatorAttestationResponse;
  await post(
    "/register/result",
    {
      nickname,
      response: {
        id: toBase64Url(credential.rawId),
        rawId: toBase64Url(credential.rawId),
        type: credential.type,
        response: { clientDataJSON: toBase64Url(attestation.clientDataJSON), attestationObject: toBase64Url(attestation.attestationObject) },
      },
    },
    role,
  );
}

export async function verifyWithPasskey(role?: string): Promise<void> {
  const raw = (await post("/assert/options", undefined, role)) as Record<string, unknown>;
  const allow = (raw.allowCredentials as { id: string; type?: string; transports?: string[] }[] | undefined) ?? [];
  const options = {
    ...raw,
    challenge: fromBase64Url(raw.challenge as string),
    allowCredentials: allow.map((credential) => ({
      type: (credential.type ?? "public-key") as PublicKeyCredentialType,
      id: fromBase64Url(credential.id),
      transports: credential.transports as AuthenticatorTransport[] | undefined,
    })),
  } as unknown as PublicKeyCredentialRequestOptions;

  const credential = (await navigator.credentials.get({ publicKey: options })) as PublicKeyCredential | null;
  if (!credential) throw new DOMException("No passkey was used.", "NotAllowedError");
  const assertion = credential.response as AuthenticatorAssertionResponse;
  await post(
    "/assert/result",
    {
      response: {
        id: toBase64Url(credential.rawId),
        rawId: toBase64Url(credential.rawId),
        type: credential.type,
        response: {
          authenticatorData: toBase64Url(assertion.authenticatorData),
          signature: toBase64Url(assertion.signature),
          clientDataJSON: toBase64Url(assertion.clientDataJSON),
          userHandle: assertion.userHandle ? toBase64Url(assertion.userHandle) : null,
        },
      },
    },
    role,
  );
}
