"use client";

import { useCallback, useEffect, useState } from "react";
import { useUI } from "@/context/UIStateContext";
import { apiBaseUrl, scopedHeaders } from "@/lib/config";

async function readSafeApiError(response: Response, fallback: string): Promise<string> {
  try {
    const body = await response.json() as { message?: unknown };
    if (typeof body.message === "string" && body.message.trim()) return body.message;
  } catch {
    // The fallback remains safe for non-JSON or empty error responses.
  }
  return fallback;
}
export type ClaimDocumentView = { documentId: string; documentType: string; processingStatus: string; classificationConfidence: number };
export type MotorFnolDetailsView = {
  vehicleRegistration: string | null;
  accidentTime: string | null;
  accidentLocation: string | null;
  driverName: string | null;
  drivingPermitNumber: string | null;
  thirdPartyInvolved: string | null;
  injuryIndicator: string | null;
  policeReportStatus: string | null;
  preferredCommunicationChannel: string | null;
  uploadedDocuments: string | null;
};
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
  motorFnolDetails: MotorFnolDetailsView | null;
  status: string;
  completenessScore: number;
  riskLevel: string;
  priority: string;
  slaDueAt: string | null;
  assignedUserId: string | null;
  documents: ClaimDocumentView[];
};
export type TimelineEventView = { eventId: string; eventType: string; actor: string; timestamp: string; summary: string };
export type ActionStateView = { action: string; label: string; requiredPermission: string; reason: string | null };
export type CustomerUpdateView = {
  updateId: string;
  reasonCategory: string;
  channel: string;
  status: string;
  messageBody: string;
  requiresApproval: boolean;
  deliveryStatus: string | null;
  updatedAt: string;
  allowedActions: ActionStateView[];
  blockedActions: ActionStateView[];
};

