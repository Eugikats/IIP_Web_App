"use client";

import { apiBaseUrl, scopedHeaders } from "@/lib/config";

export type ProviderHealthItem = { name: string; mode: string; status: string; detail: string };
export type ActionState = { action: string; label: string; requiredPermission: string; reason: string | null };

export type EmailIntegrationHealth = {
  integrationId: string;
  displayName: string;
  overallStatus: string;
  providers: ProviderHealthItem[];
  allowedActions: ActionState[];
  blockedActions: ActionState[];
};

export type MailboxConnection = {
  mailboxConnectionId: string;
  displayName: string;
  mailboxAddress: string;
  ingestionEnabled: boolean;
  defaultChannel: string;
  productLine: string | null;
  processedFolderName: string | null;
  createdAt: string;
};

export type CreateMailboxInput = {
  displayName: string;
  mailboxAddress: string;
  ingestionEnabled: boolean;
  defaultChannel: string;
  productLine?: string;
};

export type InboundEmailMessage = {
  messageId: string;
  scope: unknown;
  mailboxConnectionId: string;
  externalMessageId: string | null;
  internetMessageId: string | null;
  sender: string;
  subject: string;
  bodyPreview: string;
  receivedAt: string;
  // Serialized as the raw EmailProcessingStatus enum ordinal (no string-enum converter on this
  // type): 0 Received, 1 LinkedToCase, 2 CaseCreated, 3 Failed.
  processingStatus: 0 | 1 | 2 | 3;
  linkedCaseId: string | null;
  attachments: { attachmentId: string; fileName: string; contentType: string; sizeBytes: number }[];
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

export type EmailResult<T> = { ok: true; value: T } | { ok: false; error: string };

export async function getEmailHealth(role: string): Promise<EmailResult<EmailIntegrationHealth>> {
  try {
    const response = await fetch(`${apiBaseUrl}/api/v1/integrations/email/health`, { headers: scopedHeaders(role, "") });
    if (!response.ok) return { ok: false, error: await safeError(response, `Status could not be loaded.`) };
    return { ok: true, value: (await response.json()) as EmailIntegrationHealth };
  } catch (reason) {
    return { ok: false, error: safeNetworkFailure(reason, "Email integration status could not be loaded.") };
  }
}

export async function listMailboxes(role: string): Promise<EmailResult<MailboxConnection[]>> {
  try {
    const response = await fetch(`${apiBaseUrl}/api/v1/integrations/email/mailboxes`, { headers: scopedHeaders(role, "") });
    if (!response.ok) return { ok: false, error: await safeError(response, `Mailboxes could not be loaded.`) };
    return { ok: true, value: (await response.json()) as MailboxConnection[] };
  } catch (reason) {
    return { ok: false, error: safeNetworkFailure(reason, "Mailboxes could not be loaded.") };
  }
}

export async function createMailbox(role: string, input: CreateMailboxInput, auditReason: string): Promise<EmailResult<MailboxConnection>> {
  try {
    const response = await fetch(`${apiBaseUrl}/api/v1/integrations/email/mailboxes`, {
      method: "POST",
      headers: { "Content-Type": "application/json", "X-Audit-Reason": auditReason, ...scopedHeaders(role, "") },
      body: JSON.stringify(input),
    });
    if (!response.ok) return { ok: false, error: await safeError(response, "This mailbox could not be added.") };
    return { ok: true, value: (await response.json()) as MailboxConnection };
  } catch (reason) {
    return { ok: false, error: safeNetworkFailure(reason, "This mailbox could not be added.") };
  }
}

export async function runTestIngestion(role: string, auditReason: string): Promise<EmailResult<{ messageId?: string; ingested?: number }>> {
  try {
    const response = await fetch(`${apiBaseUrl}/api/v1/integrations/email/test-ingestion`, {
      method: "POST",
      headers: { "X-Audit-Reason": auditReason, ...scopedHeaders(role, "") },
    });
    if (!response.ok) return { ok: false, error: await safeError(response, "Test ingestion could not run.") };
    const body = await response.json();
    return { ok: true, value: { messageId: body.messageId, ingested: body.ingested } };
  } catch (reason) {
    return { ok: false, error: safeNetworkFailure(reason, "Test ingestion could not run.") };
  }
}

export async function listMessages(role: string): Promise<EmailResult<InboundEmailMessage[]>> {
  try {
    const response = await fetch(`${apiBaseUrl}/api/v1/integrations/email/messages`, { headers: scopedHeaders(role, "") });
    if (!response.ok) return { ok: false, error: await safeError(response, `Messages could not be loaded.`) };
    return { ok: true, value: (await response.json()) as InboundEmailMessage[] };
  } catch (reason) {
    return { ok: false, error: safeNetworkFailure(reason, "Inbound messages could not be loaded.") };
  }
}

export async function processMessage(
  role: string,
  messageId: string,
  auditReason: string
): Promise<EmailResult<{ caseId: string; matchReason: string }>> {
  try {
    const response = await fetch(`${apiBaseUrl}/api/v1/integrations/email/process-message`, {
      method: "POST",
      headers: { "Content-Type": "application/json", "X-Audit-Reason": auditReason, ...scopedHeaders(role, "") },
      body: JSON.stringify({ messageId }),
    });
    if (!response.ok) return { ok: false, error: await safeError(response, "This message could not be processed.") };
    const body = await response.json();
    return { ok: true, value: { caseId: body.caseId, matchReason: body.matchReason } };
  } catch (reason) {
    return { ok: false, error: safeNetworkFailure(reason, "This message could not be processed.") };
  }
}
