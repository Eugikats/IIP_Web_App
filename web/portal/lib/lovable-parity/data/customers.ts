"use client";

import { useCallback, useEffect, useState } from "react";
import { useUI } from "@/context/UIStateContext";
import { apiBaseUrl, scopedHeaders } from "@/lib/config";
import { LOVABLE_VISUAL_PARITY_MODE } from "@/lib/lovable-parity/mode";
import { customers as fixtureCustomers } from "@/lib/lovable-parity/mock";

export type CustomerListView = {
  id: string;
  name: string;
  contact: string;
  activeClaims: number;
};

type CustomerListItemDto = {
  customerId: string;
  fullName: string;
  email?: string | null;
  phone?: string | null;
  activeClaims: number;
};

export type CustomerProfileView = {
  customerId: string;
  identity: Record<string, string>;
  policies: Record<string, string>[];
  claims: Record<string, string>[];
  interactions: Record<string, string>[];
  documents: Record<string, string>[];
  consent: Record<string, string>;
  auditHistory: { actionName: string; actorType: string; targetType: string; targetId: string; timestamp: string }[];
};

const PARITY_LIST: CustomerListView[] = fixtureCustomers.map((c) => ({
  id: c.id,
  name: c.name,
  contact: c.contact,
  activeClaims: c.activeClaims,
}));

export function useCustomersList() {
  const { roleDef } = useUI();
  const role = roleDef.key;
  const [customers, setCustomers] = useState<CustomerListView[]>(LOVABLE_VISUAL_PARITY_MODE ? PARITY_LIST : []);
  const [loading, setLoading] = useState(!LOVABLE_VISUAL_PARITY_MODE);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    if (LOVABLE_VISUAL_PARITY_MODE) return;
    setLoading(true);
    setError(null);
    try {
      const response = await fetch(`${apiBaseUrl}/api/v1/customers`, { headers: scopedHeaders(role, "") });
      if (!response.ok) throw new Error(`Customers failed.`);
      const rows = (await response.json()) as CustomerListItemDto[];
      setCustomers(rows.map((r) => ({
        id: r.customerId,
        name: r.fullName,
        contact: r.email || r.phone || "—",
        activeClaims: r.activeClaims,
      })));
    } catch (err) {
      setError(err instanceof Error ? err.message : "Customers failed to load.");
      setCustomers([]);
    } finally {
      setLoading(false);
    }
  }, [role]);

  useEffect(() => {
    void load();
  }, [load]);

  return { customers, loading, error, parityMode: LOVABLE_VISUAL_PARITY_MODE };
}

export function useCustomerProfile(customerId: string) {
  const { roleDef } = useUI();
  const role = roleDef.key;
  const [profile, setProfile] = useState<CustomerProfileView | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    if (LOVABLE_VISUAL_PARITY_MODE) {
      const c = fixtureCustomers.find((x) => x.id === customerId) ?? fixtureCustomers[0];
      setProfile(c ? {
        customerId: c.id,
        identity: { fullName: c.name, contact: c.contact, region: c.region },
        policies: c.policies.map((p) => ({ policyNumber: p })),
        claims: [],
        interactions: [],
        documents: [],
        consent: { status: c.consent ? "On file" : "Missing" },
        auditHistory: [],
      } : null);
      setLoading(false);
      return;
    }
    try {
      const response = await fetch(`${apiBaseUrl}/api/v1/customers/${encodeURIComponent(customerId)}`, { headers: scopedHeaders(role, "") });
      if (response.status === 404) { setProfile(null); setLoading(false); return; }
      if (!response.ok) throw new Error(`Customer profile failed.`);
      setProfile((await response.json()) as CustomerProfileView);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Customer profile failed to load.");
      setProfile(null);
    } finally {
      setLoading(false);
    }
  }, [role, customerId]);

  useEffect(() => {
    void load();
  }, [load]);

  return { profile, loading, error };
}
