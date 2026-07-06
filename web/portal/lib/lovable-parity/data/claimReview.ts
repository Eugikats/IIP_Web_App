"use client";

import { useCallback, useEffect, useState } from "react";
import { useUI } from "@/context/UIStateContext";
import { apiBaseUrl, scopedHeaders } from "@/lib/config";
import { LOVABLE_VISUAL_PARITY_MODE } from "@/lib/lovable-parity/mode";

export type ClaimDocumentView = { documentId: string; documentType: string; processingStatus: string; classificationConfidence: number };
export type ClaimDetailView = {
  claimId: string;
  customerName: string;
  customerEmail: string | null;
  customerPhone: string | null;
  policyNumber: string;
  productLine: string;
  claimType: string;
  channel: string;
  incidentDate: string;
  incidentDescription: string;
  status: string;
  completenessScore: number;
  riskLevel: string;
  priority: string;
  slaDueAt: string | null;
  assignedUserId: string | null;
  documents: ClaimDocumentView[];
};
export type TimelineEventView = { eventId: string; eventType: string; actor: string; timestamp: string; summary: string };

export function useClaimReview(claimId: string) {
  const { roleDef } = useUI();
  const role = roleDef.key;
  const [detail, setDetail] = useState<ClaimDetailView | null>(null);
  const [timeline, setTimeline] = useState<TimelineEventView[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [drafting, setDrafting] = useState(false);

  const load = useCallback(async () => {
    if (LOVABLE_VISUAL_PARITY_MODE || !claimId) { setLoading(false); return; }
    setLoading(true);
    setError(null);
    try {
      const headers = scopedHeaders(role, "");
      const detailRes = await fetch(`${apiBaseUrl}/api/v1/claims/${claimId}`, { headers });
      if (detailRes.status === 404) { setError("This claim was not found in the current workspace."); setDetail(null); return; }
      if (!detailRes.ok) throw new Error(`Claim failed with HTTP ${detailRes.status}.`);
      const d = await detailRes.json();
      setDetail({
        claimId: d.claimId, customerName: d.customerName, customerEmail: d.customerEmail, customerPhone: d.customerPhone,
        policyNumber: d.policyNumber, productLine: d.productLine, claimType: d.claimType, channel: d.channel,
        incidentDate: d.incidentDate, incidentDescription: d.incidentDescription, status: d.status,
        completenessScore: d.completenessScore, riskLevel: d.riskLevel, priority: d.priority, slaDueAt: d.slaDueAt,
        assignedUserId: d.assignedUserId,
        documents: (d.documents ?? []).map((x: ClaimDocumentView) => ({ ...x })),
      });

      // Timeline is best-effort — a claim can be valid without timeline events.
      try {
        const tlRes = await fetch(`${apiBaseUrl}/api/v1/cases/${claimId}/timeline`, { headers });
        if (tlRes.ok) {
          const rows = await tlRes.json();
          setTimeline((rows ?? []).map((e: TimelineEventView) => ({ eventId: e.eventId, eventType: e.eventType, actor: e.actor, timestamp: e.timestamp, summary: e.summary })));
        }
      } catch { /* timeline optional */ }
    } catch (err) {
      setError(err instanceof Error ? err.message : "Claim failed to load.");
      setDetail(null);
    } finally {
      setLoading(false);
    }
  }, [claimId, role]);

  useEffect(() => { void load(); }, [load]);

  const draftCustomerUpdate = useCallback(async (): Promise<{ ok: boolean; message?: string }> => {
    if (LOVABLE_VISUAL_PARITY_MODE) return { ok: true };
    setDrafting(true);
    try {
      const res = await fetch(`${apiBaseUrl}/api/v1/cases/${claimId}/customer-updates/draft`, {
        method: "POST",
        headers: { ...scopedHeaders(role, ""), "Content-Type": "application/json" },
        body: JSON.stringify({ reasonCategory: "StatusUpdate", channel: "Email" }),
      });
      if (res.status === 403) return { ok: false, message: "You do not have permission to draft customer updates." };
      if (!res.ok) return { ok: false, message: `Draft failed with HTTP ${res.status}.` };
      return { ok: true };
    } catch (err) {
      return { ok: false, message: err instanceof Error ? err.message : "Draft failed." };
    } finally {
      setDrafting(false);
    }
  }, [claimId, role]);

  return { detail, timeline, loading, error, drafting, draftCustomerUpdate, parityMode: LOVABLE_VISUAL_PARITY_MODE };
}
