"use client";
import { useEffect, useState } from "react";
import { GlassCard, SectionHeader, Badge } from "@/components/iip/Primitives";
import { LoadingSkeleton, ErrorStateSurface } from "@/components/ui/StateSurfaces";
import GuardrailBanner from "@/components/iip/GuardrailBanner";
import { ShieldCheck, ShieldOff } from "lucide-react";
import { apiBaseUrl, scopedHeaders } from "@/lib/config";
import { useUI } from "@/context/UIStateContext";

type TenantPolicyRule = { rule: string; enforced: boolean; enforcementSource: string };

export default function TenantPolicy() {
  const { roleDef } = useUI();
  const [rules, setRules] = useState<TenantPolicyRule[] | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

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
    </>
  );
}
