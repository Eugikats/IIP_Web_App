"use client";

import { useCallback, useEffect, useState } from "react";
import { useUI } from "@/context/UIStateContext";
import { apiBaseUrl, scopedHeaders } from "@/lib/config";
import { LOVABLE_VISUAL_PARITY_MODE } from "@/lib/lovable-parity/mode";

// The Escalations, Agents and Channel Intelligence views of the acquisition dashboard. Each is counted
// by the API from records that exist; the screen only draws them.

export type EscalationRow = {
  escalationId: string;
  customer: string;
  contact: string;
  type: string;
  status: "Open" | "Pending customer" | "With a person" | "Closed";
  priority: "High" | "Medium" | "Low" | "Not scored";
  assigneeName: string | null;
  raisedAt: string;
  updatedAt: string;
  conversationId: string | null;
  leadId: string | null;
  bankCustomer: boolean | null;
};
export type EscalationReport = { open: number; highPriority: number; pendingCustomer: number; newToBank: number; unassigned: number; rows: EscalationRow[] };

export type AgentOperationRow = {
  key: string;
  name: string;
  purpose: string;
  kind: string;
  enabled: boolean;
  health: string;
  runs: number;
  errors: number;
  lastRunAt: string | null;
  lastOkAt: string | null;
  errorRatePercent: number;
  canRunNow: boolean;
};
export type AgentOperations = { total: number; live: number; errors: number; noRuns: number; rows: AgentOperationRow[] };

export type ChannelRow = { channel: string; interactions: number; newLeads: number; qualified: number; conversionPercent: number | null };
export type ChannelIntelligence = {
  totalInteractions: number;
  newLeads: number;
  qualified: number;
  conversionPercent: number | null;
  channels: ChannelRow[];
  agents: AgentOperationRow[];
};

type Loaded<T> = { data: T | null; loading: boolean; error: string | null; forbidden: boolean; reload: () => Promise<void> };

function useReport<T>(path: string, failure: string): Loaded<T> {
  const { roleDef } = useUI();
  const role = roleDef.key;
  const [data, setData] = useState<T | null>(null);
  const [loading, setLoading] = useState(!LOVABLE_VISUAL_PARITY_MODE);
  const [error, setError] = useState<string | null>(null);
  const [forbidden, setForbidden] = useState(false);

  const load = useCallback(async () => {
    if (LOVABLE_VISUAL_PARITY_MODE) return;
    setLoading(true);
    setError(null);
    setForbidden(false);
    try {
      const response = await fetch(`${apiBaseUrl}${path}`, { headers: scopedHeaders(role, ""), cache: "no-store" });
      if (response.status === 403) {
        setForbidden(true);
        setData(null);
        return;
      }
      if (!response.ok) throw new Error(failure);
      setData((await response.json()) as T);
    } catch (err) {
      setError(err instanceof Error ? err.message : failure);
      setData(null);
    } finally {
      setLoading(false);
    }
  }, [role, path, failure]);

  useEffect(() => { void load(); }, [load]);
  return { data, loading, error, forbidden, reload: load };
}

export const useEscalations = () => useReport<EscalationReport>("/api/v1/acquisition/escalations", "The escalations could not be loaded.");
export const useAgentOperations = () => useReport<AgentOperations>("/api/v1/acquisition/agents", "The agents could not be loaded.");
export const useChannelIntelligence = () => useReport<ChannelIntelligence>("/api/v1/acquisition/channel-intelligence", "Channel intelligence could not be loaded.");
