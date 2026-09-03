"use client";

import { useCallback, useState } from "react";
import { apiBaseUrl, scopedHeaders } from "@/lib/config";

export type ClaimsSummaryKnowledgeSource = {
  label: string;
  documentName: string;
  chunkId: string;
  excerpt: string;
};

export type ClaimsSummaryView = {
  caseId: string;
  summary: string;
  openQuestions: string[];
  knowledgeSources: ClaimsSummaryKnowledgeSource[];
  evidenceRefs: string[];
  confidence: number;
  agentActionLogId: string;
  correlationId: string;
};

export type ClaimsSummaryOutcome =
  | { ok: true; summary: ClaimsSummaryView }
  | { ok: false; kind: "agent_disabled" | "error"; message: string };

// WO-10: thin client for POST /api/v1/cases/{caseId}/summary (the claims-summary agent). Follows
// the same data-hook shape every other lovable-parity/data module uses — real API call, honest
// error/refusal states, no synthetic fallback.
export function useClaimsSummary(caseId: string, role: string) {
  const [summary, setSummary] = useState<ClaimsSummaryView | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const generate = useCallback(async (): Promise<ClaimsSummaryOutcome> => {
    setLoading(true);
    setError(null);
    try {
      const response = await fetch(`${apiBaseUrl}/api/v1/cases/${caseId}/summary`, {
        method: "POST",
        headers: { ...scopedHeaders(role, ""), "Content-Type": "application/json" },
      });

      if (response.status === 503) {
        const body = (await response.json().catch(() => null)) as { message?: string } | null;
        const message = body?.message ?? "The claims-summary agent is not available right now.";
        setError(message);
        return { ok: false, kind: "agent_disabled", message };
      }

      if (!response.ok) {
        const message = `Claim summary failed with HTTP ${response.status}.`;
        setError(message);
        return { ok: false, kind: "error", message };
      }

      const body = (await response.json()) as ClaimsSummaryView;
      setSummary(body);
      return { ok: true, summary: body };
    } catch (err) {
      const message = err instanceof Error ? err.message : "Claim summary failed.";
      setError(message);
      return { ok: false, kind: "error", message };
    } finally {
      setLoading(false);
    }
  }, [caseId, role]);

  return { summary, loading, error, generate };
}
