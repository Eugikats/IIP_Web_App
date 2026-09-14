"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { useUI } from "@/context/UIStateContext";
import { apiBaseUrl, scopedHeaders } from "@/lib/config";
import { LOVABLE_VISUAL_PARITY_MODE } from "@/lib/lovable-parity/mode";

// Client for POST /api/v1/customers/{id}/summary, the Customer Summary Agent. The briefing is
// prepared when the page opens rather than on a button press: the person opened this customer to
// understand them, and asking them to request the explanation first is one step too many.

export type CustomerSummarySource = { tag: string; label: string; kind: string; href: string | null };

export type CustomerSummaryView = {
  customerId: string;
  summary: string;
  sources: CustomerSummarySource[];
  generatedAt: string;
  fromCache: boolean;
};

export type CustomerSummaryState =
  | { status: "idle" | "loading" }
  | { status: "ready"; summary: CustomerSummaryView }
  | { status: "disabled"; message: string }
  | { status: "unavailable"; message: string }
  | { status: "not_configured" };

export function useCustomerSummary(customerId: string) {
  const { roleDef } = useUI();
  const role = roleDef.key;
  const [state, setState] = useState<CustomerSummaryState>({ status: LOVABLE_VISUAL_PARITY_MODE ? "not_configured" : "idle" });
  const requested = useRef<string | null>(null);

  const load = useCallback(
    async (refresh = false) => {
      if (LOVABLE_VISUAL_PARITY_MODE || !customerId) return;
      setState({ status: "loading" });
      try {
        const response = await fetch(
          `${apiBaseUrl}/api/v1/customers/${encodeURIComponent(customerId)}/summary${refresh ? "?refresh=true" : ""}`,
          { method: "POST", headers: { ...scopedHeaders(role, ""), "Content-Type": "application/json" }, body: "{}" },
        );
        if (response.status === 503) {
          const payload = (await response.json().catch(() => ({}))) as { status?: string; message?: string };
          setState(
            payload.status === "AgentDisabled"
              ? { status: "disabled", message: payload.message || "The customer summary agent is switched off for this workspace." }
              : { status: "unavailable", message: payload.message || "The customer summary could not be prepared just now. Try again in a moment." },
          );
          return;
        }
        if (!response.ok) {
          setState({ status: "unavailable", message: "The customer summary could not be prepared just now. Try again in a moment." });
          return;
        }
        setState({ status: "ready", summary: (await response.json()) as CustomerSummaryView });
      } catch {
        setState({ status: "unavailable", message: "The customer summary service could not be reached. Check your connection and try again." });
      }
    },
    [customerId, role],
  );

  useEffect(() => {
    if (requested.current === customerId) return;
    requested.current = customerId;
    void load(false);
  }, [customerId, load]);

  return { state, refresh: () => load(true) };
}
