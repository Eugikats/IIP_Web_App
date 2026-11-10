"use client";

import { useCallback, useEffect, useState } from "react";
import { apiBaseUrl, scopedHeaders } from "@/lib/config";

// The fraud agent's review of one claim: GET /api/v1/cases/{caseId}/fraud-review reads the latest
// review without calling the model, POST .../fraud-review/run has the agent read the claim now.
// Each finding cites the claim facts it rests on. Everything here is advisory.

export type FraudAgentFact = { id: string; text: string };

export type FraudAgentFinding = {
  indicatorCode: string;
  indicatorLabel: string;
  line: string;
  title: string;
  observation: string;
  whatToCheck: string;
  severity: "AdvisorySignal" | "PotentialAnomaly" | "RequiresInvestigation";
  severityLabel: string;
  facts: FraudAgentFact[];
};

export type FraudAgentReview = {
  caseId: string;
  claimReference: string;
  agentEnabled: boolean;
  ran: boolean;
  stale: boolean;
  findings: FraudAgentFinding[];
  leftOut: number;
  generatedAt: string | null;
  generatedBy: string | null;
  provider: string | null;
  isMock: boolean;
  playbookVersion: string;
  notice: string;
};

export function useFraudAgentReview(caseId: string, role: string) {
  const [review, setReview] = useState<FraudAgentReview | null>(null);
  const [loading, setLoading] = useState(true);
  // "denied" means this person's role does not see fraud signals, so the card is not shown at all.
  const [denied, setDenied] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [running, setRunning] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const res = await fetch(`${apiBaseUrl}/api/v1/cases/${caseId}/fraud-review`, { headers: scopedHeaders(role, "") });
      if (res.status === 403) {
        setDenied(true);
        return;
      }
      if (!res.ok) {
        setError("The fraud review could not be loaded.");
        return;
      }
      setReview((await res.json()) as FraudAgentReview);
    } catch {
      setError("The fraud review could not be loaded.");
    } finally {
      setLoading(false);
    }
  }, [caseId, role]);

  useEffect(() => {
    void load();
  }, [load]);

  const run = useCallback(async () => {
    setRunning(true);
    setError(null);
    try {
      const res = await fetch(`${apiBaseUrl}/api/v1/cases/${caseId}/fraud-review/run`, { method: "POST", headers: scopedHeaders(role, "") });
      if (res.ok) {
        setReview((await res.json()) as FraudAgentReview);
        return;
      }
      const body = (await res.json().catch(() => null)) as { message?: string } | null;
      setError(body?.message ?? "The fraud agent could not run just now.");
    } catch {
      setError("The fraud agent could not run just now.");
    } finally {
      setRunning(false);
    }
  }, [caseId, role]);

  return { review, loading, denied, error, running, run };
}
