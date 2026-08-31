"use client";

import { useCallback, useEffect, useState } from "react";
import { useUI } from "@/context/UIStateContext";
import { apiBaseUrl, scopedHeaders } from "@/lib/config";

export type MyWorkSourceType = "WorkflowTask" | "Approval" | "DocumentReview" | "CommunicationReview" | "AgentWorkItem";
export type MyWorkOwnership = "Mine" | "Unassigned" | "All";
export type MyWorkState = "Ready" | "InProgress" | "Escalated" | "Completed" | "Closed";
export type MyWorkDueState = "Overdue" | "DueSoon" | "OnTrack" | "NoDueDate";
export type MyWorkEvidenceReadiness = "Ready" | "Gaps" | "NotAssessed";

export type MyWorkItem = {
  id: string;
  sourceType: MyWorkSourceType;
  sourceId: string;
  caseId?: string | null;
  title: string;
  requiredAction: string;
  sourceStatus: string;
  state: MyWorkState;
  ownerUserId?: string | null;
  ownerTeam?: string | null;
  ownerBasis: string;
  priority: string;
  dueAt?: string | null;
  dueState: MyWorkDueState;
  evidenceReadiness: MyWorkEvidenceReadiness;
  approvalRequired: boolean;
  href: string;
  updatedAt: string;
};

export type MyWorkResponse = {
  ownership: MyWorkOwnership;
  returnedCount: number;
  hasMore: boolean;
  items: MyWorkItem[];
  summary: { total: number; overdue: number; unassigned: number; approvalRequired: number };
};

type MyWorkFilters = {
  ownership: "mine" | "unassigned" | "all";
  sourceType?: MyWorkSourceType;
  state?: MyWorkState;
};

async function readError(response: Response) {
  try {
    const body = await response.json() as { title?: string; detail?: string; errors?: Record<string, string[]> };
    return (body.errors ? Object.values(body.errors).flat()[0] : null) ?? body.detail ?? body.title ?? `My Work failed with HTTP ${response.status}.`;
  } catch {
    return `My Work failed with HTTP ${response.status}.`;
  }
}

export function useMyWork(filters: MyWorkFilters) {
  const { roleDef } = useUI();
  const [response, setResponse] = useState<MyWorkResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const refresh = useCallback(async () => {
    setLoading(true);
    setError(null);
    const query = new URLSearchParams({ ownership: filters.ownership, limit: "100" });
    if (filters.sourceType) query.set("sourceType", filters.sourceType);
    if (filters.state) query.set("state", filters.state);
    try {
      const result = await fetch(`${apiBaseUrl}/api/v1/my-work?${query}`, {
        headers: scopedHeaders(roleDef.key, ""),
        cache: "no-store",
      });
      if (!result.ok) {
        setResponse(null);
        setError(await readError(result));
      } else {
        setResponse(await result.json() as MyWorkResponse);
      }
    } catch (requestError) {
      setResponse(null);
      setError(requestError instanceof Error ? requestError.message : "My Work is unavailable.");
    } finally {
      setLoading(false);
    }
  }, [filters.ownership, filters.sourceType, filters.state, roleDef.key]);

  useEffect(() => { void refresh(); }, [refresh]);
  return { response, loading, error, refresh };
}
