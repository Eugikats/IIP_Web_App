"use client";

import { useCallback, useEffect, useState } from "react";
import { useUI } from "@/context/UIStateContext";
import { apiBaseUrl, scopedHeaders } from "@/lib/config";
import { LOVABLE_VISUAL_PARITY_MODE } from "@/lib/lovable-parity/mode";
import { syntheticWorkspaceSnapshot } from "@/lib/lovable-parity/syntheticWorkspaceSnapshot";
import { failedState, loadingState, settledState, UiServiceError, type UiLoadState } from "@/lib/uiLoadState";

export type ClaimRowView = {
  id: string;
  customer: string;
  policy: string;
  productLine: string;
  claimType: string;
  status: string;
  complete: boolean;
  sla: string;
  risk: string;
  channel: string;
  updated: string;
  confidence: number;
  region: string;
  assignedUserId: string | null;
  assignedTeam: string | null;
};

type ClaimListItemDto = {
  claimId: string;
  customerName: string;
  policyNumber: string;
  productLine: string;
  claimType: string;
  channel: string;
  status: string;
  completenessScore: number;
  riskLevel: string;
  priority: string;
  slaStatus: string;
  slaDueAt?: string | null;
  assignedUserId?: string | null;
  assignedTeam?: string | null;
  updatedAt: string;
};

function formatWhen(iso: string): string {
  const t = Date.parse(iso);
  if (Number.isNaN(t)) return iso || "—";
  return new Date(t).toLocaleString(undefined, { month: "short", day: "numeric", hour: "2-digit", minute: "2-digit" });
}

function mapDto(dto: ClaimListItemDto): ClaimRowView {
  return {
    id: dto.claimId,
    customer: dto.customerName,
    policy: dto.policyNumber,
    productLine: dto.productLine,
    claimType: dto.claimType,
    status: dto.status,
    complete: dto.completenessScore >= 100,
    sla: dto.slaStatus,
    risk: dto.riskLevel,
    channel: dto.channel,
    updated: formatWhen(dto.updatedAt),
    confidence: Math.max(0, Math.min(1, dto.completenessScore / 100)),
    region: "—",
    assignedUserId: dto.assignedUserId ?? null,
    assignedTeam: dto.assignedTeam ?? null,
  };
}

// Fixture claims use a slightly different shape; project them into the view.
const PARITY_ROWS: ClaimRowView[] = syntheticWorkspaceSnapshot.claims.map((c) => ({
  id: c.id,
  customer: c.customer,
  policy: c.policy,
  productLine: c.productLine,
  claimType: c.claimType,
  status: c.status,
  complete: c.missingDocs === 0,
  sla: c.sla === "At Risk" ? "AtRisk" : c.sla,
  risk: c.risk,
  channel: c.channel,
  updated: c.updated,
  confidence: c.confidence,
  region: c.region,
  assignedUserId: c.officer === "Unassigned" ? null : c.officer,
  assignedTeam: null,
}));

export function useClaims() {
  const { roleDef } = useUI();
  const role = roleDef.key;
  const [claims, setClaims] = useState<ClaimRowView[]>(LOVABLE_VISUAL_PARITY_MODE ? PARITY_ROWS : []);
  const [loading, setLoading] = useState(!LOVABLE_VISUAL_PARITY_MODE);
  const [error, setError] = useState<UiServiceError | null>(null);
  const [loadState, setLoadState] = useState<UiLoadState<ClaimRowView[]>>(
    LOVABLE_VISUAL_PARITY_MODE ? settledState(PARITY_ROWS, PARITY_ROWS.length === 0) : loadingState(),
  );

  const load = useCallback(async () => {
    if (LOVABLE_VISUAL_PARITY_MODE) return;
    setLoading(true);
    setError(null);
    setLoadState(loadingState());
    try {
      const response = await fetch(`${apiBaseUrl}/api/v1/claims`, { headers: scopedHeaders(role, "") });
      if (!response.ok) {
        const serviceError = await UiServiceError.fromResponse(response, `Claims are unavailable (HTTP ${response.status}).`);
        setError(serviceError);
        setLoadState(failedState(serviceError));
        return;
      }
      const payload = (await response.json()) as ClaimListItemDto[] | { items?: ClaimListItemDto[] };
      const list = Array.isArray(payload) ? payload : payload.items ?? [];
      const nextClaims = list.map(mapDto);
      setClaims(nextClaims);
      setLoadState(settledState(nextClaims, nextClaims.length === 0));
    } catch (err) {
      const serviceError = UiServiceError.fromUnknown(err, "Claims service could not be reached.");
      setError(serviceError);
      setLoadState(failedState(serviceError));
    } finally {
      setLoading(false);
    }
  }, [role]);

  useEffect(() => {
    void load();
  }, [load]);

  const assignCase = useCallback(
    async (caseId: string, assignedUserId: string, assignedTeam = "Motor Review") => {
      if (LOVABLE_VISUAL_PARITY_MODE) return { ok: true };
      const response = await fetch(`${apiBaseUrl}/api/v1/cases/${caseId}/assign`, {
        method: "POST",
        headers: { ...scopedHeaders(role, ""), "Content-Type": "application/json" },
        body: JSON.stringify({
          assignedUserId,
          assignedTeam,
          auditReason: "Assigned from claims work queue",
        }),
      });
      if (!response.ok) {
        return { ok: false, message: `Assign failed with HTTP ${response.status}.` };
      }
      await load();
      return { ok: true };
    },
    [load, role],
  );

  const escalateCase = useCallback(
    async (caseId: string, auditReason: string) => {
      if (LOVABLE_VISUAL_PARITY_MODE) return { ok: true };
      const response = await fetch(`${apiBaseUrl}/api/v1/cases/${caseId}/escalate`, {
        method: "POST",
        headers: { ...scopedHeaders(role, ""), "Content-Type": "application/json" },
        body: JSON.stringify({
          auditReason,
          assignedTeam: "Claims Supervision",
        }),
      });
      if (!response.ok) {
        return { ok: false, message: `Escalate failed with HTTP ${response.status}.` };
      }
      await load();
      return { ok: true };
    },
    [load, role],
  );

  return { claims, loading, error, loadState, parityMode: LOVABLE_VISUAL_PARITY_MODE, refresh: load, assignCase, escalateCase };
}
