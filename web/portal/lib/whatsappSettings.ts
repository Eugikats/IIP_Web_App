"use client";

import { apiBaseUrl, scopedHeaders } from "@/lib/config";

export type WhatsAppConfigurationStatus = {
  connected: boolean;
  phoneNumberId: string | null;
  wabaId: string | null;
  businessDisplayName: string | null;
  configuredByUserId: string | null;
  configuredAt: string | null;
  lastValidatedAt: string | null;
  lastValidationStatus: string | null;
  lastValidationError: string | null;
  webhookCallbackUrl: string | null;
  webhookRegistrationStatus: string | null;
  webhookRegistrationError: string | null;
  channelProviderActive: boolean;
  requiresChannelProviderDeploymentChange: boolean;
};

export type SaveWhatsAppConfigurationInput = {
  phoneNumberId: string;
  accessToken: string;
  appSecret: string;
  wabaId: string;
  businessDisplayName?: string;
};

export type WhatsAppMessagingMode = {
  mode: "Review" | "Autopilot";
  effectiveMode: "Review" | "Autopilot";
  autopilotAvailable: boolean;
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

export async function getWhatsAppMessagingMode(role: string): Promise<WhatsAppResult<WhatsAppMessagingMode>> {
  try {
    const response = await fetch(`${apiBaseUrl}/api/v1/integrations/whatsapp/messaging-mode`, {
      headers: scopedHeaders(role, ""),
    });
    if (!response.ok) return { ok: false, error: await safeError(response, "Messaging mode could not be loaded.") };
    return { ok: true, value: (await response.json()) as WhatsAppMessagingMode };
  } catch (reason) {
    return { ok: false, error: safeNetworkFailure(reason, "Messaging mode could not be loaded.") };
  }
}

export async function updateWhatsAppMessagingMode(
  role: string,
  mode: WhatsAppMessagingMode["mode"],
  auditReason: string,
): Promise<WhatsAppResult<WhatsAppMessagingMode>> {
  try {
    const response = await fetch(`${apiBaseUrl}/api/v1/integrations/whatsapp/messaging-mode`, {
      method: "PUT",
      headers: { "Content-Type": "application/json", "X-Audit-Reason": auditReason, ...scopedHeaders(role, "") },
      body: JSON.stringify({ mode }),
    });
    if (!response.ok) return { ok: false, error: await safeError(response, "Messaging mode could not be changed.") };
    return { ok: true, value: (await response.json()) as WhatsAppMessagingMode };
  } catch (reason) {
    return { ok: false, error: safeNetworkFailure(reason, "Messaging mode could not be changed.") };
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

export type WhatsAppNumberElsewhere = {
  fromTenantId: string;
  fromWorkspaceId: string;
  organisationName: string;
  phoneNumberId: string;
  businessDisplayName: string | null;
};

/** Numbers held by other workspaces that this person administers and could bring to the workspace being viewed. */
export async function listWhatsAppNumbersElsewhere(role: string): Promise<WhatsAppResult<{ heldHere: boolean; elsewhere: WhatsAppNumberElsewhere[] }>> {
  try {
    const response = await fetch(`${apiBaseUrl}/api/v1/integrations/whatsapp/configuration/elsewhere`, { headers: scopedHeaders(role, ""), cache: "no-store" });
    if (!response.ok) return { ok: false, error: await safeError(response, "Numbers in your other workspaces could not be loaded.") };
    return { ok: true, value: await response.json() };
  } catch (reason) {
    return { ok: false, error: safeNetworkFailure(reason, "Numbers in your other workspaces could not be loaded.") };
  }
}

/** Moves a connected number here from another workspace the person administers. The credentials stay on the server. */
export async function moveWhatsAppNumberHere(
  role: string,
  from: Pick<WhatsAppNumberElsewhere, "fromTenantId" | "fromWorkspaceId">,
  auditReason: string,
): Promise<WhatsAppResult<null>> {
  try {
    const response = await fetch(`${apiBaseUrl}/api/v1/integrations/whatsapp/configuration/move-here`, {
      method: "POST",
      headers: { "Content-Type": "application/json", "X-Audit-Reason": auditReason, ...scopedHeaders(role, "") },
      body: JSON.stringify(from),
    });
    if (!response.ok) return { ok: false, error: await safeError(response, "The number could not be moved.") };
    return { ok: true, value: null };
  } catch (reason) {
    return { ok: false, error: safeNetworkFailure(reason, "The number could not be moved.") };
  }
}

export type WhatsAppReceipt = {
  receivedAt: string;
  phoneNumberId: string;
  outcome: "accepted" | "duplicate_only" | "refused_signature" | "no_messages" | string;
  messagesInCall: number;
  messagesNew: number;
  deliveryUpdates: number;
};

export type WhatsAppReceipts = { lastCallAt: string | null; lastMessageAt: string | null; receipts: WhatsAppReceipt[] };

/** The recent calls Meta made to IIP's webhook: counts and an outcome, never message text. */
export async function getWhatsAppReceipts(role: string): Promise<WhatsAppResult<WhatsAppReceipts>> {
  try {
    const response = await fetch(`${apiBaseUrl}/api/v1/integrations/whatsapp/receipts?limit=20`, { headers: scopedHeaders(role, ""), cache: "no-store" });
    if (!response.ok) return { ok: false, error: await safeError(response, "The recent messages could not be loaded.") };
    return { ok: true, value: (await response.json()) as WhatsAppReceipts };
  } catch (reason) {
    return { ok: false, error: safeNetworkFailure(reason, "The recent messages could not be loaded.") };
  }
}

export type DeliveryFinding = { level: "ok" | "warning" | "problem"; text: string };

/** Asks Meta, read-only, where it sends this number's messages. */
export async function checkWhatsAppWithMeta(role: string): Promise<WhatsAppResult<{ checkedAt: string; findings: DeliveryFinding[] }>> {
  try {
    const response = await fetch(`${apiBaseUrl}/api/v1/integrations/whatsapp/configuration/meta-check`, { method: "POST", headers: scopedHeaders(role, "") });
    if (!response.ok) return { ok: false, error: await safeError(response, "Meta could not be checked.") };
    return { ok: true, value: await response.json() };
  } catch (reason) {
    return { ok: false, error: safeNetworkFailure(reason, "Meta could not be checked.") };
  }
}
