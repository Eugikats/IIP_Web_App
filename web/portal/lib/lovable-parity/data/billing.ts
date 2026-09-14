"use client";

import { useCallback, useEffect, useState } from "react";
import { useUI } from "@/context/UIStateContext";
import { apiBaseUrl, scopedHeaders } from "@/lib/config";
import { LOVABLE_VISUAL_PARITY_MODE } from "@/lib/lovable-parity/mode";

export type UsageMeterView = {
  key: string;
  label: string;
  used: number;
  limit: number;
  unit: string;
  metered: boolean;
  overLimit: boolean;
  percentUsed: number;
};

export type BillingSummaryView = {
  packageName: string;
  modules: string[];
  seatLimit: number;
  meters: UsageMeterView[];
};

const PARITY: BillingSummaryView = {
  packageName: "IIP Operations, Standard",
  seatLimit: 50,
  modules: ["Command Center", "Channels", "Customer 360", "Claims Intelligence", "Document Intelligence", "Policy Intelligence", "Fraud Intelligence", "Underwriting Intelligence", "Workflow & Approvals", "Knowledge Hub", "Executive Intelligence", "Integrations", "Compliance & Audit", "AI & Agent Management", "Support & Health"],
  meters: [
    { key: "claims", label: "Claims processed", used: 1284, limit: 5000, unit: "claims", metered: true, overLimit: false, percentUsed: 26 },
    { key: "documents", label: "Documents processed", used: 4210, limit: 20000, unit: "docs", metered: true, overLimit: false, percentUsed: 21 },
    { key: "ai-calls", label: "AI generation calls", used: 9820, limit: 50000, unit: "calls", metered: true, overLimit: false, percentUsed: 20 },
    { key: "storage", label: "Document storage", used: 38, limit: 100, unit: "GB", metered: true, overLimit: false, percentUsed: 38 },
  ],
};

export function useBilling() {
  const { roleDef } = useUI();
  const role = roleDef.key;
  const [summary, setSummary] = useState<BillingSummaryView | null>(LOVABLE_VISUAL_PARITY_MODE ? PARITY : null);
  const [loading, setLoading] = useState(!LOVABLE_VISUAL_PARITY_MODE);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    if (LOVABLE_VISUAL_PARITY_MODE) return;
    setLoading(true);
    setError(null);
    try {
      const response = await fetch(`${apiBaseUrl}/api/v1/admin/billing`, { headers: scopedHeaders(role, "") });
      if (!response.ok) throw new Error(`Billing failed.`);
      setSummary((await response.json()) as BillingSummaryView);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Billing failed to load.");
      setSummary(null);
    } finally {
      setLoading(false);
    }
  }, [role]);

  useEffect(() => {
    void load();
  }, [load]);

  return { summary, loading, error, parityMode: LOVABLE_VISUAL_PARITY_MODE };
}
