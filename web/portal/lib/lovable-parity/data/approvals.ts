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
  executedAt: string | null;
  allowedActions: { action: string; label: string; requiredPermission: string; reason?: string | null }[];
  blockedActions: { action: string; label: string; requiredPermission: string; reason?: string | null }[];
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
  executedAt?: string | null;
  allowedActions?: { action: string; label: string; requiredPermission: string; reason?: string | null }[];
  blockedActions?: { action: string; label: string; requiredPermission: string; reason?: string | null }[];
};

const PARITY: ApprovalView[] = fixtureApprovals.map((a) => ({
  id: a.id, title: a.title, claim: a.claim, requester: a.requester, reviewer: a.reviewer,
  risk: a.risk, ageHrs: a.ageHrs, summary: a.summary, status: a.status,
  executedAt: null,
  allowedActions: [],
  blockedActions: [],
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
    executedAt: dto.executedAt ?? null,
    allowedActions: dto.allowedActions ?? [],
    blockedActions: dto.blockedActions ?? [],
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
      if (!response.ok) throw new Error(`Approvals failed.`);
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

  const decide = useCallback(
    async (approvalId: string, decision: "approve" | "reject" | "escalate" | "override", auditReason: string) => {
      if (LOVABLE_VISUAL_PARITY_MODE) return { ok: true };
      const response = await fetch(`${apiBaseUrl}/api/v1/approvals/${approvalId}/${decision}`, {
        method: "POST",
        headers: {
          ...scopedHeaders(role, ""),
          "Content-Type": "application/json",
          "X-Audit-Reason": auditReason,
        },
        body: JSON.stringify({ comments: auditReason }),
      });
      if (!response.ok) return { ok: false, message: `${decision} failed.` };
      await load();
      return { ok: true };
    },
    [load, role],
  );

  const executeNextStep = useCallback(
    async (approvalId: string, auditReason: string) => {
      if (LOVABLE_VISUAL_PARITY_MODE) return { ok: true };
      const response = await fetch(`${apiBaseUrl}/api/v1/approvals/${approvalId}/execute-next-step`, {
        method: "POST",
        headers: {
          ...scopedHeaders(role, ""),
          "Content-Type": "application/json",
          "X-Audit-Reason": auditReason,
        },
        body: JSON.stringify({ comments: auditReason }),
      });
      if (!response.ok) return { ok: false, message: `Execute failed.` };
      await load();
      return { ok: true };
    },
    [load, role],
  );

  return {
    approvals,
    loading,
    error,
    parityMode: LOVABLE_VISUAL_PARITY_MODE,
    approve: (id: string, reason: string) => decide(id, "approve", reason),
    reject: (id: string, reason: string) => decide(id, "reject", reason),
    escalate: (id: string, reason: string) => decide(id, "escalate", reason),
    override: (id: string, reason: string) => decide(id, "override", reason),
    executeNextStep,
  };
}
