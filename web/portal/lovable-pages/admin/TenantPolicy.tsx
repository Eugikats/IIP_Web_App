"use client";
import { useEffect, useState } from "react";
import { GlassCard, SectionHeader, Badge } from "@/components/iip/Primitives";
import { LoadingSkeleton, ErrorStateSurface } from "@/components/ui/StateSurfaces";
import GuardrailBanner from "@/components/iip/GuardrailBanner";
import { Button } from "@/components/ui/Button";
import { Field, Input } from "@/components/ui/Input";
import { Loader2, RefreshCw, ShieldCheck, ShieldOff } from "lucide-react";
import { apiBaseUrl, scopedHeaders } from "@/lib/config";
import { useUI } from "@/context/UIStateContext";
import { useRuntimeContext } from "@/lib/runtimeContext";

type TenantPolicyRule = { rule: string; enforced: boolean; enforcementSource: string };

type DemoSeedSummary = {
  products: number;
  customers: number;
  policies: number;
  cases: number;
  documents: number;
  approvals: number;
};

type TestPolicyholderSummary = {
  fullName: string;
  phone: string;
  policies: { policyNumber: string; productLine: string }[];
  pastClaims: { policyNumber: string; productLine: string; status: string }[];
};

export default function TenantPolicy() {
  const { roleDef } = useUI();
  const { environment } = useRuntimeContext();
  const [rules, setRules] = useState<TenantPolicyRule[] | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [seeding, setSeeding] = useState(false);
  const [seedError, setSeedError] = useState<string | null>(null);
  const [seedResult, setSeedResult] = useState<DemoSeedSummary | null>(null);

  const [testName, setTestName] = useState("");
  const [testPhone, setTestPhone] = useState("");
  const [testEmail, setTestEmail] = useState("");
  const [testSeeding, setTestSeeding] = useState(false);
  const [testSeedError, setTestSeedError] = useState<string | null>(null);
  const [testSeedResult, setTestSeedResult] = useState<TestPolicyholderSummary | null>(null);

  const canSeedDemoData = environment?.syntheticData === true;

  async function onSeedTestPolicyholder() {
    setTestSeeding(true);
    setTestSeedError(null);
    setTestSeedResult(null);
    try {
      const response = await fetch(`${apiBaseUrl}/api/v1/admin/demo-seed/test-policyholder`, {
        method: "POST",
        headers: { ...scopedHeaders(roleDef.key, ""), "Content-Type": "application/json" },
        body: JSON.stringify({ fullName: testName.trim(), phone: testPhone.trim(), email: testEmail.trim() || null }),
      });
      if (!response.ok) {
        const payload = await response.json().catch(() => null) as { detail?: string; title?: string; errors?: Record<string, string[]> } | null;
        const firstFieldError = payload?.errors ? Object.values(payload.errors).flat()[0] : null;
        throw new Error(firstFieldError || payload?.detail || payload?.title || `Test policyholder could not be created (${response.status}).`);
      }
      const payload = await response.json() as {
        fullName: string; phone: string;
        policies: { policyNumber: string; productLine: string }[];
        pastClaims: { policyNumber: string; productLine: string; status: string }[];
      };
      setTestSeedResult(payload);
    } catch (reason) {
      setTestSeedError(reason instanceof Error ? reason.message : "Test policyholder could not be created.");
    } finally {
      setTestSeeding(false);
    }
  }

  async function onSeedDemoData() {
    setSeeding(true);
    setSeedError(null);
    setSeedResult(null);
    try {
      const response = await fetch(`${apiBaseUrl}/api/v1/admin/demo-seed`, {
        method: "POST",
        headers: scopedHeaders(roleDef.key, ""),
      });
      if (!response.ok) {
        const payload = await response.json().catch(() => null) as { detail?: string; title?: string } | null;
        throw new Error(payload?.detail || payload?.title || `Demo data could not be seeded (${response.status}).`);
      }
      const payload = await response.json() as {
        products: number; customers: number; policies: number; cases: number; documents: number; approvals: number;
      };
      setSeedResult({
        products: payload.products, customers: payload.customers, policies: payload.policies,
        cases: payload.cases, documents: payload.documents, approvals: payload.approvals,
      });
    } catch (reason) {
      setSeedError(reason instanceof Error ? reason.message : "Demo data could not be seeded.");
    } finally {
      setSeeding(false);
    }
  }

  useEffect(() => {
    let cancelled = false;
    async function load() {
      setLoading(true);
      setError(null);
      try {
        const response = await fetch(`${apiBaseUrl}/api/v1/admin/tenant-policy`, { headers: scopedHeaders(roleDef.key, "") });
        if (!response.ok) throw new Error(`Tenant policy could not be loaded (${response.status}).`);
        const payload = (await response.json()) as { rules: TenantPolicyRule[] };
        if (!cancelled) setRules(payload.rules);
      } catch (reason) {
        if (!cancelled) setError(reason instanceof Error ? reason.message : "Tenant policy could not be loaded.");
      } finally {
        if (!cancelled) setLoading(false);
      }
    }
    void load();
    return () => { cancelled = true; };
  }, [roleDef.key]);

  return (
    <>
      <SectionHeader
        eyebrow="Admin · Tenant Agent Policy"
        title="Tenant agent policy"
        sub="Baseline guardrails every agent in this workspace inherits. Each row names the actual code path or configuration key that enforces it, this is a live report, not a description."
      />
      <div className="mb-4"><GuardrailBanner /></div>

      {loading && <GlassCard><LoadingSkeleton rows={5} /></GlassCard>}
      {!loading && error && <GlassCard><ErrorStateSurface title="Tenant policy could not be loaded" message={error} /></GlassCard>}

      {!loading && !error && rules && (
        <GlassCard>
          <div className="space-y-2">
            {rules.map((r) => (
              <div key={r.rule} className="flex items-start gap-3 rounded-dls-card border border-dls-border-subtle bg-dls-surface p-3">
                {r.enforced
                  ? <ShieldCheck className="mt-0.5 h-4 w-4 shrink-0 text-dls-info-text" aria-hidden="true" />
                  : <ShieldOff className="mt-0.5 h-4 w-4 shrink-0 text-dls-text-muted" aria-hidden="true" />}
                <div className="flex-1">
                  <div className="text-[13px] font-semibold text-dls-text-primary">{r.rule}</div>
                  <div className="text-[11.5px] text-dls-text-secondary">{r.enforcementSource}</div>
                </div>
                <Badge tone={r.enforced ? "success" : "neutral"}>{r.enforced ? "Enforced" : "Not implemented"}</Badge>
              </div>
            ))}
          </div>
        </GlassCard>
      )}

      {canSeedDemoData && (
        <GlassCard>
          <div className="mb-1 text-[14px] font-bold text-dls-text-primary">Demo dataset</div>
          <p className="mb-3 text-[12.5px] text-dls-text-secondary">
            This workspace runs on synthetic data only. Seeding creates realistic customers, policies and
            claims across Motor, Medical, Life, General and Agriculture, each pinned to a real, published
            product with its own claim process and document matrix. Running this again is safe: an
            already-published product for a line is reused, never duplicated, and new work is assigned to
            you, so it shows up under your own My Work.
          </p>
          <Button
            variant="secondary"
            icon={seeding ? <Loader2 className="animate-spin" aria-hidden="true" /> : <RefreshCw aria-hidden="true" />}
            onClick={() => void onSeedDemoData()}
            disabled={seeding}
          >
            {seeding ? "Seeding..." : "Seed demo data"}
          </Button>
          {seedError && <p className="mt-3 text-[12.5px] text-dls-danger-text" role="alert">{seedError}</p>}
          {seedResult && (
            <p className="mt-3 text-[12.5px] text-dls-text-secondary">
              Seeded {seedResult.products} product{seedResult.products === 1 ? "" : "s"}, {seedResult.customers} customers,{" "}
              {seedResult.policies} policies, {seedResult.cases} claims, {seedResult.documents} documents and{" "}
              {seedResult.approvals} approvals.
            </p>
          )}

          <div className="mt-5 border-t border-dls-border-subtle pt-4">
            <div className="mb-1 text-[13px] font-bold text-dls-text-primary">Policyholder for your own phone</div>
            <p className="mb-3 text-[12.5px] text-dls-text-secondary">
              Creates (or refreshes) a policyholder profile for testing the WhatsApp assistant from a real
              phone: a Motor and a Medical policy, and two past claims. Safe to run again with the same
              number, it updates the same profile rather than duplicating it.
            </p>
            <div className="grid gap-3 sm:grid-cols-3">
              <Field label="Full name">
                <Input value={testName} onChange={(e) => setTestName(e.target.value)} placeholder="e.g. Eugene Katusiime" />
              </Field>
              <Field label="Phone" hint="With country code, e.g. +256 7XX XXXXXX">
                <Input value={testPhone} onChange={(e) => setTestPhone(e.target.value)} placeholder="+256776789989" />
              </Field>
              <Field label="Email (optional)">
                <Input value={testEmail} onChange={(e) => setTestEmail(e.target.value)} placeholder="you@example.com" />
              </Field>
            </div>
            <Button
              variant="secondary"
              className="mt-3"
              icon={testSeeding ? <Loader2 className="animate-spin" aria-hidden="true" /> : <RefreshCw aria-hidden="true" />}
              onClick={() => void onSeedTestPolicyholder()}
              disabled={testSeeding || !testName.trim() || !testPhone.trim()}
            >
              {testSeeding ? "Creating..." : "Create test policyholder"}
            </Button>
            {testSeedError && <p className="mt-3 text-[12.5px] text-dls-danger-text" role="alert">{testSeedError}</p>}
            {testSeedResult && (
              <p className="mt-3 text-[12.5px] text-dls-text-secondary">
                {testSeedResult.fullName} ({testSeedResult.phone}) now has {testSeedResult.policies.length} active{" "}
                polic{testSeedResult.policies.length === 1 ? "y" : "ies"} ({testSeedResult.policies.map((p) => p.productLine).join(", ")}) and{" "}
                {testSeedResult.pastClaims.length} past claim{testSeedResult.pastClaims.length === 1 ? "" : "s"}. Message this
                number on WhatsApp to try the assistant.
              </p>
            )}
          </div>
        </GlassCard>
      )}
    </>
  );
}
