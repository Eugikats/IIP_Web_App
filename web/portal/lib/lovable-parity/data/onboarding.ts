"use client";

import { useCallback, useEffect, useState } from "react";
import { useUI } from "@/context/UIStateContext";
import { apiBaseUrl, scopedHeaders } from "@/lib/config";
import { LOVABLE_VISUAL_PARITY_MODE } from "@/lib/lovable-parity/mode";

export type OnboardingView = {
  onboardingId: string;
  customerName: string;
  expectedEntraTenantOrDomain: string;
  adminContactEmail: string;
  targetWorkspaces: string[];
  azureSubscriptionIds: string[];
  internalTenantId: string;
  status: string;
  externalEntraTenantId: string | null;
  createdAt: string;
  consentedAt: string | null;
};

export type CreateOnboardingInput = {
  customerName: string;
  expectedEntraTenantOrDomain: string;
  adminContactEmail: string;
  targetWorkspaces: string[];
  azureSubscriptionIds: string[];
};

export type ConsentUrlView = { configured: boolean; url: string | null; message: string };

const PARITY: OnboardingView[] = [
  {
    onboardingId: "ONB-DEMO-01", customerName: "Uganda Insurer Ltd.", expectedEntraTenantOrDomain: "ugandainsurer.onmicrosoft.com",
    adminContactEmail: "it-admin@ugandainsurer.test", targetWorkspaces: ["workspace-claims"], azureSubscriptionIds: ["sub-0001"],
    internalTenantId: "tenant-demo", status: "Consented", externalEntraTenantId: "00000000-0000-0000-0000-000000000000",
    createdAt: new Date().toISOString(), consentedAt: new Date().toISOString(),
  },
];

export function useOnboarding() {
  const { roleDef } = useUI();
  const role = roleDef.key;
  const [records, setRecords] = useState<OnboardingView[]>(LOVABLE_VISUAL_PARITY_MODE ? PARITY : []);
  const [loading, setLoading] = useState(!LOVABLE_VISUAL_PARITY_MODE);
  const [error, setError] = useState<string | null>(null);
  const [creating, setCreating] = useState(false);

  const load = useCallback(async () => {
    if (LOVABLE_VISUAL_PARITY_MODE) return;
    setLoading(true);
    setError(null);
    try {
      const response = await fetch(`${apiBaseUrl}/api/v1/onboarding`, { headers: scopedHeaders(role, "") });
      if (response.status === 403) throw new Error("You do not have permission to manage tenant onboarding.");
      if (!response.ok) throw new Error(`Onboarding failed.`);
      setRecords(((await response.json()) as OnboardingView[]).map((r) => ({ ...r })));
    } catch (err) {
      setError(err instanceof Error ? err.message : "Onboarding failed to load.");
      setRecords([]);
    } finally {
      setLoading(false);
    }
  }, [role]);

  useEffect(() => { void load(); }, [load]);

  const create = useCallback(async (input: CreateOnboardingInput): Promise<{ ok: boolean; message?: string }> => {
    if (LOVABLE_VISUAL_PARITY_MODE) {
      setRecords((prev) => [{
        onboardingId: `ONB-${Date.now()}`, ...input, internalTenantId: "tenant-new", status: "Pending",
        externalEntraTenantId: null, createdAt: new Date().toISOString(), consentedAt: null,
      }, ...prev]);
      return { ok: true };
    }
    setCreating(true);
    try {
      const response = await fetch(`${apiBaseUrl}/api/v1/onboarding`, {
        method: "POST",
        headers: { ...scopedHeaders(role, ""), "Content-Type": "application/json" },
        body: JSON.stringify(input),
      });
      if (response.status === 403) return { ok: false, message: "You do not have permission to onboard tenants." };
      if (response.status === 400) return { ok: false, message: "Please complete the required fields." };
      if (!response.ok) return { ok: false, message: `Create failed.` };
      await load();
      return { ok: true };
    } catch (err) {
      return { ok: false, message: err instanceof Error ? err.message : "Create failed." };
    } finally {
      setCreating(false);
    }
  }, [role, load]);

  const getConsentUrl = useCallback(async (onboardingId: string): Promise<ConsentUrlView> => {
    if (LOVABLE_VISUAL_PARITY_MODE) {
      return { configured: false, url: null, message: "Prototype, configure Microsoft Entra to enable admin consent." };
    }
    try {
      const response = await fetch(`${apiBaseUrl}/api/v1/onboarding/${onboardingId}/consent-url`, { headers: scopedHeaders(role, "") });
      if (!response.ok) return { configured: false, url: null, message: `Consent URL failed.` };
      return (await response.json()) as ConsentUrlView;
    } catch (err) {
      return { configured: false, url: null, message: err instanceof Error ? err.message : "Consent URL failed." };
    }
  }, [role]);

  return { records, loading, error, creating, create, getConsentUrl, parityMode: LOVABLE_VISUAL_PARITY_MODE };
}
