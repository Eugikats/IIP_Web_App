"use client";

import { useCallback, useEffect, useState } from "react";
import { useUI } from "@/context/UIStateContext";
import { apiBaseUrl, scopedHeaders } from "@/lib/config";
import { LOVABLE_VISUAL_PARITY_MODE } from "@/lib/lovable-parity/mode";

export type ReviewMode = "Production" | "Demo" | "LocalDevelopment";

export type EnvironmentView = {
  name: string;
  reviewMode: ReviewMode;
  syntheticData: boolean;
  dataClassification: string;
};

type MeDto = {
  environment?: {
    name?: string;
    reviewMode?: string;
    syntheticData?: boolean;
    dataClassification?: string;
  };
};

// In the prototype tour (parity mode) we always show a synthetic-demo banner so the
// two-mode experience is visible without a backend. Live mode reads the authoritative
// classification the API derives from its own runtime profile and seed state.
const PARITY: EnvironmentView = {
  name: "Demo / Review sandbox",
  reviewMode: "Demo",
  syntheticData: true,
  dataClassification: "Synthetic — not real customer data",
};

function normalizeReviewMode(value: string | undefined): ReviewMode {
  if (value === "Production" || value === "Demo") return value;
  return "LocalDevelopment";
}

export function useEnvironment() {
  const { roleDef } = useUI();
  const role = roleDef.key;
  const [environment, setEnvironment] = useState<EnvironmentView | null>(
    LOVABLE_VISUAL_PARITY_MODE ? PARITY : null,
  );

  const load = useCallback(async () => {
    if (LOVABLE_VISUAL_PARITY_MODE) return;
    try {
      const response = await fetch(`${apiBaseUrl}/api/v1/me`, { headers: scopedHeaders(role, "") });
      if (!response.ok) throw new Error(`me failed with HTTP ${response.status}.`);
      const body = (await response.json()) as MeDto;
      const env = body.environment;
      if (!env) {
        setEnvironment(null);
        return;
      }
      setEnvironment({
        name: env.name ?? "Environment",
        reviewMode: normalizeReviewMode(env.reviewMode),
        syntheticData: Boolean(env.syntheticData),
        dataClassification: env.dataClassification ?? "",
      });
    } catch {
      // Fail closed to a cautious "unknown" banner rather than implying production.
      setEnvironment(null);
    }
  }, [role]);

  useEffect(() => {
    void load();
  }, [load]);

  return { environment, parityMode: LOVABLE_VISUAL_PARITY_MODE };
}