export function useClaimReview(claimId: string) {
  const { roleDef } = useUI();
  const role = roleDef.key;
  const [detail, setDetail] = useState<ClaimDetailView | null>(null);
  const [timeline, setTimeline] = useState<TimelineEventView[]>([]);
  const [customerUpdates, setCustomerUpdates] = useState<CustomerUpdateView[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [drafting, setDrafting] = useState(false);
  const [requestingApproval, setRequestingApproval] = useState(false);
  const [exporting, setExporting] = useState(false);

  const loadCustomerUpdates = useCallback(async () => {
    if (!claimId) { setCustomerUpdates([]); return; }
    const res = await fetch(`${apiBaseUrl}/api/v1/cases/${claimId}/customer-updates`, {
      headers: scopedHeaders(role, ""),
    });
    if (!res.ok) {
      setCustomerUpdates([]);
      return;
    }

    const rows = await res.json();
    setCustomerUpdates((rows ?? []).map((update: CustomerUpdateView) => ({
      updateId: update.updateId,
      reasonCategory: update.reasonCategory,
      channel: update.channel,
      status: update.status,
      messageBody: update.messageBody,
      requiresApproval: update.requiresApproval,
      deliveryStatus: update.deliveryStatus,
      updatedAt: update.updatedAt,
      allowedActions: update.allowedActions ?? [],
      blockedActions: update.blockedActions ?? [],
    })));
  }, [claimId, role]);

  const load = useCallback(async () => {
    if (!claimId) { setLoading(false); return; }
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
        motorFnolDetails: d.motorFnolDetails ?? null,
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

      await loadCustomerUpdates();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Claim failed to load.");
      setDetail(null);
      setCustomerUpdates([]);
    } finally {
      setLoading(false);
    }
  }, [claimId, loadCustomerUpdates, role]);

  useEffect(() => { void load(); }, [load]);

  const draftCustomerUpdate = useCallback(async (): Promise<{ ok: boolean; message?: string }> => {
    setDrafting(true);
    try {
      const res = await fetch(`${apiBaseUrl}/api/v1/cases/${claimId}/customer-updates/draft`, {
        method: "POST",
        headers: { ...scopedHeaders(role, ""), "Content-Type": "application/json" },
        body: JSON.stringify({ reasonCategory: "StatusUpdate", channel: "Email" }),
      });
      if (res.status === 403) return { ok: false, message: "You do not have permission to draft customer updates." };
      if (!res.ok) return { ok: false, message: await readSafeApiError(res, `Draft failed with HTTP ${res.status}.`) };
      await loadCustomerUpdates();
      return { ok: true };
    } catch (err) {
      return { ok: false, message: err instanceof Error ? err.message : "Draft failed." };
    } finally {
      setDrafting(false);
    }
  }, [claimId, loadCustomerUpdates, role]);

  const draftDocumentFollowUp = useCallback(async (): Promise<{ ok: boolean; message?: string }> => {
    setDrafting(true);
    try {
      const res = await fetch(`${apiBaseUrl}/api/v1/cases/${claimId}/customer-updates/draft`, {
        method: "POST",
        headers: { ...scopedHeaders(role, ""), "Content-Type": "application/json" },
        body: JSON.stringify({ reasonCategory: "MissingDocuments", channel: "Email" }),
      });
      if (res.status === 403) return { ok: false, message: "You do not have permission to draft document follow-ups." };
      if (!res.ok) return { ok: false, message: await readSafeApiError(res, `Document follow-up draft failed with HTTP ${res.status}.`) };
      await loadCustomerUpdates();
      return { ok: true };
    } catch (err) {
      return { ok: false, message: err instanceof Error ? err.message : "Document follow-up draft failed." };
    } finally {
      setDrafting(false);
    }
  }, [claimId, loadCustomerUpdates, role]);

  const requestSupervisorReview = useCallback(async (): Promise<{ ok: boolean; message?: string }> => {
    setRequestingApproval(true);
    try {
      const res = await fetch(`${apiBaseUrl}/api/v1/cases/${claimId}/approvals`, {
        method: "POST",
        headers: { ...scopedHeaders(role, ""), "Content-Type": "application/json" },
        body: JSON.stringify({
          approvalType: "SupervisorReview",
          evidenceSummary: "Supervisor review requested from Claim Review before advancing the workflow next step.",
          sourceReferences: [claimId],
          riskLevel: detail?.riskLevel ?? "Medium",
        }),
      });
      if (res.status === 403) return { ok: false, message: "You do not have permission to request supervisor review." };
      if (!res.ok) return { ok: false, message: `Supervisor review request failed with HTTP ${res.status}.` };
      await load();
      return { ok: true };
    } catch (err) {
      return { ok: false, message: err instanceof Error ? err.message : "Supervisor review request failed." };
    } finally {
      setRequestingApproval(false);
    }
  }, [claimId, detail?.riskLevel, load, role]);

  const approveCustomerUpdate = useCallback(async (updateId: string, auditReason: string): Promise<{ ok: boolean; message?: string }> => {
    try {
      const res = await fetch(`${apiBaseUrl}/api/v1/customer-updates/${updateId}/approve`, {
        method: "POST",
        headers: { ...scopedHeaders(role, ""), "Content-Type": "application/json", "X-Audit-Reason": auditReason },
        body: JSON.stringify({}),
      });
      if (res.status === 403) return { ok: false, message: "You do not have permission to approve customer updates." };
      if (!res.ok) return { ok: false, message: `Approve failed with HTTP ${res.status}.` };
      await loadCustomerUpdates();
      return { ok: true };
    } catch (err) {
      return { ok: false, message: err instanceof Error ? err.message : "Approve failed." };
    }
  }, [loadCustomerUpdates, role]);

  const sendCustomerUpdate = useCallback(async (
    update: CustomerUpdateView,
    auditReason: string
  ): Promise<{ ok: boolean; message?: string }> => {
    const channel = update.channel.toLowerCase();
    const recipient = channel === "email"
      ? detail?.customerEmail
      : channel === "sms" || channel === "whatsapp"
        ? detail?.customerPhone
        : null;

    try {
      const res = await fetch(`${apiBaseUrl}/api/v1/customer-updates/${update.updateId}/send`, {
        method: "POST",
        headers: { ...scopedHeaders(role, ""), "Content-Type": "application/json", "X-Audit-Reason": auditReason },
        body: JSON.stringify({ recipient }),
      });
      if (res.status === 403) return { ok: false, message: "You do not have permission to send customer updates." };
      if (!res.ok) {
        const body = await res.json().catch(() => null);
        return { ok: false, message: body?.message ?? `Send failed with HTTP ${res.status}.` };
      }
      await loadCustomerUpdates();
      await load();
      return { ok: true };
    } catch (err) {
      return { ok: false, message: err instanceof Error ? err.message : "Send failed." };
    }
  }, [detail?.customerEmail, detail?.customerPhone, load, loadCustomerUpdates, role]);

  const exportEvidencePack = useCallback(async (auditReason: string): Promise<{ ok: boolean; objectKey?: string; message?: string }> => {
    setExporting(true);
    try {
      const res = await fetch(`${apiBaseUrl}/api/v1/compliance/cases/${claimId}/audit-pack/export`, {
        method: "POST",
        headers: { ...scopedHeaders(role, ""), "Content-Type": "application/json", "X-Audit-Reason": auditReason },
        body: JSON.stringify({}),
      });
      if (res.status === 403) return { ok: false, message: "You do not have permission to export evidence packs." };
      if (!res.ok) {
        const body = await res.json().catch(() => null);
        return { ok: false, message: body?.message ?? `Evidence-pack export failed with HTTP ${res.status}.` };
      }
      const body = await res.json();
      await load();
      return { ok: true, objectKey: body.objectKey };
    } catch (err) {
      return { ok: false, message: err instanceof Error ? err.message : "Evidence-pack export failed." };
    } finally {
      setExporting(false);
    }
  }, [claimId, load, role]);

  return {
    detail,
    timeline,
    customerUpdates,
    loading,
    error,
    drafting,
    requestingApproval,
    exporting,
    draftCustomerUpdate,
    draftDocumentFollowUp,
    approveCustomerUpdate,
    sendCustomerUpdate,
    exportEvidencePack,
    requestSupervisorReview,
  };
}
