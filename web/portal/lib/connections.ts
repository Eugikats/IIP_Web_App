"use client";

import { apiBaseUrl, scopedHeaders } from "@/lib/config";

export type ConnectionType = "sms" | "email";

export type ConnectionStatus = {
  connectionType: string;
  connected: boolean;
  providerKey: string | null;
  nonSecretSettings: Record<string, string> | null;
  configuredByUserId: string | null;
  configuredAt: string | null;
  lastValidatedAt: string | null;
  lastValidationStatus: string | null;
  lastValidationError: string | null;
  lastValidationDiscovered: Record<string, string> | null;
};

export type SaveConnectionInput = {
  providerKey: string;
  secretFields: Record<string, string>;
  nonSecretFields: Record<string, string>;
};

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
    return "The integrations service could not be reached. Check your connection and try again.";
  }
  return fallback;
}

export type ConnectionResult<T> = { ok: true; value: T } | { ok: false; error: string };

export async function getConnectionProviders(role: string, connectionType: ConnectionType): Promise<ConnectionResult<string[]>> {
  try {
    const response = await fetch(`${apiBaseUrl}/api/v1/integrations/connections/${connectionType}/providers`, {
      headers: scopedHeaders(role, ""),
    });
    if (!response.ok) {
      return { ok: false, error: await safeError(response, "Available providers could not be loaded.") };
    }
    const payload = (await response.json()) as { availableProviders: string[] };
    return { ok: true, value: payload.availableProviders };
  } catch (reason) {
    return { ok: false, error: safeNetworkFailure(reason, "Available providers could not be loaded.") };
  }
}

export async function getConnectionStatus(role: string, connectionType: ConnectionType): Promise<ConnectionResult<ConnectionStatus>> {
  try {
    const response = await fetch(`${apiBaseUrl}/api/v1/integrations/connections/${connectionType}`, {
      headers: scopedHeaders(role, ""),
    });
    if (!response.ok) {
      return { ok: false, error: await safeError(response, "Connection status could not be loaded.") };
    }
    return { ok: true, value: (await response.json()) as ConnectionStatus };
  } catch (reason) {
    return { ok: false, error: safeNetworkFailure(reason, "Connection status could not be loaded.") };
  }
}

export async function saveConnection(
  role: string,
  connectionType: ConnectionType,
  input: SaveConnectionInput,
  auditReason: string
): Promise<ConnectionResult<ConnectionStatus>> {
  try {
    const response = await fetch(`${apiBaseUrl}/api/v1/integrations/connections/${connectionType}`, {
      method: "POST",
      headers: { "Content-Type": "application/json", "X-Audit-Reason": auditReason, ...scopedHeaders(role, "") },
      body: JSON.stringify(input),
    });
    if (!response.ok) {
      return { ok: false, error: await safeError(response, "The provider rejected these credentials. Double-check them and try again.") };
    }
    return { ok: true, value: (await response.json()) as ConnectionStatus };
  } catch (reason) {
    return { ok: false, error: safeNetworkFailure(reason, "The connection could not be saved.") };
  }
}

export async function testConnection(
  role: string,
  connectionType: ConnectionType
): Promise<ConnectionResult<{ success: boolean; message: string | null; discovered: Record<string, string> | null }>> {
  try {
    const response = await fetch(`${apiBaseUrl}/api/v1/integrations/connections/${connectionType}/test`, {
      method: "POST",
      headers: scopedHeaders(role, ""),
    });
    if (!response.ok) {
      return { ok: false, error: await safeError(response, "The connection test could not be run.") };
    }
    return { ok: true, value: await response.json() };
  } catch (reason) {
    return { ok: false, error: safeNetworkFailure(reason, "The connection test could not be run.") };
  }
}

export async function deleteConnection(role: string, connectionType: ConnectionType, auditReason: string): Promise<ConnectionResult<null>> {
  try {
    const response = await fetch(`${apiBaseUrl}/api/v1/integrations/connections/${connectionType}`, {
      method: "DELETE",
      headers: { "X-Audit-Reason": auditReason, ...scopedHeaders(role, "") },
    });
    if (!response.ok) {
      return { ok: false, error: await safeError(response, "The connection could not be removed.") };
    }
    return { ok: true, value: null };
  } catch (reason) {
    return { ok: false, error: safeNetworkFailure(reason, "The connection could not be removed.") };
  }
}

// Field guide per provider — what to show, what's secret. Kept here (not fetched from the
// backend) since it's presentation metadata, the same way WhatsAppSettings.tsx's FIELD_GUIDE is
// local — the backend only needs to know field names, not labels/help text/placeholders.
export type ConnectionFieldSpec = {
  key: string;
  label: string;
  placeholder: string;
  help: string;
  secret: boolean;
};

export const SMS_PROVIDER_FIELDS: Record<string, ConnectionFieldSpec[]> = {
  EgoSms: [
    { key: "username", label: "API username", placeholder: "your_username", help: "Comms Platform — Settings → API Settings.", secret: false },
    { key: "apiKey", label: "API key", placeholder: "your_api_key", help: "Comms Platform — Settings → API Settings.", secret: true },
    { key: "senderId", label: "Sender ID (optional, max 11 characters)", placeholder: "IIP", help: "As configured in your Comms account.", secret: false },
    { key: "sandbox", label: "Use sandbox", placeholder: "true or false", help: "Sandbox needs a balance top-up from customer care — use it for testing.", secret: false },
  ],
  AfricasTalking: [
    { key: "username", label: "Username", placeholder: "sandbox", help: "Africa's Talking dashboard — Settings → API Key.", secret: false },
    { key: "apiKey", label: "API key", placeholder: "your_api_key", help: "Africa's Talking dashboard — Settings → API Key.", secret: true },
    { key: "senderId", label: "Sender ID (optional)", placeholder: "IIP", help: "As registered with Africa's Talking.", secret: false },
  ],
};
