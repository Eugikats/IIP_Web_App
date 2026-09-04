"use client";

import { useCallback, useEffect, useState } from "react";
import { useUI } from "@/context/UIStateContext";
import { apiBaseUrl, scopedHeaders } from "@/lib/config";
import { LOVABLE_VISUAL_PARITY_MODE } from "@/lib/lovable-parity/mode";

export type FraudSeverity = "Advisory" | "Anomaly" | "Investigation";

export type ActionState = { action: string; label: string; requiredPermission: string; reason: string | null };

export type FraudDispositionView = {
  status: string;
  assignedToUserId: string | null;
  outcome: string | null;
  outcomeNotes: string | null;
  createdByUserId: string;
  createdAt: string;
  updatedAt: string;
  allowedActions: ActionState[];
  blockedActions: ActionState[];
};

export type FraudSignalView = {
  id: string;
  signalId: string;
  ruleId: string;
  title: string;
  detail: string;
  severity: FraudSeverity;
  linked: string;
  relatedCaseIds: string[];
  evidence: string[];
  disposition: FraudDispositionView;
};

type FraudDispositionDto = {
  status: string;
  assignedToUserId: string | null;
  outcome: string | null;
  outcomeNotes: string | null;
  createdByUserId: string;
  createdAt: string;
  updatedAt: string;
  allowedActions?: ActionState[];
  blockedActions?: ActionState[];
};

type FraudSignalDto = {
  signalId: string;
  ruleId: string;
  signalType: string;
  title: string;
  description: string;
  severity: string;
  relatedCaseIds: string[];
  evidence: string[];
  disposition: FraudDispositionDto | null;
};

const SEVERITY_MAP: Record<string, FraudSeverity> = {
  AdvisorySignal: "Advisory",
  PotentialAnomaly: "Anomaly",
  RequiresInvestigation: "Investigation",
};

export const OUTCOME_LABEL: Record<string, string> = {
  NoFurtherActionRequired: "No further action required",
  ReferredForSpecialInvestigation: "Referred for special investigation",
  ClaimContinuesUnderNormalReview: "Claim continues under normal review",
  UnableToSubstantiateConcern: "Unable to substantiate concern",
};

export const SELECTABLE_OUTCOMES = Object.keys(OUTCOME_LABEL);

const PARITY_FIXTURES: FraudSignalView[] = [
  { id: "R-VELOCITY-1", signalId: "R-VELOCITY-1", ruleId: "R-VELOCITY", title: "High claim velocity", detail: "3 claims from one claimant within 30 days.", severity: "Investigation", linked: "CLM-1071, CLM-1074, CLM-1082", relatedCaseIds: [], evidence: ["Claim CLM-1071 · Collision · 2026-03-01"], disposition: { status: "NotTriaged", assignedToUserId: null, outcome: null, outcomeNotes: null, createdByUserId: "", createdAt: "", updatedAt: "", allowedActions: [], blockedActions: [] } },
  { id: "R-DUP-CONTACT-1", signalId: "R-DUP-CONTACT-1", ruleId: "R-DUP-CONTACT", title: "Shared contact across claims", detail: "2 distinct claims linked to the same phone number.", severity: "Anomaly", linked: "CLM-1048, CLM-1052", relatedCaseIds: [], evidence: ["phone number: 256700000111"], disposition: { status: "NotTriaged", assignedToUserId: null, outcome: null, outcomeNotes: null, createdByUserId: "", createdAt: "", updatedAt: "", allowedActions: [], blockedActions: [] } },
];

function mapDisposition(dto: FraudDispositionDto | null): FraudDispositionView {
  if (!dto) {
    return { status: "NotTriaged", assignedToUserId: null, outcome: null, outcomeNotes: null, createdByUserId: "", createdAt: "", updatedAt: "", allowedActions: [], blockedActions: [] };
  }
  return {
    status: dto.status,
    assignedToUserId: dto.assignedToUserId,
    outcome: dto.outcome,
    outcomeNotes: dto.outcomeNotes,
    createdByUserId: dto.createdByUserId,
    createdAt: dto.createdAt,
    updatedAt: dto.updatedAt,
    allowedActions: dto.allowedActions ?? [],
    blockedActions: dto.blockedActions ?? [],
  };
}

