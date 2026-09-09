"use client";
import { GlassCard, SectionHeader, Badge } from "@/components/iip/Primitives";
import GuardrailBanner from "@/components/iip/GuardrailBanner";
import { ShieldCheck } from "lucide-react";

export default function TenantPolicy() {
  const rules = [
    { rule: "Regulated decisions require named human reviewer", enforced: true, note: "Cannot be disabled by tenant admin." },
    { rule: "Customer communications require approval before send", enforced: true, note: "Applies to WhatsApp, Email, SMS, Web." },
    { rule: "Fraud signals are advisory only", enforced: true, note: "Never a fraud determination." },
    { rule: "Unknown knowledge sensitivity blocked from retrieval", enforced: true, note: "Blocks contains_secrets, contains_unapproved_pii, unknown by default." },
    { rule: "Extraction human-review threshold", enforced: true, note: "Pilot: 0.80 confidence — configurable." },
    { rule: "Write-back requires human approval", enforced: true, note: "Adapter-based, controlled, idempotent." },
    { rule: "AI autonomy increases require secondary approval", enforced: true, note: "Production only." },
    { rule: "Coverage questions must be grounded in approved sources", enforced: true, note: "Model memory alone is not acceptable." },
  ];
  return (
    <>
      <SectionHeader
        eyebrow="Admin · Tenant Agent Policy"
        title="Tenant agent policy"
        sub="Baseline guardrails that agents in this workspace inherit. Some rules are enforced by the platform and cannot be relaxed."
      />
      <div className="mb-4"><GuardrailBanner /></div>
      <GlassCard>
        <div className="space-y-2">
          {rules.map((r) => (
            <div key={r.rule} className="flex items-start gap-3 rounded-dls-card border border-dls-border-subtle bg-dls-surface p-3">
              <ShieldCheck className="mt-0.5 h-4 w-4 text-dls-info-text" aria-hidden="true" />
              <div className="flex-1">
                <div className="text-[13px] font-semibold text-dls-text-primary">{r.rule}</div>
                <div className="text-[11.5px] text-dls-text-secondary">{r.note}</div>
              </div>
              <Badge tone={r.enforced ? "success" : "neutral"}>{r.enforced ? "Enforced" : "Optional"}</Badge>
            </div>
          ))}
        </div>
      </GlassCard>
    </>
  );
}
