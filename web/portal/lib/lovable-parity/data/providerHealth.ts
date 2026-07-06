"use client";

import { useCallback, useEffect, useState } from "react";
import { useUI } from "@/context/UIStateContext";
import { apiBaseUrl, scopedHeaders } from "@/lib/config";
import { LOVABLE_VISUAL_PARITY_MODE } from "@/lib/lovable-parity/mode";

export type ProviderHealthView = {
  name: string;
  category: string;
  status: string;
  latency: string;
  lastCheck: string;
};

type ProviderHealthItemDto = { name: string; mode: string; status: string; detail: string };
type ProviderHealthResponseDto = { overallStatus: string; providers: ProviderHealthItemDto[] };

const PARITY_FIXTURES: ProviderHealthView[] = [
  { name: "Azure OpenAI · GPT-4o", category: "AI", status: "Operational", latency: "412ms", lastCheck: "1 min ago" },
  { name: "Azure AI Document Intelligence", category: "Document", status: "Operational", latency: "1.1s", lastCheck: "2 min ago" },
  { name: "Azure AI Search (RAG)", category: "AI", status: "Operational", latency: "220ms", lastCheck: "1 min ago" },
  { name: "WhatsApp Cloud API", category: "Channel", status: "Operational", latency: "220ms", lastCheck: "just now" },
  { name: "Microsoft Graph / Outlook", category: "Channel", status: "Degraded", latency: "2.4s", lastCheck: "1 min ago" },
  { name: "SMS Gateway", category: "Channel", status: "Operational", latency: "310ms", lastCheck: "3 min ago" },
];

function mapItem(dto: ProviderHealthItemDto): ProviderHealthView {
  return {
    name: dto.name,
    category: dto.mode || "Provider",
    status: dto.status,
    latency: "—",
    lastCheck: dto.detail || "—",
  };
}

export function useProviderHealth() {
  const { roleDef } = useUI();
  const role = roleDef.key;
  const [providers, setProviders] = useState<ProviderHealthView[]>(LOVABLE_VISUAL_PARITY_MODE ? PARITY_FIXTURES : []);
  const [loading, setLoading] = useState(!LOVABLE_VISUAL_PARITY_MODE);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    if (LOVABLE_VISUAL_PARITY_MODE) return;
    setLoading(true);
    setError(null);
    try {
      const response = await fetch(`${apiBaseUrl}/api/v1/admin/provider-health`, { headers: scopedHeaders(role, "") });
      if (!response.ok) throw new Error(`Provider health failed with HTTP ${response.status}.`);
      const payload = (await response.json()) as ProviderHealthResponseDto;
      setProviders((payload.providers ?? []).map(mapItem));
    } catch (err) {
      setError(err instanceof Error ? err.message : "Provider health failed to load.");
      setProviders([]);
    } finally {
      setLoading(false);
    }
  }, [role]);

  useEffect(() => {
    void load();
  }, [load]);

  return { providers, loading, error, parityMode: LOVABLE_VISUAL_PARITY_MODE };
}
