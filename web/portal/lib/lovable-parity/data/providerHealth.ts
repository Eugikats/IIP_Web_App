"use client";

import { useCallback, useEffect, useState } from "react";
import { useUI } from "@/context/UIStateContext";
import { apiBaseUrl, scopedHeaders } from "@/lib/config";
import { LOVABLE_VISUAL_PARITY_MODE } from "@/lib/lovable-parity/mode";
import { formatProviderStatusLabel } from "@/lib/providerHealth";
import { syntheticWorkspaceSnapshot } from "@/lib/lovable-parity/syntheticWorkspaceSnapshot";

export type ProviderHealthView = {
  name: string;
  category: string;
  status: string;
  latency: string;
  lastCheck: string;
  detail: string;
};

type ProviderHealthItemDto = { name: string; mode: string; status: string; detail: string };
type ProviderHealthResponseDto = { overallStatus: string; providers: ProviderHealthItemDto[] };
type ProviderHealthMetadata = {
  dataAsOf: string;
  snapshotId: string;
  source: "live" | "synthetic";
  limitations: string;
};

const PARITY_FIXTURES: ProviderHealthView[] = syntheticWorkspaceSnapshot.providerHealth.providers.map((provider) => ({
  name: provider.displayName,
  category: provider.category,
  status: formatProviderStatusLabel(provider.status),
  latency: provider.latency,
  lastCheck: `Snapshot ${new Date(syntheticWorkspaceSnapshot.dataAsOf).toLocaleString()}`,
  detail: provider.detail,
}));

function mapItem(dto: ProviderHealthItemDto, checkedAt: string): ProviderHealthView {
  return {
    name: dto.name,
    category: dto.mode || "Provider",
    status: formatProviderStatusLabel(dto.status),
    latency: "-",
    lastCheck: `Checked ${new Date(checkedAt).toLocaleString()}`,
    detail: dto.detail || "No provider detail reported.",
  };
}

export function useProviderHealth() {
  const { roleDef } = useUI();
  const role = roleDef.key;
  const [providers, setProviders] = useState<ProviderHealthView[]>(LOVABLE_VISUAL_PARITY_MODE ? PARITY_FIXTURES : []);
  const [loading, setLoading] = useState(!LOVABLE_VISUAL_PARITY_MODE);
  const [error, setError] = useState<string | null>(null);
  const [metadata, setMetadata] = useState<ProviderHealthMetadata | null>(LOVABLE_VISUAL_PARITY_MODE ? {
    dataAsOf: syntheticWorkspaceSnapshot.dataAsOf,
    snapshotId: syntheticWorkspaceSnapshot.snapshotId,
    source: "synthetic",
    limitations: syntheticWorkspaceSnapshot.limitations,
  } : null);

  const load = useCallback(async () => {
    if (LOVABLE_VISUAL_PARITY_MODE) return;
    setLoading(true);
    setError(null);
    try {
      const response = await fetch(`${apiBaseUrl}/api/v1/provider-status`, { headers: scopedHeaders(role, "") });
      if (!response.ok) throw new Error(`Provider health failed.`);
      const payload = (await response.json()) as ProviderHealthResponseDto;
      const checkedAt = new Date().toISOString();
      setProviders((payload.providers ?? []).map((provider) => mapItem(provider, checkedAt)));
      setMetadata({
        dataAsOf: checkedAt,
        snapshotId: `provider-health-received-${checkedAt}`,
        source: "live",
        limitations: "Runtime-reported configuration health received at this time; it is not a continuous availability guarantee.",
      });
    } catch (err) {
      setError(err instanceof Error ? err.message : "Provider health failed to load.");
      setProviders([]);
      setMetadata(null);
    } finally {
      setLoading(false);
    }
  }, [role]);

  useEffect(() => {
    void load();
  }, [load]);

  return {
    providers,
    loading,
    error,
    parityMode: LOVABLE_VISUAL_PARITY_MODE,
    dataAsOf: metadata?.dataAsOf ?? null,
    snapshotId: metadata?.snapshotId ?? null,
    source: metadata?.source ?? null,
    limitations: metadata?.limitations ?? null,
  };
}
