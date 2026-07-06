"use client";

import { useCallback, useEffect, useState } from "react";
import { useUI } from "@/context/UIStateContext";
import { apiBaseUrl, scopedHeaders } from "@/lib/config";
import { LOVABLE_VISUAL_PARITY_MODE } from "@/lib/lovable-parity/mode";
import { agents as fixtureAgents } from "@/lib/lovable-parity/mock";

export type AgentEntryView = {
  key: string;
  name: string;
  purpose: string;
  riskLevel: string;
  humanApprovalRequired: boolean;
  enabled: boolean;
  updatedBy?: string | null;
  reason?: string | null;
};

type AgentEntryDto = {
  key: string;
  name: string;
  purpose: string;
  riskLevel: string;
  humanApprovalRequired: boolean;
  enabled: boolean;
  updatedByUserId?: string | null;
  reason?: string | null;
};

const PARITY: AgentEntryView[] = fixtureAgents.map((a) => ({
  key: a.id,
  name: a.name,
  purpose: a.purpose,
  riskLevel: a.risk,
  humanApprovalRequired: a.name.toLowerCase().includes("update") || a.risk === "High",
  enabled: a.status === "Active",
  updatedBy: null,
  reason: null,
}));

function mapDto(d: AgentEntryDto): AgentEntryView {
  return {
    key: d.key,
    name: d.name,
    purpose: d.purpose,
    riskLevel: d.riskLevel,
    humanApprovalRequired: d.humanApprovalRequired,
    enabled: d.enabled,
    updatedBy: d.updatedByUserId,
    reason: d.reason,
  };
}

export function useAgents() {
  const { roleDef } = useUI();
  const role = roleDef.key;
  const [agents, setAgents] = useState<AgentEntryView[]>(LOVABLE_VISUAL_PARITY_MODE ? PARITY : []);
  const [loading, setLoading] = useState(!LOVABLE_VISUAL_PARITY_MODE);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    if (LOVABLE_VISUAL_PARITY_MODE) return;
    setLoading(true);
    setError(null);
    try {
      const response = await fetch(`${apiBaseUrl}/api/v1/admin/agents`, { headers: scopedHeaders(role, "") });
      if (!response.ok) throw new Error(`Agents failed with HTTP ${response.status}.`);
      const rows = (await response.json()) as AgentEntryDto[];
      setAgents(rows.map(mapDto));
    } catch (err) {
      setError(err instanceof Error ? err.message : "Agents failed to load.");
      setAgents([]);
    } finally {
      setLoading(false);
    }
  }, [role]);

  useEffect(() => {
    void load();
  }, [load]);

  const setKillSwitch = useCallback(async (key: string, enabled: boolean, reason: string) => {
    if (LOVABLE_VISUAL_PARITY_MODE) {
      setAgents((prev) => prev.map((a) => (a.key === key ? { ...a, enabled } : a)));
      return true;
    }
    const response = await fetch(`${apiBaseUrl}/api/v1/admin/agents/${encodeURIComponent(key)}/kill-switch`, {
      method: "POST",
      headers: { ...scopedHeaders(role, ""), "Content-Type": "application/json" },
      body: JSON.stringify({ enabled, reason }),
    });
    if (!response.ok) return false;
    const updated = mapDto((await response.json()) as AgentEntryDto);
    setAgents((prev) => prev.map((a) => (a.key === key ? updated : a)));
    return true;
  }, [role]);

  return { agents, loading, error, setKillSwitch, parityMode: LOVABLE_VISUAL_PARITY_MODE };
}
