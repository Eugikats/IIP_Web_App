"use client";

import { useCallback, useEffect, useState } from "react";
import { useUI } from "@/context/UIStateContext";
import { apiBaseUrl, scopedHeaders } from "@/lib/config";
import { LOVABLE_VISUAL_PARITY_MODE } from "@/lib/lovable-parity/mode";
import { documentLibrary } from "@/lib/lovable-parity/mock";
import { syntheticWorkspaceSnapshot } from "@/lib/lovable-parity/syntheticWorkspaceSnapshot";
import { failedState, loadingState, settledState, UiServiceError, type UiLoadState } from "@/lib/uiLoadState";

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
  requiresConfirmation: boolean;
  assignedUserId: string | null;
  documents: ClaimDocumentView[];
  claimedAmount: number | null;
  advisoryRiskSignals: string[];
  // Outcome N: the real Policy record behind this policy number, when one has actually been
  // loaded (file import today). Null, not a fabricated date, when no such record exists yet.
  policyEffectiveFrom: string | null;
  policyEffectiveTo: string | null;
  policyRecordSource: string | null;
  policyLastRefreshedAt: string | null;
};

export type ConfirmClaimCorrections = {
  productLine?: string;
  claimType?: string;
  incidentDate?: string;
  incidentDescription?: string;
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

export type ReviewRequestView = {
  requestId: string;
  status: string;
  origin: string;
  requestedByUserId: string;
  reason: string;
  requestedAt: string;
  resolvedAt: string | null;
  resolvedByUserId: string | null;
  resolutionNote: string | null;
};

function parityClaimDetail(claimId: string): ClaimDetailView | null {
  const claim = syntheticWorkspaceSnapshot.claims.find((item) => item.id === claimId);
  if (!claim) return null;

  return {
    claimId: claim.id,
    customerName: claim.customer,
    customerEmail: null,
    customerPhone: null,
    policyNumber: claim.policy,
    productLine: claim.productLine,
    claimType: claim.claimType,
    channel: claim.channel,
    incidentDate: "Not captured in synthetic fixture",
    incidentDescription: claim.incident,
    motorFnolDetails: null,
    status: claim.status,
    completenessScore: Math.round(claim.confidence * 100),
    riskLevel: claim.risk,
    priority: claim.sla === "Breached" ? "High" : claim.sla === "At Risk" ? "Medium" : "Normal",
    slaDueAt: null,
    requiresConfirmation: false,
    assignedUserId: claim.officer === "Unassigned" ? null : claim.officer,
    claimedAmount: null,
    advisoryRiskSignals: [],
    policyEffectiveFrom: null,
    policyEffectiveTo: null,
    policyRecordSource: null,
    policyLastRefreshedAt: null,
    documents: documentLibrary
      .filter((document) => document.claim === claim.id)
      .map((document) => ({
        documentId: document.id,
        documentType: document.type,
        processingStatus: document.status,
        classificationConfidence: document.confidence ?? 0,
      })),
  };
}

export function useClaimReview(claimId: string) {
  const { roleDef } = useUI();
  const role = roleDef.key;
  const [detail, setDetail] = useState<ClaimDetailView | null>(null);
  const [timeline, setTimeline] = useState<TimelineEventView[]>([]);
  const [customerUpdates, setCustomerUpdates] = useState<CustomerUpdateView[]>([]);
  const [reviewRequests, setReviewRequests] = useState<ReviewRequestView[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<UiServiceError | null>(null);
  const [loadState, setLoadState] = useState<UiLoadState<ClaimDetailView>>(loadingState());
  const [drafting, setDrafting] = useState(false);
  const [requestingApproval, setRequestingApproval] = useState(false);
  const [exporting, setExporting] = useState(false);
  const [confirming, setConfirming] = useState(false);

  const loadCustomerUpdates = useCallback(async () => {
    if (!claimId || LOVABLE_VISUAL_PARITY_MODE) { setCustomerUpdates([]); return; }
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

  const loadReviewRequests = useCallback(async () => {
    if (!claimId || LOVABLE_VISUAL_PARITY_MODE) { setReviewRequests([]); return; }
    try {
      const res = await fetch(`${apiBaseUrl}/api/v1/cases/${claimId}/review-requests`, {
        headers: scopedHeaders(role, ""),
      });
      if (!res.ok) {
        setReviewRequests([]);
        return;
      }
      const rows = await res.json();
      setReviewRequests((rows ?? []).map((request: ReviewRequestView) => ({
        requestId: request.requestId,
        status: request.status,
        origin: request.origin,
        requestedByUserId: request.requestedByUserId,
        reason: request.reason,
        requestedAt: request.requestedAt,
        resolvedAt: request.resolvedAt ?? null,
        resolvedByUserId: request.resolvedByUserId ?? null,
        resolutionNote: request.resolutionNote ?? null,
      })));
    } catch {
      setReviewRequests([]);
    }
  }, [claimId, role]);

  const load = useCallback(async () => {
    if (!claimId) { setLoading(false); return; }
    setLoading(true);
    setError(null);
    setLoadState(loadingState());
    try {
      if (LOVABLE_VISUAL_PARITY_MODE) {
        const fixtureDetail = parityClaimDetail(claimId);
        if (!fixtureDetail) {
          const serviceError = new UiServiceError({
            kind: "terminal_failure",
            message: "This synthetic claim is not available in the current fixture workspace.",
          });
          setError(serviceError);
          setDetail(null);
          setLoadState(failedState(serviceError));
          return;
        }
        setDetail(fixtureDetail);
        setTimeline([]);
        setCustomerUpdates([]);
        setReviewRequests([]);
        setLoadState(settledState(fixtureDetail, false));
        return;
      }
      const headers = scopedHeaders(role, "");
      const detailRes = await fetch(`${apiBaseUrl}/api/v1/claims/${claimId}`, { headers });
      if (!detailRes.ok) {
        const serviceError = await UiServiceError.fromResponse(
          detailRes,
          detailRes.status === 404 ? "This claim was not found in the current workspace." : `Claim is unavailable.`,
        );
        setError(serviceError);
        setDetail(null);
        setLoadState(failedState(serviceError));
        return;
      }
      const d = await detailRes.json();
      const nextDetail: ClaimDetailView = {
        claimId: d.claimId, customerName: d.customerName, customerEmail: d.customerEmail, customerPhone: d.customerPhone,
        policyNumber: d.policyNumber, productLine: d.productLine, claimType: d.claimType, channel: d.channel,
        incidentDate: d.incidentDate, incidentDescription: d.incidentDescription, status: d.status,
        motorFnolDetails: d.motorFnolDetails ?? null,
        completenessScore: d.completenessScore, riskLevel: d.riskLevel, priority: d.priority, slaDueAt: d.slaDueAt,
        requiresConfirmation: Boolean(d.requiresConfirmation),
        assignedUserId: d.assignedUserId,
        claimedAmount: d.claimedAmount ?? null,
        advisoryRiskSignals: d.advisoryRiskSignals ?? [],
        policyEffectiveFrom: d.policyEffectiveFrom ?? null,
        policyEffectiveTo: d.policyEffectiveTo ?? null,
        policyRecordSource: d.policyRecordSource ?? null,
        policyLastRefreshedAt: d.policyLastRefreshedAt ?? null,
        documents: (d.documents ?? []).map((x: ClaimDocumentView) => ({ ...x })),
      };
      setDetail(nextDetail);
      setLoadState(settledState(nextDetail, false));

      // Timeline is best-effort, a claim can be valid without timeline events.
      try {
        const tlRes = await fetch(`${apiBaseUrl}/api/v1/cases/${claimId}/timeline`, { headers });
        if (tlRes.ok) {
          const rows = await tlRes.json();
          setTimeline((rows ?? []).map((e: TimelineEventView) => ({ eventId: e.eventId, eventType: e.eventType, actor: e.actor, timestamp: e.timestamp, summary: e.summary })));
        }
      } catch { /* timeline optional */ }

      await loadCustomerUpdates();
      await loadReviewRequests();
    } catch (err) {
      const serviceError = UiServiceError.fromUnknown(err, "Claim service could not be reached.");
      setError(serviceError);
      setDetail(null);
      setCustomerUpdates([]);
      setReviewRequests([]);
      setLoadState(failedState(serviceError));
    } finally {
      setLoading(false);
    }
  }, [claimId, loadCustomerUpdates, loadReviewRequests, role]);

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
      if (!res.ok) return { ok: false, message: await readSafeApiError(res, `Draft failed.`) };
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
      if (!res.ok) return { ok: false, message: await readSafeApiError(res, `Document follow-up draft failed.`) };
      await loadCustomerUpdates();
      return { ok: true };
    } catch (err) {
      return { ok: false, message: err instanceof Error ? err.message : "Document follow-up draft failed." };
    } finally {
      setDrafting(false);
    }
  }, [claimId, loadCustomerUpdates, role]);

  const draftDecisionOutcomeUpdate = useCallback(async (): Promise<{ ok: boolean; message?: string }> => {
    setDrafting(true);
    try {
      const res = await fetch(`${apiBaseUrl}/api/v1/cases/${claimId}/customer-updates/draft`, {
        method: "POST",
        headers: { ...scopedHeaders(role, ""), "Content-Type": "application/json" },
        body: JSON.stringify({ reasonCategory: "DecisionOutcome", channel: "Email" }),
      });
      if (res.status === 403) return { ok: false, message: "You do not have permission to draft decision-outcome updates." };
      if (!res.ok) return { ok: false, message: await readSafeApiError(res, `Decision-outcome draft failed.`) };
      await loadCustomerUpdates();
      return { ok: true };
    } catch (err) {
      return { ok: false, message: err instanceof Error ? err.message : "Decision-outcome draft failed." };
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
      if (!res.ok) return { ok: false, message: `Supervisor review request failed.` };
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
      if (!res.ok) return { ok: false, message: `Approve failed.` };
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
        return { ok: false, message: body?.message ?? `Send failed.` };
      }
      await loadCustomerUpdates();
      await load();
      return { ok: true };
    } catch (err) {
      return { ok: false, message: err instanceof Error ? err.message : "Send failed." };
    }
  }, [detail?.customerEmail, detail?.customerPhone, load, loadCustomerUpdates, role]);

  const acknowledgeReviewRequest = useCallback(async (requestId: string): Promise<{ ok: boolean; message?: string }> => {
    try {
      const res = await fetch(`${apiBaseUrl}/api/v1/cases/${claimId}/review-requests/${requestId}/acknowledge`, {
        method: "POST",
        headers: scopedHeaders(role, ""),
      });
      if (res.status === 403) return { ok: false, message: "You do not have permission to acknowledge re-review requests." };
      if (res.status === 404) return { ok: false, message: "Re-review request was not found for this claim." };
      if (!res.ok) return { ok: false, message: await readSafeApiError(res, `Acknowledge failed.`) };
      await loadReviewRequests();
      return { ok: true };
    } catch (err) {
      return { ok: false, message: err instanceof Error ? err.message : "Acknowledge failed." };
    }
  }, [claimId, loadReviewRequests, role]);

  const resolveReviewRequest = useCallback(async (
    requestId: string,
    resolutionNote: string
  ): Promise<{ ok: boolean; message?: string }> => {
    try {
      const res = await fetch(`${apiBaseUrl}/api/v1/cases/${claimId}/review-requests/${requestId}/resolve`, {
        method: "POST",
        headers: { ...scopedHeaders(role, ""), "Content-Type": "application/json" },
        body: JSON.stringify({ resolutionNote }),
      });
      if (res.status === 403) return { ok: false, message: "You do not have permission to resolve re-review requests." };
      if (res.status === 404) return { ok: false, message: "Re-review request was not found for this claim." };
      if (!res.ok) return { ok: false, message: await readSafeApiError(res, `Resolve failed.`) };
      await loadReviewRequests();
      return { ok: true };
    } catch (err) {
      return { ok: false, message: err instanceof Error ? err.message : "Resolve failed." };
    }
  }, [claimId, loadReviewRequests, role]);

  const confirmClaim = useCallback(async (
    corrections: ConfirmClaimCorrections,
    auditReason: string
  ): Promise<{ ok: boolean; message?: string }> => {
    setConfirming(true);
    try {
      const res = await fetch(`${apiBaseUrl}/api/v1/cases/${claimId}/confirm`, {
        method: "POST",
        headers: { ...scopedHeaders(role, ""), "Content-Type": "application/json" },
        body: JSON.stringify({
          auditReason,
          productLine: corrections.productLine || undefined,
          claimType: corrections.claimType || undefined,
          incidentDate: corrections.incidentDate || undefined,
          incidentDescription: corrections.incidentDescription || undefined,
        }),
      });
      if (res.status === 403) return { ok: false, message: "You do not have permission to confirm this claim." };
      if (res.status === 409) return { ok: false, message: "This claim has already been confirmed." };
      if (res.status === 404) return { ok: false, message: "This claim was not found." };
      if (!res.ok) return { ok: false, message: await readSafeApiError(res, `Confirm failed.`) };
      await load();
      return { ok: true };
    } catch (err) {
      return { ok: false, message: err instanceof Error ? err.message : "Confirm failed." };
    } finally {
      setConfirming(false);
    }
  }, [claimId, load, role]);

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
        return { ok: false, message: body?.message ?? `Evidence-pack export failed.` };
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

  // WO 6.2: the export endpoint only ever returned an object key, a real single-document export
  // existed but the officer had no way to actually get the file. Fetches it as a blob (the
  // permission-gated GET needs the same scoped headers a plain <a href> can't carry) and triggers
  // a normal browser download.
  const downloadExportedAuditPack = useCallback(async (objectKey: string): Promise<{ ok: boolean; message?: string }> => {
    try {
      const res = await fetch(`${apiBaseUrl}/api/v1/compliance/cases/${claimId}/audit-pack/export/download?objectKey=${encodeURIComponent(objectKey)}`, {
        headers: scopedHeaders(role, ""),
      });
      if (res.status === 403) return { ok: false, message: "You do not have permission to download evidence packs." };
      if (!res.ok) return { ok: false, message: await readSafeApiError(res, "Evidence-pack download failed.") };

      const blob = await res.blob();
      const url = URL.createObjectURL(blob);
      const anchor = document.createElement("a");
      anchor.href = url;
      anchor.download = objectKey.split("/").pop() ?? "evidence-pack.json";
      anchor.click();
      URL.revokeObjectURL(url);
      return { ok: true };
    } catch (err) {
      return { ok: false, message: err instanceof Error ? err.message : "Evidence-pack download failed." };
    }
  }, [claimId, role]);

  return {
    detail,
    timeline,
    customerUpdates,
    reviewRequests,
    loading,
    error,
    loadState,
    drafting,
    requestingApproval,
    exporting,
    confirming,
    draftCustomerUpdate,
    draftDocumentFollowUp,
    draftDecisionOutcomeUpdate,
    approveCustomerUpdate,
    sendCustomerUpdate,
    acknowledgeReviewRequest,
    resolveReviewRequest,
    exportEvidencePack,
    downloadExportedAuditPack,
    requestSupervisorReview,
    confirmClaim,
    reload: load,
  };
}
