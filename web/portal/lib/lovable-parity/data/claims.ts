"use client";

import { useCallback, useEffect, useState } from "react";
import { useUI } from "@/context/UIStateContext";
import { apiBaseUrl, scopedHeaders } from "@/lib/config";
import { LOVABLE_VISUAL_PARITY_MODE } from "@/lib/lovable-parity/mode";
import { claims as fixtureClaims } from "@/lib/lovable-parity/mock";

export type ClaimRowView = {
  id: string;
  customer: string;
  policy: string;
  productLine: string;
  claimType: string;
  status: string;
  complete: boolean;
  sla: string;
  risk: string;
  channel: string;
  updated: string;
  confidence: number;
  region: string;
};

type ClaimListItemDto = {
  claimId: string;
  customerName: string;
  policyNumber: string;
  productLine: string;
  claimType: string;
  channel: string;
  status: string;
  completenessScore: number;
  riskLevel: string;
  priority: string;
  slaStatus: string;
  slaDueAt?: string | null;
  updatedAt: string;
};

function formatWhen(iso: string): string {
  const t = Date.parse(iso);
  if (Number.isNaN(t)) return iso || "—";
  return new Date(t).toLocaleString(undefined, { month: "short", day: "numeric", hour: "2-digit", minute: "2-digit" });
}

function mapDto(dto: ClaimListItemDto): ClaimRowView {
  return {
    id: dto.claimId,
    customer: dto.customerName,
    policy: dto.policyNumber,
    productLine: dto.productLine,
    claimType: dto.claimType,
    status: dto.status,
    complete: dto.completenessScore >= 100,
    sla: dto.slaStatus,
    risk: dto.riskLevel,
    channel: dto.channel,
    updated: formatWhen(dto.updatedAt),
    confidence: Math.max(0, Math.min(1, dto.completenessScore / 100)),
    region: "—",
  };
}

// Fixture claims use a slightly different shape; project them into the view.
const PARITY_ROWS: ClaimRowView[] = fixtureClaims.map((c) => ({
  id: c.id,
  customer: c.customer,
  policy: c.policy,
  productLine: c.productLine,
  claimType: c.claimType,
  status: c.status,
  complete: c.missingDocs === 0,
  sla: c.sla === "At Risk" ? "AtRisk" : c.sla,
  risk: c.risk,
  channel: c.channel,
  updated: c.updated,
  confidence: c.confidence,
  region: c.region,
}));

export function useClaims() {
  const { roleDef } = useUI();
  const role = roleDef.key;
  const [claims, setClaims] = useState<ClaimRowView[]>(LOVABLE_VISUAL_PARITY_MODE ? PARITY_ROWS : []);
  const [loading, setLoading] = useState(!LOVABLE_VISUAL_PARITY_MODE);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    if (LOVABLE_VISUAL_PARITY_MODE) return;
    setLoading(true);
    setError(null);
    try {
      const response = await fetch(`${apiBaseUrl}/api/v1/claims`, { headers: scopedHeaders(role, "") });
      if (!response.ok) throw new Error(`Claims failed with HTTP ${response.status}.`);
      const payload = (await response.json()) as ClaimListItemDto[] | { items?: ClaimListItemDto[] };
      const list = Array.isArray(payload) ? payload : payload.items ?? [];
      setClaims(list.map(mapDto));
    } catch (err) {
      setError(err instanceof Error ? err.message : "Claims failed to load.");
      setClaims([]);
    } finally {
      setLoading(false);
    }
  }, [role]);

  useEffect(() => {
    void load();
  }, [load]);

  return { claims, loading, error, parityMode: LOVABLE_VISUAL_PARITY_MODE };
}
