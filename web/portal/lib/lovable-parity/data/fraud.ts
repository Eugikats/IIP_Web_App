"use client";

import { useCallback, useEffect, useState } from "react";
import { useUI } from "@/context/UIStateContext";
import { apiBaseUrl, scopedHeaders } from "@/lib/config";
import { LOVABLE_VISUAL_PARITY_MODE } from "@/lib/lovable-parity/mode";

export type FraudSeverity = "Advisory" | "Anomaly" | "Investigation";

export type FraudSignalView = {
  id: string;
  title: string;
  detail: string;
  severity: FraudSeverity;
  linked: string;
  evidence: string[];
};

type FraudSignalDto = {
  ruleId: string;
  signalType: string;
  title: string;
  description: string;
  severity: string;
  relatedCaseIds: string[];
  evidence: string[];
};

const SEVERITY_MAP: Record<string, FraudSeverity> = {
  AdvisorySignal: "Advisory",
  PotentialAnomaly: "Anomaly",
  RequiresInvestigation: "Investigation",
};

const PARITY_FIXTURES: FraudSignalView[] = [
  { id: "R-VELOCITY-1", title: "High claim velocity", detail: "3 claims from one claimant within 30 days.", severity: "Investigation", linked: "CLM-1071, CLM-1074, CLM-1082", evidence: ["Claim CLM-1071 · Collision · 2026-03-01"] },
  { id: "R-DUP-CONTACT-1", title: "Shared contact across claims", detail: "2 distinct claims linked to the same phone number.", severity: "Anomaly", linked: "CLM-1048, CLM-1052", evidence: ["phone number: 256700000111"] },
  { id: "R-DUP-POLICY-1", title: "Policy linked to multiple claimants", detail: "Policy MTR-555 appears under 2 claimant names.", severity: "Anomaly", linked: "CLM-1061, CLM-1064", evidence: ["Claim CLM-1061 · Grace Wanjiku"] },
  { id: "R-LOW-COMPLETENESS-1", title: "Low documentation completeness", detail: "Documentation completeness 20%, below the 40% threshold.", severity: "Advisory", linked: "CLM-1090", evidence: ["Completeness: 20%"] },
];

function mapDto(dto: FraudSignalDto): FraudSignalView {
  return {
    id: `${dto.ruleId}:${dto.relatedCaseIds.join(",")}`,
    title: dto.signalType || dto.title,
    detail: dto.description,
    severity: SEVERITY_MAP[dto.severity] ?? "Advisory",
    linked: dto.relatedCaseIds.length > 0 ? dto.relatedCaseIds.join(", ") : "—",
    evidence: dto.evidence,
  };
}

export function useFraudSignals() {
  const { roleDef } = useUI();
  const role = roleDef.key;
  const [signals, setSignals] = useState<FraudSignalView[]>(LOVABLE_VISUAL_PARITY_MODE ? PARITY_FIXTURES : []);
  const [loading, setLoading] = useState(!LOVABLE_VISUAL_PARITY_MODE);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    if (LOVABLE_VISUAL_PARITY_MODE) return;
    setLoading(true);
    setError(null);
    try {
      const response = await fetch(`${apiBaseUrl}/api/v1/fraud/signals`, { headers: scopedHeaders(role, "") });
      if (!response.ok) throw new Error(`Fraud signals failed with HTTP ${response.status}.`);
      const rows = (await response.json()) as FraudSignalDto[];
      setSignals(rows.map(mapDto));
    } catch (err) {
      setError(err instanceof Error ? err.message : "Fraud signals failed to load.");
      setSignals([]);
    } finally {
      setLoading(false);
    }
  }, [role]);

  useEffect(() => {
    void load();
  }, [load]);

  const counts = {
    advisory: signals.filter((s) => s.severity === "Advisory").length,
    anomaly: signals.filter((s) => s.severity === "Anomaly").length,
    investigation: signals.filter((s) => s.severity === "Investigation").length,
    total: signals.length,
  };

  return { signals, loading, error, counts, parityMode: LOVABLE_VISUAL_PARITY_MODE };
}
