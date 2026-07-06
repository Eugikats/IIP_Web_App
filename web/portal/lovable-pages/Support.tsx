"use client";
import { SectionHeader, GlassCard, Badge } from "@/components/iip/Primitives";
import ProviderHealthGrid from "@/components/iip/ProviderHealthGrid";
import { Search, Activity, LifeBuoy } from "lucide-react";
import { toast } from "sonner";
import { LOVABLE_VISUAL_PARITY_MODE } from "@/lib/lovable-parity/mode";

const parity = LOVABLE_VISUAL_PARITY_MODE;

export default function Support() {
  return (
    <>
      <SectionHeader
        eyebrow="Support & Health"
        title="Support & health"
        sub="Provider health, queue state, retryable errors, and safe diagnostics. Empty data is not an error — provider failure is."
      />

      <div className="grid grid-cols-12 gap-4 mb-6">
        <GlassCard className="col-span-12 md:col-span-4">
          <div className="text-[11px] uppercase tracking-[0.14em] text-stone">Queue depth</div>
          {parity ? (
            <div className="mt-1 flex items-baseline gap-2"><div className="text-[26px] font-semibold text-navy">128</div><Badge tone="info">normal</Badge></div>
          ) : (
            <div className="mt-1"><Badge tone="warning">Not configured</Badge></div>
          )}
          <div className="text-[11.5px] text-stone mt-1">Extraction · Communications · Retry</div>
        </GlassCard>
        <GlassCard className="col-span-12 md:col-span-4">
          <div className="text-[11px] uppercase tracking-[0.14em] text-stone">Failed jobs (24h)</div>
          {parity ? (
            <div className="mt-1 flex items-baseline gap-2"><div className="text-[26px] font-semibold text-navy">7</div><Badge tone="warning">retryable · 6</Badge></div>
          ) : (
            <div className="mt-1"><Badge tone="warning">Not configured</Badge></div>
          )}
          <div className="text-[11.5px] text-stone mt-1">Retry does not duplicate external actions (idempotent).</div>
        </GlassCard>
        <GlassCard className="col-span-12 md:col-span-4">
          <div className="text-[11px] uppercase tracking-[0.14em] text-stone">Correlation ID search</div>
          <div className="mt-2 flex items-center gap-2 rounded-xl border border-cloud bg-white/70 px-3 py-2 text-[12.5px] text-stone">
            <Search className="h-3.5 w-3.5" /><span>corr_… or run_…</span>
          </div>
        </GlassCard>
      </div>

      <div className="text-[11px] uppercase tracking-[0.14em] text-stone mb-2">Provider health</div>
      <ProviderHealthGrid />

      <div className="grid md:grid-cols-2 gap-4 mt-6">
        <GlassCard>
          <div className="flex items-center gap-2 mb-2"><Activity className="h-4 w-4 text-info" /><div className="text-[14px] font-semibold text-navy">Safe diagnostics</div></div>
          {parity ? (
            <div className="text-[12.5px] text-slate2 space-y-1.5">
              <div>Latest webhook delivery: <span className="text-navy">OK · 220ms</span></div>
              <div>Redelivery queue: <span className="text-navy">0 pending</span></div>
              <div>Retention job: <span className="text-navy">last run yesterday 03:12 UTC</span></div>
              <div>Logs surface event metadata only — no secrets, no full stacks, no unnecessary PII.</div>
            </div>
          ) : (
            <div className="text-[12.5px] text-slate2 space-y-1.5">
              <div><Badge tone="warning">Not configured</Badge></div>
              <div>Diagnostics (webhook delivery, redelivery queue, retention job status) surface here once the operations telemetry endpoints are wired. Logs surface event metadata only — no secrets, no full stacks, no unnecessary PII.</div>
            </div>
          )}
        </GlassCard>
        <GlassCard>
          <div className="flex items-center gap-2 mb-2"><LifeBuoy className="h-4 w-4 text-info" /><div className="text-[14px] font-semibold text-navy">Escalate to publisher support</div></div>
          <div className="text-[12.5px] text-slate2 mb-3">Otic Support responds within your priority-tier SLA. Include the correlation ID for faster triage.</div>
          <button onClick={() => toast.success("Support ticket drafted")} className="rounded-2xl gradient-primary text-white text-[13px] px-4 py-2 shadow-l2 hover-lift">Open support ticket</button>
        </GlassCard>
      </div>
    </>
  );
}