function mapDto(dto: FraudSignalDto): FraudSignalView {
  return {
    id: dto.signalId,
    signalId: dto.signalId,
    ruleId: dto.ruleId,
    title: dto.signalType || dto.title,
    detail: dto.description,
    severity: SEVERITY_MAP[dto.severity] ?? "Advisory",
    linked: dto.relatedCaseIds.length > 0 ? dto.relatedCaseIds.join(", ") : "—",
    relatedCaseIds: dto.relatedCaseIds,
    evidence: dto.evidence,
    disposition: mapDisposition(dto.disposition),
  };
}

async function readSafeApiError(response: Response, fallback: string): Promise<string> {
  try {
    const body = (await response.json()) as { message?: unknown };
    if (typeof body.message === "string" && body.message.trim()) return body.message;
  } catch {
    // The fallback remains safe for non-JSON or empty error responses.
  }
  return fallback;
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

  const triage = useCallback(async (signal: FraudSignalView, auditReason: string): Promise<{ ok: boolean; message?: string }> => {
    const response = await fetch(`${apiBaseUrl}/api/v1/fraud/signals/triage`, {
      method: "POST",
      headers: { ...scopedHeaders(role, ""), "Content-Type": "application/json", "X-Audit-Reason": auditReason },
      body: JSON.stringify({ ruleId: signal.ruleId, relatedCaseIds: signal.relatedCaseIds }),
    });
    if (!response.ok) return { ok: false, message: await readSafeApiError(response, `Triage failed with HTTP ${response.status}.`) };
    await load();
    return { ok: true };
  }, [role, load]);

  const assign = useCallback(async (signalId: string, assignedToUserId: string, auditReason: string): Promise<{ ok: boolean; message?: string }> => {
    const response = await fetch(`${apiBaseUrl}/api/v1/fraud/signals/${encodeURIComponent(signalId)}/assign`, {
      method: "POST",
      headers: { ...scopedHeaders(role, ""), "Content-Type": "application/json", "X-Audit-Reason": auditReason },
      body: JSON.stringify({ assignedToUserId }),
    });
    if (!response.ok) return { ok: false, message: await readSafeApiError(response, `Assign failed with HTTP ${response.status}.`) };
    await load();
    return { ok: true };
  }, [role, load]);

  const recordOutcome = useCallback(async (
    signalId: string, outcome: string, notes: string, auditReason: string
  ): Promise<{ ok: boolean; message?: string }> => {
    const response = await fetch(`${apiBaseUrl}/api/v1/fraud/signals/${encodeURIComponent(signalId)}/outcome`, {
      method: "POST",
      headers: { ...scopedHeaders(role, ""), "Content-Type": "application/json", "X-Audit-Reason": auditReason },
      body: JSON.stringify({ outcome, notes: notes || null }),
    });
    if (!response.ok) return { ok: false, message: await readSafeApiError(response, `Record outcome failed with HTTP ${response.status}.`) };
    await load();
    return { ok: true };
  }, [role, load]);

  const close = useCallback(async (signalId: string, auditReason: string): Promise<{ ok: boolean; message?: string }> => {
    const response = await fetch(`${apiBaseUrl}/api/v1/fraud/signals/${encodeURIComponent(signalId)}/close`, {
      method: "POST",
      headers: { ...scopedHeaders(role, ""), "Content-Type": "application/json", "X-Audit-Reason": auditReason },
      body: JSON.stringify({}),
    });
    if (!response.ok) return { ok: false, message: await readSafeApiError(response, `Close failed with HTTP ${response.status}.`) };
    await load();
    return { ok: true };
  }, [role, load]);

  const counts = {
    advisory: signals.filter((s) => s.severity === "Advisory").length,
    anomaly: signals.filter((s) => s.severity === "Anomaly").length,
    investigation: signals.filter((s) => s.severity === "Investigation").length,
    total: signals.length,
  };

  return { signals, loading, error, counts, parityMode: LOVABLE_VISUAL_PARITY_MODE, triage, assign, recordOutcome, close };
}
