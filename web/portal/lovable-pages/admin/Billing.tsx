"use client";
import { GlassCard, SectionHeader, Badge } from "@/components/iip/Primitives";
import { useBilling, type UsageMeterView } from "@/lib/lovable-parity/data/billing";

function Meter({ m }: { m: UsageMeterView }) {
  if (!m.metered) {
    return (
      <div className="glass-subtle p-4">
        <div className="text-[11px] uppercase tracking-[0.14em] text-muted">{m.label}</div>
        <div className="mt-2"><Badge tone="warning">Not configured</Badge></div>
        <div className="mt-2 text-[11px] text-muted">No counter wired for this dimension yet.</div>
      </div>
    );
  }
  const tone = m.overLimit ? "error" : m.percentUsed >= 85 ? "warning" : "info";
  return (
    <div className="glass-subtle p-4">
      <div className="flex items-center justify-between mb-2">
        <div className="text-[11px] uppercase tracking-[0.14em] text-muted">{m.label}</div>
        <Badge tone={tone as any}>{m.percentUsed}%</Badge>
      </div>
      <div className="text-[20px] font-semibold text-ink">{m.used.toLocaleString()} <span className="text-[12px] text-muted font-normal">/ {m.limit.toLocaleString()} {m.unit}</span></div>
      <div className="mt-2 h-1.5 rounded-full bg-line overflow-hidden"><div className="h-full gradient-primary" style={{ width: `${m.percentUsed}%` }} /></div>
    </div>
  );
}

export default function Billing() {
  const { summary, loading, error } = useBilling();

  return (
    <>
      <SectionHeader
        eyebrow="Admin · Billing & Usage"
        title="Package & entitlements"
        sub="AI is metered — human-in-loop and auditability are the core platform value. Usage controls protect provider costs."
      />

      {loading ? (
        <GlassCard><div className="p-6 text-center text-[13px] text-muted">Loading billing & usage…</div></GlassCard>
      ) : !summary ? (
        <GlassCard><div className="p-10 text-center text-muted"><div className="text-ink font-medium">Billing unavailable</div><div className="text-[12.5px] mt-1">{error ?? "No billing data for this workspace."}</div></div></GlassCard>
      ) : (
        <>
          <GlassCard className="mb-4">
            <div className="flex flex-wrap items-center justify-between gap-3">
              <div>
                <div className="text-[11px] uppercase tracking-[0.14em] text-muted">Current package</div>
                <div className="text-[18px] font-semibold text-ink">{summary.packageName}</div>
                <div className="text-[12px] text-muted">Seat limit · {summary.seatLimit}</div>
              </div>
              <div className="flex flex-wrap gap-1.5">
                <Badge tone="info">Human-in-loop enforced</Badge>
                <Badge tone="violet">Auditability included</Badge>
              </div>
            </div>
          </GlassCard>

          <div className="grid md:grid-cols-2 lg:grid-cols-4 gap-3 mb-6">
            {summary.meters.map((m) => <Meter key={m.key} m={m} />)}
          </div>

          <GlassCard>
            <div className="text-[14px] font-semibold text-ink mb-3">Module entitlements</div>
            <div className="grid sm:grid-cols-2 md:grid-cols-3 gap-2 text-[13px]">
              {summary.modules.map((m) => (
                <div key={m} className="flex items-center justify-between rounded-xl border border-line bg-white/70 px-3 py-2">
                  <span className="text-ink-soft">{m}</span>
                  <Badge tone="success">Enabled</Badge>
                </div>
              ))}
            </div>
            <div className="mt-4 text-[11.5px] text-muted">Commercial plan changes are intentionally not simulated in the portal. A publisher-backed upgrade workflow must be configured before an upgrade action is exposed.</div>
          </GlassCard>
        </>
      )}
    </>
  );
}
