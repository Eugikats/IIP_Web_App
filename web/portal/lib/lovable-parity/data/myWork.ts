"use client";

import { useCallback, useEffect, useState } from "react";
import { useUI } from "@/context/UIStateContext";
import { apiBaseUrl, scopedHeaders } from "@/lib/config";
import { failedState, loadingState, settledState, UiServiceError, type UiLoadState } from "@/lib/uiLoadState";

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
  caseReference?: string | null;
  reason: string;
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

export function useMyWork(filters: MyWorkFilters) {
  const { roleDef } = useUI();
  const [response, setResponse] = useState<MyWorkResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<UiServiceError | null>(null);
  const [loadState, setLoadState] = useState<UiLoadState<MyWorkResponse>>(loadingState());

  const refresh = useCallback(async () => {
    setLoading(true);
    setError(null);
    setLoadState(loadingState());
    const query = new URLSearchParams({ ownership: filters.ownership, limit: "100" });
    if (filters.sourceType) query.set("sourceType", filters.sourceType);
    if (filters.state) query.set("state", filters.state);
    try {
      const result = await fetch(`${apiBaseUrl}/api/v1/my-work?${query}`, {
        headers: scopedHeaders(roleDef.key, ""),
        cache: "no-store",
      });
      if (!result.ok) {
        const serviceError = await UiServiceError.fromResponse(result, `My Work is unavailable.`);
        setError(serviceError);
        setLoadState(failedState(serviceError));
      } else {
        const nextResponse = await result.json() as MyWorkResponse;
        setResponse(nextResponse);
        setLoadState(settledState(nextResponse, nextResponse.items.length === 0));
      }
    } catch (requestError) {
      const serviceError = UiServiceError.fromUnknown(requestError, "My Work service could not be reached.");
      setError(serviceError);
      setLoadState(failedState(serviceError));
    } finally {
      setLoading(false);
    }
  }, [filters.ownership, filters.sourceType, filters.state, roleDef.key]);

  useEffect(() => { void refresh(); }, [refresh]);
  return { response, loading, error, loadState, refresh };
}
