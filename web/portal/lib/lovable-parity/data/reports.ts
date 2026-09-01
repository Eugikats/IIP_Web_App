"use client";

import { useCallback, useEffect, useState } from "react";
import { useUI } from "@/context/UIStateContext";
import { apiBaseUrl, scopedHeaders } from "@/lib/config";
import { LOVABLE_VISUAL_PARITY_MODE } from "@/lib/lovable-parity/mode";
import { syntheticWorkspaceSnapshot } from "@/lib/lovable-parity/syntheticWorkspaceSnapshot";

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
  dataAsOf: string;
  snapshotId: string;
  source: "live" | "synthetic";
  limitations: string;
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
  dataAsOf?: string;
  snapshotId?: string;
};

const PARITY: OperationsSummaryView = {
  ...syntheticWorkspaceSnapshot.operationsSummary,
  dataAsOf: syntheticWorkspaceSnapshot.dataAsOf,
  snapshotId: syntheticWorkspaceSnapshot.snapshotId,
  source: syntheticWorkspaceSnapshot.source,
  limitations: syntheticWorkspaceSnapshot.limitations,
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
      const receivedAt = new Date().toISOString();
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
        dataAsOf: dto.dataAsOf ?? receivedAt,
        snapshotId: dto.snapshotId ?? `operations-received-${receivedAt}`,
        source: "live",
        limitations: "Tenant-scoped operational aggregates. Values can change after this timestamp.",
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
