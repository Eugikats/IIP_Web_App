"use client";

import { useCallback, useEffect, useState } from "react";
import { apiBaseUrl, scopedHeaders } from "@/lib/config";

export type ChecklistDocument = { documentType: string; label: string; received: boolean };

export type ChecklistDetails = {
  found: number;
  total: number;
  percentage: number;
  missing: { documentLabel: string; detail: string }[];
  unreadDocuments: string[];
};

export type ClaimChecklist = {
  caseId: string;
  productLine: string;
  completenessScore: number;
  requiredReceived: number;
  requiredTotal: number;
  missingRequiredDocuments: ChecklistDocument[];
  details: ChecklistDetails | null;
};

// GET /api/v1/cases/{caseId}/document-checklist: what the claim still needs, as the backend works it out. Nothing is
// guessed here; when the call fails the screen says so instead of showing a made-up list.
export function useClaimChecklist(caseId: string, role: string, refreshKey: unknown) {
  const [checklist, setChecklist] = useState<ClaimChecklist | null>(null);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    if (!caseId) return;
    try {
      const response = await fetch(`${apiBaseUrl}/api/v1/cases/${caseId}/document-checklist`, {
        headers: scopedHeaders(role, ""),
      });
      if (!response.ok) {
        setError("The list of what is still needed could not be loaded.");
        return;
      }
      setChecklist((await response.json()) as ClaimChecklist);
      setError(null);
    } catch {
      setError("The list of what is still needed could not be loaded.");
    }
  }, [caseId, role]);

  useEffect(() => { void load(); }, [load, refreshKey]);

  return { checklist, error, reload: load };
}
