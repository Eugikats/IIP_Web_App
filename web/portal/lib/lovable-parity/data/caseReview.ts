"use client";

import { useCallback, useState } from "react";
import { apiBaseUrl, scopedHeaders } from "@/lib/config";
import type { ClaimsSummaryView } from "./claimsSummary";

// Thin hook for GET /api/v1/cases/{caseId}/case-review — loads the most recently
// persisted case review without triggering a new generation. Returns null (not an
// error) on 404, which simply means no review has been generated for this case yet.
export function useCaseReview(caseId: string, role: string) {
  const [review, setReview] = useState<ClaimsSummaryView | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const res = await fetch(`${apiBaseUrl}/api/v1/cases/${caseId}/case-review`, {
        headers: scopedHeaders(role, ""),
      });
      if (res.status === 404) {
        setReview(null); // no review generated yet — valid empty state, not an error
        return;
      }
      if (!res.ok) {
        setError(`Case review load failed (HTTP ${res.status}).`);
        return;
      }
      setReview((await res.json()) as ClaimsSummaryView);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Case review load failed.");
    } finally {
      setLoading(false);
    }
  }, [caseId, role]);

  return { review, loading, error, load };
}
