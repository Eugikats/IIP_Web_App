"use client";

import { useEffect, useState } from "react";
import { AlertTriangle } from "lucide-react";
import { GlassCard } from "@/components/iip/Primitives";
import { Button } from "@/components/ui/Button";
import { Field, Input } from "@/components/ui/Input";
import { getEntraApiAccessToken } from "@/lib/entraAuth";
import { apiBaseUrl, scopedHeaders } from "@/lib/config";

type FraudRules = {
  velocityWindowDays: number;
  velocityClaimCount: number;
  lowCompletenessThreshold: number;
  earlyClaimWindowDays: number;
  highValueSumInsuredThreshold: number;
};

type TenantConfiguration = {
  insurerName: string;
  logoUrl: string | null;
  country: string;
  region: string;
  businessLines: unknown[];
  channels: unknown[];
  approvalThresholds: unknown[];
  checklists: unknown[];
  auditRetentionDays: number;
  fraudRules: FraudRules;
};

const DEFAULT_RULES: FraudRules = {
  velocityWindowDays: 30,
  velocityClaimCount: 3,
  lowCompletenessThreshold: 0.4,
  earlyClaimWindowDays: 14,
  highValueSumInsuredThreshold: 100_000_000,
};

export default function FraudRuleSettings({ role }: { role: string }) {
  const [configuration, setConfiguration] = useState<TenantConfiguration | null>(null);
  const [rules, setRules] = useState<FraudRules>(DEFAULT_RULES);
  const [auditReason, setAuditReason] = useState("");
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [saved, setSaved] = useState(false);

  useEffect(() => {
    let active = true;
    void (async () => {
      try {
        const token = await getEntraApiAccessToken();
        const response = await fetch(`${apiBaseUrl}/api/v1/admin/tenant-configuration`, {
          headers: {
            ...scopedHeaders(role, ""),
            ...(token ? { Authorization: `Bearer ${token}` } : {}),
          },
        });
        if (!response.ok) throw new Error("Fraud settings could not be loaded. Check your access and try again.");
        const current = (await response.json()) as TenantConfiguration;
        if (active) {
          setConfiguration(current);
          setRules(current.fraudRules ?? DEFAULT_RULES);
        }
      } catch (loadError) {
        if (active) setError(loadError instanceof Error ? loadError.message : "Fraud settings could not be loaded.");
      } finally {
        if (active) setLoading(false);
      }
    })();
    return () => { active = false; };
  }, [role]);

  async function save() {
    if (!configuration || saving || !auditReason.trim()) return;
    setSaving(true);
    setError(null);
    setSaved(false);
    try {
      const token = await getEntraApiAccessToken();
      const response = await fetch(`${apiBaseUrl}/api/v1/admin/tenant-configuration`, {
        method: "PUT",
        headers: {
          ...scopedHeaders(role, ""),
          ...(token ? { Authorization: `Bearer ${token}` } : {}),
          "Content-Type": "application/json",
          "X-Audit-Reason": auditReason.trim(),
        },
        body: JSON.stringify({ ...configuration, fraudRules: rules }),
      });
      if (!response.ok) throw new Error("Fraud settings were not saved. Check the values and your permission, then try again.");
      const updated = (await response.json()) as TenantConfiguration;
      setConfiguration(updated);
      setRules(updated.fraudRules);
      setAuditReason("");
      setSaved(true);
    } catch (saveError) {
      setError(saveError instanceof Error ? saveError.message : "Fraud settings were not saved.");
    } finally {
      setSaving(false);
    }
  }

  function update<K extends keyof FraudRules>(key: K, value: number) {
    setRules((current) => ({ ...current, [key]: value }));
    setSaved(false);
  }

  return (
    <GlassCard className="lg:col-span-2">
      <div className="mb-3 flex items-start gap-3">
        <AlertTriangle className="mt-0.5 h-4 w-4 text-dls-text-secondary" aria-hidden="true" />
        <div>
          <h2 className="text-[14px] font-bold text-dls-text-primary">Fraud review rules</h2>
          <p className="mt-1 max-w-3xl text-[12px] leading-5 text-dls-text-secondary">
            These thresholds raise advisory signals for a person to review. They do not determine fraud or decide a claim.
          </p>
        </div>
      </div>

      {loading ? <p className="text-[12px] text-dls-text-muted">Loading workspace rules…</p> : null}
      {!loading && !configuration && !error ? <p className="text-[12px] text-dls-text-muted">Fraud rules are not configured.</p> : null}
      {error ? <p role="alert" className="mb-3 text-[12px] text-dls-danger-text">{error}</p> : null}
      {configuration ? (
        <>
          <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
            <NumberField label="Claims in velocity window" value={rules.velocityClaimCount} min={2} max={100} onChange={(value) => update("velocityClaimCount", value)} />
            <NumberField label="Velocity window (days)" value={rules.velocityWindowDays} min={1} max={365} onChange={(value) => update("velocityWindowDays", value)} />
            <NumberField label="Low completeness threshold (0 to 1)" value={rules.lowCompletenessThreshold} min={0} max={1} step={0.01} onChange={(value) => update("lowCompletenessThreshold", value)} />
            <NumberField label="Early-claim review window (days)" value={rules.earlyClaimWindowDays} min={0} max={365} onChange={(value) => update("earlyClaimWindowDays", value)} />
            <NumberField label="High sum-insured threshold (UGX)" value={rules.highValueSumInsuredThreshold} min={1} onChange={(value) => update("highValueSumInsuredThreshold", value)} />
          </div>
          <div className="mt-4 grid gap-3 md:grid-cols-[minmax(0,1fr)_auto] md:items-end">
            <Field label="Reason for this change" htmlFor="fraud-rule-audit-reason" hint="This reason is recorded in the workspace audit trail.">
              <Input id="fraud-rule-audit-reason" value={auditReason} onChange={(event) => setAuditReason(event.target.value)} maxLength={500} />
            </Field>
            <Button variant="primary" onClick={() => void save()} disabled={loading || saving || !configuration || !auditReason.trim()}>
              {saving ? "Saving rules…" : "Save fraud rules"}
            </Button>
          </div>
          {saved ? <p role="status" className="mt-3 text-[12px] text-dls-text-secondary">Fraud rules saved and audited.</p> : null}
        </>
      ) : null}
    </GlassCard>
  );
}

function NumberField({
  label,
  value,
  min,
  max,
  step,
  onChange,
}: {
  label: string;
  value: number;
  min: number;
  max?: number;
  step?: number;
  onChange: (value: number) => void;
}) {
  return (
    <Field label={label}>
      <Input type="number" value={value} min={min} max={max} step={step ?? 1} onChange={(event) => onChange(Number(event.target.value))} />
    </Field>
  );
}
