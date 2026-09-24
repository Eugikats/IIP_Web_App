"use client";

import { useCallback, useEffect, useState } from "react";
import { useUI } from "@/context/UIStateContext";
import { apiBaseUrl, scopedHeaders } from "@/lib/config";
import { LOVABLE_VISUAL_PARITY_MODE } from "@/lib/lovable-parity/mode";

export type ConsentRecordView = {
  recordId: string;
  basis: string;
  purpose: string;
  channel: string;
  recordedAt: string;
  recordedByUserId: string;
  withdrawnAt: string | null;
  interactionReference: string | null;
  isCurrentlyPermitted: boolean;
};

export const CONSENT_PURPOSES = [
  { purpose: "whatsapp_messaging", label: "WhatsApp" },
  { purpose: "sms_messaging", label: "SMS" },
  { purpose: "email_messaging", label: "Email" },
] as const;

const PARITY: ConsentRecordView[] = [
  {
    recordId: "parity-consent-1",
    basis: "CustomerInitiatedConversation",
    purpose: "whatsapp_messaging",
    channel: "WhatsApp",
    recordedAt: new Date(Date.now() - 3 * 24 * 3600_000).toISOString(),
    recordedByUserId: "system:customer-assistant",
    withdrawnAt: null,
    interactionReference: "whatsapp:phone-1:256772118001",
    isCurrentlyPermitted: true,
  },
];

export function useCustomerConsent(customerId: string) {
  const { roleDef } = useUI();
  const role = roleDef.key;
  const [records, setRecords] = useState<ConsentRecordView[]>(LOVABLE_VISUAL_PARITY_MODE ? PARITY : []);
  const [loading, setLoading] = useState(!LOVABLE_VISUAL_PARITY_MODE);
  const [error, setError] = useState<string | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);
  const [busyPurpose, setBusyPurpose] = useState<string | null>(null);

  const load = useCallback(async () => {
    if (LOVABLE_VISUAL_PARITY_MODE || !customerId) return;
    setLoading(true);
    setError(null);
    try {
      const response = await fetch(`${apiBaseUrl}/api/v1/customers/${customerId}/consent`, { headers: scopedHeaders(role, "") });
      if (!response.ok) throw new Error("Consent history failed to load.");
      setRecords((await response.json()) as ConsentRecordView[]);
    } catch {
      setError("Consent history failed to load.");
      setRecords([]);
    } finally {
      setLoading(false);
    }
  }, [customerId, role]);

  useEffect(() => {
    void load();
  }, [load]);

  const recordOptIn = useCallback(async (purpose: string) => {
    setBusyPurpose(purpose);
    setActionError(null);
    try {
      const response = await fetch(`${apiBaseUrl}/api/v1/customers/${customerId}/consent`, {
        method: "POST",
        headers: { ...scopedHeaders(role, ""), "Content-Type": "application/json" },
        body: JSON.stringify({ purpose }),
      });
      if (!response.ok) {
        const payload = await response.json().catch(() => null) as { errors?: Record<string, string[]> } | null;
        throw new Error(payload?.errors ? Object.values(payload.errors).flat()[0] : "Could not record consent.");
      }
      await load();
      return { ok: true as const };
    } catch (reason) {
      const message = reason instanceof Error ? reason.message : "Could not record consent.";
      setActionError(message);
      return { ok: false as const, message };
    } finally {
      setBusyPurpose(null);
    }
  }, [customerId, load, role]);

  const withdraw = useCallback(async (purpose: string) => {
    setBusyPurpose(purpose);
    setActionError(null);
    try {
      const response = await fetch(`${apiBaseUrl}/api/v1/customers/${customerId}/consent/withdraw`, {
        method: "POST",
        headers: { ...scopedHeaders(role, ""), "Content-Type": "application/json" },
        body: JSON.stringify({ purpose }),
      });
      if (!response.ok) {
        const payload = await response.json().catch(() => null) as { errors?: Record<string, string[]> } | null;
        throw new Error(payload?.errors ? Object.values(payload.errors).flat()[0] : "Could not withdraw consent.");
      }
      await load();
      return { ok: true as const };
    } catch (reason) {
      const message = reason instanceof Error ? reason.message : "Could not withdraw consent.";
      setActionError(message);
      return { ok: false as const, message };
    } finally {
      setBusyPurpose(null);
    }
  }, [customerId, load, role]);

  return { records, loading, error, actionError, busyPurpose, recordOptIn, withdraw, reload: load };
}
