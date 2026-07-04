"use client";
import { GlassCard, SectionHeader, Badge } from "@/components/iip/Primitives";
import { usage } from "@/lib/lovable-parity/mock";
import { toast } from "sonner";

function Meter({ label, used, limit, unit }: { label: string; used: number; limit: number; unit: string }) {
  const pct = Math.min(100, Math.round((used / limit) * 100));
  const tone = pct >= 85 ? "warning" : pct >= 95 ? "error" : "info";
  return (
    <div className="glass-subtle p-4">
      <div className="flex items-center justify-between mb-2">
        <div className="text-[11px] uppercase tracking-[0.14em] text-stone">{label}</div>
        <Badge tone={tone as any}>{pct}%</Badge>
      </div>
      <div className="text-[20px] font-semibold text-navy">{used.toLocaleString()} <span className="text-[12px] text-stone font-normal">/ {limit.toLocaleString()} {unit}</span></div>
      <div className="mt-2 h-1.5 rounded-full bg-cloud overflow-hidden"><div className="h-full gradient-primary" style={{ width: `${pct}%` }} /></div>
    </div>
  );
}

export default function Billing() {
  return (
    <>
      <SectionHeader
        eyebrow="Admin · Billing & Usage"
        title="Package & entitlements"
        sub="AI is metered — human-in-loop and auditability are the core platform value. Usage controls protect provider costs."
        action={<button onClick={() => toast.success("Upgrade request sent to publisher")} className="rounded-2xl gradient-primary text-white text-[13px] px-4 py-2 shadow-l2 hover-lift">Request upgrade</button>}
      />

      <GlassCard className="mb-4">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <div>
            <div className="text-[11px] uppercase tracking-[0.14em] text-stone">Current package</div>
            <div className="text-[18px] font-semibold text-navy">{usage.package}</div>
            <div className="text-[12px] text-stone">Billing period · {usage.billingPeriod}</div>
          </div>
          <div className="flex flex-wrap gap-1.5">
            <Badge tone="success">Support · {usage.supportTier}</Badge>
            <Badge tone="info">Human-in-loop enforced</Badge>
            <Badge tone="violet">Auditability included</Badge>
          </div>
        </div>
      </GlassCard>

      <div className="grid md:grid-cols-2 lg:grid-cols-5 gap-3 mb-6">
        <Meter label="Seats" used={usage.seatsUsed} limit={usage.seatsLimit} unit="users" />
        <Meter label="AI runs" used={usage.aiRunsUsed} limit={usage.aiRunsLimit} unit="runs" />
        <Meter label="Documents processed" used={usage.docsProcessed} limit={usage.docsLimit} unit="docs" />
        <Meter label="Channel messages" used={usage.channelMessages} limit={usage.channelLimit} unit="msgs" />
        <Meter label="Storage" used={usage.storageGB} limit={usage.storageLimit} unit="GB" />
      </div>

      <GlassCard>
        <div className="text-[14px] font-semibold text-navy mb-3">Module entitlements</div>
        <div className="grid sm:grid-cols-2 md:grid-cols-3 gap-2 text-[13px]">
          {["Claims Intelligence","Channels","Customer 360","Customer Acquisition","Policy Intelligence","Fraud Intelligence","Underwriting","Workflow & Approvals","Knowledge Hub","Executive Intelligence","Integrations","Reports & Audit","AI & Agent Management","Support & Health"].map((m) => (
            <div key={m} className="flex items-center justify-between rounded-xl border border-cloud bg-white/70 px-3 py-2">
              <span className="text-slate2">{m}</span>
              <Badge tone="success">Enabled</Badge>
            </div>
          ))}
        </div>
      </GlassCard>
    </>
  );
}
