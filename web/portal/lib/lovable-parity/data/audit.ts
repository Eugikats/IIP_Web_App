"use client";

import { useCallback, useEffect, useState } from "react";
import { useUI } from "@/context/UIStateContext";
import { apiBaseUrl, scopedHeaders } from "@/lib/config";
import { LOVABLE_VISUAL_PARITY_MODE } from "@/lib/lovable-parity/mode";

export type AuditRowView = {
  id: string;
  time: string;
  actor: string;
  action: string;
  target: string;
  targetType: string;
  correlation: string;
};

export type CompliancePostureView = {
  retention: {
    auditRetentionDays: number;
    meetsMinimum: boolean;
    lastUpdatedAt: string;
  };
  consent: {
    onboardingRecords: number;
    consentedRecords: number;
    pendingConsent: boolean;
    blockedConsent: boolean;
    lastConsentedAt?: string | null;
  };
  sla: {
    totalOpenClaims: number;
    onTrack: number;
    atRisk: number;
    breached: number;
  };
  dataResidency: {
    tenantCountry: string;
    tenantRegion: string;
    residencyCountry: string;
    azureGeography: string;
    azureRegion: string;
    mode: string;
    azureOpenAiDeploymentType: string;
    allowedAzureRegions: string[];
    approvalReference: string;
    isConfigured: boolean;
    isDeploymentRegionApproved: boolean;
    isAzureOpenAiDeploymentTypeApproved: boolean;
    status: "Compliant" | "Mismatch" | "Not configured";
  };
  providers: {
    authMode: string;
    storageProvider: string;
    aiProvider: string;
    documentProvider: string;
    retrievalProvider: string;
    seedDemoData: boolean;
  };
};

type AuditEventDto = {
  id: string;
  eventType: string;
  actorUserId?: string | null;
  targetType?: string | null;
  targetId?: string | null;
  summary: string;
  occurredAt: string;
  correlationId?: string | null;
};

const PARITY_FIXTURES: AuditRowView[] = [
  { id: "1", time: "10:12", actor: "AI Copilot", action: "Field extraction", target: "CLM-1048 / Police report", targetType: "document", correlation: "corr_a12b" },
  { id: "2", time: "10:05", actor: "T. Okello", action: "Human override", target: "CLM-1064 / Risk state → High", targetType: "claim", correlation: "corr_a12a" },
  { id: "3", time: "09:58", actor: "AI Copilot", action: "Draft customer update", target: "CLM-1082", targetType: "communication", correlation: "corr_a129" },
  { id: "4", time: "09:41", actor: "J. Mugisha", action: "Approved next workflow step", target: "CLM-1090", targetType: "approval", correlation: "corr_a128" },
  { id: "5", time: "08:47", actor: "System", action: "Integration event", target: "Microsoft Graph · degraded", targetType: "integration", correlation: "corr_a125" },
];

const PARITY_POSTURE: CompliancePostureView = {
  retention: {
    auditRetentionDays: 2555,
    meetsMinimum: true,
    lastUpdatedAt: "2026-07-08T00:00:00.0000000Z",
  },
  consent: {
    onboardingRecords: 1,
    consentedRecords: 1,
    pendingConsent: false,
    blockedConsent: false,
    lastConsentedAt: "2026-07-08T00:00:00.0000000Z",
  },
  sla: {
    totalOpenClaims: 8,
    onTrack: 4,
    atRisk: 2,
    breached: 2,
  },
  dataResidency: {
    tenantCountry: "Uganda",
    tenantRegion: "East Africa",
    residencyCountry: "",
    azureGeography: "",
    azureRegion: "",
    mode: "regional",
    azureOpenAiDeploymentType: "Not configured",
    allowedAzureRegions: [],
    approvalReference: "",
    isConfigured: false,
    isDeploymentRegionApproved: false,
    isAzureOpenAiDeploymentTypeApproved: false,
    status: "Not configured",
  },
  providers: {
    authMode: "Entra",
    storageProvider: "SqlServer",
    aiProvider: "NotConfigured",
    documentProvider: "NotConfigured",
    retrievalProvider: "AzureAiSearch",
    seedDemoData: false,
  },
};

function formatTime(iso: string): string {
  const t = Date.parse(iso);
  if (Number.isNaN(t)) return iso || "—";
  return new Date(t).toLocaleString(undefined, { month: "short", day: "numeric", hour: "2-digit", minute: "2-digit" });
}

function mapDto(dto: AuditEventDto): AuditRowView {
  return {
    id: dto.id,
    time: formatTime(dto.occurredAt),
    actor: dto.actorUserId || "System",
    action: dto.eventType,
    target: [dto.targetId, dto.summary].filter(Boolean).join(" · ") || "—",
    targetType: dto.targetType || "other",
    correlation: dto.correlationId || "—",
  };
}

export function toCsv(rows: AuditRowView[]): string {
  const header = ["Time", "Actor", "Action", "Target", "Type", "Correlation"];
  const escape = (v: string) => `"${(v ?? "").replace(/"/g, '""')}"`;
  const lines = rows.map((r) => [r.time, r.actor, r.action, r.target, r.targetType, r.correlation].map(escape).join(","));
  return [header.join(","), ...lines].join("\r\n");
}

export function useAuditEvents() {
  const { roleDef } = useUI();
  const role = roleDef.key;
  const [rows, setRows] = useState<AuditRowView[]>(LOVABLE_VISUAL_PARITY_MODE ? PARITY_FIXTURES : []);
  const [loading, setLoading] = useState(!LOVABLE_VISUAL_PARITY_MODE);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    if (LOVABLE_VISUAL_PARITY_MODE) return;
    setLoading(true);
    setError(null);
    try {
      const response = await fetch(`${apiBaseUrl}/api/v1/audit/events`, { headers: scopedHeaders(role, "") });
      if (!response.ok) throw new Error(`Audit events failed with HTTP ${response.status}.`);
      const payload = (await response.json()) as AuditEventDto[] | { items?: AuditEventDto[] };
      const list = Array.isArray(payload) ? payload : payload.items ?? [];
      setRows(list.map(mapDto));
    } catch (err) {
      setError(err instanceof Error ? err.message : "Audit events failed to load.");
      setRows([]);
    } finally {
      setLoading(false);
    }
  }, [role]);

  useEffect(() => {
    void load();
  }, [load]);

  return { rows, loading, error, parityMode: LOVABLE_VISUAL_PARITY_MODE };
}

export function useCompliancePosture() {
  const { roleDef } = useUI();
  const role = roleDef.key;
  const [posture, setPosture] = useState<CompliancePostureView | null>(
    LOVABLE_VISUAL_PARITY_MODE ? PARITY_POSTURE : null
  );
  const [loading, setLoading] = useState(!LOVABLE_VISUAL_PARITY_MODE);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    if (LOVABLE_VISUAL_PARITY_MODE) return;
    setLoading(true);
    setError(null);
    try {
      const response = await fetch(`${apiBaseUrl}/api/v1/compliance/posture`, { headers: scopedHeaders(role, "") });
      if (!response.ok) throw new Error(`Compliance posture failed with HTTP ${response.status}.`);
      setPosture((await response.json()) as CompliancePostureView);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Compliance posture failed to load.");
      setPosture(null);
    } finally {
      setLoading(false);
    }
  }, [role]);

  useEffect(() => {
    void load();
  }, [load]);

  return { posture, loading, error, parityMode: LOVABLE_VISUAL_PARITY_MODE };
}
