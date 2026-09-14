"use client";

import { useCallback, useEffect, useState } from "react";
import { useUI } from "@/context/UIStateContext";
import { apiBaseUrl, scopedHeaders } from "@/lib/config";
import { LOVABLE_VISUAL_PARITY_MODE } from "@/lib/lovable-parity/mode";
import { agents as fixtureAgents } from "@/lib/lovable-parity/mock";

// WO-11: matches InsuranceIntelligence.Domain.Agents.AgentImplementationKind exactly (see
// AgentRegistry.cs). "NotImplemented" means no code path in the backend invokes anything under
// this agent key, the admin surface must say so plainly, not imply it is a working AI agent.
export type AgentImplementationKind = "ModelBacked" | "AiServiceBacked" | "RuleBased" | "NotImplemented";

export type AgentEntryView = {
  key: string;
  name: string;
  purpose: string;
  riskLevel: string;
  humanApprovalRequired: boolean;
  implementationKind: AgentImplementationKind;
  tools: string[];
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
  implementationKind: AgentImplementationKind;
  tools: string[];
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
  // The visual-parity fixture has no backend implementation-kind data of its own; label every
  // preview row honestly as unverified rather than guessing "ModelBacked" for all of them.
  implementationKind: "NotImplemented",
  tools: [],
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
    implementationKind: d.implementationKind,
    tools: d.tools ?? [],
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
      if (!response.ok) throw new Error(`Agents failed.`);
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
