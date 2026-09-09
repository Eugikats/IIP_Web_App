"use client";

import { apiBaseUrl, scopedHeaders } from "@/lib/config";

export type WhatsAppConfigurationStatus = {
  connected: boolean;
  phoneNumberId: string | null;
  businessDisplayName: string | null;
  configuredByUserId: string | null;
  configuredAt: string | null;
  lastValidatedAt: string | null;
  lastValidationStatus: string | null;
  lastValidationError: string | null;
  channelProviderActive: boolean;
  requiresChannelProviderDeploymentChange: boolean;
};

export type SaveWhatsAppConfigurationInput = {
  phoneNumberId: string;
  accessToken: string;
  appSecret: string;
  verifyToken: string;
  businessDisplayName?: string;
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

export type WhatsAppResult<T> = { ok: true; value: T } | { ok: false; error: string };

export async function getWhatsAppConfigurationStatus(role: string): Promise<WhatsAppResult<WhatsAppConfigurationStatus>> {
  try {
    const response = await fetch(`${apiBaseUrl}/api/v1/integrations/whatsapp/configuration`, {
      headers: scopedHeaders(role, ""),
    });
    if (!response.ok) {
      return { ok: false, error: await safeError(response, `Status could not be loaded.`) };
    }
    return { ok: true, value: (await response.json()) as WhatsAppConfigurationStatus };
  } catch (reason) {
    return { ok: false, error: safeNetworkFailure(reason, "WhatsApp connection status could not be loaded.") };
  }
}

export async function saveWhatsAppConfiguration(
  role: string,
  input: SaveWhatsAppConfigurationInput,
  auditReason: string
): Promise<WhatsAppResult<WhatsAppConfigurationStatus>> {
  try {
    const response = await fetch(`${apiBaseUrl}/api/v1/integrations/whatsapp/configuration`, {
      method: "POST",
      headers: { "Content-Type": "application/json", "X-Audit-Reason": auditReason, ...scopedHeaders(role, "") },
      body: JSON.stringify(input),
    });
    if (!response.ok) {
      return { ok: false, error: await safeError(response, "Meta rejected these credentials. Double-check them and try again.") };
    }
    return { ok: true, value: (await response.json()) as WhatsAppConfigurationStatus };
  } catch (reason) {
    return { ok: false, error: safeNetworkFailure(reason, "The connection could not be saved.") };
  }
}

export async function testWhatsAppConfiguration(role: string): Promise<WhatsAppResult<{ success: boolean; message: string | null; verifiedName: string | null }>> {
  try {
    const response = await fetch(`${apiBaseUrl}/api/v1/integrations/whatsapp/configuration/test`, {
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

export async function deleteWhatsAppConfiguration(role: string, auditReason: string): Promise<WhatsAppResult<null>> {
  try {
    const response = await fetch(`${apiBaseUrl}/api/v1/integrations/whatsapp/configuration`, {
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
