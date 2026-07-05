"use client";

import { useCallback, useEffect, useState } from "react";
import { useUI } from "@/context/UIStateContext";
import { apiBaseUrl, scopedHeaders } from "@/lib/config";
import { LOVABLE_VISUAL_PARITY_MODE } from "@/lib/lovable-parity/mode";
import { approvals as fixtureApprovals } from "@/lib/lovable-parity/mock";

export type ApprovalView = {
  id: string;
  title: string;
  claim?: string;
  requester: string;
  reviewer: string;
  risk: string;
  ageHrs: number;
  summary: string;
  status: string;
};

type ApprovalDto = {
  approvalId: string;
  caseId: string;
  approvalType: string;
  status: string;
  riskLevel: string;
  evidenceSummary: string;
  requestedByUserId: string;
  reviewerUserId?: string | null;
  createdAt: string;
};

const PARITY: ApprovalView[] = fixtureApprovals.map((a) => ({
  id: a.id, title: a.title, claim: a.claim, requester: a.requester, reviewer: a.reviewer,
  risk: a.risk, ageHrs: a.ageHrs, summary: a.summary, status: a.status,
}));

function ageHours(iso: string): number {
  const t = Date.parse(iso);
  if (Number.isNaN(t)) return 0;
  return Math.max(0, Math.round(((Date.now() - t) / 3_600_000) * 10) / 10);
}

function mapDto(dto: ApprovalDto): ApprovalView {
  return {
    id: dto.approvalId,
    title: dto.approvalType,
    claim: dto.caseId,
    requester: dto.requestedByUserId,
    reviewer: dto.reviewerUserId || "—",
    risk: dto.riskLevel,
    ageHrs: ageHours(dto.createdAt),
    summary: dto.evidenceSummary,
    status: dto.status,
  };
}

export function useApprovals() {
  const { roleDef } = useUI();
  const role = roleDef.key;
  const [approvals, setApprovals] = useState<ApprovalView[]>(LOVABLE_VISUAL_PARITY_MODE ? PARITY : []);
  const [loading, setLoading] = useState(!LOVABLE_VISUAL_PARITY_MODE);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    if (LOVABLE_VISUAL_PARITY_MODE) return;
    setLoading(true);
    setError(null);
    try {
      const response = await fetch(`${apiBaseUrl}/api/v1/approvals`, { headers: scopedHeaders(role, "") });
      if (!response.ok) throw new Error(`Approvals failed with HTTP ${response.status}.`);
      const rows = (await response.json()) as ApprovalDto[];
      setApprovals(rows.map(mapDto));
    } catch (err) {
      setError(err instanceof Error ? err.message : "Approvals failed to load.");
      setApprovals([]);
    } finally {
      setLoading(false);
    }
  }, [role]);

  useEffect(() => {
    void load();
  }, [load]);

  return { approvals, loading, error, parityMode: LOVABLE_VISUAL_PARITY_MODE };
}
