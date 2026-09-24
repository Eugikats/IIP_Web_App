"use client";

import { useCallback, useEffect, useState } from "react";
import { useUI } from "@/context/UIStateContext";
import { apiBaseUrl, scopedHeaders } from "@/lib/config";
import { LOVABLE_VISUAL_PARITY_MODE } from "@/lib/lovable-parity/mode";
import { communications as fixtureComms } from "@/lib/lovable-parity/mock";

// WO-16: real client for the tenant-wide customer-update queue (GET /api/v1/customer-updates) and
// its governed approve/reject/send actions, the drafting -> approve -> reject -> send chain
// already existed server-side (CustomerUpdateEndpoints) and was already wired per-case inside
// ClaimReview.tsx; this screen previously showed an unrelated, always-terminal Communication
// ledger mislabeled with a Draft/Approval status vocabulary that never actually applied to it.
// Only an approved draft is ever sendable, enforced server-side, never bypassed from here (I4).

export type ActionState = { action: string; label: string; requiredPermission: string; reason: string | null };

export type CustomerUpdateQueueItem = {
  updateId: string;
  caseId: string;
  customerName: string;
  customerEmail: string | null;
  customerPhone: string | null;
  policyNumber: string;
  reasonCategory: string;
  channel: string;
  status: string;
  messageBody: string;
  requiresApproval: boolean;
  updatedAt: string;
  allowedActions: ActionState[];
  blockedActions: ActionState[];
};

type CustomerUpdateResponseDto = {
  updateId: string;
  caseId: string;
  reasonCategory: string;
  channel: string;
  status: string;
  messageBody: string;
  requiresApproval: boolean;
  updatedAt: string;
  allowedActions?: ActionState[];
  blockedActions?: ActionState[];
};

type CustomerUpdateWithCaseDto = {
  update: CustomerUpdateResponseDto;
  customerName: string;
  customerEmail: string | null;
  customerPhone: string | null;
  policyNumber: string;
};

async function readSafeApiError(response: Response, fallback: string): Promise<string> {
  try {
    const body = (await response.json()) as { message?: unknown };
    if (typeof body.message === "string" && body.message.trim()) return body.message;
  } catch {
    // The fallback remains safe for non-JSON or empty error responses.
  }
  return fallback;
}

const STATUS_LABELS: Record<string, string> = {
  Draft: "Draft",
  PendingApproval: "Pending Approval",
  Approved: "Approved",
  Rejected: "Rejected",
  Sent: "Sent",
  SimulatedSent: "Sent",
  ConfigurationRequired: "Send Failed",
  Failed: "Send Failed",
};

function mapDto(dto: CustomerUpdateWithCaseDto): CustomerUpdateQueueItem {
  return {
    updateId: dto.update.updateId,
    caseId: dto.update.caseId,
    customerName: dto.customerName,
    customerEmail: dto.customerEmail,
    customerPhone: dto.customerPhone,
    policyNumber: dto.policyNumber,
    reasonCategory: dto.update.reasonCategory,
    channel: dto.update.channel,
    status: STATUS_LABELS[dto.update.status] ?? dto.update.status,
    messageBody: dto.update.messageBody,
    requiresApproval: dto.update.requiresApproval,
    updatedAt: dto.update.updatedAt,
    allowedActions: dto.update.allowedActions ?? [],
    blockedActions: dto.update.blockedActions ?? [],
  };
}

const PARITY: CustomerUpdateQueueItem[] = fixtureComms.map((c, index) => ({
  updateId: c.id,
  caseId: c.claim ?? `case-${index}`,
  customerName: c.customer,
  customerEmail: null,
  customerPhone: null,
  policyNumber: "-",
  reasonCategory: "StatusUpdate",
  channel: String(c.channel),
  status: c.status,
  messageBody: `${c.subject}, ${c.preview}`,
  requiresApproval: c.status !== "Sent",
  updatedAt: c.drafted,
  allowedActions: [],
  blockedActions: [],
}));

export function useCustomerUpdateQueue() {
  const { roleDef } = useUI();
  const role = roleDef.key;
  const [items, setItems] = useState<CustomerUpdateQueueItem[]>(LOVABLE_VISUAL_PARITY_MODE ? PARITY : []);
  const [loading, setLoading] = useState(!LOVABLE_VISUAL_PARITY_MODE);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    if (LOVABLE_VISUAL_PARITY_MODE) return;
    setLoading(true);
    setError(null);
    try {
      const response = await fetch(`${apiBaseUrl}/api/v1/customer-updates`, { headers: scopedHeaders(role, "") });
      if (!response.ok) throw new Error(await readSafeApiError(response, `Customer updates failed.`));
      const rows = (await response.json()) as CustomerUpdateWithCaseDto[];
      setItems(rows.map(mapDto));
    } catch (err) {
      setError(err instanceof Error ? err.message : "Customer updates failed to load.");
      setItems([]);
    } finally {
      setLoading(false);
    }
  }, [role]);

  useEffect(() => {
    void load();
  }, [load]);

  const approve = useCallback(async (updateId: string, auditReason: string): Promise<{ ok: boolean; message?: string }> => {
    const response = await fetch(`${apiBaseUrl}/api/v1/customer-updates/${encodeURIComponent(updateId)}/approve`, {
      method: "POST",
      headers: { ...scopedHeaders(role, ""), "Content-Type": "application/json", "X-Audit-Reason": auditReason },
      body: JSON.stringify({}),
    });
    if (!response.ok) return { ok: false, message: await readSafeApiError(response, `Approve failed.`) };
    await load();
    return { ok: true };
  }, [role, load]);

  const reject = useCallback(async (updateId: string, auditReason: string): Promise<{ ok: boolean; message?: string }> => {
    const response = await fetch(`${apiBaseUrl}/api/v1/customer-updates/${encodeURIComponent(updateId)}/reject`, {
      method: "POST",
      headers: { ...scopedHeaders(role, ""), "Content-Type": "application/json", "X-Audit-Reason": auditReason },
      body: JSON.stringify({}),
    });
    if (!response.ok) return { ok: false, message: await readSafeApiError(response, `Reject failed.`) };
    await load();
    return { ok: true };
  }, [role, load]);

  const send = useCallback(async (item: CustomerUpdateQueueItem, auditReason: string): Promise<{ ok: boolean; message?: string }> => {
    const channel = item.channel.toLowerCase();
    const recipient = channel === "email"
      ? item.customerEmail
      : channel === "sms" || channel === "whatsapp"
        ? item.customerPhone
        : null;
    if (!recipient) {
      return { ok: false, message: `No ${channel} contact is on file for this customer.` };
    }

    const response = await fetch(`${apiBaseUrl}/api/v1/customer-updates/${encodeURIComponent(item.updateId)}/send`, {
      method: "POST",
      headers: { ...scopedHeaders(role, ""), "Content-Type": "application/json", "X-Audit-Reason": auditReason },
      body: JSON.stringify({ recipient }),
    });
    if (!response.ok) return { ok: false, message: await readSafeApiError(response, `Send failed.`) };
    await load();
    return { ok: true };
  }, [role, load]);

  return { items, loading, error, parityMode: LOVABLE_VISUAL_PARITY_MODE, approve, reject, send, reload: load };
}
