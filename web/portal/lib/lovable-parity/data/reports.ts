"use client";

import { useCallback, useEffect, useState } from "react";
import { useUI } from "@/context/UIStateContext";
import { apiBaseUrl, scopedHeaders } from "@/lib/config";
import { LOVABLE_VISUAL_PARITY_MODE } from "@/lib/lovable-parity/mode";

export type OperationsSummaryView = {
  openClaims: number;
  newClaimsToday: number;
  incompleteClaims: number;
  averageCompletenessScore: number;
  pendingApprovals: number;
  customerUpdatesPending: number;
  assistantQuestionsAnswered: number;
  escalationsRequired: number;
  slaAtRisk: number;
  slaBreached: number;
  claimsByStatus: { label: string; count: number }[];
};

type OperationsSummaryDto = {
  openClaims: number;
  newClaimsToday: number;
  incompleteClaims: number;
  averageCompletenessScore: number;
  pendingApprovals: number;
  openTasks: number;
  customerUpdatesPending: number;
  assistantQuestionsAnswered: number;
  escalationsRequired: number;
  slaAtRisk: number;
  slaBreached: number;
  claimsByStatus: { label: string; count: number }[];
  workQueueSummary: { label: string; count: number }[];
};

const PARITY: OperationsSummaryView = {
  openClaims: 148, newClaimsToday: 23, incompleteClaims: 41, averageCompletenessScore: 92,
  pendingApprovals: 9, customerUpdatesPending: 17, assistantQuestionsAnswered: 1284,
  escalationsRequired: 4, slaAtRisk: 6, slaBreached: 2,
  claimsByStatus: [
    { label: "In Review", count: 42 }, { label: "Missing Documents", count: 41 },
    { label: "Human Review Required", count: 28 }, { label: "Pending Approval", count: 9 },
    { label: "Ready for Export", count: 18 },
  ],
};

export function useOperationsSummary() {
  const { roleDef } = useUI();
  const role = roleDef.key;
  const [summary, setSummary] = useState<OperationsSummaryView | null>(LOVABLE_VISUAL_PARITY_MODE ? PARITY : null);
  const [loading, setLoading] = useState(!LOVABLE_VISUAL_PARITY_MODE);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    if (LOVABLE_VISUAL_PARITY_MODE) return;
    setLoading(true);
    setError(null);
    try {
      const response = await fetch(`${apiBaseUrl}/api/v1/reports/operations-summary`, { headers: scopedHeaders(role, "") });
      if (!response.ok) throw new Error(`Operations summary failed with HTTP ${response.status}.`);
      const dto = (await response.json()) as OperationsSummaryDto;
      setSummary({
        openClaims: dto.openClaims,
        newClaimsToday: dto.newClaimsToday,
        incompleteClaims: dto.incompleteClaims,
        averageCompletenessScore: dto.averageCompletenessScore,
        pendingApprovals: dto.pendingApprovals,
        customerUpdatesPending: dto.customerUpdatesPending,
        assistantQuestionsAnswered: dto.assistantQuestionsAnswered,
        escalationsRequired: dto.escalationsRequired,
        slaAtRisk: dto.slaAtRisk,
        slaBreached: dto.slaBreached,
        claimsByStatus: dto.claimsByStatus ?? [],
      });
    } catch (err) {
      setError(err instanceof Error ? err.message : "Operations summary failed to load.");
      setSummary(null);
    } finally {
      setLoading(false);
    }
  }, [role]);

  useEffect(() => {
    void load();
  }, [load]);

  return { summary, loading, error, parityMode: LOVABLE_VISUAL_PARITY_MODE };
}
