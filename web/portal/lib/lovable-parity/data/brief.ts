"use client";

import { useCallback, useEffect, useState } from "react";
import { useUI } from "@/context/UIStateContext";
import { apiBaseUrl, scopedHeaders } from "@/lib/config";

export type BriefItem = {
  caseId: string;
  customerName: string;
  policyNumber: string;
  claimType: string;
  status: string;
  reason: string;
  primaryActionLabel: string;
  primaryActionHref: string;
};

export type HandledForYouItem = {
  caseId: string | null;
  summary: string;
  handledAt: string;
};

type BriefDto = {
  items: BriefItem[];
  handledForYou: HandledForYouItem[];
};

export function useBrief() {
  const { roleDef } = useUI();
  const role = roleDef.key;
  const [items, setItems] = useState<BriefItem[]>([]);
  const [handledForYou, setHandledForYou] = useState<HandledForYouItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [noPermission, setNoPermission] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    setNoPermission(false);
    try {
      const response = await fetch(`${apiBaseUrl}/api/v1/brief`, { headers: scopedHeaders(role, "") });
      if (response.status === 403) {
        setNoPermission(true);
        setItems([]);
        setHandledForYou([]);
        return;
      }
      if (!response.ok) throw new Error("Your brief could not be loaded.");
      const dto = (await response.json()) as BriefDto;
      setItems(dto.items);
      setHandledForYou(dto.handledForYou);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Your brief could not be loaded.");
      setItems([]);
      setHandledForYou([]);
    } finally {
      setLoading(false);
    }
  }, [role]);

  useEffect(() => { void load(); }, [load]);

  return { items, handledForYou, loading, error, noPermission, reload: load };
}
